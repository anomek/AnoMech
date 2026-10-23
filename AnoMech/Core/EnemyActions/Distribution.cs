using System;
using System.Collections.Generic;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.EnemyActions;

// How one cast's hit lands on each target it reaches: in full on everyone, shared as a stack, heavier
// on the front of a line. Unset hits fall back to the cast's.
public abstract record Distribution
{
    public static readonly Distribution Each = new EachDistribution();

    // Too few in it, and everyone takes `understacked` instead (Lethal by default).
    public static Distribution Stack(int min, Hit? understacked = null)
        => new WildChargeDistribution(0, min, null, understacked);

    // The `front` first in `hits` take `frontHit`, the rest the cast's hit. With `min`, too few in
    // it and everyone takes `understacked` instead (Lethal by default).
    public static Distribution WildCharge(int front, int? min = null, Hit? frontHit = null, Hit? understacked = null)
        => new WildChargeDistribution(front, min, frontHit, understacked);

    public static Distribution Falloff(float lethalWithin) => new FalloffDistribution(lethalWithin);

    // Anything but `count` in it, and everyone takes `wrongCount` instead (Lethal by default): TEA's
    // Crashing Thunder (two).
    public static Distribution Exactly(int count, Hit? wrongCount = null) => new ExactDistribution(count, wrongCount);

    // `index` is the target's place in `hits`, front first: nearest the origin, or furthest up a line. The reason is said of a
    // death the hit deals.
    internal abstract (Hit? Hit, string? Reason) Assign(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits, int index, SimCharacter target);
}

internal sealed record EachDistribution : Distribution
{
    internal override (Hit? Hit, string? Reason) Assign(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits, int index, SimCharacter target) => (null, null);
}

internal sealed record WildChargeDistribution(int Front, int? Min, Hit? FrontHit, Hit? Understacked) : Distribution
{
    internal override (Hit? Hit, string? Reason) Assign(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits, int index, SimCharacter target)
    {
        if (hits.Count < Min)
            return (Understacked ?? Severity.Lethal, $"{hits.Count}/{Min} players in stack");
        return (index < Front ? FrontHit : null, null);
    }
}

internal sealed record FalloffDistribution(float LethalWithin) : Distribution
{
    internal override (Hit? Hit, string? Reason) Assign(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits, int index, SimCharacter target)
    {
        var dx = target.Position.X - ctx.Origin.Position.X;
        var dz = target.Position.Z - ctx.Origin.Position.Z;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        return distance < LethalWithin
            ? (Severity.Lethal, $"{distance:F0}y from it, lethal inside {LethalWithin:F0}y")
            : (null, null);
    }
}

internal sealed record ExactDistribution(int Count, Hit? WrongCount) : Distribution
{
    internal override (Hit? Hit, string? Reason) Assign(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits, int index, SimCharacter target)
        => hits.Count == Count ? (null, null) : (WrongCount ?? Severity.Lethal, $"{hits.Count} players in it, it takes exactly {Count}");
}
