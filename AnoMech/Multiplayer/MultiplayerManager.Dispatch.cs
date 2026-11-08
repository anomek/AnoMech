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
using AnoMech.Core.UserActions;
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

    // A poses snapshot directly followed by another is skipped: under a bad connection they back
    // up and would replay as a burst. The packets never are: each is an event.
    private void DrainPendingMessages()
    {
        var budget = NetGuard.MaxMessagesPerDrain;
        while (budget-- > 0 && !peerRunStartPending && pendingMessages.TryDequeue(out var entry))
        {
            Interlocked.Decrement(ref pendingMessageCount);
            if (entry.Message is RolesSnapshotMessage && pendingMessages.TryPeek(out var next) && next.Message is RolesSnapshotMessage)
                continue;
            if (!TakeFromStream(entry.Message, entry.SenderPeerId)) continue;
            Dispatch(entry.Message, entry.IsFromHost, entry.SenderId, entry.SenderPeerId);
        }
    }

    // The ReferenceEquals guard drops events from superseded clients, and a manual Leave
    // (which nulls `relay` first) never gets past it.
    private void OnDisconnectedOffThread(IRelayConnection source, Exception? failure)
        => Plugin.Framework.Run(() =>
        {
            if (!ReferenceEquals(relay, source)) return;
            source.Dispose();
            relay = null;
            Interlocked.Exchange(ref helloSentMs, 0);
            if (IsHost && SessionCode == null && hostFallbacks.TryDequeue(out var next))
            {
                DiagnosticLog.Warn($"[Multiplayer] Couldn't host at {RelayUrl} ({failure?.Message}) -- trying {next}.");
                HostSessionCore(next);
                return;
            }
            if (!IsHost && !everHeardFromHost && !joinRedirected && SessionCode != null
                && failure is RelaySessionRejectedException { MovedTo: { } movedTo } && RelayGroup.IsRelayAddress(movedTo))
            {
                DiagnosticLog.Info($"[Multiplayer] Session {SessionCode} is at {movedTo} -- joining there.");
                JoinSessionCore(movedTo, SessionCode, redirected: true);
                return;
            }
            disconnectedSinceMs ??= NowMs;
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

    private void OnPeersRemovedOffThread(IRelayConnection source, IReadOnlyList<Guid> removed)
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

    // Host-driven state is applied only once this run's zone entry is done; outside one it would
    // act on the real inn.
    private bool PeerInRun => !IsHost && running && peerEnteredInstance && Plugin.Game.World.Map.IsInInstance;

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
        if (IsHost && message is IPeerMessage { PeerId: var claimedId })
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
                    _ = Send(new KickMessage(claimedId, Banned: true));
                    _ = relay?.ModerateAsync("ban", claimedId);
                }
                return;
            }
        }
        if (!IsHost && isFromHost)
        {
            lastHostMessageMs = NowMs;
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
                peerLastSeenMs[hello.PeerId] = NowMs;
                peerLastHelloMs[hello.PeerId] = NowMs;
                answeredPingMs[hello.PeerId] = PingClockMs();
                var build = new PeerBuildInfo(NetGuard.Clean(hello.Version), NetGuard.Clean(hello.Checksum));
                Session.Names[hello.PeerId] = NetGuard.Clean(hello.DisplayName);
                Session.Builds[hello.PeerId] = build;
                Session.Jobs[hello.PeerId] = NetGuard.ClassJob(hello.ClassJob);
                SetDebugBot(hello.PeerId, hello.DebugBot);
                DiagnosticLog.Info($"[Multiplayer] Hello from {hello.PeerId} ({Session.NameOf(hello.PeerId)}), build {build.Version} ({build.ShortChecksum}), mismatch={IsVersionMismatched(hello.PeerId)}, debug bot={hello.DebugBot}.");
                // Before the lobby state, which a fresh join takes as its first message.
                if (stream != null) Transmit(stream.Hello(hello.PeerId, hello.StreamEpoch, hello.StreamReceived));
                BroadcastLobbyState();
                lastRoles = null;
                break;
            }
            case ClaimRoleMessage claim when IsHost:
                peerLastSeenMs[claim.PeerId] = NowMs;
                if (!Enum.IsDefined(claim.Role))
                {
                    DiagnosticLog.Warn($"[Multiplayer] Dropped a claim for role {(int)claim.Role} from {Session.NameOf(claim.PeerId)} -- no such role.");
                    break;
                }
                ApplyClaim(claim.PeerId, claim.Role);
                break;
            case ReleaseRoleMessage release when IsHost:
                peerLastSeenMs[release.PeerId] = NowMs;
                ApplyRelease(release.PeerId);
                break;
            case SelfPoseMessage pose when IsHost:
                peerLastSeenMs[pose.PeerId] = NowMs;
                OnSelfPoseReceived(pose);
                break;
            case PongMessage pong when IsHost:
                peerLastSeenMs[pong.PeerId] = NowMs;
                peerLatencyMs[pong.PeerId] = PingClockMs() - pong.SentAtMs;
                answeredPingMs[pong.PeerId] = Math.Max(answeredPingMs.GetValueOrDefault(pong.PeerId), Math.Min(pong.SentAtMs, PingClockMs()));
                if (stream?.Pong(pong.PeerId, pong.StreamEpoch, pong.StreamSent, pong.StreamReceived) is { } askPeer) Transmit(askPeer);
                break;
            case StreamGapMessage gap when IsHost:
                if (stream != null) Transmit(stream.Resend(gap.PeerId, gap.From));
                break;
            case StreamGapMessage gap when gap.PeerId == MyPeerId:
                if (stream != null) Transmit(stream.Resend(senderPeerId, gap.From));
                break;
            case StreamLostMessage lost when IsHost:
                DiagnosticLog.Warn($"[Multiplayer] {Session.NameOf(lost.PeerId)} no longer has the messages this host missed -- going on from #{lost.Next}.");
                stream?.Lost(lost.PeerId, lost.Next, lost.Epoch);
                break;
            case StreamLostMessage lost when lost.PeerId == MyPeerId:
                OnHostStreamLost(senderPeerId, lost.Next);
                break;
            case StreamStateMessage state when !IsHost && state.PeerId == MyPeerId:
                if (stream != null) Transmit(stream.State(senderPeerId, state.StreamEpoch, state.Sent, state.Received));
                break;
            case ActedMessage acted when IsHost:
                OnPeerActed(acted);
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
                var enemies = Plugin.Game.World.Children.OfType<SimEnemy>().Where(e => e.IsActive).ToList();
                foreach (var entityId in NetGuard.Cap(applied.Enemies, NetGuard.MaxReportedEnemies))
                {
                    if (enemies.FirstOrDefault(e => e.EntityId == entityId) is not { } enemy)
                    {
                        DiagnosticLog.Warn($"[Multiplayer] Host: {who} reported status {applied.StatusId} on unknown enemy 0x{entityId:X} -- dropping.");
                        continue;
                    }
                    enemy.RemoveStatus(applied.StatusId);
                    enemy.AddStatusParam(applied.StatusId, applied.Param, duration);
                    DiagnosticLog.Info($"[Multiplayer] Host: applied {who}'s reported status {applied.StatusId} (duration={duration:F1}) to enemy 0x{entityId:X}.");
                }
                break;
            }
            case PeerAppliedRoleStatusMessage applied when IsHost:
            {
                var who = Session.NameOf(applied.PeerId);
                if (Session.RoleOf(applied.PeerId) is not { } reporter) break;
                var sprint = applied.StatusId == SprintHandler.SprintStatusId;
                if (!sprint && !Mitigation.ByStatusId.ContainsKey(applied.StatusId))
                {
                    DiagnosticLog.Warn($"[Multiplayer] Host: {who} reported role status {applied.StatusId}, which is no known mitigation -- dropping.");
                    break;
                }
                var duration = NetGuard.Clamp(applied.Duration, 0f, NetGuard.MaxMitigationSeconds);
                foreach (var role in NetGuard.Cap(applied.Roles, 8).Where(Enum.IsDefined))
                {
                    // Sprint is only ever a player's own.
                    if (sprint && role != reporter) continue;
                    if (Plugin.Game.World.Party.Get(role) is not { } member)
                    {
                        DiagnosticLog.Warn($"[Multiplayer] Host: {who} reported status {applied.StatusId} on role {role}, but that slot is empty -- dropping.");
                        continue;
                    }
                    if (sprint) SprintHandler.Apply(member, duration);
                    else
                    {
                        // A re-press replaces the status, as it does on the peer; AddStatus alone would stack it.
                        member.RemoveStatus(applied.StatusId);
                        member.AddStatusParam(applied.StatusId, applied.Param, duration);
                    }
                    DiagnosticLog.Info($"[Multiplayer] Host: applied {who}'s reported status {applied.StatusId} (duration={duration:F1}) to role {role}.");
                }
                break;
            }

            case LobbyStateMessage lobby when !IsHost:
                Session.ApplyLobbyState(lobby);
                TagBotSeats();
                NoteHelloAcknowledged();
                ApplyHostScenarioSettings();
                LobbyChanged?.Invoke();
                // A StartMessage lost on the way still starts a peer that has entered;
                // OnStartReceived is idempotent.
                if (lobby.Started && MyClaimedRole != null)
                    OnStartReceived(lobby.Clock);
                break;
            case EnterMessage enter when !IsHost:
                OnEnterReceived(enter);
                break;
            case EnteredMessage entered when IsHost:
                if (pendingEntered?.Remove(entered.PeerId) == true)
                    DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(entered.PeerId)} is in the zone ({pendingEntered.Count} to go).");
                break;
            case StartMessage start when !IsHost:
                OnStartReceived(start.Clock);
                break;
            case GamePacketsMessage packets when !IsHost:
                OnGamePacketsReceived(packets);
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
                if (Plugin.Game.World.Map.IsInInstance) Plugin.Game.Leave();
                BroadcastRunEnded(returnedToInn: true, reason);
                break;
            }
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
            case EndMessage end when !IsHost:
                OnEndReceived(end);
                break;
            case PingMessage ping when !IsHost:
            {
                var (epoch, sent, received) = stream?.PongFields(senderPeerId) ?? (0, 0, -1);
                Transmit(new PongMessage(MyPeerId, ping.SentAtMs, epoch, sent, received));
                var ours = NetGuard.Cap(ping.StreamReceived, NetGuard.MaxSessionPeers).FirstOrDefault(c => c?.PeerId == MyPeerId);
                if (stream?.Ping(senderPeerId, ping.StreamSent, ours) is { } askHost) Transmit(askHost);
                if (peerRunStarted && ping.Clock is { } clock) pingClock = (clock, Time.GetTimestamp());
                break;
            }
            case PeerStatusMessage status when !IsHost:
                peerStatuses.Clear();
                foreach (var (id, entry) in status.Statuses)
                    peerStatuses[id] = entry;
                if (peerStatuses.GetValueOrDefault(MyPeerId)?.LatencyMs is { } roundTrip)
                {
                    roundTripsMs.Enqueue(NetGuard.Clamp(roundTrip, 0f, 4000f));
                    while (roundTripsMs.Count > LeadRoundTrips) roundTripsMs.Dequeue();
                }
                break;
            // The host leaving ends the session for everyone; a departing peer only shrinks
            // the roster.
            case SessionEndedMessage ended when ended.PeerId == Session.HostId && isFromHost:
            {
                var who = Session.NameOf(ended.PeerId); // before LeaveSessionInternal wipes Session
                DiagnosticLog.Info($"[Multiplayer] Host {who} left -- session ending for the whole group.");
                // IsInInstance, not running: a Reset clears running while staying in-instance.
                if (Plugin.Game.World.Map.IsInInstance) Plugin.Game.Leave();
                LeaveSessionInternal(notifyOthers: false);
                SessionEndReason = $"{who} left -- session ended.";
                LobbyChanged?.Invoke();
                break;
            }
            case SessionEndedMessage ended when IsHost:
            {
                // A leave sent on the way out can still be in flight when the same peer comes
                // back. Right after a Hello it is held until the peer's silence confirms it.
                var sinceHello = NowMs - peerLastHelloMs.GetValueOrDefault(ended.PeerId, long.MinValue / 2);
                if (sinceHello < RejoinGraceMs)
                {
                    DiagnosticLog.Info($"[Multiplayer] Holding a leave from {Session.NameOf(ended.PeerId)} -- they registered {sinceHello}ms ago, so it may have crossed their rejoin.");
                    deferredLeaveMs[ended.PeerId] = NowMs;
                    break;
                }
                RemovePeer(ended.PeerId);
                break;
            }
            // Everyone else learns of it from the lobby state broadcast alongside.
            case KickMessage kick when !IsHost && kick.PeerId == MyPeerId:
                DiagnosticLog.Info($"[Multiplayer] {(kick.Banned ? "Banned" : "Removed")} from the session by the host.");
                if (Plugin.Game.World.Map.IsInInstance) Plugin.Game.Leave();
                LeaveSessionInternal(notifyOthers: false);
                SessionEndReason = kick.Banned
                    ? "You were banned from the session by the host."
                    : "You were removed from the session by the host.";
                LobbyChanged?.Invoke();
                break;
            // Only a seated peer can end the run for everyone.
            case ResetRequestMessage req when IsHost && Session.RoleOf(req.PeerId) != null:
                DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(req.PeerId)} requested a reset.");
                Plugin.Game.Reset();
                break;
            // IsInInstance, not running: Leave must still work after a Reset.
            case LeaveRequestMessage req when IsHost && Session.RoleOf(req.PeerId) != null:
                DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(req.PeerId)} requested to leave the instance.");
                if (Plugin.Game.World.Map.IsInInstance)
                    Plugin.Game.Leave();
                // Unconditional, or the requester's own Leave button waits forever.
                BroadcastRunEnded(returnedToInn: true);
                break;
            case AnnouncementMessage announcement when PeerInRun:
                Plugin.Game.World.Announce(NetGuard.Clean(announcement.Text));
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
