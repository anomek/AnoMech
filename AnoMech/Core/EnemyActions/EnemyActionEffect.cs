using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.EnemyActions;

// Applied to the characters an action hit (EnemyActionContext.Hits), after the area decided who
// they are. Effects run in list order: one listed after a Damage sees who that damage killed.
public interface IEnemyActionEffect
{
    void Apply(EnemyActionContext ctx);
}

// Authoring helpers: `using static AnoMech.Core.EnemyActions.EnemyActionEffects;`.
public static class EnemyActionEffects
{
    // The damage kind comes from the Action sheet's AttackType; override it only where the game's
    // own hit carries a different one.
    public static IEnemyActionEffect Damage(Severity? severity = null, Distribution? split = null, DamageKind? kindOverride = null)
        => Damage(VulnSpec.None, severity, split, kindOverride);

    public static IEnemyActionEffect Damage(VulnSpec vulns, Severity? severity = null, Distribution? split = null, DamageKind? kindOverride = null)
        => new DamageEffect(vulns, severity ?? Severity.Normal, split ?? Distribution.Each, kindOverride);

    public static VulnSpec VulnerableTo(ushort statusId, float? requiredMitigation = null, int minStacks = 1)
        => VulnSpec.None.VulnerableTo(statusId, requiredMitigation, minStacks);

    public static VulnSpec ProtectedBy(ushort statusId) => VulnSpec.None.ProtectedBy(statusId);

    // Survivors only.
    public static IEnemyActionEffect ApplyStatus(ushort statusId, float duration) => new ApplyStatusEffect(statusId, duration);

    // Survivors only.
    public static IEnemyActionEffect RemoveStatus(ushort statusId) => new RemoveStatusEffect(statusId);

    // One more stack of the family's `times` status, or the death it completes. Survivors only.
    public static IEnemyActionEffect ApplyRuin(RuinSpec ruin, int times, float duration)
        => new ApplyRuinEffect(ruin, ruin.StatusId(times), times, duration);

    // ApplyStatus, except whoever already holds `maxStacks` dies instead. Survivors only.
    public static IEnemyActionEffect ApplyStatusOrOverload(ushort statusId, int maxStacks, float duration = 0f)
        => new ApplyStatusOrOverloadEffect(statusId, maxStacks, duration);

    // `followUp` cast by the same caster once this resolve's deaths are dealt, when `when` holds.
    public static IEnemyActionEffect FollowUp(EnemyAction followUp, Func<EnemyActionContext, bool> when)
        => new FollowUpEffect(followUp, when);

    // Away from the caster, by a Knockback sheet row. Survivors only. `knockbackDelay` is from
    // resolve; null = TimingSpec.DamageDelay.
    public static IEnemyActionEffect Knockback(uint knockbackId, float? knockbackDelay = null)
        => new KnockbackEffect((_, _) => KnockbackLookup.TryGet(knockbackId, out var distance, out var speed) ? (distance, speed) : null, knockbackDelay);

    // Away from the caster, for an action with no known Knockback row. Survivors only.
    public static IEnemyActionEffect Knockback(float distance, float speed, float? knockbackDelay = null)
        => new KnockbackEffect((_, _) => (distance, speed), knockbackDelay);

    // Away from the caster, as far as `distance` says for each target (read at resolve; null = not
    // pushed). Survivors only.
    public static IEnemyActionEffect Knockback(Func<EnemyActionContext, SimCharacter, float?> distance, float speed, float? knockbackDelay = null)
        => new KnockbackEffect((ctx, target) => distance(ctx, target) is { } d ? (d, speed) : null, knockbackDelay);

    // Kills by facing alone: with `lookAway`, whoever has the origin in their front 90° arc; without,
    // whoever has it in their back 90° arc.
    public static IEnemyActionEffect Gaze(bool lookAway) => new GazeEffect(lookAway);

    // `effect` applied to the cast's target alone, when the area caught it.
    public static IEnemyActionEffect OnTarget(IEnemyActionEffect effect)
        => new FilteredEffect(effect, ctx => ctx.Hits.Where(t => ReferenceEquals(t, ctx.Target)));

    // `effect` applied to everyone hit but the cast's target.
    public static IEnemyActionEffect OnOthers(IEnemyActionEffect effect)
        => new FilteredEffect(effect, ctx => ctx.Hits.Where(t => !ReferenceEquals(t, ctx.Target)));

    // `effect` applied to those hit who carry `statusId` when it runs.
    public static IEnemyActionEffect OnHavingStatus(ushort statusId, IEnemyActionEffect effect)
        => new FilteredEffect(effect, ctx => ctx.Hits.Where(t => t.HasStatus(statusId)));

    // `effect` applied to the `count` hit nearest the origin (the front of a wild charge).
    public static IEnemyActionEffect OnFront(int count, IEnemyActionEffect effect)
        => new FilteredEffect(effect, ctx => ctx.Hits.Take(count));
}

internal sealed class FilteredEffect(IEnemyActionEffect effect, Func<EnemyActionContext, IEnumerable<SimCharacter>> select) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        var hits = ctx.Hits;
        ctx.Hits = select(ctx).ToList();
        try { effect.Apply(ctx); }
        finally { ctx.Hits = hits; }
    }
}

// A hit on someone an earlier hit already kills still shows its own number.
internal sealed class DamageEffect(VulnSpec vulns, Severity severity, Distribution split, DamageKind? kindOverride) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        var kind = kindOverride ?? DamageKinds.FromAttackType(Natives.Data.Action(ctx.Action.ActionId)?.AttackType ?? 0);
        for (var i = 0; i < ctx.Hits.Count; i++)
        {
            var target = ctx.Hits[i];
            var (hit, reason) = split.Assign(ctx, i, target);
            var hitSeverity = hit?.Severity ?? severity;
            var cause = DamageCheck.LethalCause(target, hit?.Vulns ?? vulns, hitSeverity, kind, ctx.Party);
            ctx.ShowDamage(target, hitSeverity.FlyTextAmount(kills: cause != null), kind.Icon());
            if (cause != null) ctx.Kill(target, Explain(reason, cause));
        }
    }

    // `cause` "" kills with no explanation of its own.
    private static string? Explain(string? reason, string cause)
        => cause.Length == 0 ? reason : reason is null ? cause : $"{reason}; {cause}";
}

internal sealed class ApplyStatusEffect(ushort statusId, float duration) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        foreach (var target in ctx.Hits)
            if (!ctx.IsKilled(target) && target.IsAlive())
                target.AddStatus(statusId, duration);
    }
}

internal sealed class RemoveStatusEffect(ushort statusId) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        foreach (var target in ctx.Hits)
            if (!ctx.IsKilled(target) && target.IsAlive())
                target.RemoveStatus(statusId);
    }
}

internal sealed class ApplyRuinEffect(RuinSpec ruin, ushort statusId, int times, float duration) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        foreach (var target in ctx.Hits)
        {
            if (ctx.IsKilled(target) || !target.IsAlive()) continue;
            if (ruin.Overloads(target, times))
                ctx.Kill(target, $"{StatusLookup.Name(statusId)} overload");
            else
                target.AddStatus(statusId, duration);
        }
    }
}

internal sealed class ApplyStatusOrOverloadEffect(ushort statusId, int maxStacks, float duration) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        foreach (var target in ctx.Hits)
        {
            if (ctx.IsKilled(target) || !target.IsAlive()) continue;
            if (target.FindStatus(statusId) is { } status && status.Stacks >= maxStacks)
                ctx.Kill(target, $"already at {maxStacks} {StatusLookup.Name(statusId)}");
            else
                target.AddStatus(statusId, duration);
        }
    }
}

internal sealed class GazeEffect(bool lookAway) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        var from = ctx.Origin.Position;
        foreach (var target in ctx.Hits)
        {
            var facing = target.Placement();
            if (lookAway ? facing.IsLookingAt(from) : facing.IsLookingAwayFrom(from))
                ctx.Kill(target, lookAway ? "looked at the gaze" : "faced away from the gaze");
        }
    }
}

internal sealed class FollowUpEffect(EnemyAction followUp, Func<EnemyActionContext, bool> when) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        if (!when(ctx)) return;
        var caster = ctx.Caster;
        ctx.AfterResolve(() => caster.Cast(followUp));
    }
}

internal sealed class KnockbackEffect(Func<EnemyActionContext, SimCharacter, (float Distance, float Speed)?> push, float? knockbackDelay) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        foreach (var target in ctx.Hits)
        {
            if (ctx.IsKilled(target) || !target.IsAlive() || target is not ISimPartyMember) continue;
            if (push(ctx, target) is { } p)
                ctx.Knockback(target, ctx.Caster.Position, p.Distance, p.Speed, knockbackDelay);
        }
    }
}
