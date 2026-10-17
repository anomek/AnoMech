using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Multiplayer;

public sealed partial class MultiplayerManager
{
    // ---- Per-frame tick (framework thread; see Plugin.OnFrameworkUpdate) ----

    // Local end-of-run bookkeeping only; doesn't touch the relay, session or roster.
    private void EndHostRunLocally()
    {
        running = false;
        hostRunParams = null;
        pendingEntered = null;
        hostStageSetFor = null;
        DebugBotControl.Enabled = false;
        Plugin.Game.PartyMemberKilled -= OnPartyMemberKilledHost;
        Session.Started = false; // or the Start button stays disabled
    }

    // Tick()'s edge trigger fires once per run (hostScenarioStarted), so a Reset followed by
    // a separate Leave relies on OnInstanceExited for the Leave half.
    //
    // returnedToInn is caller-supplied: Leave()/Reset() do their work in a deferred
    // Framework.Run callback, so IsInInstance read right after Leave() still says true.
    private void BroadcastRunEnded(bool returnedToInn, string? reason = null)
    {
        FlushPackets(force: true);
        EndHostRunLocally();
        _ = Send(LobbyMessage());
        DiagnosticLog.Info($"[Multiplayer] Run ended (ReturnedToInn={returnedToInn}) -- broadcasting EndMessage.");
        _ = Send(new EndMessage(ReturnedToInn: returnedToInn, Reason: reason));
        if (reason != null) AnnounceRunEnded(reason);
        endSentReturnedToInn = returnedToInn;
        LobbyChanged?.Invoke();
    }

    // The stay's last end went out with ReturnedToInn, so the zone going away says nothing new.
    private bool endSentReturnedToInn;

    // The stay is over, however it ended: the natives go back to the real ones. On the host this
    // follows the teardown, which peers still need; a peer's own leave path lifts its natives
    // before the teardown (LeavePeerStay), and this catches the zone going away under it.
    private void OnInstanceExited()
    {
        if (IsHost)
        {
            FlushPackets(force: true);
            recorder?.Uninstall();
            recorder = null;
            if (endSentReturnedToInn) return;
            BroadcastRunEnded(returnedToInn: true);
            return;
        }
        replay?.Clear();
        replay = null;
        peerNatives?.Uninstall();
        peerNatives = null;
        peerEnteredInstance = false;
        peerRunStarted = false;
    }

    // Subscribed on first Tick (Plugin.Game isn't set at field-init time); the
    // handlers gate on IsHost.
    private bool gameEventsSubscribed;

    private void SubscribeGameEventsOnce()
    {
        if (gameEventsSubscribed) return;
        gameEventsSubscribed = true;
        Plugin.Game.InstanceExited += OnInstanceExited;
        Natives.UserActions.Acted += OnActed;
        Plugin.Game.World.Announced += text =>
        {
            if (!IsHost || stream == null) return;
            _ = Send(new AnnouncementMessage(text));
        };
    }

    // A peer heard from after its held leave came back (pongs arrive every ping); one that
    // stays silent for the stale timeout really left.
    private void ConfirmDeferredLeaves()
    {
        var now = NowMs;
        foreach (var (peerId, heldAt) in deferredLeaveMs.ToList())
        {
            if (peerLastSeenMs.GetValueOrDefault(peerId, long.MinValue) > heldAt)
            {
                deferredLeaveMs.Remove(peerId);
                continue;
            }
            if (now - heldAt < PeerStaleTimeoutMs) continue;
            DiagnosticLog.Info($"[Multiplayer] {Session.NameOf(peerId)} stayed silent after their held leave -- removing them.");
            RemovePeer(peerId);
        }
    }

    // The run's packets, at most PacketSendsPerSecond messages of them; force sends what's
    // pending before something the peers must read after it.
    private const float PacketSendInterval = 1f / 60f;
    private float sincePacketSend = PacketSendInterval;

    private void FlushPackets(bool force = false)
    {
        if (recorder is not { HasPending: true } r) return;
        if (!force && sincePacketSend < PacketSendInterval) return;
        sincePacketSend = 0f;
        _ = Send(new GamePacketsMessage(r.Drain()));
    }

    // Zone loads take seconds on a slow client; past this the run is called off.
    private const float EnterTimeoutSeconds = 30f;

    // As Game.EnterAndStart waits after seating: the doppels stand on every client before the
    // run talks to them.
    private const float StageSettleSeconds = 2f;

    private void TickHostEntry(float deltaSeconds)
    {
        if (!running || hostRunParams is not { } p || pendingEntered is not { } pending) return;
        enterTimer += deltaSeconds;
        if (pending.Count > 0)
        {
            if (enterTimer < EnterTimeoutSeconds) return;
            var names = string.Join(", ", pending.Select(Session.NameOf));
            DiagnosticLog.Warn($"[Multiplayer] {names} didn't enter the zone within {EnterTimeoutSeconds:F0}s -- calling the run off.");
            if (Plugin.Game.World.Map.IsInInstance) Plugin.Game.Leave();
            BroadcastRunEnded(returnedToInn: true, $"{names} didn't make it into the zone");
            return;
        }
        if (hostStageSetFor is not { } settled) return;
        hostStageSetFor = settled + deltaSeconds;
        if (hostStageSetFor < StageSettleSeconds) return;
        pendingEntered = null;
        hostStageSetFor = null;
        recorder!.Recording = true;
        DiagnosticLog.Info("[Multiplayer] Host: everyone is in -- starting the run.");
        Plugin.Game.StartScenario(p, OnHostStartResolved);
    }

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds is > 0f and < MaxFrameSampleSeconds)
            averageFrameSeconds += (deltaSeconds - averageFrameSeconds) * FrameSmoothing;
        sincePacketSend += deltaSeconds;
        StampEventClock();
        SubscribeGameEventsOnce();
        DrainPendingMessages();
        TickReconnect();

        // Before the IsConnected return below: the join may still be connecting or reconnecting.
        if (IsJoinTimedOut)
        {
            var reason = JoinTimedOutReason();
            DiagnosticLog.Warn($"[Multiplayer] Gave up joining session {SessionCode} after {JoinDeadlineMs / 1000}s: {reason}");
            LeaveSession();
            SessionEndReason = reason;
            LobbyChanged?.Invoke();
            return;
        }

        // Before the IsConnected return below: this is the relay-down case.
        if (IsHost && disconnectedSinceMs is { } since && running && Plugin.Game.World.Map.IsInInstance
            && NowMs - since > PeerStaleTimeoutMs)
        {
            DiagnosticLog.Warn($"[Multiplayer] Disconnected from the relay for over {PeerStaleTimeoutMs / 1000}s while running -- leaving the zone myself (every peer has likely already given up waiting and left on their own).");
            Plugin.Game.Leave();
            EndHostRunLocally();
            disconnectedSinceMs = null;
            LobbyChanged?.Invoke();
        }

        if (relay is { IsConnected: true } && !TickLink(deltaSeconds)) return;
        TickRun(deltaSeconds);
    }

    // What needs the link up: the Hello until it's answered, the ping cycle and the liveness it
    // checks, the lobby's start check. False once the session is over.
    private bool TickLink(float deltaSeconds)
    {
        AnnounceOwnJobIfChanged();
        ResendHelloUntilAcknowledged(deltaSeconds);

        if (IsHost)
        {
            pingTimer += deltaSeconds;
            if (pingTimer >= PingIntervalSeconds)
            {
                pingTimer = 0f;
                SendPingAndRefreshStatuses();
            }

            if (pendingStartResponses is { Count: > 0 } pending)
            {
                startCheckTimer += deltaSeconds;
                if (startCheckTimer >= StartCheckTimeoutSeconds)
                {
                    DiagnosticLog.Info($"[Multiplayer] StartCheck timed out waiting on: {string.Join(", ", pending.Select(Session.NameOf))}.");
                    foreach (var peerId in pending)
                        startCheckFailures[peerId] = "no response";
                    FinishStartCheck();
                }
            }

            if (deferredLeaveMs.Count > 0) ConfirmDeferredLeaves();
            if (running && Plugin.Game.World.Map.IsInInstance) EndRunIfBehind();
        }
        else if (IsSessionNotFound)
        {
            DiagnosticLog.Warn($"[Multiplayer] No host responded within {NoHostFoundTimeoutMs / 1000}s of joining session {SessionCode} -- session not found.");
            var reason = $"Reached the server, but no host answered in session {SessionCode}. "
                         + "Check the code, or ask the host whether their session is still up.";
            LeaveSession();
            SessionEndReason = reason;
            LobbyChanged?.Invoke();
            return false;
        }
        else if (IsHostStale)
        {
            // Only a host that vanished without a SessionEnded (crash, hard drop). IsInInstance
            // rather than running: the deferred zone entry may not have happened yet, and
            // Leave() assumes it has.
            DiagnosticLog.Warn($"[Multiplayer] Lost contact with the host (no message in {SecondsSinceHostMessage:F1}s, threshold {PeerStaleTimeoutMs / 1000}s) -- leaving.");
            if (running && Plugin.Game.World.Map.IsInInstance) LeavePeerStay();
            LeaveSession();
            SessionEndReason = "Lost contact with the host.";
            LobbyChanged?.Invoke();
            return false;
        }

        if (IsHost)
        {
            if (startWait.Tick()) LobbyChanged?.Invoke();
        }
        else AnswerStartCheck(deltaSeconds);
        return true;
    }

    // The run goes on whether the link is up or not: what it sends meanwhile goes out once the
    // link is back (SessionStream).
    private void TickRun(float deltaSeconds)
    {
        if (!running) return;

        if (IsHost)
        {
            TickHostEntry(deltaSeconds);
            if (Plugin.Game.ActiveScenario != null)
            {
                if (!hostScenarioStarted)
                {
                    hostScenarioStarted = true;
                    AnnounceStart();
                }
            }
            else if (!hostScenarioStarted)
            {
                return;
            }
            else
            {
                // Reset/Leave's deferred work is done by now, so IsInInstance is safe to read
                // here (see BroadcastRunEnded).
                BroadcastRunEnded(!Plugin.Game.World.Map.IsInInstance);
                return;
            }
            recorder?.RecordAfflictions(Plugin.Game.World.Party);
            FlushPackets();
            if (!aiReplayStateSent) TrySendAiReplayState();
            SendMidRunUpdate();
            SendForcedMoves();
            if (pendingRolesSend is null or { IsCompleted: true })
                pendingRolesSend = SampleAndBroadcastRoles();
        }
        else
        {
            if (!peerEnteredInstance) return; // this run's zone entry is still queued
            if (!Plugin.Game.World.Map.IsInInstance)
            {
                DiagnosticLog.Info("[Multiplayer] Peer's zone was unloaded out from under the run (IsInInstance went false) -- stopping locally.");
                running = false;
                StopDebugBotReplay();
                return;
            }
            replay?.Tick();
            TickDebugBotReplay(deltaSeconds);
            SyncClockToHost();
            CatchUpClockToHost();
            SendSelfPose();
        }
    }
}
