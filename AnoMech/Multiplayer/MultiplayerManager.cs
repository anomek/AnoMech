using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using AnoMech.Scenarios;

namespace AnoMech.Multiplayer;

// Session lifecycle and the host<->peer replication loop. The host runs the real scenario
// with joined peers' roles spawned as SimNetworkPuppet; peers run no scenario logic, apply
// what the host broadcasts, and report their own position back (SelfPose).
//
// Everything here runs on the framework thread: Tick() via OnFrameworkUpdate, and relay
// messages are queued and drained there (see DrainPendingMessages).
public sealed partial class MultiplayerManager : IDisposable
{
    private RelayClient? relay;
    private bool running;

    private readonly Dictionary<SimEnemy, int> hostEnemyNetIds = new();
    private int nextEnemyNetId;
    private readonly Dictionary<SimTether, int> hostTetherNetIds = new();
    private int nextTetherNetId;
    // Host-only, for edge-triggered logging; every snapshot still carries the full state.
    private readonly Dictionary<SimEnemy, byte> hostEnemyLastLoggedModelState = new();
    private readonly Dictionary<SimEnemy, Dictionary<(ushort Id, int Ordinal), ushort>> hostEnemyLastLoggedStatuses = new();
    private readonly Dictionary<SimEnemy, int> hostEnemyLastLoggedAnimationTimeline = new();
    private readonly Dictionary<SimEnemy, int> hostEnemyLastLoggedAnimationState = new();
    private readonly Dictionary<SimEnemy, int> hostEnemyLastLoggedInstantCastSeq = new();
    private readonly Dictionary<PartyRole, Dictionary<(ushort Id, int Ordinal), ushort>> hostRoleLastLoggedStatuses = new();
    private readonly Dictionary<PartyRole, int> hostRoleLastLoggedAnimationTimeline = new();

    private readonly Dictionary<SimEventObject, int> hostEventObjectNetIds = new();
    private int nextEventObjectNetId;

    private readonly Dictionary<int, SimEnemy> peerEnemies = new();
    private readonly Dictionary<int, SimTether> peerTethers = new();
    private readonly Dictionary<int, SimEventObject> peerEventObjects = new();
    // Peer-only: last applied value per NetId. Re-issuing an unchanged ModelState rebuilds the
    // model (visible flicker) and re-playing an animation restarts it.
    private readonly Dictionary<int, byte> peerEnemyModelState = new();
    private readonly Dictionary<int, Dictionary<(ushort Id, int Ordinal), ushort>> peerEnemyLastLoggedStatuses = new();
    private readonly Dictionary<int, int> peerEnemyAnimationTimeline = new();
    private readonly Dictionary<int, int> peerEnemyAnimationState = new();
    private readonly Dictionary<int, int> peerEnemyLastInstantCastSeq = new();
    private readonly Dictionary<int, int> peerEnemyLastCastSeq = new();
    // NetIds whose real-packet spawn the engine dropped locally; recreated as plain doppels.
    private readonly HashSet<int> peerEnemyTemplateFailed = new();
    private readonly Dictionary<int, ushort> peerEventObjectState = new();
    private readonly Dictionary<int, int> peerEventObjectAnimationSeq = new();
    private readonly Dictionary<int, int> peerEventObjectFadeSeq = new();
    private readonly Dictionary<int, int> peerEventObjectDirectorSeq = new();
    // Engine-state seqs applied per NetId (see ActorEngineState): re-issuing an unchanged mode
    // or hold restarts it.
    private readonly Dictionary<int, (int Mode, int Hold, int Direct, int ForceLoad)> peerEnemyEngineSeqs = new();
    private readonly Dictionary<int, bool> peerEnemyModelHidden = new();
    private readonly Dictionary<PartyRole, int> peerRoleAnimationTimelineSeq = new();
    private readonly Dictionary<PartyRole, int> peerRolePlayedActionSeq = new();
    private readonly Dictionary<PartyRole, Dictionary<(ushort Id, int Ordinal), ushort>> peerRoleLastLoggedStatuses = new();
    // Statuses this client put on a role by reconciliation; removal must only undo those, never
    // a status the local client manages itself (Sprint via LocalPlayerInputHooks).
    private readonly Dictionary<PartyRole, HashSet<(ushort Id, GameObjectId Source)>> peerRoleReconciledStatuses = new();
    private readonly Dictionary<int, HashSet<(ushort Id, GameObjectId Source)>> peerEnemyReconciledStatuses = new();
    // The host's status instance last applied per id (see DropRecreatedStatuses).
    private readonly Dictionary<int, Dictionary<ushort, int>> peerEnemyStatusInstances = new();
    private readonly Dictionary<PartyRole, Dictionary<ushort, int>> peerRoleStatusInstances = new();
    // Set by OnPeerStartResolved once this run's deferred zone entry has completed. After a
    // Reset the zone is still loaded, so IsInInstance alone can't tell this run's party from
    // the last one's.
    private bool peerEnteredInstance;
    private bool peerEntryQueued;
    // An EndMessage that arrived while the entry was queued: acted on once it completes.
    private bool? endAfterPeerEntry;
    // The host's clock from the message that started this run, and when it arrived; applied by
    // SyncClocksToHost once the entry completes.
    private RunClockState? hostClockAtStart;
    private long hostClockReceivedAt;
    private bool eventClockSynced;
    // Smoothed frame time, sent in RunClockState; load hitches are left out.
    private float averageFrameSeconds = 1f / 60f;
    private const float MaxFrameSampleSeconds = 0.1f;
    private const float FrameSmoothing = 0.05f;

    // ---- Connection-quality tracking (runs in the lobby too) ---------------
    private const float PingIntervalSeconds = 2f;
    private const long PeerStaleTimeoutMs = 8000;
    // Catches a mistyped/nonexistent code; the relay has no "session not found" at the
    // transport level.
    private const long NoHostFoundTimeoutMs = 4000;
    private float pingTimer;
    private readonly Dictionary<Guid, long> peerLastSeenMs = new();
    // When each peer last registered. A leave that arrives right after one crossed the peer's
    // own rejoin and must not evict them again -- see the SessionEndedMessage case.
    private readonly Dictionary<Guid, long> peerLastHelloMs = new();
    // Leaves held back by that grace: confirmed unless the peer is heard from again.
    private readonly Dictionary<Guid, long> deferredLeaveMs = new();
    private readonly Dictionary<Guid, float> peerLatencyMs = new();
    private readonly HashSet<Guid> warnedStalePeers = new();
    // Host-only: each peer's self-reported mitigation statuses (SelfMitigationMessage); read by
    // TankMitigation.ComputeMitigation.
    private readonly Dictionary<Guid, HashSet<ushort>> peerMitigationStatusIds = new();
    // Host-only: peers whose own character the bot Ai drives, from SelfPoseMessage.
    private readonly HashSet<Guid> botControlledPeers = new();
    // Rebuilt by the host each ping cycle and broadcast (PeerStatusMessage).
    private readonly Dictionary<Guid, PeerStatusEntry> peerStatuses = new();
    // Peer-only: the host never pings itself, so its liveness is the time since any host broadcast.
    private long lastHostMessageMs;
    // lastHostMessageMs is seeded to "now" on join, so it alone can't tell "never heard" from
    // "went silent".
    private bool everHeardFromHost;

    // ---- Pre-start readiness check (see StartScenario/FinishStartCheck) -----
    private const float StartCheckTimeoutSeconds = 5f;
    private HashSet<Guid>? pendingStartResponses;
    private readonly Dictionary<Guid, string> startCheckFailures = new();
    private float startCheckTimer;
    public bool IsStartCheckPending => pendingStartResponses != null;
    public string? StartCheckFailureReason { get; private set; }
    // What the host's own Start is waiting to settle before it runs.
    public string? StartWaitingOn { get; private set; }
    // A settling peer answers once settled, inside the host's StartCheckTimeoutSeconds.
    private const float StartCheckReplyMaxWaitSeconds = 4f;
    private float? startCheckReplyWaited;

    // ---- Debug: bot-controlled host or peer ---------------------------------
    // Testing aid: the user's own role is driven by the bot AI, so one developer can fill a
    // session alone. The host's scenario.Run already schedules that choreography against every
    // role and forwards a peer's to it (MoveMessage and co.); the flag just lets PlayerMovement
    // act on it. Sticky across Start/Reset; lobby-only to toggle.
    private bool debugBotControlled;
    public bool DebugBotControlled => debugBotControlled;

    // EndMessage is resent a few times: a lost one would leave peers waiting out PeerStaleTimeoutMs.
    private const int EndMessageResendCount = 4;
    private const float EndMessageResendIntervalSeconds = 1f;
    private bool? pendingEndResendReturnedToInn;
    private string? pendingEndResendReason;
    private int endResendsRemaining;
    private float endResendTimer;

    // RunScenarioAsHost sets ActiveScenario a frame late; without this Tick() would read the
    // null as "run ended".
    private bool hostScenarioStarted;

    public bool SetDebugBotControlled(bool value)
    {
        if (running) return false;
        debugBotControlled = value;
        return true;
    }

    // ---- Reconnection --------------------------------------------------------
    // Identity survives a reconnect via Configuration.PeerSecret, and the host keeps a stale
    // peer's role, so rejoining resumes where it left off.
    private static readonly TimeSpan[] ReconnectBackoff =
        { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(15) };
    private CancellationTokenSource? reconnectCts;
    private bool reconnecting;
    public bool IsReconnecting => reconnecting;
    // Host-only; lets Tick() end a run whose peers have all given up (IsHostStale) while the
    // host would otherwise keep retrying.
    private long? disconnectedSinceMs;
    public int ReconnectAttempt { get; private set; }

    public MultiplayerSession Session { get; private set; } = new();
    public Guid MyPeerId { get; private set; }
    public bool IsHost { get; private set; }
    public bool IsConnected => relay?.IsConnected ?? false;
    public bool IsEncrypted => relay?.IsEncrypted ?? false;
    public bool FellBackToUnencrypted => relay?.FellBackToUnencrypted ?? false;
    public bool SupportsCompression => relay?.SupportsCompression ?? false;
    // True on any connected relay: RelayClient refuses one that can't authenticate senders.
    public bool RelayAttestsSender => relay?.SupportsSenderIdentity ?? false;
    public bool IsRunning => running;
    public string? SessionCode { get; private set; }
    // The host has no SessionCode until the relay assigns one.
    public bool IsHostConnecting => IsHost && relay != null && SessionCode == null;
    public bool InSession => SessionCode != null || IsHostConnecting;
    public EmbeddedRelay Embedded { get; } = new();
    public string? RelayUrl { get; private set; }
    // Captured at Host/Join time so a mid-session config edit doesn't change what the
    // reconnect loop sends.
    private string? relayAccessToken;
    private string peerSecret = "";
    public string DisplayName { get; set; } = "Player";
    // Failed-connect reason for the UI; cleared on the next Host/Join.
    public string? ConnectionError { get; private set; }
    // Why the session ended, when it wasn't this client's own Leave.
    public string? SessionEndReason { get; private set; }

    public PartyRole? MyClaimedRole => Session.RoleOf(MyPeerId);

    // ScenarioIndex comes off the wire; every read goes through here rather than indexing
    // Scenarios directly.
    internal IScenario? TryResolveScenario()
    {
        var scenarios = Plugin.GameInstance.Scenarios;
        if (Session.ScenarioIndex < 0) return null; // nothing chosen yet
        if (NetGuard.InRange(Session.ScenarioIndex, scenarios.Count)) return scenarios[Session.ScenarioIndex];
        if (warnedBadScenarioIndex == Session.ScenarioIndex) return null;
        warnedBadScenarioIndex = Session.ScenarioIndex;
        DiagnosticLog.Warn($"[Multiplayer] Host sent scenario index {Session.ScenarioIndex}, but only {scenarios.Count} exist -- ignoring.");
        return null;
    }

    private int? warnedBadScenarioIndex;

    public event Action? LobbyChanged;

    public PeerStatusEntry? GetPeerStatus(Guid peerId) => peerStatuses.GetValueOrDefault(peerId);

    // Host-only (a peer has no view of other peers' statuses); empty if unclaimed or unreported.
    public IReadOnlyCollection<ushort> PeerMitigationStatusIds(PartyRole role)
    {
        if (!IsHost || !Session.ClaimedBy.TryGetValue(role, out var peerId)) return [];
        return peerMitigationStatusIds.TryGetValue(peerId, out var ids) ? ids : [];
    }

    public bool IsPeerBotControlled(PartyRole role)
        => IsHost && Session.ClaimedBy.TryGetValue(role, out var peerId) && botControlledPeers.Contains(peerId);

    public float SecondsSinceHostMessage => (Environment.TickCount64 - lastHostMessageMs) / 1000f;
    // SessionCode is set synchronously on Join; this is what confirms a host is actually there.
    public bool EverHeardFromHost => everHeardFromHost;
    public bool IsHostStale => !IsHost && everHeardFromHost && SecondsSinceHostMessage * 1000f > PeerStaleTimeoutMs;
    public bool IsSessionNotFound => !IsHost && !everHeardFromHost && SecondsSinceHostMessage * 1000f > NoHostFoundTimeoutMs;

    // ---- Session lifecycle ----------------------------------------------

    public void HostSession(string relayUrl)
    {
        LeaveSession();
        HostSessionCore(relayUrl);
    }

    // Null on success, otherwise why the server couldn't start.
    public string? HostEmbeddedSession(int port)
    {
        LeaveSession();
        if (Embedded.Start(port) is { } error) return error;
        HostSessionCore(Embedded.LocalUrl);
        return null;
    }

    private void HostSessionCore(string relayUrl)
    {
        ConnectionError = null;
        peerSecret = RelayWire.RelayCredential(Plugin.Config.EnsurePeerSecret(), relayUrl);
        MyPeerId = RelayWire.PeerId(peerSecret);
        IsHost = true;
        RelayUrl = relayUrl;
        relayAccessToken = Plugin.Config.TokenForRelay(relayUrl);
        Session = new MultiplayerSession { HostId = MyPeerId };
        Session.Names[MyPeerId] = DisplayName;
        Session.Jobs[MyPeerId] = LocalClassJob;
        Session.Builds[MyPeerId] = new PeerBuildInfo(PluginBuildInfo.Version, PluginBuildInfo.Checksum);

        DiagnosticLog.Info($"[Multiplayer] Hosting a new session at {relayUrl} as {MyPeerId} ({DisplayName}), build {PluginBuildInfo.ShortChecksum}.");
        var client = new RelayClient(peerSecret);
        WireRelay(client);
        relay = client;
        _ = FinishHostConnectAsync(client);
        LobbyChanged?.Invoke();
    }

    // The ReferenceEquals check keeps a late-arriving code from resurrecting a session that was
    // left mid-connect.
    private async Task FinishHostConnectAsync(RelayClient client)
    {
        var code = await client.ConnectAndHostAsync(RelayUrl!, relayAccessToken);
        if (!ReferenceEquals(relay, client)) return;
        if (code is null) return; // Disconnected already fired
        SessionCode = code;
        DiagnosticLog.Info($"[Multiplayer] Relay assigned session code {code}.");
        LobbyChanged?.Invoke();
    }

    public void JoinSession(string relayUrl, string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        // Rejoining the session we are already on must not announce a leave first: that message
        // races our own Hello, and a host that reads it second drops us straight back out.
        var rejoining = SessionCode == normalized && RelayUrl != null
                        && Configuration.OriginOf(RelayUrl) == Configuration.OriginOf(relayUrl);
        LeaveSessionInternal(notifyOthers: !rejoining);
        ConnectionError = null;
        peerSecret = RelayWire.RelayCredential(Plugin.Config.EnsurePeerSecret(), relayUrl);
        MyPeerId = RelayWire.PeerId(peerSecret);
        IsHost = false;
        RelayUrl = relayUrl;
        relayAccessToken = Plugin.Config.TokenForRelay(relayUrl);
        SessionCode = normalized;
        Session = new MultiplayerSession();
        // Seeded to now, or the host reads as silent since 1970 until its first broadcast.
        lastHostMessageMs = Environment.TickCount64;
        everHeardFromHost = false;

        DiagnosticLog.Info($"[Multiplayer] Joining session {SessionCode} at {relayUrl} as {MyPeerId} ({DisplayName}), build {PluginBuildInfo.ShortChecksum}.");
        relay = new RelayClient(peerSecret);
        WireRelay(relay);
        helloAcknowledged = false;
        helloRetryTimer = 0f;
        _ = ConnectAndHelloAsync(relayUrl, SessionCode);
    }

    private async Task ConnectAndHelloAsync(string relayUrl, string code)
    {
        await relay!.ConnectAsync(relayUrl, code, relayAccessToken);
        DiagnosticLog.Info($"[Multiplayer] Connected to relay, socket ready -- sending Hello.");
        await relay.SendAsync(new HelloMessage(MyPeerId, DisplayName, PluginBuildInfo.Version, PluginBuildInfo.Checksum, LocalClassJob));
    }

    // Disconnected captures the instance so an event from a replaced client can be told apart.
    private void WireRelay(RelayClient client)
    {
        client.MessageReceived += OnMessageReceivedOffThread;
        client.PeersRemoved += removed => OnPeersRemovedOffThread(client, removed);
        client.Disconnected += failure => OnDisconnectedOffThread(client, failure);
    }

    public void LeaveSession() => LeaveSessionInternal(notifyOthers: true);

    private void LeaveSessionInternal(bool notifyOthers)
    {
        if (SessionCode != null)
            DiagnosticLog.Info($"[Multiplayer] Leaving session {SessionCode} (was {(IsHost ? "host" : "peer")}, notifyOthers={notifyOthers}).");
        reconnectCts?.Cancel();
        reconnectCts?.Dispose();
        reconnectCts = null;
        reconnecting = false;
        ReconnectAttempt = 0;
        disconnectedSinceMs = null;
        if (IsHost) Plugin.GameInstance.PartyMemberKilled -= OnPartyMemberKilledHost;
        if (IsHost) Plugin.GameInstance.World.OmenSpawned -= OnOmenSpawnedHost;

        // Dispose only after the SessionEnded send completes, or it usually aborts before
        // reaching the wire. notifyOthers is false when reacting to someone else's
        // SessionEnded, to avoid a cascade.
        if (notifyOthers && relay is { IsConnected: true } activeRelay)
            _ = activeRelay.SendAsync(new SessionEndedMessage(MyPeerId)).ContinueWith(_ => activeRelay.Dispose());
        else
            relay?.Dispose();
        relay = null;
        Embedded.Stop();

        running = false;
        peerEntryQueued = false;
        endAfterPeerEntry = null;
        SessionCode = null;
        RelayUrl = null;
        ConnectionError = null;
        SessionEndReason = null;
        RunEndReason = null;
        warnedBadScenarioIndex = null;
        peerConnectionIds.Clear();
        bannedPeers.Clear();
        pendingRelayUnbans.Clear();
        Session = new MultiplayerSession();
        hostEnemyNetIds.Clear();
        hostEnemyLastLoggedModelState.Clear();
        hostEnemyLastLoggedStatuses.Clear();
        hostEnemyLastLoggedAnimationTimeline.Clear();
        hostEnemyLastLoggedAnimationState.Clear();
        hostEnemyLastLoggedInstantCastSeq.Clear();
        hostRoleLastLoggedStatuses.Clear();
        hostRoleLastLoggedAnimationTimeline.Clear();
        hostTetherNetIds.Clear();
        hostEventObjectNetIds.Clear();
        peerEnemies.Clear();
        peerEnemyModelState.Clear();
        peerEnemyLastLoggedStatuses.Clear();
        peerEnemyAnimationTimeline.Clear();
        peerEnemyAnimationState.Clear();
        peerEnemyLastInstantCastSeq.Clear();
        peerEnemyLastCastSeq.Clear();
        peerEnemyTemplateFailed.Clear();
        peerRoleLastLoggedStatuses.Clear();
        peerRoleAnimationTimelineSeq.Clear();
        peerRolePlayedActionSeq.Clear();
        peerRoleReconciledStatuses.Clear();
        peerEnemyReconciledStatuses.Clear();
        peerEnemyStatusInstances.Clear();
        peerRoleStatusInstances.Clear();
        peerTethers.Clear();
        peerEventObjects.Clear();
        peerEventObjectState.Clear();
        peerEventObjectAnimationSeq.Clear();
        peerEventObjectFadeSeq.Clear();
        peerEventObjectDirectorSeq.Clear();
        peerEnemyEngineSeqs.Clear();
        peerEnemyModelHidden.Clear();
        peerLastSeenMs.Clear();
        peerLastHelloMs.Clear();
        deferredLeaveMs.Clear();
        peerLatencyMs.Clear();
        peerStatuses.Clear();
        peerMitigationStatusIds.Clear();
        botControlledPeers.Clear();
        lastSentMitigationStatusIds.Clear();
        lastSentShieldFraction = 0f;
        TankShieldTracker.Reset();
        warnedStalePeers.Clear();
        pingTimer = 0f;
        pendingStartResponses = null;
        startCheckFailures.Clear();
        StartCheckFailureReason = null;
        StartWaitingOn = null;
        startCheckReplyWaited = null;
        startCheckTimer = 0f;
        debugBotControlled = false;
        pendingEndResendReturnedToInn = null;
        RestoreOwnScenarioSettings();
        DebugBotControl.Enabled = false;
    }

    public void Dispose() => LeaveSession();

    // ---- Reconnection ---------------------------------------------------

    // No attempt limit: "Leave session" is always the user's way out.
    private void BeginReconnect()
    {
        if (SessionCode == null || RelayUrl == null || reconnecting) return;
        DiagnosticLog.Info($"[Multiplayer] Connection to {RelayUrl} lost -- beginning reconnect loop for session {SessionCode}.");
        reconnecting = true;
        ReconnectAttempt = 0;
        reconnectCts = new CancellationTokenSource();
        _ = ReconnectLoopAsync(RelayUrl, SessionCode, reconnectCts.Token);
    }

    private async Task ReconnectLoopAsync(string relayUrl, string sessionCode, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var delay = ReconnectBackoff[Math.Min(ReconnectAttempt, ReconnectBackoff.Length - 1)];
            DiagnosticLog.Info($"[Multiplayer] Reconnect attempt {ReconnectAttempt + 1} in {delay.TotalSeconds}s.");
            try { await Task.Delay(delay, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            if (token.IsCancellationRequested) return;

            var client = new RelayClient(peerSecret);
            WireRelay(client);
            // OnDisconnectedOffThread ignores a client not yet installed as `relay`, so the
            // failure is captured here.
            Exception? failure = null;
            client.Disconnected += e => failure = e;
            await client.ConnectAsync(relayUrl, sessionCode, relayAccessToken).ConfigureAwait(false);
            var connected = client.IsConnected;
            DiagnosticLog.Info($"[Multiplayer] Reconnect attempt {ReconnectAttempt + 1}: {(connected ? "succeeded" : "failed")}.");

            if (!connected && failure is RelaySessionRejectedException rejected)
            {
                // The relay says the session doesn't exist (it most likely restarted); retrying
                // the same code can never succeed.
                client.Dispose();
                DiagnosticLog.Warn($"[Multiplayer] Giving up on session {sessionCode} -- relay says: {rejected.Message}.");
                var wasHost = IsHost;
                _ = Plugin.Framework.Run(() =>
                {
                    if (token.IsCancellationRequested) return;
                    // Refused mid-run (a ban, a lost room), this client would otherwise stay in the
                    // fake instance; every other way a session ends leaves it too.
                    if (Plugin.GameInstance.World.Map.IsInInstance) Plugin.GameInstance.Leave();
                    LeaveSessionInternal(notifyOthers: false);
                    SessionEndReason = rejected.Message == "banned from room"
                        ? "You were banned from the session by the host."
                        : wasHost
                            ? $"The relay lost this session ({rejected.Message}) -- start a new one."
                            : $"The relay lost this session ({rejected.Message}) -- ask the host to start a new one.";
                    LobbyChanged?.Invoke();
                });
                return;
            }

            _ = Plugin.Framework.Run(() => FinishReconnectAttempt(client, connected, token));
            if (connected) return;
            ReconnectAttempt++;
        }
    }

    private void FinishReconnectAttempt(RelayClient client, bool connected, CancellationToken token)
    {
        if (token.IsCancellationRequested || !connected)
        {
            client.Dispose();
            return;
        }
        relay = client;
        reconnecting = false;
        ReconnectAttempt = 0;
        disconnectedSinceMs = null;
        DiagnosticLog.Info($"[Multiplayer] Reconnected to session {SessionCode}.");
        lastHostMessageMs = Environment.TickCount64;
        ConnectionError = null;
        // Re-registering with the host resumes a running scenario the same way a late join does.
        if (!IsHost) _ = relay.SendAsync(new HelloMessage(MyPeerId, DisplayName, PluginBuildInfo.Version, PluginBuildInfo.Checksum, LocalClassJob));
        else foreach (var peerId in pendingRelayUnbans) _ = relay.ModerateAsync("unban", peerId);
        pendingRelayUnbans.Clear();
        LobbyChanged?.Invoke();
    }

    // ---- Role claiming ----------------------------------------------------

    public void ClaimRole(PartyRole role)
    {
        if (relay == null) return;
        if (IsHost) ApplyClaim(MyPeerId, role);
        else _ = relay.SendAsync(new ClaimRoleMessage(MyPeerId, role));
    }

    public void ReleaseRole()
    {
        if (relay == null) return;
        if (IsHost) ApplyRelease(MyPeerId);
        else _ = relay.SendAsync(new ReleaseRoleMessage(MyPeerId));
    }

    public void RequestReset()
    {
        if (IsHost || relay is not { IsConnected: true }) return;
        _ = relay.SendAsync(new ResetRequestMessage(MyPeerId));
    }

    // Peer-only: the host ends the run for the whole group (LeaveRequestMessage); LeaveSession
    // disconnects just this client.
    public void RequestLeaveInstance()
    {
        if (IsHost || relay is not { IsConnected: true }) return;
        _ = relay.SendAsync(new LeaveRequestMessage(MyPeerId));
    }

    private void ApplyClaim(Guid peerId, PartyRole role)
    {
        if (Session.ClaimedBy.TryGetValue(role, out var holder) && holder != peerId)
        {
            DiagnosticLog.Info($"[Multiplayer] Rejected role claim: {Session.NameOf(peerId)} wanted {role}, already held by {Session.NameOf(holder)}.");
            return;
        }
        // Rejected rather than kicked, so it self-resolves once the peer updates.
        if (IsVersionMismatched(peerId))
        {
            DiagnosticLog.Warn($"[Multiplayer] Rejected role claim from {Session.NameOf(peerId)} -- plugin build mismatch.");
            return;
        }
        foreach (var r in Session.ClaimedBy.Where(kv => kv.Value == peerId).Select(kv => kv.Key).ToList())
            Session.ClaimedBy.Remove(r);
        Session.ClaimedBy[role] = peerId;
        DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(peerId)} claimed {role}.");
        BroadcastLobbyState();
    }

    // An "unknown" checksum (local checksumming failed) fails open.
    public bool IsVersionMismatched(Guid peerId)
    {
        if (!Session.Builds.TryGetValue(peerId, out var build)) return false;
        if (build.Checksum == "unknown" || PluginBuildInfo.Checksum == "unknown") return false;
        return build.Checksum != PluginBuildInfo.Checksum;
    }

    private void ApplyRelease(Guid peerId)
    {
        foreach (var r in Session.ClaimedBy.Where(kv => kv.Value == peerId).Select(kv => kv.Key).ToList())
            Session.ClaimedBy.Remove(r);
        DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(peerId)} released their role.");
        BroadcastLobbyState();
    }

    // ---- Host-side roster management ----------------------------------------

    // Lobby only. The previous holder is unseated, not swapped.
    public void AssignRole(Guid peerId, PartyRole role)
    {
        if (!IsHost || Session.Started || !Session.Names.ContainsKey(peerId)) return;
        if (IsVersionMismatched(peerId))
        {
            DiagnosticLog.Warn($"[Multiplayer] Not assigning {role} to {Session.NameOf(peerId)} -- plugin build mismatch.");
            return;
        }
        if (Session.ClaimedBy.TryGetValue(role, out var holder) && holder != peerId)
        {
            Session.ClaimedBy.Remove(role);
            DiagnosticLog.Info($"[Multiplayer] Host unseated {Session.NameOf(holder)} from {role} to assign it to {Session.NameOf(peerId)}.");
        }
        foreach (var r in Session.ClaimedBy.Where(kv => kv.Value == peerId).Select(kv => kv.Key).ToList())
            Session.ClaimedBy.Remove(r);
        Session.ClaimedBy[role] = peerId;
        DiagnosticLog.Info($"[Multiplayer] Host assigned {role} to {Session.NameOf(peerId)}.");
        BroadcastLobbyState();
    }

    public void UnassignRole(Guid peerId)
    {
        if (!IsHost || Session.Started) return;
        ApplyRelease(peerId);
    }

    // By stable per-install PeerId, with the name for the ban list. A banned client's messages
    // are dropped and the kick repeated (see DispatchCore); a plain kick leaves the way back open.
    private readonly Dictionary<Guid, string> bannedPeers = new();
    // Unbans made while disconnected; the relay keeps its own ban list, so they're sent on reconnect.
    private readonly HashSet<Guid> pendingRelayUnbans = new();
    public IReadOnlyDictionary<Guid, string> BannedPeers => bannedPeers;

    public void KickPeer(Guid peerId) => RemoveByHost(peerId, ban: false);
    public void BanPeer(Guid peerId) => RemoveByHost(peerId, ban: true);

    private void RemoveByHost(Guid peerId, bool ban)
    {
        if (!IsHost || peerId == MyPeerId || relay == null) return;
        var who = Session.NameOf(peerId);
        DiagnosticLog.Info($"[Multiplayer] Host {(ban ? "banned" : "kicked")} {who} ({peerId}).");
        if (ban) bannedPeers[peerId] = who;
        _ = relay.SendAsync(new KickMessage(peerId, ban));
        // The relay closes their connection too, so a client that ignores the KickMessage is
        // still out, and a banned one can't come back under this identity or address.
        _ = relay.ModerateAsync(ban ? "ban" : "kick", peerId);
        RemovePeer(peerId);
    }

    public void UnbanPeer(Guid peerId)
    {
        if (!IsHost || !bannedPeers.Remove(peerId, out var who)) return;
        if (relay is { IsConnected: true }) _ = relay.ModerateAsync("unban", peerId);
        else pendingRelayUnbans.Add(peerId);
        DiagnosticLog.Info($"[Multiplayer] Host unbanned {who} ({peerId}).");
        LobbyChanged?.Invoke();
    }

    // Both the display summary and the overrides themselves: a peer's own code reads them too
    // (IScenario.RunInstanceEvents), so the host's choices have to reach it.
    public void PublishScenarioSettings(IScenario? scenario)
    {
        if (!IsHost) return;
        var overrides = scenario is { SupportsMultiplayer: true } ? scenario.SettingsOverrides : null;
        var lines = ScenarioSettingsSummary.Describe(overrides);
        var json = ScenarioSettingsSync.Serialize(overrides);
        if (lines.SequenceEqual(Session.ScenarioSettings) && json == Session.ScenarioSettingsJson) return;
        Session.ScenarioSettings = lines;
        Session.ScenarioSettingsJson = json;
        BroadcastLobbyState();
    }

    // Peer-only: the host's overrides for the selected scenario, applied to our own instance of
    // them. Ours are put back when the session ends, so a lobby can't leave its debug knobs
    // behind on someone's solo settings.
    private string? appliedScenarioSettingsJson;
    private (object Target, string Json)? scenarioSettingsBackup;

    private void ApplyHostScenarioSettings()
    {
        if (IsHost) return;
        var json = Session.ScenarioSettingsJson;
        if (json == appliedScenarioSettingsJson) return;
        if (string.IsNullOrEmpty(json) || TryResolveScenario()?.SettingsOverrides is not { } overrides) return;
        if (scenarioSettingsBackup is not { } backup || !ReferenceEquals(backup.Target, overrides))
        {
            RestoreOwnScenarioSettings();
            if (ScenarioSettingsSync.Serialize(overrides) is { } ownJson) scenarioSettingsBackup = (overrides, ownJson);
        }
        appliedScenarioSettingsJson = json;
        ScenarioSettingsSync.Apply(overrides, json);
    }

    private void RestoreOwnScenarioSettings()
    {
        appliedScenarioSettingsJson = null;
        if (scenarioSettingsBackup is not { } backup) return;
        scenarioSettingsBackup = null;
        DiagnosticLog.Info($"[Multiplayer] Restoring our own {backup.Target.GetType().Name} settings.");
        ScenarioSettingsSync.Apply(backup.Target, backup.Json);
    }

    // Mirrors the main-window selection pre-start so the lobby can name it.
    public void PublishSelectedScenario(IScenario? scenario)
    {
        if (!IsHost || Session.Started || scenario == null) return;
        var index = Plugin.GameInstance.Scenarios.ToList().IndexOf(scenario);
        if (index < 0 || index == Session.ScenarioIndex) return;
        Session.ScenarioIndex = index;
        BroadcastLobbyState();
    }

    // Drops the peer from the roster entirely, unlike ApplyRelease.
    private void RemovePeer(Guid peerId)
    {
        var who = Session.NameOf(peerId);
        DiagnosticLog.Info($"[Multiplayer] Removing {who} ({peerId}) from the session (running={running}).");
        foreach (var r in Session.ClaimedBy.Where(kv => kv.Value == peerId).Select(kv => kv.Key).ToList())
        {
            Session.ClaimedBy.Remove(r);
            // Their banked shield must not linger onto the role's next claimant.
            TankShieldTracker.SetFromPeerReport(r, 0f);
        }
        Session.Names.Remove(peerId);
        Session.Builds.Remove(peerId);
        peerConnectionIds.Remove(peerId);
        peerLastSeenMs.Remove(peerId);
        peerLastHelloMs.Remove(peerId);
        deferredLeaveMs.Remove(peerId);
        peerLatencyMs.Remove(peerId);
        peerStatuses.Remove(peerId);
        peerMitigationStatusIds.Remove(peerId);
        botControlledPeers.Remove(peerId);
        warnedStalePeers.Remove(peerId);
        startCheckFailures.Remove(peerId);
        if (pendingStartResponses?.Remove(peerId) == true && pendingStartResponses.Count == 0)
            FinishStartCheck();
        // A missing party member usually dooms the mechanic; Tick() broadcasts the end once
        // ActiveScenario clears.
        if (running && Plugin.GameInstance.World.Map.IsInInstance)
        {
            DiagnosticLog.Info($"[Multiplayer] Ending the run because {who} left mid-fight.");
            Plugin.GameInstance.Leave();
        }
        BroadcastLobbyState();
    }

    // A job only reaches the roster on a Hello, so a lobby job change needs re-announcing.
    private byte announcedClassJob;

    private void AnnounceOwnJobIfChanged()
    {
        var job = LocalClassJob;
        if (job == 0 || job == announcedClassJob) return;
        announcedClassJob = job;
        if (IsHost)
        {
            Session.Jobs[MyPeerId] = job;
            BroadcastLobbyState();
            return;
        }
        _ = relay?.SendAsync(new HelloMessage(MyPeerId, DisplayName, PluginBuildInfo.Version, PluginBuildInfo.Checksum, job));
    }

    // A Hello is the peer's only way into the roster and can be lost; retried until a lobby
    // state comes back naming us.
    private const float HelloRetrySeconds = 2f;
    private bool helloAcknowledged;
    private float helloRetryTimer;

    private void ResendHelloUntilAcknowledged(float deltaSeconds)
    {
        if (IsHost || helloAcknowledged) return;
        helloRetryTimer += deltaSeconds;
        if (helloRetryTimer < HelloRetrySeconds) return;
        helloRetryTimer = 0f;
        DiagnosticLog.Info($"[Multiplayer] No lobby state naming us yet -- re-sending Hello to session {SessionCode}.");
        _ = relay?.SendAsync(new HelloMessage(MyPeerId, DisplayName, PluginBuildInfo.Version, PluginBuildInfo.Checksum, LocalClassJob));
    }

    // The host only learns our name from a Hello, so being in the roster is the acknowledgement.
    private void NoteHelloAcknowledged()
    {
        if (helloAcknowledged || !Session.Names.ContainsKey(MyPeerId)) return;
        helloAcknowledged = true;
        DiagnosticLog.Info("[Multiplayer] Host's lobby state now names us -- Hello acknowledged.");
    }

    private void BroadcastLobbyState()
    {
        LobbyChanged?.Invoke();
        _ = relay?.SendAsync(LobbyMessage());
    }

    private LobbyStateMessage LobbyMessage() => Session.ToMessage() with { Clock = HostRunClock() };

    // Null until the host's own run is up, so a broadcast while the start is queued can't hand
    // out the idle clock.
    private RunClockState? HostRunClock()
    {
        if (!IsHost || !running || Plugin.GameInstance.ActiveScenario is not { } active) return null;
        return new RunClockState(Plugin.GameInstance.EventClockNow, averageFrameSeconds);
    }

    // ---- Starting the scenario ---------------------------------------------

    // Also printed to chat: by the time it matters everyone is back in the inn and the window
    // may be closed.
    public string? RunEndReason { get; private set; }

    internal void AnnounceRunEnded(string reason)
    {
        RunEndReason = reason;
        DiagnosticLog.Warn($"[Multiplayer] Run ended: {reason}");
        Plugin.ChatGui.PrintError($"[AnoMech] Run ended -- {reason}");
        LobbyChanged?.Invoke();
    }

    private void AbortStart(string reason)
    {
        DiagnosticLog.Warn($"[Multiplayer] Refusing the host's start: {reason}.");
        _ = relay?.SendAsync(new StartAbortMessage(MyPeerId, reason));
        AnnounceRunEnded($"you couldn't start: {reason}");
    }

    // The preconditions RunScenarioInternal enforces, checked up front so a failure is reported
    // instead of a silent no-op. A claimed tank role must be on a tank job: bot mitigation picks
    // ability ids off the seat's job.
    private string? CheckOwnStartReadiness() => CheckOwnStartReadiness(out _);

    private string? CheckOwnStartReadiness(out string? settling)
    {
        if (ZoneSession.StartBlockedReason(out settling) is { } blocked) return blocked;
        if (MyClaimedRole is { } role && role.IsTank())
        {
            var jobId = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
            if (!PartyPresets.SkipRoleForJob(jobId).IsTank())
                return "queued as a tank role but not on a tank job";
        }
        return null;
    }

    // Doesn't start immediately: every claimed peer first confirms readiness
    // (StartCheckMessage), so a peer who isn't in an inn fails loudly here rather than
    // silently seconds later.
    public void StartScenario()
    {
        if (!IsHost || relay == null) return;
        if (!IsConnected)
        {
            DiagnosticLog.Warn("[Multiplayer] Cannot start: not connected to the relay.");
            return;
        }
        if (MyClaimedRole == null)
        {
            DiagnosticLog.Warn("[Multiplayer] Cannot start: host has not claimed a role.");
            return;
        }
        if (Plugin.MainWindow.SelectedScenario is not { } selectedScenario
            || !selectedScenario.SupportsMultiplayer)
        {
            DiagnosticLog.Warn("[Multiplayer] Cannot start: no multiplayer-supported scenario is selected in the main window.");
            return;
        }
        // A region with no strats leaves SelectedStrat at -1, which a debug-bot peer would index with.
        if (!Plugin.MainWindow.HasStartableStrat())
        {
            DiagnosticLog.Warn("[Multiplayer] Cannot start: no strat available for the selected scenario/region.");
            return;
        }
        // Starting anyway would drop whatever the slot layout can't seat, giving the host a run
        // they didn't set up.
        if (selectedScenario.SettingsConflicts is { Count: > 0 } conflicts)
        {
            foreach (var conflict in conflicts)
                DiagnosticLog.Warn($"[Multiplayer] Cannot start: {conflict}");
            return;
        }
        var scenarioIndex = Plugin.GameInstance.Scenarios.ToList().IndexOf(selectedScenario);
        Session.ScenarioIndex = scenarioIndex;
        Session.SelectedAi = Plugin.MainWindow.SelectedStrat;
        Session.SelectedWaymark = Plugin.MainWindow.SelectedWaymark;
        // ApplyClaim already rejects these; this closes the race.
        if (Session.ClaimedBy.Values.Any(IsVersionMismatched))
        {
            DiagnosticLog.Warn("[Multiplayer] Cannot start: one or more claimed players are on a different plugin build.");
            return;
        }
        if (IsStartCheckPending) return;

        if (CheckOwnStartReadiness(out var settling) is { } ownReason)
        {
            if (settling != null)
            {
                if (StartWaitingOn == null) DiagnosticLog.Info($"[Multiplayer] Start waiting for {settling} to settle.");
                StartWaitingOn = settling;
                StartCheckFailureReason = null;
                LobbyChanged?.Invoke();
                return;
            }
            StartWaitingOn = null;
            StartCheckFailureReason = $"You cannot start: {ownReason}.";
            DiagnosticLog.Info($"[Multiplayer] Cannot start: {ownReason}.");
            LobbyChanged?.Invoke();
            return;
        }

        StartWaitingOn = null;
        StartCheckFailureReason = null;
        RunEndReason = null;
        startCheckFailures.Clear();
        startCheckTimer = 0f;
        pendingStartResponses = Session.ClaimedBy.Values.Where(id => id != MyPeerId).ToHashSet();
        // Don't wait out the full timeout for someone already known gone.
        foreach (var peerId in pendingStartResponses.ToList())
        {
            if (!IsPeerStale(peerId)) continue;
            startCheckFailures[peerId] = "disconnected";
            pendingStartResponses.Remove(peerId);
        }
        DiagnosticLog.Info($"[Multiplayer] Start requested -- waiting on readiness from: {string.Join(", ", pendingStartResponses.Select(Session.NameOf))}.");
        _ = relay.SendAsync(new StartCheckMessage());
        LobbyChanged?.Invoke();

        if (pendingStartResponses.Count == 0) FinishStartCheck();
    }

    // Anything but a settle in the way surfaces through StartScenario's own refusal.
    private void RetryWaitingStart()
    {
        if (StartWaitingOn is not { } waitingOn) return;
        if (ZoneSession.StartBlockedReason(out var settling) != null && settling != null)
        {
            if (settling == waitingOn) return;
            StartWaitingOn = settling;
            LobbyChanged?.Invoke();
            return;
        }
        StartWaitingOn = null;
        StartScenario();
    }

    private void AnswerStartCheck(float deltaSeconds)
    {
        if (startCheckReplyWaited is not { } waited) return;
        waited += deltaSeconds;
        var reason = CheckOwnStartReadiness(out var settling);
        if (settling != null && waited < StartCheckReplyMaxWaitSeconds)
        {
            if (startCheckReplyWaited == 0f) DiagnosticLog.Info($"[Multiplayer] Start check: waiting for {settling} to settle before answering.");
            startCheckReplyWaited = waited;
            return;
        }
        startCheckReplyWaited = null;
        _ = relay?.SendAsync(new StartCheckResponseMessage(MyPeerId, reason == null, reason));
    }

    private void FinishStartCheck()
    {
        pendingStartResponses = null;
        if (startCheckFailures.Count > 0)
        {
            var summary = string.Join(", ", startCheckFailures.Select(kv => $"{Session.NameOf(kv.Key)} ({kv.Value})"));
            StartCheckFailureReason = $"{startCheckFailures.Count} player(s) cannot start: {summary}.";
            DiagnosticLog.Info($"[Multiplayer] Start check failed: {StartCheckFailureReason}");
            LobbyChanged?.Invoke();
            return;
        }
        DiagnosticLog.Info("[Multiplayer] Start check passed -- starting the scenario.");
        ActuallyStartScenario();
    }

    private void ActuallyStartScenario()
    {
        if (MyClaimedRole is not { } myRole) return;

        if (TryResolveScenario() is not { } scenario) return;
        var networkRoles = Session.ClaimedBy.Where(kv => kv.Value != MyPeerId).Select(kv => kv.Key).ToHashSet();
        DiagnosticLog.Info($"[Multiplayer] Host starting '{scenario.Name}' as {myRole}. Network roles: {string.Join(", ", networkRoles.Select(r => $"{r}={Session.NameOf(Session.ClaimedBy[r])}"))}.");

        // Locks the lobby now; peers are only told once the host's own run is up.
        Session.Started = true;

        hostEnemyNetIds.Clear();
        hostEnemyLastLoggedModelState.Clear();
        hostEnemyLastLoggedStatuses.Clear();
        hostEnemyLastLoggedAnimationTimeline.Clear();
        hostEnemyLastLoggedAnimationState.Clear();
        hostEnemyLastLoggedInstantCastSeq.Clear();
        hostRoleLastLoggedStatuses.Clear();
        hostRoleLastLoggedAnimationTimeline.Clear();
        hostTetherNetIds.Clear();
        hostEventObjectNetIds.Clear();
        nextEnemyNetId = 0;
        nextTetherNetId = 0;
        nextEventObjectNetId = 0;
        warnedStalePeers.Clear();
        pendingEndResendReturnedToInn = null;
        hostScenarioStarted = false;
        var nowMs = Environment.TickCount64;
        foreach (var peerId in Session.ClaimedBy.Values)
            if (peerId != MyPeerId)
                peerLastSeenMs[peerId] = nowMs;
        Plugin.GameInstance.PartyMemberKilled += OnPartyMemberKilledHost;
        Plugin.GameInstance.World.OmenSpawned += OnOmenSpawnedHost;
        // scenario.Run schedules the Ai against every role; this lets it move the host's own
        // character.
        if (debugBotControlled)
        {
            DiagnosticLog.Info("[Multiplayer] Host: debug-bot mode active for own character this run.");
            DebugBotControl.Enabled = true;
        }
        running = true;
        Plugin.GameInstance.RunScenarioAsHost(scenario, myRole, Session.SelectedAi, Session.SelectedWaymark, networkRoles, ClaimedRoleSeats(), OnHostStartResolved);
        LobbyChanged?.Invoke();
    }

    // Called from RunScenarioInternal's own callback, so peers only hear Start once the host's run
    // exists, and the party exists for the obstacle field (separate Framework.Run calls aren't ordered).
    private void OnHostStartResolved(string? refusal)
    {
        // Ended while the start was queued.
        if (!running) return;
        if (refusal != null)
        {
            DiagnosticLog.Warn($"[Multiplayer] Host's own start was refused: {refusal}.");
            StartCheckFailureReason = $"You couldn't start: {refusal}.";
            if (Plugin.GameInstance.World.Map.IsInInstance) Plugin.GameInstance.Leave();
            // Any lobby broadcast made while the start was queued carried Started.
            BroadcastRunEnded(returnedToInn: true);
            return;
        }
        _ = relay?.SendAsync(LobbyMessage());
        _ = relay?.SendAsync(new StartMessage(HostRunClock()));
        if (debugBotControlled) GiveLocalPlayerObstacles();
    }

    private void OnStartReceived(RunClockState? clock)
    {
        // Idempotent: a fresh start delivers both LobbyState(Started) and StartMessage, and a
        // late join replays it from LobbyState.
        if (IsHost) return;
        if (running)
        {
            DiagnosticLog.Debug("[Multiplayer] OnStartReceived: already running -- ignoring (idempotency guard).");
            return;
        }
        if (MyClaimedRole is not { } myRole)
        {
            DiagnosticLog.Warn("[Multiplayer] Host started the scenario, but I never claimed a role -- ignoring.");
            return;
        }

        if (TryResolveScenario() is not { } scenario)
        {
            AbortStart("the host chose a scenario this build doesn't have");
            return;
        }
        // The start check is advisory (a late join skips it, and state changes in between);
        // RunScenarioInternal would silently no-op, so refuse and say why.
        if (CheckOwnStartReadiness() is { } notReady)
        {
            AbortStart(notReady);
            return;
        }

        RunEndReason = null;
        var networkRoles = Enum.GetValues<PartyRole>().Where(r => r != myRole).ToHashSet();
        DiagnosticLog.Info($"[Multiplayer] Peer entering '{scenario.Name}' as {myRole}.");

        peerEnemies.Clear();
        peerEnemyModelState.Clear();
        peerEnemyLastLoggedStatuses.Clear();
        peerEnemyAnimationTimeline.Clear();
        peerEnemyAnimationState.Clear();
        peerEnemyLastInstantCastSeq.Clear();
        peerEnemyLastCastSeq.Clear();
        peerEnemyTemplateFailed.Clear();
        peerRoleLastLoggedStatuses.Clear();
        peerRoleAnimationTimelineSeq.Clear();
        peerRolePlayedActionSeq.Clear();
        peerRoleReconciledStatuses.Clear();
        peerEnemyReconciledStatuses.Clear();
        peerEnemyStatusInstances.Clear();
        peerRoleStatusInstances.Clear();
        peerTethers.Clear();
        peerEventObjects.Clear();
        peerEventObjectState.Clear();
        peerEventObjectAnimationSeq.Clear();
        peerEventObjectFadeSeq.Clear();
        peerEventObjectDirectorSeq.Clear();
        peerEnemyEngineSeqs.Clear();
        peerEnemyModelHidden.Clear();
        peerEnteredInstance = false;
        peerEntryQueued = true;
        endAfterPeerEntry = null;
        hostClockAtStart = clock;
        hostClockReceivedAt = Stopwatch.GetTimestamp();
        eventClockSynced = false;
        DebugBotControl.Enabled = false;
        running = true;
        Plugin.GameInstance.RunScenarioAsPeer(scenario, myRole, Session.SelectedWaymark, networkRoles, ClaimedRoleSeats(), OnPeerStartResolved);
    }

    // Called from RunScenarioInternal's own callback, so snapshots and debug-bot control only
    // touch this run's party (a Reset leaves the zone loaded, so IsInInstance can't tell).
    private void OnPeerStartResolved(string? refusal)
    {
        peerEntryQueued = false;
        var end = endAfterPeerEntry;
        endAfterPeerEntry = null;
        if (!running)
        {
            // Ended while the entry was queued: what the end would have done had the zone been
            // entered then.
            if (refusal != null) return;
            if (end == false) Plugin.GameInstance.Reset();
            else if (Plugin.GameInstance.World.Map.IsInInstance) Plugin.GameInstance.Leave();
            return;
        }
        if (refusal != null)
        {
            running = false;
            AbortStart(refusal);
            return;
        }
        DiagnosticLog.Info("[Multiplayer] Peer's deferred zone entry completed -- applying snapshots and sending SelfPose.");
        peerEnteredInstance = true;
        // The host's scenario schedules the Ai against our puppet and forwards each move to us;
        // this lets PlayerMovement act on them.
        if (debugBotControlled)
        {
            DiagnosticLog.Info("[Multiplayer] Peer: debug-bot mode active for own character this run.");
            DebugBotControl.Enabled = true;
            GiveLocalPlayerObstacles();
        }
    }

    // Starting from zero would leave this run behind the host's by the host's load time plus the
    // travel time. It also runs ahead of the host by our poses' own delay (the trip back, plus
    // about a host frame since the host applies poses after that frame's hit checks), so the host
    // judges our character in step with its own bots. Compared at this frame's event tick, the
    // instant Events.Elapsed describes.
    private void SyncClocksToHost()
    {
        if (hostClockAtStart is not { } clock) return;
        if (eventClockSynced) return;
        var game = Plugin.GameInstance;
        var eventAtStart = NetGuard.Clamp(clock.EventClock, 0f, 3600f);
        var oneWay = NetGuard.Clamp(peerStatuses.GetValueOrDefault(MyPeerId)?.LatencyMs ?? 0f, 0f, 4000f) / 2000f;
        var hostFrame = NetGuard.Clamp(clock.FrameSeconds, 0f, MaxFrameSampleSeconds);
        var poseLead = oneWay + (hostFrame > 0f ? hostFrame : 1f / 60f);
        var target = eventAtStart + oneWay + poseLead
            + (float)Stopwatch.GetElapsedTime(hostClockReceivedAt, game.LastEventTick).TotalSeconds;

        eventClockSynced = true;
        var advance = target - game.Events.Elapsed;
        game.Events.Advance(advance);
        DiagnosticLog.Info($"[Multiplayer] Peer: run clock {(advance > 0f ? $"moved up {advance * 1000f:F0} ms" : "left as is")}: the host's time plus a {poseLead * 1000f:F0} ms lead (Start sent {eventAtStart:F3}s into the host's run, {oneWay * 1000f:F0} ms one-way, {hostFrame * 1000f:F1} ms host frame).");
    }

    // PartyCreator wires the obstacle field to bot doppels only; a bot-driven real character
    // steers like one.
    private static void GiveLocalPlayerObstacles()
    {
        var world = Plugin.GameInstance.World;
        if (world.Party.Player is { } player) player.Obstacles = world.Obstacles;
    }

    // Names for the puppets: every role claimed by someone else, the host's included from a
    // peer's side.
    private Dictionary<PartyRole, NetworkSeat> ClaimedRoleSeats() =>
        Session.ClaimedBy.Where(kv => kv.Value != MyPeerId)
            .ToDictionary(kv => kv.Key, kv => new NetworkSeat(Session.NameOf(kv.Value), Session.JobOf(kv.Value)));

    private static byte LocalClassJob => (byte)(Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0);

    private string DescribeRoleOwner(PartyRole role, SimCharacter? member)
    {
        if (member == null) return "empty";
        if (member is SimNetworkPuppet puppet) return $"{puppet.DisplayName}, job {puppet.ClassJob}";
        if (ReferenceEquals(member, Plugin.GameInstance.World.Party.Player))
            return $"{DisplayName} (me), job {Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId.ToString() ?? "?"}";
        return "bot";
    }

    // Keyed by id and its ordinal, so a character holding the same id twice logs both.
    private static void LogStatusChanges(string who, IReadOnlyList<(ushort StatusId, ushort Stacks, float RemainingTime)> current, Dictionary<(ushort Id, int Ordinal), ushort> lastSeen)
    {
        var currentIds = new HashSet<(ushort Id, int Ordinal)>();
        var ordinals = new Dictionary<ushort, int>();
        foreach (var (id, stacks, remaining) in current)
        {
            var ordinal = ordinals.GetValueOrDefault(id);
            ordinals[id] = ordinal + 1;
            var key = (id, ordinal);
            currentIds.Add(key);
            if (!lastSeen.TryGetValue(key, out var lastStacks))
                DiagnosticLog.Info($"[Multiplayer] {who}: status {id} gained (stacks={stacks}, duration={remaining:F1}, #{ordinal + 1}).");
            else if (lastStacks != stacks)
                DiagnosticLog.Info($"[Multiplayer] {who}: status {id} stacks {lastStacks}->{stacks}.");
            lastSeen[key] = stacks;
        }
        foreach (var key in lastSeen.Keys.Where(k => !currentIds.Contains(k)).ToList())
        {
            DiagnosticLog.Info($"[Multiplayer] {who}: status {key.Id} lost.");
            lastSeen.Remove(key);
        }
    }

}
