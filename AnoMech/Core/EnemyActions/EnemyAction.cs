using System;
using System.Collections.Generic;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.EnemyActions;

// Everything one enemy action does; a purely visual cast needs none, use SimEnemy.Cast(actionId).
// Most defaults (cast time, area shape) come from the game's Action Excel sheet.
// Lives as a static readonly field in a scenario's <Scenario>Actions.cs.
public sealed record EnemyAction(uint ActionId)
{
    public CastSpec Cast { get; init; } = new();
    public AreaSpec Area { get; init; } = new();
    public IReadOnlyList<IEnemyActionEffect> Effects { get; init; } = [];
    public TimingSpec Timing { get; init; } = new();

    // Extra explanation for deaths caused by this action
    public string? DeathExplanation { get; init; }
}

// Cast bar and animation settings the Action sheet lacks, plus overrides of the ones it has.
public sealed record CastSpec
{
    // Delay from cast bar end to action effect. The in-game bar is this much shorter than the
    // sheet's cast time. Instant actions have neither.
    public const float ActionEffectOffset = 0.3f;

    // How long after release the caster stays rooted while the animation plays. Read it from an
    // in-game replay.
    public float AnimationLock { get; init; } = 0.6f;
    // How long after the bar starts the omen appears. Read it from an in-game replay.
    public float OmenDelay { get; init; }

    // Only for the rare case where the in-game cast time differs from the sheet's.
    public float? CastSecondsOverride { get; init; }

    // Turns the omen, the animation and the area from the caster's facing, in radians.
    public float Rotation { get; init; }
}

// The sheet's area is enough for most actions; customize it here when it isn't.
public sealed record AreaSpec
{
    // The one dimension the sheet lacks.
    // Cone: half-angle in radians (default 30°).
    // Donut: inner safe radius (default 0).
    // Ignored for other shapes.
    public float? Size { get; init; }

    // Changes the area's shape; required when the sheet's CastType is Custom.
    public CastType? CastTypeOverride { get; init; }

    // Spares the one character on caster's spot.
    public bool ExcludeCaster { get; init; }

    // Filters or orders the characters caught in the area before effects are applied.
    public Func<EnemyActionContext, IReadOnlyList<SimCharacter>, IReadOnlyList<SimCharacter>>? AdjustTargets { get; init; }
}

// When the action's effects land.
// Offsets count from cast bar end, not from the ActionEffect (vfx, always plays CastSpec.ActionEffectOffset later).
public sealed record TimingSpec
{
    // Snapshot: who is hit, when statuses land and who dies are decided here.
    // Hard to measure; keep 0 (bar end) unless the game clearly snapshots later.
    public float ResolveSnapshotOffset { get; init; }
    // Visual only: when flytext shows and killed characters die, counted from the snapshot.
    // Also starts knockbacks that have no delay of their own.
    // The default is a placeholder; measure each action's from a replay.
    public float DamageDelay { get; init; } = 1f;
}
