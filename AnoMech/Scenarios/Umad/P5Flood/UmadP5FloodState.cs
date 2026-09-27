using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Umad.P5Flood;

// Per-run randomization: whether each line marches forward (see UmadP5FloodScenario's
// NeSwPoints/NwSePoints) or reversed, plus which diagonal leads; the stack's target is rolled
// per tick (PickStackTarget).
public sealed class UmadP5FloodState
{
    public bool NeSwReversed { get; }
    public bool NwSeReversed { get; }
    public bool NeSwFirst { get; }

    // The party stacks in a cardinal quadrant near the boss and rotates 90 deg after every tick,
    // in one direction for the whole sequence. Every line sits at a fixed +-45 deg rotation at
    // one of 4 anchors per diagonal, offset +-5 or +-15 from its centreline, so the two rects on
    // a side tile [0,20] with no gap. Solving which
    // cardinal survives every tick for all 8 (NeSwFirst, NeSwReversed, NwSeReversed)
    // combinations gives a unique start quadrant and direction: the same quadrant is safe for
    // the first two ticks, then it rotates once per tick.
    public int StartQuadrant { get; } // 0=N, 1=E, 2=S, 3=W (see UmadP5FloodAi.Quadrants)
    public bool RotationClockwise { get; }

    // Rolled per tick: the target is a different non-tank from one tick to the next, never a
    // tank. The override pins every tick to one role.
    public PartyRole? StackTargetOverride { get; }

    public PartyRole PickStackTarget() => StackTargetOverride ?? rng.NextObj(NonTankRoles);

    private static readonly PartyRole[] NonTankRoles =
    [
        PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    // The scenario's own unscaled clock; bots schedule on it, not the EventTimeScale-scaled AiManager.
    public EventScheduler Timeline { get; }

    private readonly Rng rng = new();

    public UmadP5FloodState(UmadP5FloodStateOverrides overrides, EventScheduler timeline)
    {
        Timeline = timeline;
        NeSwReversed = Resolve(overrides.LineNeSw);
        NwSeReversed = Resolve(overrides.LineNwSe);
        // Which diagonal leads is an even coin flip.
        NeSwFirst = overrides.NeSwFirst ?? rng.NextObj(true, false);
        StackTargetOverride = overrides.AnchorRole;
        StartQuadrant = overrides.StartQuadrant ?? DeriveStartQuadrant(NeSwReversed, NwSeReversed);
        RotationClockwise = overrides.RotationClockwise ?? DeriveRotationClockwise(NeSwFirst, NeSwReversed, NwSeReversed);
    }

    // 0=N, 1=E, 2=S, 3=W.
    private static int DeriveStartQuadrant(bool neSwReversed, bool nwSeReversed) => (neSwReversed, nwSeReversed) switch
    {
        (false, false) => 2, // S
        (true, false) => 1,  // E
        (false, true) => 3,  // W
        (true, true) => 0,   // N
    };

    private static bool DeriveRotationClockwise(bool neSwFirst, bool neSwReversed, bool nwSeReversed) =>
        neSwFirst != (neSwReversed == nwSeReversed);

    private bool Resolve(FloodDirection direction)
    {
        if (direction == FloodDirection.Random)
            direction = rng.NextObj(FloodDirection.Forward, FloodDirection.Reversed);
        return direction == FloodDirection.Reversed;
    }
}
