using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// Live in-world handles the scenario wires up and the AI reads, so the AI never has
// to scan world.Children for the boss or tethers it choreographs around. The scenario
// assigns Chaos/Exdeath at spawn and, as each black-hole wave starts, points
// TetherSortFrom at that wave's Kefka direction; Tethers then reads back the currently
// active grabby tethers already ordered clockwise from that direction.
public sealed class UmadP3BlackHoleScenarioObjects
{
    private readonly SimWorld world;

    public UmadP3BlackHoleScenarioObjects(SimWorld world) => this.world = world;

    public SimEnemy? Chaos { get; set; }
    public SimEnemy? Exdeath { get; set; }

    // Clockwise reference for ordering Tethers — set per wave to the active black-hole
    // wave's Kefka direction. Defaults to north before the first wave (no tethers yet).
    public Direction TetherSortFrom { get; set; } = Direction.N;

    public IReadOnlyList<SimTether> Tethers =>
        world.Children.OfType<SimTether>()
             .Where(t => t.IsActive && t.TetherId == TetherId.GrabbyTether && t.A is not null)
             .OrderBy(t => ClockwiseFrom(TetherSortFrom.RadiansFromNorth, t.A!.Position))
             .ToList();

    private static float ClockwiseFrom(float north, Vector3 p)
    {
        var d = (MathF.Atan2(p.X, -p.Z) - north) % MathF.Tau;
        return d < 0f ? d + MathF.Tau : d;
    }
}
