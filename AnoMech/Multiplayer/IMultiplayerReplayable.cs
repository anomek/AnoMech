using System.Collections.Generic;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;

namespace AnoMech.Multiplayer;

// A scenario owns its own debug-bot-replay wiring here, so MultiplayerManager never switches
// on a concrete scenario type and needs no change to support one more.
public interface IMultiplayerReplayable : IScenario
{
    // Host-only, polled each tick until non-null: LastState isn't set on the first post-Start
    // tick, and a live-RNG tie-break may not have rolled yet.
    MpMessage? BuildReplayStateMessage();

    // Peer-only. Rebuild a shadow state, start the given Ai strat against it, and return that
    // state (opaque to everything but this scenario); null if the message/index don't check out.
    object? StartReplay(MpMessage message, int aiIndex, PartyRole myRole, SimWorld world);

    // Optional, host-only: for state resolved mid-run, which the one-shot broadcast can't carry.
    // Track your own last-sent value to stay edge-triggered.
    MpMessage? BuildMidRunUpdateMessage() => null;

    // Optional, peer-only: applies whatever BuildMidRunUpdateMessage produced.
    void ApplyMidRunUpdate(object shadowState, MpMessage message) { }

    // Optional, peer-only, every frame: re-resolve SimEnemy handles that hadn't reached this
    // client yet when StartReplay ran. peerEnemies: this client's copies of the host's enemies.
    void RefreshLiveHandles(object shadowState, IReadOnlyDictionary<int, SimEnemy> peerEnemies) { }

    // Optional, peer-only: this client's own sim also holds the statuses the host's lists show,
    // for a strat that reads them and the locks they bring. Off, it holds only its own.
    bool MirrorsHostStatuses => false;

    // Optional, peer-only, every frame: per-frame Ai work the scenario's own Tick drives on the
    // host, since a peer never runs that Tick.
    void TickReplay(object shadowState, float deltaSeconds) { }

    // Optional, peer-only, every frame, after `obstacles` was cleared: a bot-driven peer's
    // steering obstacles, rebuilt from what this client was shown, since the scenario's own
    // Obstacles calls never run there.
    void RebuildPeerObstacles(ObstacleField obstacles, IReadOnlyDictionary<int, SimEnemy> peerEnemies, IReadOnlyDictionary<int, SimEventObject> peerEventObjects, SimCharacter? localPlayer) { }
}
