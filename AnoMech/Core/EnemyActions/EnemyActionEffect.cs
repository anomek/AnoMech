using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.EnemyActions;

// Applied to `hits`: those the area caught (EnemyActionContext.Hits), narrowed by any filter
// wrapping the effect. Effects run in list order: one listed after a Damage sees who that damage killed.
// This model is very rough approximation on what could be happening on server,
// and is candidate for rework should we want to extend this further.
public interface IEnemyActionEffect
{
    void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits);
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

    // Survivors only. delayToEffectResult as EnemyActionContext.AddStatus: retail does this for some
    // statuses (Damage Down, Sleep, Confused, HP Penalty, long resistance downs) and not others
    // (vulns, Ruin, Doom, stuns, short resistance downs). It's chosen per status within an action;
    // neither the packet nor any sheet column tells them apart, so read it off a replay.
    public static IEnemyActionEffect ApplyStatus(ushort statusId, float duration, bool delayToEffectResult = false, byte entryParam = 0)
        => new ApplyStatusEffect(statusId, duration, delayToEffectResult, entryParam);

    // Survivors only. atImpact: taken off with the EffectResult at TimingSpec.DamageDelay instead of
    // with the ActionEffect; read which off a replay.
    public static IEnemyActionEffect RemoveStatus(ushort statusId, bool atImpact = false) => new RemoveStatusEffect(statusId, atImpact);

    // One more stack of the family's `times` status, or the death it completes. Survivors only.
    public static IEnemyActionEffect ApplyRuin(RuinSpec ruin, int times, float duration)
        => new ApplyRuinEffect(ruin, ruin.StatusId(times), times, duration);

    // Away from the caster, by a Knockback sheet row. Survivors only.
    // `knockbackDelay` is from resolve and used only for bots and NPCs; null = TimingSpec.DamageDelay.
    public static IEnemyActionEffect Knockback(uint knockbackId, float? knockbackDelay = null)
        => new KnockbackEffect((_, _) => Natives.Data.Knockback(knockbackId) is { } kb ? (kb.Distance, kb.Speed) : null, knockbackDelay, knockbackId);

    // A scripted push away from the caster, which no Knockback row describes. Survivors only.
    // `pushDelay` as Knockback's.
    public static IEnemyActionEffect Push(float distance, float speed, float? pushDelay = null)
        => new KnockbackEffect((_, _) => (distance, speed), pushDelay);

    // A scripted push away from the caster, as far as `distance` says for each target (read at
    // resolve; null = not pushed). Survivors only.
    public static IEnemyActionEffect Push(Func<EnemyActionContext, SimCharacter, float?> distance, float speed, float? pushDelay = null)
        => new KnockbackEffect((ctx, target) => distance(ctx, target) is { } d ? (d, speed) : null, pushDelay);

    // Kills by facing alone: with `lookAway`, whoever has the origin in their front 90° arc; without,
    // whoever has it in their back 90° arc.
    public static IEnemyActionEffect Gaze(bool lookAway) => new GazeEffect(lookAway);

    // The caster dies by this action (a self-destruct). Its death animation waits for
    // CastSpec.AnimationLock, so set that from a replay.
    public static IEnemyActionEffect DefeatCaster() => new DefeatCasterEffect();

    // `effect` applied to the cast's target alone, when the area caught it.
    public static IEnemyActionEffect OnTarget(params IEnemyActionEffect[] effect)
        => new FilteredEffect(effect, (ctx, hits) => hits.Where(t => ReferenceEquals(t, ctx.Target)));

    // `effect` applied to everyone hit but the cast's target.
    public static IEnemyActionEffect OnOthers(params IEnemyActionEffect[] effect)
        => new FilteredEffect(effect, (ctx, hits) => hits.Where(t => !ReferenceEquals(t, ctx.Target)));

    // `effect` applied to the one hit nearest the action's origin: a line stack's front bait.
    public static IEnemyActionEffect OnNearest(params IEnemyActionEffect[] effect)
        => new FilteredEffect(effect, (ctx, hits) => hits.OrderBy(t => Vector3.DistanceSquared(t.Position, ctx.Origin.Position)).Take(1));

    // `effect` applied to those hit who carry `statusId` when it runs.
    public static IEnemyActionEffect OnHavingStatus(ushort statusId, params IEnemyActionEffect[] effect)
        => new FilteredEffect(effect, (_, hits) => hits.Where(t => t.HasStatus(statusId)));

    // `effect` applied to the NPCs of `bnpcBaseId` the area caught instead of the party hits (UWU's
    // Razor Plumes, pushed by Super Cyclone).
    public static IEnemyActionEffect OnNpcs(uint bnpcBaseId, params IEnemyActionEffect[] effect)
        => new FilteredEffect(effect, (ctx, _) => ctx.NpcHits.Where(npc => npc.BNpcBaseId == bnpcBaseId));

    // `effect` applied to the front `count` hit: nearest the origin, or furthest up a line (the
    // front of a wild charge).
    public static IEnemyActionEffect OnFront(int count, params IEnemyActionEffect[] effect)
        => new FilteredEffect(effect, (_, hits) => hits.Take(count));

    // `effect` applied to everyone hit behind the front `count`.
    public static IEnemyActionEffect OnBehindFront(int count, params IEnemyActionEffect[] effect)
        => new FilteredEffect(effect, (_, hits) => hits.Skip(count));
}

internal sealed class DefeatCasterEffect : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        if (ctx.Caster is SimEnemy enemy) enemy.DefeatByOwnAction(ctx.Action);
    }
}

internal sealed class FilteredEffect(IEnemyActionEffect[] effect, Func<EnemyActionContext, IReadOnlyList<SimCharacter>, IEnumerable<SimCharacter>> select) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        foreach (var e in effect)
            e.Apply(ctx, select(ctx, hits).ToList());
    }
}

// A hit on someone an earlier hit already kills still shows its own number.
internal sealed class DamageEffect(VulnSpec vulns, Severity severity, Distribution split, DamageKind? kindOverride) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        var kind = kindOverride ?? DamageKinds.FromAttackType(Natives.Data.Action(ctx.Action.ActionId)?.AttackType ?? 0);
        for (var i = 0; i < hits.Count; i++)
        {
            var target = hits[i];
            var (hit, reason) = split.Assign(ctx, hits, i, target);
            var hitSeverity = hit?.Severity ?? severity;
            var cause = DamageCheck.LethalCause(target, hit?.Vulns ?? vulns, hitSeverity, kind, ctx);
            ctx.ShowDamage(target, hitSeverity.FlyTextAmount(kills: cause != null), kind);
            if (cause != null) ctx.Kill(target, Explain(reason, cause));
        }
    }

    // `cause` "" kills with no explanation of its own.
    private static string? Explain(string? reason, string cause)
        => cause.Length == 0 ? reason : reason is null ? cause : $"{reason}; {cause}";
}

internal sealed class ApplyStatusEffect(ushort statusId, float duration, bool delayToEffectResult, byte entryParam) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        foreach (var target in hits)
            if (!ctx.IsKilled(target) && target.IsAlive())
                ctx.AddStatus(target, statusId, duration, delayToEffectResult: delayToEffectResult, entryParam: entryParam);
    }
}

internal sealed class RemoveStatusEffect(ushort statusId, bool atImpact) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        foreach (var target in hits)
        {
            if (ctx.IsKilled(target) || !target.IsAlive()) continue;
            if (atImpact) ctx.RemoveStatusAtImpact(target, statusId);
            else target.RemoveStatus(statusId);
        }
    }
}

internal sealed class ApplyRuinEffect(RuinSpec ruin, ushort statusId, int times, float duration) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        foreach (var target in hits)
        {
            if (ctx.IsKilled(target) || !target.IsAlive()) continue;
            if (ruin.Overloads(target, times))
                ctx.Kill(target, $"{Natives.Data.StatusName(statusId)} overload");
            else
                ctx.AddStatus(target, statusId, duration);
        }
    }
}

internal sealed class GazeEffect(bool lookAway) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        var from = ctx.Origin.Position;
        var kind = DamageKinds.FromAttackType(Natives.Data.Action(ctx.Action.ActionId)?.AttackType ?? 0);
        foreach (var target in hits)
        {
            var facing = target.Placement();
            if (lookAway ? facing.IsLookingAt(from) : facing.IsLookingAwayFrom(from))
            {
                ctx.ShowDamage(target, Severity.Lethal.FlyTextAmount(kills: true), kind);
                ctx.Kill(target, lookAway ? "looked at the gaze" : "faced away from the gaze");
            }
            else
                ctx.Dodge(target);
        }
    }
}

internal sealed class KnockbackEffect(Func<EnemyActionContext, SimCharacter, (float Distance, float Speed)?> push, float? knockbackDelay, uint? knockbackRow = null) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        foreach (var target in hits)
        {
            if (ctx.IsKilled(target) || !target.IsAlive()) continue;
            if (push(ctx, target) is { } p)
                ctx.Knockback(target, p.Distance, p.Speed, knockbackDelay, knockbackRow);
        }
    }
}
