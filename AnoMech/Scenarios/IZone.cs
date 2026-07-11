using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
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

    // Scenario-local positions whose BG SharedGroup colliders are dropped at start.
    IReadOnlyList<Vector3> ColliderRemovalPoints => Array.Empty<Vector3>();

    // Zone-wide setup, first in the cascade. Default no-op.
    void Run(SimWorld world) { }
}
