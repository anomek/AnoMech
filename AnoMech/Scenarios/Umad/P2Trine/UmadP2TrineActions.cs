using System;
using AnoMech.Core.EnemyActions;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;

namespace AnoMech.Scenarios.Umad.P2Trine;

internal static class UmadP2TrineActions
{
    internal const uint CastId = 47839;
    internal const uint BlastId = 47840;
    internal const uint LeftId = 47821;
    internal const uint RightId = 47822;
    internal const uint BusterCastId = 50311;
    internal const uint BusterId = 47823;
    internal static readonly EnemyAction Blast = new(BlastId)
    {
        Effects = [Damage(Lethal)], Timing = new() { DamageDelay = 0.35f },
    };
    internal static EnemyAction Wing(bool left) => new(left ? LeftId : RightId)
    {
        Area = new() { Rotation = left ? MathF.PI / 2f : -MathF.PI / 2f },
        Cast = new() { FaceTarget = false },
        Effects = [Damage(Lethal)], Timing = new() { DamageDelay = 0.5f },
    };
    internal static readonly EnemyAction Buster = new(BusterId)
    {
        Cast = new() { FaceTarget = false },
        Effects = [Damage(TankBuster.MinMit(0.60f))],
        Timing = new() { DamageDelay = 0.5f },
    };
}

