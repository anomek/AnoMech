using AnoMech.Core;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;

namespace AnoMech.Multiplayer;

public sealed partial class MultiplayerManager
{
    // ---- Debug: bot-controlled peer replay ----------------------------------

    // Polled from Tick until it succeeds: LastState isn't set on the first tick after Start.
    private void TrySendAiReplayState()
    {
        if (TryResolveScenario() is not IMultiplayerReplayable replayable) return;
        if (replayable.BuildReplayStateMessage() is not { } msg) return;
        aiReplayStateSent = true;
        DiagnosticLog.Info("[Multiplayer] Host: broadcasting AiReplayState for this run.");
        _ = relay!.SendAsync(msg);
    }

    private bool IsValidAiIndex(IScenario scenario) =>
        Session.SelectedAi >= 0 && Session.SelectedAi < scenario.AiStrats.Count;

    // Needs both the host's replay message and our own zone entry, which arrive in either
    // order, so Dispatch and OnPeerStartResolved both call it.
    private void TryStartDebugBotReplay()
    {
        if (!debugBotControlled || debugBotReplayStarted) return;
        // peerEnteredInstance outlives the run; the AI drives the real character.
        if (!PeerInRun) return;
        if (MyClaimedRole is not { } myRole) return;
        if (pendingGenericReplayState is not { } msg) return;
        if (TryResolveScenario() is not IMultiplayerReplayable replayable) return;
        var world = Plugin.GameInstance.World;

        debugBotReplayStarted = true;
        if (!IsValidAiIndex(replayable))
        {
            DiagnosticLog.Warn($"[Multiplayer] Peer: SelectedAi {Session.SelectedAi} is out of range for {replayable.Name} ({replayable.AiStrats.Count} strats) -- skipping debug-bot replay.");
            return;
        }
        DiagnosticLog.Info($"[Multiplayer] Peer: starting debug-bot replay for {myRole}.");
        var shadowState = world.Events.FromRunStart(() => replayable.StartReplay(msg, Session.SelectedAi, myRole, world));
        if (shadowState != null)
        {
            debugShadowStateGeneric = shadowState;
            DebugBotControl.Enabled = true;
            GiveLocalPlayerObstacles();
        }
    }

    // PartyCreator wires the obstacle field to bot doppels only; a bot-driven real character
    // steers like one (see IMultiplayerReplayable.RebuildPeerObstacles).
    private static void GiveLocalPlayerObstacles()
    {
        var world = Plugin.GameInstance.World;
        if (world.Party.Player is { } player) player.Obstacles = world.Obstacles;
    }

    // Clears the run's replay state, not the sticky debugBotControlled toggle.
    private void StopDebugBotReplay()
    {
        if (debugBotReplayStarted) DiagnosticLog.Info("[Multiplayer] Peer: stopping debug-bot replay.");
        DebugBotControl.Enabled = false;
        pendingGenericReplayState = null;
        debugShadowStateGeneric = null;
        debugBotReplayStarted = false;
    }

}
