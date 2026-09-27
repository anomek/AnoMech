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
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using AnoMech.Scenarios;

namespace AnoMech.Multiplayer;

public sealed partial class MultiplayerManager
{
    // ---- Per-frame tick (framework thread; see Plugin.OnFrameworkUpdate) ----

    // Local end-of-run bookkeeping only; doesn't touch the relay, session or roster.
    private void EndHostRunLocally()
    {
        running = false;
        DebugBotControl.Enabled = false;
        Plugin.GameInstance.PartyMemberKilled -= OnPartyMemberKilledHost;
        Plugin.GameInstance.World.OmenSpawned -= OnOmenSpawnedHost;
        Session.Started = false; // or the Start button stays disabled
    }

    // Tick()'s edge trigger fires once per run (hostScenarioStarted), so a Reset followed by
    // a separate Leave needs this called explicitly for the Leave half.
    //
    // returnedToInn is caller-supplied: Leave()/Reset() do their work in a deferred
    // Framework.Run callback, so IsInInstance read right after Leave() still says true.
    private void BroadcastRunEnded(bool returnedToInn, string? reason = null)
    {
        EndHostRunLocally();
        _ = relay?.SendAsync(LobbyMessage());
        DiagnosticLog.Info($"[Multiplayer] Run ended (ReturnedToInn={returnedToInn}) -- broadcasting EndMessage.");
        _ = relay?.SendAsync(new EndMessage(ReturnedToInn: returnedToInn, Reason: reason));
        if (reason != null) AnnounceRunEnded(reason);
        LobbyChanged?.Invoke();

        // Arms Tick()'s resend loop.
        pendingEndResendReturnedToInn = returnedToInn;
        pendingEndResendReason = reason;
        endResendsRemaining = EndMessageResendCount;
        endResendTimer = 0f;
    }

    // The host's own Leave button, right after Game.Leave(); see BroadcastRunEnded.
    public void NotifyLeftInstance()
    {
        if (!IsHost || relay is not { IsConnected: true }) return;
        BroadcastRunEnded(returnedToInn: true);
    }

    // Subscribed on first Tick (Plugin.GameInstance isn't set at field-init time); the
    // handlers gate on IsHost.
    private bool mapEventsSubscribed;

    private void SubscribeMapEventsOnce()
    {
        if (mapEventsSubscribed) return;
        mapEventsSubscribed = true;
        Plugin.GameInstance.World.Map.EffectApplied += (packetFlags, index) =>
        {
            if (!IsHost || relay is not { IsConnected: true }) return;
            DiagnosticLog.Info($"[Multiplayer] Host: broadcasting MapEffect packetFlags=0x{packetFlags:X8} index=0x{index:X}.");
            _ = relay.SendAsync(new MapEffectMessage(packetFlags, index));
        };
        Plugin.GameInstance.World.Map.DirectorUpdated += (category, arg1, arg2, arg3, arg4, arg5, arg6) =>
        {
            if (!IsHost || relay is not { IsConnected: true }) return;
            DiagnosticLog.Info($"[Multiplayer] Host: broadcasting MapDirectorUpdate category=0x{category:X8}.");
            _ = relay.SendAsync(new MapDirectorUpdateMessage(category, arg1, arg2, arg3, arg4, arg5, arg6));
        };
        Plugin.GameInstance.World.Map.WeatherChanged += (weatherId, transition) =>
        {
            if (!IsHost || relay is not { IsConnected: true }) return;
            DiagnosticLog.Info($"[Multiplayer] Host: broadcasting SetWeather weatherId={weatherId} transition={transition}.");
            _ = relay.SendAsync(new SetWeatherMessage(weatherId, transition));
        };
        Plugin.GameInstance.World.Map.FogHoldChanged += value =>
        {
            if (!IsHost || relay is not { IsConnected: true }) return;
            DiagnosticLog.Info($"[Multiplayer] Host: broadcasting SetFogHold {(value is { } v ? v.ToString("F0") : "off")}.");
            _ = relay.SendAsync(new SetFogHoldMessage(value));
        };
        Plugin.GameInstance.World.Announced += text =>
        {
            if (!IsHost || relay is not { IsConnected: true }) return;
            _ = relay.SendAsync(new AnnouncementMessage(text));
        };
    }

    // A peer heard from after its held leave came back (pongs arrive every ping); one that
    // stays silent for the stale timeout really left.
    private void ConfirmDeferredLeaves()
    {
        var now = Environment.TickCount64;
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

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds is > 0f and < MaxFrameSampleSeconds)
            averageFrameSeconds += (deltaSeconds - averageFrameSeconds) * FrameSmoothing;
        SubscribeMapEventsOnce();
        DrainPendingMessages();

        // Before the IsConnected return below: this is the relay-down case.
        if (IsHost && disconnectedSinceMs is { } since && running && Plugin.GameInstance.World.Map.IsInInstance
            && Environment.TickCount64 - since > PeerStaleTimeoutMs)
        {
            DiagnosticLog.Warn($"[Multiplayer] Disconnected from the relay for over {PeerStaleTimeoutMs / 1000}s while running -- leaving the zone myself (every peer has likely already given up waiting and left on their own).");
            Plugin.GameInstance.Leave();
            EndHostRunLocally();
            disconnectedSinceMs = null;
            LobbyChanged?.Invoke();
        }

        if (relay is not { IsConnected: true }) return;

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

            if (pendingEndResendReturnedToInn is { } returnedToInn)
            {
                endResendTimer += deltaSeconds;
                if (endResendTimer >= EndMessageResendIntervalSeconds)
                {
                    endResendTimer = 0f;
                    endResendsRemaining--;
                    DiagnosticLog.Info($"[Multiplayer] Re-broadcasting EndMessage (ReturnedToInn={returnedToInn}), {endResendsRemaining} retries left.");
                    _ = relay?.SendAsync(LobbyMessage());
                    _ = relay?.SendAsync(new EndMessage(ReturnedToInn: returnedToInn, Reason: pendingEndResendReason));
                    if (endResendsRemaining <= 0) pendingEndResendReturnedToInn = null;
                }
            }

            if (deferredLeaveMs.Count > 0) ConfirmDeferredLeaves();
        }
        else if (IsSessionNotFound)
        {
            DiagnosticLog.Warn($"[Multiplayer] No host responded within {NoHostFoundTimeoutMs / 1000}s of joining session {SessionCode} -- session not found.");
            LeaveSession();
            SessionEndReason = "Session not found.";
            LobbyChanged?.Invoke();
            return;
        }
        else if (IsHostStale)
        {
            // Only a host that vanished without a SessionEnded (crash, hard drop). IsInInstance
            // rather than running: the deferred zone entry may not have happened yet, and
            // Leave() assumes it has.
            DiagnosticLog.Warn($"[Multiplayer] Lost contact with the host (no message in {SecondsSinceHostMessage:F1}s, threshold {PeerStaleTimeoutMs / 1000}s) -- leaving.");
            if (running && Plugin.GameInstance.World.Map.IsInInstance) Plugin.GameInstance.Leave();
            LeaveSession();
            SessionEndReason = "Lost contact with the host.";
            LobbyChanged?.Invoke();
            return;
        }

        if (IsHost) RetryWaitingStart();
        else AnswerStartCheck(deltaSeconds);

        if (!running) return;

        if (IsHost)
        {
            if (Plugin.GameInstance.ActiveScenario != null)
            {
                hostScenarioStarted = true;
            }
            else if (!hostScenarioStarted)
            {
                return;
            }
            else
            {
                // Reset/Leave's deferred work is done by now, so IsInInstance is safe to read
                // here (see BroadcastRunEnded).
                BroadcastRunEnded(!Plugin.GameInstance.World.Map.IsInInstance);
                return;
            }
            if (pendingSnapshotSend is null or { IsCompleted: true })
                pendingSnapshotSend = SampleAndBroadcastSnapshot();
            if (pendingRolesSend is null or { IsCompleted: true })
                pendingRolesSend = SampleAndBroadcastRoles();
        }
        else
        {
            if (!peerEnteredInstance) return; // this run's zone entry is still queued
            if (!Plugin.GameInstance.World.Map.IsInInstance)
            {
                DiagnosticLog.Info("[Multiplayer] Peer's zone was unloaded out from under the run (IsInInstance went false) -- stopping locally.");
                running = false;
                DebugBotControl.Enabled = false;
                return;
            }
            SyncClocksToHost();
            SendSelfPose();
            SendSelfMitigationIfChanged();
        }
    }

}
