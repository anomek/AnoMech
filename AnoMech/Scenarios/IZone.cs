using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios;

// A raid territory: the identity every scenario inside it shares. Authored bottom-up —
// scenarios point at their IPhase, phases point here — and Game derives the downward
// zone -> phase -> scenario view for the menu.
public interface IZone
{
    string Name { get; }                                    // canonical duty name
    uint TerritoryId { get; }
    Vector3 Origin { get; }
    byte Level => 0;
    ushort ItemLevel => 0;

    // At least one; [0] is the default.
    IReadOnlyList<WaymarkLayout> WaymarkPresets { get; }

    // The seats in the order the fight's strats name them MT OT H1 H2 M1 M2 R1 R2.
    IReadOnlyList<PartyRole> SeatOrder => PerRole.All;

    // Scenario-local positions whose BG SharedGroup colliders are dropped at start.
    IReadOnlyList<Vector3> ColliderRemovalPoints => Array.Empty<Vector3>();

    // Zone-wide setup, first in the cascade. Default no-op. Host/solo only -- may create
    // host-only simulation state (arena boundaries, scheduled MapEffect broadcasts) that
    // would conflict if a peer ran it too.
    void Run(SimWorld world) { }

    // Peer-safe subset of zone setup: pure client-side asset/table registration with no
    // simulation side effects (no SimArenaBoundary, no MapEffect broadcasts). Called for
    // BOTH host and peer, same reasoning as IScenario.RunInstanceEvents -- a peer's own
    // client needs this data (e.g. replay-derived RSV/RSF resource paths the server would
    // normally deliver for real duty content) just as much as the host's does. Default no-op.
    void RunClientSetup(SimWorld world) { }
}
