using System;
using System.Linq;
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
    public static IEnemyActionEffect Damage(DamageSpec spec, Severity? severity = null, Distribution? split = null)
        => new DamageEffect(spec, severity ?? Severity.Normal, split ?? Distribution.Each);

    // Survivors only.
    public static IEnemyActionEffect ApplyStatus(ushort statusId, float duration) => new ApplyStatusEffect(statusId, duration);

    // Survivors only.
    public static IEnemyActionEffect RemoveStatus(ushort statusId) => new RemoveStatusEffect(statusId);

    // One more stack of the family's `times` status, or the death (or the family's Doom) it
    // completes. Survivors only.
    public static IEnemyActionEffect ApplyRuin(RuinSpec ruin, int times, float duration)
        => new ApplyRuinEffect(ruin, ruin.StatusId(times), times, duration);

    // ApplyStatus, except whoever already holds `maxStacks` dies instead. Survivors only.
    public static IEnemyActionEffect ApplyStatusOrOverload(ushort statusId, int maxStacks, float duration = 0f)
        => new ApplyStatusOrOverloadEffect(statusId, maxStacks, duration);

    // `followUp` cast by the same caster once this resolve's deaths are dealt, when `when` holds.
    public static IEnemyActionEffect FollowUp(EnemyAction followUp, Func<EnemyActionContext, bool> when)
        => new FollowUpEffect(followUp, when);

    // Away from the caster, by a Knockback sheet row. Survivors only.
    public static IEnemyActionEffect Knockback(uint knockbackId) => new KnockbackEffect(knockbackId);

    // `effect` applied to the cast's target alone, when the area caught it.
    public static IEnemyActionEffect OnTarget(IEnemyActionEffect effect) => new FilteredEffect(effect, castTarget: true);

    // `effect` applied to everyone hit but the cast's target.
    public static IEnemyActionEffect OnOthers(IEnemyActionEffect effect) => new FilteredEffect(effect, castTarget: false);
}

internal sealed class FilteredEffect(IEnemyActionEffect effect, bool castTarget) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        var hits = ctx.Hits;
        ctx.Hits = hits.Where(t => ReferenceEquals(t, ctx.Target) == castTarget).ToList();
        try { effect.Apply(ctx); }
        finally { ctx.Hits = hits; }
    }
}

// A hit on someone an earlier hit already kills still shows its own number.
internal sealed class DamageEffect(DamageSpec spec, Severity severity, Distribution split) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        for (var i = 0; i < ctx.Hits.Count; i++)
        {
            var target = ctx.Hits[i];
            var (hit, reason) = split.Assign(ctx, i, target);
            var hitSpec = hit?.Spec ?? spec;
            var hitSeverity = hit?.Severity ?? severity;
            var cause = DamageCheck.LethalCause(target, hitSpec, hitSeverity, ctx.Party);
            ctx.ShowDamage(target, hitSeverity.FlyTextAmount(kills: cause != null), hitSpec.Icon);
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
            if (!ruin.Land(target, times, duration, ctx.Action.ActionId))
                ctx.Kill(target, $"{StatusLookup.Name(statusId)} overload");
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

internal sealed class FollowUpEffect(EnemyAction followUp, Func<EnemyActionContext, bool> when) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        if (!when(ctx)) return;
        var caster = ctx.Caster;
        ctx.AfterResolve(() => caster.Cast(followUp));
    }
}

internal sealed class KnockbackEffect(uint knockbackId) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx)
    {
        if (!KnockbackLookup.TryGet(knockbackId, out var distance, out var speed)) return;
        foreach (var target in ctx.Hits)
        {
            if (ctx.IsKilled(target) || !target.IsAlive() || target is not ISimPartyMember member) continue;
            member.Knockback(ctx.Caster.Position, distance, speed);
            ctx.ShowDamage(target, 0, FlyTextIcon.Unique);
        }
    }
}
