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
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Multiplayer;

// Session lifecycle and the host<->peer replication loop. The host runs the real scenario
// with joined peers' roles spawned as SimNetworkPuppet; peers run no scenario logic, apply
// what the host broadcasts, and report their own position back (SelfPose).
//
// Everything here runs on the framework thread: Tick() via OnFrameworkUpdate, and relay
// messages are queued and drained there (see DrainPendingMessages).
public sealed partial class MultiplayerManager : IDisposable
{
    private IRelayConnection? relay;
    private bool running;

    // The relay link, the clock and the seed source: the game's own, or a test's.
    internal Func<string, IRelayConnection> RelayFactory { get; init; } = secret => new RelayClient(secret);
    internal TimeProvider Time { get; init; } = TimeProvider.System;
    internal Func<int> NextSeed { get; init; } = () => Random.Shared.Next();
    private long NowMs => Time.GetTimestamp() * 1000 / Time.TimestampFrequency;

    // Host: its natives for the stay, recording what its sim sends; peer: its own, and the replay
    // of the host's packets on them.
    private PacketRecorder? recorder;
    private PeerNatives? peerNatives;
    private PacketReplay? replay;

    // Host: this run's params, pinned at the enter (its seed seated the party), the peers yet to
    // enter, and how long its stage has stood.
    private RunScenarioParams? hostRunParams;
    private int? hostSeed;
    private HashSet<Guid>? pendingEntered;
    private float enterTimer;
    private float? hostStageSetFor;

    // Peer: set once this run's zone entry and seating have completed. A Reset leaves the zone
    // loaded, so IsInInstance alone can't tell this run's party from the last one's.
    private bool peerEnteredInstance;
    private bool peerEntryQueued;
    private bool peerRunStarted;
    // From our run's start until it is up, nothing more is taken from the host: the host set its
    // own run up before sending any of it, and its packets need that setup here too (the run's
    // strings and files, its preloads).
    private bool peerRunStartPending;
    private RunScenarioParams? peerRunParams;
    // The host's packets that came before our entry completed.
    private readonly List<GamePacket> earlyPackets = new();
    // An EndMessage that arrived while the entry was queued: acted on once it completes.
    private bool? endAfterPeerEntry;
    // The host's clock from the message that started this run, and when it arrived; applied by
    // SyncClockToHost once the entry completes.
    private RunClockState? hostClockAtStart;
    private long hostClockReceivedAt;
    private bool eventClockSynced;
    // Smoothed frame time, sent in RunClockState; load hitches are left out.
    private float averageFrameSeconds = 1f / 60f;
    private const float MaxFrameSampleSeconds = 0.1f;
    private const float FrameSmoothing = 0.05f;
    // The run clock advances once per frame by that frame's whole delta; a clock sent between
    // frames extrapolates from the stamp. Only while Elapsed still holds the stamped value: once
    // the clock has moved since (this frame's Game.Tick, a reset, an Advance) Elapsed is fresher.
    private long eventClockStampedAt;
    private float eventClockAtStamp;

    // ---- Connection-quality tracking (runs in the lobby too) ---------------
    private const float PingIntervalSeconds = 2f;
    private const long PeerStaleTimeoutMs = 8000;
    // Catches a mistyped/nonexistent code; the relay has no "session not found" at the
    // transport level. Counted from our first Hello: connecting can take longer, e.g. a bare
    // address dials wss:// before falling back to ws://.
    private const long NoHostFoundTimeoutMs = 4000;
    // A join still not answered by its host this long after Join gives up, whichever part is
    // slow: connecting, reconnecting, or the host answering.
    private const long JoinDeadlineMs = 15000;
    private float pingTimer;
    private readonly Dictionary<Guid, long> peerLastSeenMs = new();
    // When each peer last registered. A leave that arrives right after one crossed the peer's
    // own rejoin and must not evict them again -- see the SessionEndedMessage case.
    private readonly Dictionary<Guid, long> peerLastHelloMs = new();
    // Leaves held back by that grace: confirmed unless the peer is heard from again.
    private readonly Dictionary<Guid, long> deferredLeaveMs = new();
    private readonly Dictionary<Guid, float> peerLatencyMs = new();
    // Host: when the recent pings went out, and the newest each peer has answered (PingClockMs).
    private readonly Queue<long> pingsSentMs = new();
    private readonly Dictionary<Guid, long> answeredPingMs = new();
    private readonly HashSet<Guid> warnedStalePeers = new();
    // Rebuilt by the host each ping cycle and broadcast (PeerStatusMessage).
    private readonly Dictionary<Guid, PeerStatusEntry> peerStatuses = new();
    // Peer-only: the host never pings itself, so its liveness is the time since any host broadcast.
    private long lastHostMessageMs;
    // lastHostMessageMs is seeded to "now" on join, so it alone can't tell "never heard" from
    // "went silent".
    private bool everHeardFromHost;
    // When the current connection's Hello left; 0 until it has. Written off the framework thread.
    private long helloSentMs;
    private long joinStartedMs;
    // Whether this join ever got a Hello out to the relay. Written off the framework thread.
    private volatile bool joinReachedRelay;

    // ---- Pre-start readiness check (see StartScenario/FinishStartCheck) -----
    private const float StartCheckTimeoutSeconds = 5f;
    private HashSet<Guid>? pendingStartResponses;
    private readonly Dictionary<Guid, string> startCheckFailures = new();
    private float startCheckTimer;
    public bool IsStartCheckPending => pendingStartResponses != null;
    public string? StartCheckFailureReason { get; private set; }
    private readonly SettleWait startWait = new();
    public string? StartWaitingOn => startWait.WaitingOn;
    // A settling peer answers once settled, inside the host's StartCheckTimeoutSeconds.
    private const float StartCheckReplyMaxWaitSeconds = 4f;
    private float? startCheckReplyWaited;

    // ---- Debug: bot-controlled host or peer ---------------------------------
    // Testing aid: the user's own role is driven by the bot AI, so one developer can fill a
    // session alone. The host's scenario.Run already schedules that choreography (the flag just
    // lets PlayerMotion act on the player's orders); a peer rebuilds it from the host's
    // replay-state message. Sticky across Start/Reset; lobby-only to toggle.
    private bool debugBotControlled;
    public bool DebugBotControlled => debugBotControlled;

    private bool aiReplayStateSent;

    // The deferred ZoneIn/StartScenario set ActiveScenario frames late; without this Tick() would read the
    // null as "run ended".
    private bool hostScenarioStarted;

    // Peer-only: the host's replay-state message, buffered until the zone is entered (arrival
    // order isn't guaranteed), and the opaque shadow state the owning scenario built from it.
    private MpMessage? pendingGenericReplayState;
    private object? debugShadowStateGeneric;
    private bool debugBotReplayStarted;

    // Every client judges a seat its debug bot plays as a bot (SimParty.IsBotDriven), so the roster
    // carries it: a peer's Hello goes again until a lobby state shows it.
    public bool SetDebugBotControlled(bool value)
    {
        if (running) return false;
        if (debugBotControlled == value) return true;
        debugBotControlled = value;
        if (IsHost)
        {
            SetDebugBot(MyPeerId, value);
            if (relay != null) BroadcastLobbyState();
        }
        else if (SessionCode != null)
        {
            helloAcknowledged = false;
            helloRetryTimer = 0f;
            Transmit(NewHello(LocalClassJob));
        }
        return true;
    }

    private void SetDebugBot(Guid peerId, bool debugBot)
    {
        if (debugBot) Session.DebugBots.Add(peerId);
        else Session.DebugBots.Remove(peerId);
    }

    // ---- Reconnection --------------------------------------------------------
    // Identity survives a reconnect via Configuration.PeerSecret, and the host keeps a stale
    // peer's role, so rejoining resumes where it left off.
    private static readonly TimeSpan[] ReconnectBackoff =
        { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(15) };
    private CancellationTokenSource? reconnectCts;
    private bool reconnecting;
    private bool reconnectAttemptInFlight;
    private long reconnectDueMs;
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
    // Servers still to try, best first, while hosting hasn't been given a code.
    private readonly Queue<string> hostFallbacks = new();
    // Already sent on by a relay once (RelayWire.MovedTo); a second time, the group's relays disagree.
    private bool joinRedirected;
    // Why the session ended, when it wasn't this client's own Leave.
    public string? SessionEndReason { get; private set; }

    public PartyRole? MyClaimedRole => Session.RoleOf(MyPeerId);

    // ScenarioIndex comes off the wire; every read goes through here rather than indexing
    // Scenarios directly.
    internal IScenario? TryResolveScenario()
    {
        var scenarios = Plugin.Game.Catalog.Scenarios;
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

    public float SecondsSinceHostMessage => (NowMs - lastHostMessageMs) / 1000f;
    // SessionCode is set synchronously on Join; this is what confirms a host is actually there.
    public bool EverHeardFromHost => everHeardFromHost;
    public bool IsHostStale => !IsHost && everHeardFromHost && SecondsSinceHostMessage * 1000f > PeerStaleTimeoutMs;
    public bool IsSessionNotFound
    {
        get
        {
            var helloSent = Interlocked.Read(ref helloSentMs);
            return !IsHost && !everHeardFromHost && helloSent != 0 && NowMs - helloSent > NoHostFoundTimeoutMs;
        }
    }
    public bool IsJoinTimedOut => !IsHost && SessionCode != null && !everHeardFromHost && NowMs - joinStartedMs > JoinDeadlineMs;
    public float JoinElapsedSeconds => (NowMs - joinStartedMs) / 1000f;
    public static float JoinDeadlineSeconds => JoinDeadlineMs / 1000f;
    public bool IsWaitingForHost => IsConnected && Interlocked.Read(ref helloSentMs) != 0;

    // ---- Session lifecycle ----------------------------------------------

    public void HostSession(string relayUrl) => HostSession([relayUrl]);

    public void HostSession(IReadOnlyList<string> relayUrls)
    {
        LeaveSession();
        foreach (var url in relayUrls.Skip(1)) hostFallbacks.Enqueue(url);
        HostSessionCore(relayUrls[0]);
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
        OpenStream(host: true);
        Session.Names[MyPeerId] = DisplayName;
        Session.Jobs[MyPeerId] = LocalClassJob;
        Session.Builds[MyPeerId] = new PeerBuildInfo(PluginBuildInfo.Version, PluginBuildInfo.Checksum);
        // Toggled while an earlier server was tried (hostFallbacks).
        SetDebugBot(MyPeerId, debugBotControlled);

        DiagnosticLog.Info($"[Multiplayer] Hosting a new session at {relayUrl} as {MyPeerId} ({DisplayName}), build {PluginBuildInfo.ShortChecksum}.");
        var client = RelayFactory(peerSecret);
        WireRelay(client);
        relay = client;
        _ = FinishHostConnectAsync(client);
        LobbyChanged?.Invoke();
    }

    // The ReferenceEquals check keeps a late-arriving code from resurrecting a session that was
    // left mid-connect.
    private async Task FinishHostConnectAsync(IRelayConnection client)
    {
        var code = await client.ConnectAndHostAsync(RelayUrl!, relayAccessToken);
        if (!ReferenceEquals(relay, client)) return;
        if (code is null) return; // Disconnected already fired
        SessionCode = code;
        DiagnosticLog.Info($"[Multiplayer] Relay assigned session code {code}.");
        LobbyChanged?.Invoke();
    }

    public void JoinSession(string relayUrl, string code) => JoinSessionCore(relayUrl, code, redirected: false);

    private void JoinSessionCore(string relayUrl, string code, bool redirected)
    {
        var normalized = code.Trim().ToUpperInvariant();
        // Rejoining the session we are already on must not announce a leave first: that message
        // races our own Hello, and a host that reads it second drops us straight back out.
        var rejoining = SessionCode == normalized && RelayUrl != null
                        && Configuration.OriginOf(RelayUrl) == Configuration.OriginOf(relayUrl);
        LeaveSessionInternal(notifyOthers: !rejoining);
        joinRedirected = redirected;
        ConnectionError = null;
        peerSecret = RelayWire.RelayCredential(Plugin.Config.EnsurePeerSecret(), relayUrl);
        MyPeerId = RelayWire.PeerId(peerSecret);
        IsHost = false;
        RelayUrl = relayUrl;
        relayAccessToken = Plugin.Config.TokenForRelay(relayUrl);
        SessionCode = normalized;
        Session = new MultiplayerSession();
        OpenStream(host: false);
        // Seeded to now, or the host reads as silent since 1970 until its first broadcast.
        lastHostMessageMs = NowMs;
        everHeardFromHost = false;
        Interlocked.Exchange(ref helloSentMs, 0);
        joinStartedMs = NowMs;
        joinReachedRelay = false;

        DiagnosticLog.Info($"[Multiplayer] Joining session {SessionCode} at {relayUrl} as {MyPeerId} ({DisplayName}), build {PluginBuildInfo.ShortChecksum}.");
        relay = RelayFactory(peerSecret);
        WireRelay(relay);
        helloAcknowledged = false;
        helloRetryTimer = 0f;
        _ = ConnectAndHelloAsync(relayUrl, SessionCode);
    }

    private async Task ConnectAndHelloAsync(string relayUrl, string code)
    {
        var client = relay!;
        await client.ConnectAsync(relayUrl, code, relayAccessToken);
        // A failed connect is reported through Disconnected, and the reconnect loop takes over.
        if (!client.IsConnected) return;
        DiagnosticLog.Info($"[Multiplayer] Connected to relay, socket ready -- sending Hello.");
        await SendHelloAsync(client);
    }

    // Starts the no-host clock only once the Hello has left on the live connection: a superseded
    // or dropped one mustn't start it.
    private async Task SendHelloAsync(IRelayConnection client)
    {
        await client.SendAsync(NewHello(LocalClassJob));
        if (client != relay || !client.IsConnected) return;
        Interlocked.Exchange(ref helloSentMs, NowMs);
        joinReachedRelay = true;
    }

    private string JoinTimedOutReason()
    {
        var seconds = JoinDeadlineMs / 1000;
        if (joinReachedRelay)
            return $"Reached the server, but the host of session {SessionCode} didn't answer within {seconds} seconds. "
                   + "Check the code, or ask the host whether their session is still up.";
        var detail = ConnectionError is { } error ? $" ({error})" : "";
        return $"Couldn't reach the server at {RelayUrl} within {seconds} seconds{detail}. "
               + "Check the address, and that the server is running and reachable.";
    }

    // Disconnected captures the instance so an event from a replaced client can be told apart.
    private void WireRelay(IRelayConnection client)
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
        reconnectAttemptInFlight = false;
        ReconnectAttempt = 0;
        disconnectedSinceMs = null;
        hostFallbacks.Clear();
        joinRedirected = false;
        if (IsHost) Plugin.Game.PartyMemberKilled -= OnPartyMemberKilledHost;

        // Dispose only after the SessionEnded send completes, or it usually aborts before
        // reaching the wire. notifyOthers is false when reacting to someone else's
        // SessionEnded, to avoid a cascade.
        if (notifyOthers && relay is { IsConnected: true } activeRelay)
        {
            MpMessage ended = new SessionEndedMessage(MyPeerId);
            if (stream != null) ended = stream.Number(ended);
            _ = activeRelay.SendAsync(ended).ContinueWith(_ => activeRelay.Dispose());
        }
        else
            relay?.Dispose();
        relay = null;
        stream = null;
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
        replay?.Clear();
        replay = null;
        peerNatives?.Uninstall();
        peerNatives = null;
        recorder?.Uninstall();
        recorder = null;
        hostRunParams = null;
        pendingEntered = null;
        hostStageSetFor = null;
        peerRunParams = null;
        peerEnteredInstance = false;
        peerRunStarted = false;
        peerRunStartPending = false;
        earlyPackets.Clear();
        peerLastSeenMs.Clear();
        peerLastHelloMs.Clear();
        deferredLeaveMs.Clear();
        peerLatencyMs.Clear();
        pingsSentMs.Clear();
        answeredPingMs.Clear();
        peerStatuses.Clear();
        warnedStalePeers.Clear();
        pingTimer = 0f;
        pendingStartResponses = null;
        startCheckFailures.Clear();
        StartCheckFailureReason = null;
        startWait.Cancel();
        startCheckReplyWaited = null;
        startCheckTimer = 0f;
        debugBotControlled = false;
        aiReplayStateSent = false;
        endSentReturnedToInn = false;
        RestoreOwnScenarioSettings();
        StopDebugBotReplay();
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
        reconnectDueMs = NowMs + (long)ReconnectBackoff[0].TotalMilliseconds;
    }

    // On Time rather than a timer task, so a test's virtual clock paces it too.
    private void TickReconnect()
    {
        if (!reconnecting || reconnectAttemptInFlight || NowMs < reconnectDueMs) return;
        if (reconnectCts is not { } cts || SessionCode is not { } code || RelayUrl is not { } url) return;
        reconnectAttemptInFlight = true;
        DiagnosticLog.Info($"[Multiplayer] Reconnect attempt {ReconnectAttempt + 1}.");
        _ = AttemptReconnectAsync(url, code, cts.Token);
    }

    private async Task AttemptReconnectAsync(string relayUrl, string sessionCode, CancellationToken token)
    {
        var client = RelayFactory(peerSecret);
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
            var wasJoining = !IsHost && !everHeardFromHost;
            _ = Plugin.Framework.Run(() =>
            {
                if (token.IsCancellationRequested) return;
                // Refused mid-run (a ban, a lost room), this client would otherwise stay in the
                // fake instance; every other way a session ends leaves it too.
                if (Plugin.Game.World.Map.IsInInstance) Plugin.Game.Leave();
                LeaveSessionInternal(notifyOthers: false);
                SessionEndReason = rejected.Message == "banned from room"
                    ? "You were banned from the session by the host."
                    : wasJoining
                        ? rejected.Message switch
                        {
                            "session not found" => $"There's no session {sessionCode} on this server. Check the code, or ask the host for a new one.",
                            "session full" => $"Session {sessionCode} is full.",
                            _ => $"The server refused to join session {sessionCode} ({rejected.Message}).",
                        }
                        : wasHost
                            ? $"The relay lost this session ({rejected.Message}) -- start a new one."
                            : $"The relay lost this session ({rejected.Message}) -- ask the host to start a new one.";
                LobbyChanged?.Invoke();
            });
            return;
        }

        _ = Plugin.Framework.Run(() => FinishReconnectAttempt(client, connected, token));
    }

    private void FinishReconnectAttempt(IRelayConnection client, bool connected, CancellationToken token)
    {
        reconnectAttemptInFlight = false;
        if (token.IsCancellationRequested || !connected)
        {
            client.Dispose();
            if (token.IsCancellationRequested) return;
            ReconnectAttempt++;
            reconnectDueMs = NowMs + (long)ReconnectBackoff[Math.Min(ReconnectAttempt, ReconnectBackoff.Length - 1)].TotalMilliseconds;
            return;
        }
        relay = client;
        reconnecting = false;
        ReconnectAttempt = 0;
        disconnectedSinceMs = null;
        DiagnosticLog.Info($"[Multiplayer] Reconnected to session {SessionCode}.");
        lastHostMessageMs = NowMs;
        ConnectionError = null;
        // The poses sent meanwhile went with the link.
        lastSelfPose = null;
        lastRoles = null;
        // Re-registering with the host resumes a running scenario the same way a late join does.
        if (!IsHost) _ = SendHelloAsync(relay);
        else
        {
            foreach (var peerId in pendingRelayUnbans) _ = relay.ModerateAsync("unban", peerId);
            // The drop lost the pings in flight; nobody fell behind on those.
            foreach (var peerId in answeredPingMs.Keys.ToList()) answeredPingMs[peerId] = PingClockMs();
            // Ping at once: a peer that joined while we were away has heard nothing from us and soon
            // takes the session for gone (IsSessionNotFound), and the stream picks up from the ping.
            pingTimer = PingIntervalSeconds;
        }
        pendingRelayUnbans.Clear();
        LobbyChanged?.Invoke();
    }

    // ---- Role claiming ----------------------------------------------------

    public void ClaimRole(PartyRole role)
    {
        if (relay == null) return;
        if (IsHost) ApplyClaim(MyPeerId, role);
        else _ = Send(new ClaimRoleMessage(MyPeerId, role));
    }

    public void ReleaseRole()
    {
        if (relay == null) return;
        if (IsHost) ApplyRelease(MyPeerId);
        else _ = Send(new ReleaseRoleMessage(MyPeerId));
    }

    // False unless a connected peer, whose own Game.Reset() would only clear their local view.
    public bool RequestReset()
    {
        if (IsHost || relay is not { IsConnected: true }) return false;
        _ = Send(new ResetRequestMessage(MyPeerId));
        return true;
    }

    // False unless a connected peer, whose own Game.Leave() would leave the host simulating for a
    // torn-down world: the host ends the run for the whole group (LeaveRequestMessage);
    // LeaveSession disconnects just this client.
    public bool RequestLeaveInstance()
    {
        if (IsHost || relay is not { IsConnected: true }) return false;
        _ = Send(new LeaveRequestMessage(MyPeerId));
        return true;
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
        _ = Send(new KickMessage(peerId, ban));
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
        var index = Plugin.Game.Catalog.Scenarios.ToList().IndexOf(scenario);
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
            Session.ClaimedBy.Remove(r);
        Session.Names.Remove(peerId);
        stream?.Forget(peerId);
        Session.Builds.Remove(peerId);
        Session.DebugBots.Remove(peerId);
        peerConnectionIds.Remove(peerId);
        peerLastSeenMs.Remove(peerId);
        peerLastHelloMs.Remove(peerId);
        deferredLeaveMs.Remove(peerId);
        peerLatencyMs.Remove(peerId);
        answeredPingMs.Remove(peerId);
        peerStatuses.Remove(peerId);
        warnedStalePeers.Remove(peerId);
        startCheckFailures.Remove(peerId);
        if (pendingStartResponses?.Remove(peerId) == true && pendingStartResponses.Count == 0)
            FinishStartCheck();
        // A missing party member usually dooms the mechanic; Tick() broadcasts the end once
        // ActiveScenario clears.
        if (running && Plugin.Game.World.Map.IsInInstance)
        {
            DiagnosticLog.Info($"[Multiplayer] Ending the run because {who} left mid-fight.");
            Plugin.Game.Leave();
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
        Transmit(NewHello(job));
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
        DiagnosticLog.Info($"[Multiplayer] No lobby state with our Hello yet -- re-sending Hello to session {SessionCode}.");
        Transmit(NewHello(LocalClassJob));
    }

    // The host only learns our name and debug-bot mode from a Hello, so a roster with both is the
    // acknowledgement.
    private void NoteHelloAcknowledged()
    {
        if (helloAcknowledged || !Session.Names.ContainsKey(MyPeerId) || Session.DebugBots.Contains(MyPeerId) != debugBotControlled) return;
        helloAcknowledged = true;
        DiagnosticLog.Info("[Multiplayer] Host's lobby state now names us -- Hello acknowledged.");
    }

    private void BroadcastLobbyState()
    {
        TagBotSeats();
        LobbyChanged?.Invoke();
        _ = Send(LobbyMessage());
    }

    private LobbyStateMessage LobbyMessage() => Session.ToMessage() with { Clock = HostRunClock() };

    // Null until the host's own run has had its first tick, so a broadcast while the start is
    // queued can't hand out the idle clock, nor the zero it reads before that tick.
    private RunClockState? HostRunClock()
    {
        if (!IsHost || !running || !hostScenarioStarted || Plugin.Game.ActiveScenario is null) return null;
        return new RunClockState(EventClockNow(), averageFrameSeconds);
    }

    private float EventClockNow()
    {
        var game = Plugin.Game;
        if (game.Paused || eventClockStampedAt == 0 || game.Events.Elapsed != eventClockAtStamp)
            return game.Events.Elapsed;
        return eventClockAtStamp + (float)Time.GetElapsedTime(eventClockStampedAt).TotalSeconds * game.EventTimeScale;
    }

    private void StampEventClock()
    {
        var game = Plugin.Game;
        if (game.Paused) return;
        eventClockStampedAt = Time.GetTimestamp();
        eventClockAtStamp = game.Events.Elapsed;
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
        _ = Send(new StartAbortMessage(MyPeerId, reason));
        AnnounceRunEnded($"you couldn't start: {reason}");
    }

    // The preconditions Game.ZoneIn/StartScenario enforce, checked up front so a failure is reported
    // instead of a silent no-op. A claimed tank role must be on a tank job: bot mitigation picks
    // ability ids off the seat's job.
    private string? CheckOwnStartReadiness() => CheckOwnStartReadiness(out _);

    private string? CheckOwnStartReadiness(out string? settling)
    {
        if (Natives.Zone.StartBlockedReason(out settling) is { } blocked) return blocked;
        if (MyClaimedRole is { } role && role.IsTank())
        {
            var jobId = Natives.BattleCharas.LocalPlayer.ClassJob;
            if (!PartyPresets.SkipRoleForJob(jobId).IsTank())
                return "queued as a tank role but not on a tank job";
        }
        return null;
    }

    // Doesn't start immediately: every claimed peer first confirms readiness
    // (StartCheckMessage), so a peer who isn't in an inn fails loudly here rather than
    // silently seconds later.
    public void StartScenario(ScenarioSelection? selection)
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
        if (selection is not { Scenario: { SupportsMultiplayer: true } selectedScenario })
        {
            DiagnosticLog.Warn("[Multiplayer] Cannot start: no multiplayer-supported scenario is selected in the main window.");
            return;
        }
        // A region with no strats leaves SelectedStrat at -1, which a debug-bot peer would index with.
        if (selection.Strat is not { } strat)
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
        var scenarioIndex = Plugin.Game.Catalog.Scenarios.ToList().IndexOf(selectedScenario);
        Session.ScenarioIndex = scenarioIndex;
        Session.SelectedAi = strat;
        Session.SelectedWaymark = selection.Waymark;
        hostSeed = selection.Seed;
        // ApplyClaim already rejects these; this closes the race.
        if (Session.ClaimedBy.Values.Any(IsVersionMismatched))
        {
            DiagnosticLog.Warn("[Multiplayer] Cannot start: one or more claimed players are on a different plugin build.");
            return;
        }
        if (IsStartCheckPending) return;

        Natives.Zone.EndSpeedBuffs();
        if (CheckOwnStartReadiness(out var settling) is { } ownReason)
        {
            if (settling != null)
            {
                if (StartWaitingOn == null) DiagnosticLog.Info($"[Multiplayer] Start waiting for {settling} to settle.");
                startWait.Hold(settling, () => StartScenario(selection));
                StartCheckFailureReason = null;
                LobbyChanged?.Invoke();
                return;
            }
            startWait.Cancel();
            StartCheckFailureReason = $"You cannot start: {ownReason}.";
            DiagnosticLog.Info($"[Multiplayer] Cannot start: {ownReason}.");
            LobbyChanged?.Invoke();
            return;
        }

        startWait.Cancel();
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
        _ = Send(new StartCheckMessage());
        LobbyChanged?.Invoke();

        if (pendingStartResponses.Count == 0) FinishStartCheck();
    }

    private void AnswerStartCheck(float deltaSeconds)
    {
        if (startCheckReplyWaited is not { } waited) return;
        waited += deltaSeconds;
        Natives.Zone.EndSpeedBuffs();
        var reason = CheckOwnStartReadiness(out var settling);
        if (settling != null && waited < StartCheckReplyMaxWaitSeconds)
        {
            if (startCheckReplyWaited == 0f) DiagnosticLog.Info($"[Multiplayer] Start check: waiting for {settling} to settle before answering.");
            startCheckReplyWaited = waited;
            return;
        }
        startCheckReplyWaited = null;
        _ = Send(new StartCheckResponseMessage(MyPeerId, reason == null, reason));
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

        // Locks the lobby, and gives the peers the run's scenario, strat and waymark before they enter.
        Session.Started = true;
        _ = Send(LobbyMessage());

        warnedStalePeers.Clear();
        aiReplayStateSent = false;
        endSentReturnedToInn = false;
        hostScenarioStarted = false;
        var nowMs = NowMs;
        foreach (var peerId in Session.ClaimedBy.Values)
            if (peerId != MyPeerId)
                peerLastSeenMs[peerId] = nowMs;
        Plugin.Game.PartyMemberKilled += OnPartyMemberKilledHost;
        // scenario.Run schedules the Ai against every role; this lets it move the host's own
        // character.
        if (debugBotControlled)
        {
            DiagnosticLog.Info("[Multiplayer] Host: debug-bot mode active for own character this run.");
            DebugBotControl.Enabled = true;
        }
        running = true;
        var p = new RunScenarioParams(scenario, new LobbyPartyProvider(this, isPeer: false), Session.SelectedAi, Session.SelectedWaymark, hostSeed ?? NextSeed());
        hostRunParams = p;
        pendingEntered = networkRoles.Select(r => Session.ClaimedBy[r]).ToHashSet();
        enterTimer = 0f;
        hostStageSetFor = null;
        recorder ??= PacketRecorder.Install();
        _ = Send(new EnterMessage());
        var zone = scenario.Phase.Zone;
        if (Plugin.Game.Zone == zone && Plugin.Game.Mode == SessionMode.Host)
            Plugin.Game.PrepareRun(p, OnHostStageSet);
        else
            Plugin.Game.ZoneIn(zone, SessionMode.Host, refusal =>
            {
                if (refusal != null) OnHostStartResolved(refusal);
                else Plugin.Game.PrepareRun(p, OnHostStageSet);
            });
        LobbyChanged?.Invoke();
    }

    // The first stage of a stay is each client's own; the run, and every later stage, is recorded.
    private void OnHostStageSet(string? refusal)
    {
        if (!running) return;
        if (refusal != null)
        {
            OnHostStartResolved(refusal);
            return;
        }
        recorder?.TagSeats(Plugin.Game.World.Party);
        TagBotSeats();
        hostStageSetFor = 0f;
    }

    // Peers only hear Start once the host's own run exists.
    private void OnHostStartResolved(string? refusal)
    {
        // Ended while the start was queued.
        if (!running) return;
        if (refusal != null)
        {
            DiagnosticLog.Warn($"[Multiplayer] Host's own start was refused: {refusal}.");
            StartCheckFailureReason = $"You couldn't start: {refusal}.";
            if (Plugin.Game.World.Map.IsInInstance) Plugin.Game.Leave();
            // Any lobby broadcast made while the start was queued carried Started.
            BroadcastRunEnded(returnedToInn: true);
            return;
        }
        recorder?.TagSeats(Plugin.Game.World.Party);
        TagBotSeats();
    }

    // On the run's first tick, when its clock reads what a peer syncs to. Ahead of the run's
    // packets: a peer sets its own run up first, as this host did before sending them.
    private void AnnounceStart()
    {
        lastRoles = null;
        _ = Send(LobbyMessage());
        _ = Send(new StartMessage(HostRunClock()));
        FlushPackets(force: true);
    }

    // Starting from zero would leave this run behind the host's by the host's load time plus the
    // travel time. It also runs ahead of the host by our poses' own delay (the trip back, plus
    // about a host frame since the host applies poses after that frame's hit checks), so the host
    // judges our character in step with its own bots. Compared at this frame's clock stamp,
    // taken just after the Game.Tick that Events.Elapsed describes.
    private void SyncClockToHost()
    {
        if (hostClockAtStart is not { } clock || eventClockSynced) return;
        eventClockSynced = true;
        var game = Plugin.Game;
        var (target, oneWay, poseLead) = HostClockTarget(clock, hostClockReceivedAt);
        var advance = target - game.Events.Elapsed;
        game.Events.Advance(advance);
        DiagnosticLog.Info($"[Multiplayer] Peer: run clock {(advance > 0f ? $"moved up {advance * 1000f:F0} ms" : "left as is")}: the host's time plus a {poseLead * 1000f:F0} ms lead (Start sent {clock.EventClock:F3}s into the host's run, {oneWay * 1000f:F0} ms one-way, {clock.FrameSeconds * 1000f:F1} ms host frame).");
    }

    // A Start that came late, sent again after a link drop, left this run behind the host's;
    // each ping's clock catches it up. Never back, and never for less than jitter, which would
    // only creep it forward.
    private const float ClockCatchUpSeconds = 0.1f;
    private (RunClockState Clock, long ReceivedAt)? pingClock;

    private void CatchUpClockToHost()
    {
        if (pingClock is not { } ping || !eventClockSynced) return;
        pingClock = null;
        var game = Plugin.Game;
        var advance = HostClockTarget(ping.Clock, ping.ReceivedAt).Target - game.Events.Elapsed;
        if (advance < ClockCatchUpSeconds) return;
        game.Events.Advance(advance);
        DiagnosticLog.Info($"[Multiplayer] Peer: run clock caught up {advance * 1000f:F0} ms to the host's.");
    }

    // The host's run time as this frame's stamp sees it, plus our lead (see SyncClockToHost).
    private (float Target, float OneWay, float PoseLead) HostClockTarget(RunClockState clock, long receivedAt)
    {
        var hostClock = NetGuard.Clamp(clock.EventClock, 0f, 3600f);
        var oneWay = NetGuard.Clamp(peerStatuses.GetValueOrDefault(MyPeerId)?.LatencyMs ?? 0f, 0f, 4000f) / 2000f;
        var hostFrame = NetGuard.Clamp(clock.FrameSeconds, 0f, MaxFrameSampleSeconds);
        var poseLead = oneWay + (hostFrame > 0f ? hostFrame : 1f / 60f);
        var target = hostClock + oneWay + poseLead + (float)Time.GetElapsedTime(receivedAt, eventClockStampedAt).TotalSeconds;
        return (target, oneWay, poseLead);
    }

    // A peer's copy of a host actor as it spawns: its entity id here, and the host's.
    internal event Action<uint, uint>? ActorReplayed;

    // Names for the puppets: every role claimed by someone else, the host's included from a
    // peer's side.
    internal Dictionary<PartyRole, NetworkSeat> ClaimedRoleSeats() =>
        Session.ClaimedBy.Where(kv => kv.Value != MyPeerId)
            .ToDictionary(kv => kv.Key, kv => new NetworkSeat(Session.NameOf(kv.Value), Session.JobOf(kv.Value)));

    // After every seating and roster change. A seat nobody claimed is the host's bot.
    private void TagBotSeats()
    {
        var party = Plugin.Game.World.Party;
        foreach (var role in Enum.GetValues<PartyRole>())
            if (party.Get(role) is SimNetworkPuppet puppet)
                puppet.BotDriven = Session.ClaimedBy.TryGetValue(role, out var player) ? Session.DebugBots.Contains(player) : !IsHost;
    }

    private static byte LocalClassJob => Natives.BattleCharas.LocalPlayer.ClassJob;

}
