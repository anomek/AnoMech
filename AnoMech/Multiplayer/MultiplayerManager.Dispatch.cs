using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

public sealed partial class MultiplayerManager
{
    // ---- Message pump -------------------------------------------------------

    // Queued and drained in Tick rather than one Framework.Run per message: Dalamud's task
    // scheduler doesn't run pending tasks in insertion order, and wire order matters.
    private readonly ConcurrentQueue<(MpMessage Message, bool IsFromHost, uint SenderId, Guid SenderPeerId)> pendingMessages = new();
    private int pendingMessageCount;
    private long droppedQueuedMessages;

    private void OnMessageReceivedOffThread(MpMessage message, bool isFromHost, uint senderId, Guid senderPeerId)
    {
        if (Interlocked.Increment(ref pendingMessageCount) > NetGuard.MaxQueuedMessages)
        {
            Interlocked.Decrement(ref pendingMessageCount);
            if (Interlocked.Increment(ref droppedQueuedMessages) % 1000 == 1)
                DiagnosticLog.Warn($"[Multiplayer] Inbound queue is over {NetGuard.MaxQueuedMessages} deep -- dropping messages (total {Interlocked.Read(ref droppedQueuedMessages)}).");
            return;
        }
        pendingMessages.Enqueue((message, isFromHost, senderId, senderPeerId));
    }

    // A snapshot directly followed by another of the same type is skipped: under a bad
    // connection they back up and would replay as a burst.
    private void DrainPendingMessages()
    {
        var budget = NetGuard.MaxMessagesPerDrain;
        while (budget-- > 0 && pendingMessages.TryDequeue(out var entry))
        {
            Interlocked.Decrement(ref pendingMessageCount);
            if ((entry.Message is WorldSnapshotMessage && pendingMessages.TryPeek(out var next) && next.Message is WorldSnapshotMessage)
                || (entry.Message is RolesSnapshotMessage && pendingMessages.TryPeek(out var next2) && next2.Message is RolesSnapshotMessage))
                continue;
            Dispatch(entry.Message, entry.IsFromHost, entry.SenderId, entry.SenderPeerId);
        }
    }

    // The ReferenceEquals guard drops events from superseded clients, and a manual Leave
    // (which nulls `relay` first) never gets past it.
    private void OnDisconnectedOffThread(RelayClient source, Exception? failure)
        => Plugin.Framework.Run(() =>
        {
            if (!ReferenceEquals(relay, source)) return;
            source.Dispose();
            relay = null;
            disconnectedSinceMs ??= Environment.TickCount64;
            if (failure != null)
            {
                DiagnosticLog.Warn($"[Multiplayer] Disconnected: {failure.Message}");
                ConnectionError = failure.Message;
            }
            else
            {
                DiagnosticLog.Info("[Multiplayer] Disconnected (no failure reported -- socket just closed).");
            }
            LobbyChanged?.Invoke();
            BeginReconnect();
        });

    // Others the relay removed alongside a ban (same address). They leave without a
    // SessionEnded, so without this the host would keep them seated. Queued with the messages,
    // so it lands after anything they sent before the ban and a late Hello can't re-seat them.
    private sealed record RelayRemovedPeers(IReadOnlyList<Guid> Removed) : MpMessage;

    private void OnPeersRemovedOffThread(RelayClient source, IReadOnlyList<Guid> removed)
        => OnMessageReceivedOffThread(new RelayRemovedPeers(removed), false, 0, Guid.Empty);

    private void Dispatch(MpMessage message, bool isFromHost, uint senderId, Guid senderPeerId)
    {
        // One bad message must not take down the tick.
        try
        {
            DispatchCore(message, isFromHost, senderId, senderPeerId);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Multiplayer] Error handling {message.GetType().Name}: {e}");
        }
    }

    // Host-only: the relay connection each peer was last heard on.
    private readonly Dictionary<Guid, uint> peerConnectionIds = new();

    // How long after a peer registers a leave is treated as having crossed that rejoin. Sized
    // for a network race, not for a player who joins and immediately leaves again.
    private const long RejoinGraceMs = 2000;

    private static readonly Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Weather> WeatherSheet =
        Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Weather>();
    private static readonly Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.BNpcName> BNpcNameSheet =
        Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcName>();

    // Host-driven world state is applied only inside a live run; outside one it would act on
    // the real inn.
    private bool PeerInRun => !IsHost && running && peerEnteredInstance && Plugin.GameInstance.World.Map.IsInInstance;

    private bool IsOwnLimitBreak(Guid peerId, uint actionId)
        => Session.Jobs.TryGetValue(peerId, out var job)
           && Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>().TryGetRow(job, out var row)
           && (row.LimitBreak1.RowId == actionId || row.LimitBreak2.RowId == actionId || row.LimitBreak3.RowId == actionId);

    private static Guid? ClaimedPeerId(MpMessage message) => message switch
    {
        HelloMessage m => m.PeerId,
        ClaimRoleMessage m => m.PeerId,
        ReleaseRoleMessage m => m.PeerId,
        SelfPoseMessage m => m.PeerId,
        PongMessage m => m.PeerId,
        StartCheckResponseMessage m => m.PeerId,
        StartAbortMessage m => m.PeerId,
        SessionEndedMessage m => m.PeerId,
        ResetRequestMessage m => m.PeerId,
        LeaveRequestMessage m => m.PeerId,
        PeerAppliedEnemyStatusMessage m => m.PeerId,
        PeerAppliedRoleStatusMessage m => m.PeerId,
        PeerLimitBreakMessage m => m.PeerId,
        PeerClearedStatusMessage m => m.PeerId,
        _ => null,
    };

    private void DispatchCore(MpMessage message, bool isFromHost, uint senderId, Guid senderPeerId)
    {
        if (message is RelayRemovedPeers notice)
        {
            if (!IsHost) return;
            foreach (var peerId in notice.Removed)
            {
                if (!Session.Names.ContainsKey(peerId)) continue;
                DiagnosticLog.Info($"[Multiplayer] Relay removed {Session.NameOf(peerId)} ({peerId}) along with a banned player on the same network.");
                RemovePeer(peerId);
            }
            return;
        }
        if (message is IHostOnlyMessage && !isFromHost)
        {
            DiagnosticLog.Warn($"[Multiplayer] Dropped {message.GetType().Name} -- relay says it wasn't from the host.");
            return;
        }
        if (IsHost && ClaimedPeerId(message) is { } claimedId)
        {
            // The relay stamps each message with the identity it derived from the sender's
            // secret, and RelayWire.Validate already refused a mismatch; checked again here.
            if (claimedId != senderPeerId)
            {
                DiagnosticLog.Warn($"[Multiplayer] Dropped {message.GetType().Name} claiming to be {Session.NameOf(claimedId)} ({claimedId}) from {senderPeerId}.");
                return;
            }
            if (peerConnectionIds.TryGetValue(claimedId, out var bound) && bound != senderId)
                DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(claimedId)} reconnected on connection #{senderId} (was #{bound}).");
            peerConnectionIds[claimedId] = senderId;
            if (bannedPeers.ContainsKey(claimedId))
            {
                // Repeat the kick and the relay ban for a banned client that rejoins or never got it.
                if (message is HelloMessage)
                {
                    _ = relay?.SendAsync(new KickMessage(claimedId, Banned: true));
                    _ = relay?.ModerateAsync("ban", claimedId);
                }
                return;
            }
        }
        // Host liveness: only types the host itself broadcasts (SessionEnded excluded, any peer
        // can send it). Keep in sync with the `when !IsHost` cases below.
        if (!IsHost && message is LobbyStateMessage or StartMessage or WorldSnapshotMessage or RolesSnapshotMessage
            or RoleKilledMessage or KnockbackMessage or TeleportMessage or PushMessage or FollowMessage
            or SpawnOmenMessage or EndMessage or PingMessage or PeerStatusMessage
            or SetFogHoldMessage or AnnouncementMessage
            or IScenarioReplayStateMessage or IScenarioMidRunUpdateMessage or KickMessage)
        {
            lastHostMessageMs = Environment.TickCount64;
            everHeardFromHost = true;
        }

        switch (message)
        {
            case HelloMessage hello when IsHost:
            {
                if (!Session.Names.ContainsKey(hello.PeerId) && Session.Names.Count >= NetGuard.MaxSessionPeers)
                {
                    DiagnosticLog.Warn($"[Multiplayer] Ignoring Hello from {hello.PeerId} -- roster already holds {NetGuard.MaxSessionPeers} peers.");
                    break;
                }
                peerLastSeenMs[hello.PeerId] = Environment.TickCount64;
                peerLastHelloMs[hello.PeerId] = Environment.TickCount64;
                var build = new PeerBuildInfo(NetGuard.Clean(hello.Version), NetGuard.Clean(hello.Checksum));
                Session.Names[hello.PeerId] = NetGuard.Clean(hello.DisplayName);
                Session.Builds[hello.PeerId] = build;
                Session.Jobs[hello.PeerId] = NetGuard.ClassJob(hello.ClassJob);
                DiagnosticLog.Info($"[Multiplayer] Hello from {hello.PeerId} ({Session.NameOf(hello.PeerId)}), build {build.Version} ({build.ShortChecksum}), mismatch={IsVersionMismatched(hello.PeerId)}.");
                BroadcastLobbyState();
                break;
            }
            case ClaimRoleMessage claim when IsHost:
                peerLastSeenMs[claim.PeerId] = Environment.TickCount64;
                if (!Enum.IsDefined(claim.Role))
                {
                    DiagnosticLog.Warn($"[Multiplayer] Dropped a claim for role {(int)claim.Role} from {Session.NameOf(claim.PeerId)} -- no such role.");
                    break;
                }
                ApplyClaim(claim.PeerId, claim.Role);
                break;
            case ReleaseRoleMessage release when IsHost:
                peerLastSeenMs[release.PeerId] = Environment.TickCount64;
                ApplyRelease(release.PeerId);
                break;
            case SelfPoseMessage pose when IsHost:
                peerLastSeenMs[pose.PeerId] = Environment.TickCount64;
                OnSelfPoseReceived(pose);
                break;
            case PongMessage pong when IsHost:
                peerLastSeenMs[pong.PeerId] = Environment.TickCount64;
                peerLatencyMs[pong.PeerId] = PingClockMs() - pong.SentAtMs;
                break;
            // Mitigation reports put statuses on the host's own characters: seated peers only,
            // known ids only, durations clamped.
            case PeerAppliedEnemyStatusMessage applied when IsHost:
            {
                var who = Session.NameOf(applied.PeerId);
                if (Session.RoleOf(applied.PeerId) is null) break;
                if (!AnoMech.Core.UserActions.Mitigation.ByStatusId.ContainsKey(applied.StatusId))
                {
                    DiagnosticLog.Warn($"[Multiplayer] Host: {who} reported enemy status {applied.StatusId}, which is no known mitigation -- dropping.");
                    break;
                }
                var duration = NetGuard.Clamp(applied.Duration, 0f, NetGuard.MaxMitigationSeconds);
                foreach (var netId in NetGuard.Cap(applied.EnemyNetIds, NetGuard.MaxEnemiesPerSnapshot))
                {
                    var enemy = hostEnemyNetIds.FirstOrDefault(kv => kv.Value == netId).Key;
                    if (enemy == null)
                    {
                        DiagnosticLog.Warn($"[Multiplayer] Host: {who} reported status {applied.StatusId} on unknown enemy NetId {netId} -- dropping.");
                        continue;
                    }
                    enemy.RemoveStatus(applied.StatusId);
                    enemy.AddStatus(applied.StatusId, duration);
                    DiagnosticLog.Info($"[Multiplayer] Host: applied {who}'s reported status {applied.StatusId} (duration={duration:F1}) to enemy NetId {netId}.");
                }
                break;
            }
            case PeerAppliedRoleStatusMessage applied when IsHost:
            {
                var who = Session.NameOf(applied.PeerId);
                if (Session.RoleOf(applied.PeerId) is null) break;
                if (!AnoMech.Core.UserActions.Mitigation.ByStatusId.ContainsKey(applied.StatusId))
                {
                    DiagnosticLog.Warn($"[Multiplayer] Host: {who} reported role status {applied.StatusId}, which is no known mitigation -- dropping.");
                    break;
                }
                var duration = NetGuard.Clamp(applied.Duration, 0f, NetGuard.MaxMitigationSeconds);
                foreach (var role in NetGuard.Cap(applied.Roles, 8).Where(Enum.IsDefined))
                {
                    if (Plugin.GameInstance.World.Party.Get(role) is not { } member)
                    {
                        DiagnosticLog.Warn($"[Multiplayer] Host: {who} reported status {applied.StatusId} on role {role}, but that slot is empty -- dropping.");
                        continue;
                    }
                    // A re-press replaces the status, as it does on the peer; AddStatus alone would stack it.
                    member.RemoveStatus(applied.StatusId);
                    member.AddStatus(applied.StatusId, duration);
                    DiagnosticLog.Info($"[Multiplayer] Host: applied {who}'s reported status {applied.StatusId} (duration={duration:F1}) to role {role}.");
                }
                break;
            }

            case PeerClearedStatusMessage cleared when IsHost:
            {
                var who = Session.NameOf(cleared.PeerId);
                if (Session.RoleOf(cleared.PeerId) is not { } clearedRole || Plugin.GameInstance.World.Party.Get(clearedRole) is not { } clearedMember) break;
                foreach (var statusId in NetGuard.Cap(cleared.StatusIds, NetGuard.MaxStatusesPerEntity))
                {
                    if (!AnoMech.Core.UserActions.Mitigation.ByStatusId.ContainsKey(statusId)) continue;
                    clearedMember.RemoveStatus(statusId);
                    DiagnosticLog.Info($"[Multiplayer] Host: {who}'s own press cleared status {statusId} off role {clearedRole}.");
                }
                break;
            }

            case PeerLimitBreakMessage limitBreak when IsHost:
            {
                var who = Session.NameOf(limitBreak.PeerId);
                if (!running || Session.RoleOf(limitBreak.PeerId) is not { } limitBreakRole) break;
                if (!IsOwnLimitBreak(limitBreak.PeerId, limitBreak.ActionId))
                {
                    DiagnosticLog.Warn($"[Multiplayer] Host: {who} reported action {limitBreak.ActionId} as a limit break, which isn't one of its job's -- dropping.");
                    break;
                }
                // An aim that fails the checks keeps the host's own view of the peer instead.
                var puppet = Plugin.GameInstance.World.Party.Get(limitBreakRole);
                var origin = NetGuard.TryPosition(limitBreak.X, limitBreak.Y, limitBreak.Z, out var sent) ? sent : puppet?.Position ?? Vector3.Zero;
                var aim = new LimitBreakAim(origin, NetGuard.Rotation(limitBreak.Heading),
                    NetGuard.TryPosition(limitBreak.LocationX, limitBreak.LocationY, limitBreak.LocationZ));
                DiagnosticLog.Info($"[Multiplayer] Host: {who} ({limitBreakRole})'s {ActionLookup.Name(limitBreak.ActionId)} landed, aimed from "
                                    + $"({aim.Origin.X:F2},{aim.Origin.Z:F2}) heading {aim.Heading:F3}{(aim.Location is { } at ? $" at ({at.X:F2},{at.Z:F2})" : "")}.");
                (Plugin.GameInstance.ActiveScenario as IPartyLimitBreakScenario)?.OnPartyLimitBreak(limitBreakRole, limitBreak.ActionId, aim);
                break;
            }

            case LobbyStateMessage lobby when !IsHost:
                Session.ApplyLobbyState(lobby);
                NoteHelloAcknowledged();
                ApplyHostScenarioSettings();
                ApplyHostActionSettings();
                LobbyChanged?.Invoke();
                // A late join or mid-fight rejoin never gets a StartMessage; OnStartReceived is
                // idempotent, so this is safe on a fresh start too.
                if (lobby.Started && MyClaimedRole != null)
                    OnStartReceived(lobby.Clock);
                break;
            case StartMessage start when !IsHost:
                OnStartReceived(start.Clock);
                break;
            case StartCheckMessage when !IsHost:
                startCheckReplyWaited = 0f;
                AnswerStartCheck(0f);
                break;
            case StartCheckResponseMessage resp when IsHost:
                DiagnosticLog.Info($"[Multiplayer] StartCheck reply from {Session.NameOf(resp.PeerId)}: ready={resp.Ready}{(resp.Reason is { } r ? $" ({NetGuard.Clean(r)})" : "")}.");
                // A duplicate/stale reply, or the timeout already gave up on this peer.
                if (pendingStartResponses == null || !pendingStartResponses.Remove(resp.PeerId)) break;
                if (!resp.Ready) startCheckFailures[resp.PeerId] = NetGuard.Clean(resp.Reason) is { Length: > 0 } cleaned ? cleaned : "not ready";
                if (pendingStartResponses.Count == 0) FinishStartCheck();
                break;
            // Ending the run beats simulating around a player who never entered.
            case StartAbortMessage abort when IsHost:
            {
                var who = Session.NameOf(abort.PeerId);
                var reason = $"{who} couldn't start: {NetGuard.Clean(abort.Reason)}";
                DiagnosticLog.Warn($"[Multiplayer] {reason} -- ending the run for everyone.");
                if (Plugin.GameInstance.World.Map.IsInInstance) Plugin.GameInstance.Leave();
                BroadcastRunEnded(returnedToInn: true, reason);
                break;
            }
            case WorldSnapshotMessage snap when !IsHost:
                OnWorldSnapshotReceived(snap);
                break;
            case RolesSnapshotMessage rolesSnap when !IsHost:
                OnRolesSnapshotReceived(rolesSnap);
                break;
            case RoleKilledMessage killed when !IsHost:
                OnRoleKilledReceived(killed);
                break;
            case KnockbackMessage kb when !IsHost:
                OnKnockbackReceived(kb);
                break;
            case TeleportMessage teleport when !IsHost:
                OnTeleportReceived(teleport);
                break;
            case PushMessage push when !IsHost:
                OnPushReceived(push);
                break;
            case CarryMessage carry when !IsHost:
                OnCarryReceived(carry);
                break;
            case FollowMessage follow when !IsHost:
                OnFollowReceived(follow);
                break;
            case SpawnOmenMessage omen when !IsHost:
                OnSpawnOmenReceived(omen);
                break;
            case EndMessage end when !IsHost:
                OnEndReceived(end);
                break;
            case PingMessage ping when !IsHost:
                _ = relay?.SendAsync(new PongMessage(MyPeerId, ping.SentAtMs));
                break;
            case PeerStatusMessage status when !IsHost:
                peerStatuses.Clear();
                foreach (var (id, entry) in status.Statuses)
                    peerStatuses[id] = entry;
                break;
            // The host leaving ends the session for everyone; a departing peer only shrinks
            // the roster.
            case SessionEndedMessage ended when ended.PeerId == Session.HostId && isFromHost:
            {
                var who = Session.NameOf(ended.PeerId); // before LeaveSessionInternal wipes Session
                DiagnosticLog.Info($"[Multiplayer] Host {who} left -- session ending for the whole group.");
                // IsInInstance, not running: a Reset clears running while staying in-instance.
                if (Plugin.GameInstance.World.Map.IsInInstance) Plugin.GameInstance.Leave();
                LeaveSessionInternal(notifyOthers: false);
                SessionEndReason = $"{who} left -- session ended.";
                LobbyChanged?.Invoke();
                break;
            }
            case SessionEndedMessage ended when IsHost:
            {
                // A leave sent on the way out can still be in flight when the same peer comes
                // back. Right after a Hello it is held until the peer's silence confirms it.
                var sinceHello = Environment.TickCount64 - peerLastHelloMs.GetValueOrDefault(ended.PeerId, long.MinValue / 2);
                if (sinceHello < RejoinGraceMs)
                {
                    DiagnosticLog.Info($"[Multiplayer] Holding a leave from {Session.NameOf(ended.PeerId)} -- they registered {sinceHello}ms ago, so it may have crossed their rejoin.");
                    deferredLeaveMs[ended.PeerId] = Environment.TickCount64;
                    break;
                }
                RemovePeer(ended.PeerId);
                break;
            }
            // Everyone else learns of it from the lobby state broadcast alongside.
            case KickMessage kick when !IsHost && kick.PeerId == MyPeerId:
                DiagnosticLog.Info($"[Multiplayer] {(kick.Banned ? "Banned" : "Removed")} from the session by the host.");
                if (Plugin.GameInstance.World.Map.IsInInstance) Plugin.GameInstance.Leave();
                LeaveSessionInternal(notifyOthers: false);
                SessionEndReason = kick.Banned
                    ? "You were banned from the session by the host."
                    : "You were removed from the session by the host.";
                LobbyChanged?.Invoke();
                break;
            // Only a seated peer can end the run for everyone.
            case ResetRequestMessage req when IsHost && Session.RoleOf(req.PeerId) != null:
                DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(req.PeerId)} requested a reset.");
                Plugin.GameInstance.Reset();
                break;
            // IsInInstance, not running: Leave must still work after a Reset.
            case LeaveRequestMessage req when IsHost && Session.RoleOf(req.PeerId) != null:
                DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(req.PeerId)} requested to leave the instance.");
                if (Plugin.GameInstance.World.Map.IsInInstance)
                    Plugin.GameInstance.Leave();
                // Unconditional, or the requester's own Leave button waits forever.
                BroadcastRunEnded(returnedToInn: true);
                break;
            // Can't loop into a re-broadcast: the map-event handlers gate on IsHost.
            case MapEffectMessage effect when PeerInRun:
                DiagnosticLog.Info($"[Multiplayer] Peer: applying MapEffect packetFlags=0x{effect.PacketFlags:X8} index=0x{effect.Index:X}.");
                Plugin.GameInstance.World.Map.AddEffect(effect.PacketFlags, effect.Index);
                break;
            case MapDirectorUpdateMessage directorUpdate when PeerInRun:
                if (!MapController.IsReplayableDirectorCategory(directorUpdate.Category))
                {
                    DiagnosticLog.Warn($"[Multiplayer] Dropped MapDirectorUpdate category=0x{directorUpdate.Category:X8} -- no scenario uses it.");
                    break;
                }
                DiagnosticLog.Info($"[Multiplayer] Peer: applying MapDirectorUpdate category=0x{directorUpdate.Category:X8}.");
                Plugin.GameInstance.World.Map.DirectorUpdate(
                    directorUpdate.Category, directorUpdate.Arg1, directorUpdate.Arg2,
                    directorUpdate.Arg3, directorUpdate.Arg4, directorUpdate.Arg5, directorUpdate.Arg6);
                break;
            case MapBattleTalkMessage battleTalk when PeerInRun:
                if (!SimAssets.Allow(SimAssetKind.BattleTalk, battleTalk.TextId, "BattleTalk")
                    || !BNpcNameSheet.HasRow(battleTalk.SpeakerNameId))
                    break;
                DiagnosticLog.Info($"[Multiplayer] Peer: applying BattleTalk text={battleTalk.TextId}.");
                Plugin.GameInstance.World.Map.BattleTalk(
                    battleTalk.SpeakerNameId, battleTalk.TextId, (uint)NetGuard.Clamp(battleTalk.DurationMs, 0f, 30_000f));
                break;
            case SetWeatherMessage weather when PeerInRun:
                if (!WeatherSheet.HasRow(weather.WeatherId))
                {
                    DiagnosticLog.Warn($"[Multiplayer] Dropped SetWeather weatherId={weather.WeatherId} -- no such Weather row.");
                    break;
                }
                DiagnosticLog.Info($"[Multiplayer] Peer: applying SetWeather weatherId={weather.WeatherId} transition={weather.Transition}.");
                Plugin.GameInstance.World.Map.SetWeather(weather.WeatherId, NetGuard.Clamp(weather.Transition, 0f, 60f, 0.5f));
                break;
            case SetFogHoldMessage fog when PeerInRun:
                DiagnosticLog.Info($"[Multiplayer] Peer: applying SetFogHold {(fog.FogHold is { } value ? value.ToString("F0") : "off")}.");
                Plugin.GameInstance.World.Map.SetFogHold(fog.FogHold is { } hold ? NetGuard.Clamp(hold, 0f, 100_000f) : null);
                break;
            case AnnouncementMessage announcement when PeerInRun:
                Plugin.GameInstance.World.Announce(NetGuard.Clean(announcement.Text));
                break;
            // Every IMultiplayerReplayable scenario routes through these two cases.
            case MpMessage genericMsg when !IsHost && genericMsg is IScenarioReplayStateMessage:
                pendingGenericReplayState = genericMsg;
                TryStartDebugBotReplay();
                break;
            // Without a shadow state yet, nothing needs the update.
            case MpMessage midRunUpdate when !IsHost && midRunUpdate is IScenarioMidRunUpdateMessage:
                if (debugShadowStateGeneric != null && TryResolveScenario() is IMultiplayerReplayable replayable)
                    replayable.ApplyMidRunUpdate(debugShadowStateGeneric, midRunUpdate);
                break;
        }
    }
}
