using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.UserActions;

// What a player action does client-side, standing in for the firewalled server response.
// Composable: leaf effects (Gauge / Status) do the work; wrappers (Combo / Random) gate
// them. Authored via the factory helpers in JobActions (Gauge(...), Status(...), etc.).
internal interface IActionEffect
{
    void Apply(ActionContext ctx);
}

// Per-dispatch state an effect may need. The caster is the local player for a real press, or
// a bot for SimPartyNpc.UseAction.
internal sealed class ActionContext(uint actionId, ulong targetId, SimCharacter caster, Random rng)
{
    public ulong TargetId { get; } = targetId;
    public uint ActionId { get; } = actionId;
    public SimCharacter Caster { get; } = caster;
    // The job gauge and combo state are the local player's alone.
    public bool CasterIsPlayer => Caster is SimPlayer;
    public Random Rng { get; } = rng;
}

// Leaf: add an amount to a job gauge (ResourceGauge.Add clamps to [0, Max]).
internal sealed unsafe class GaugeEffect(ResourceGauge gauge, int amount) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (!ctx.CasterIsPlayer) return;
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge != null) gauge.Add(edit.Gauge, amount);
    }
}

// Leaf: set a job gauge to an absolute value, clamped like Add.
internal sealed unsafe class SetGaugeEffect(ResourceGauge gauge, int value) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (!ctx.CasterIsPlayer) return;
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge != null) gauge.Add(edit.Gauge, value - gauge.Read(edit.Gauge));
    }
}

// Leaf: grant a status to the player for `duration` seconds (replaces any existing copy).
// `stacks` sets Status.Param — 0 for a plain buff, or the stack count for a stacking buff
// (Requiescat, Sacred Sight); we don't decrement per-consume yet, it just expires.
internal sealed class StatusEffect(ushort statusId, float duration, int stacks = 0) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        ctx.Caster.RemoveStatus(statusId);
        ctx.Caster.AddStatusParam(statusId, stacks, duration);
        if (ctx.Caster is ISimPartyMember member) HostReport.RoleStatus([member.Role], statusId, duration, stacks);
    }
}

// A peer runs no scenario logic: the host decides who lives, from the statuses on its own copy
// of each character. Only the statuses the host acts on (mitigation, knockback immunity) and
// Sprint, which every other client shows, are sent, with what a scenario reacts to: a provoke,
// an enemy's first hit, a cleanse.
internal static class HostReport
{
    public static bool IsReported(ushort statusId)
        => Mitigation.ByStatusId.ContainsKey(statusId) || KnockbackImmunity.IsImmunity(statusId) || statusId == SprintHandler.SprintStatusId;

    public static void RoleStatus(IReadOnlyList<PartyRole> roles, ushort statusId, float duration, int stacks)
    {
        if (IsReported(statusId))
            Plugin.Multiplayer?.ReportAppliedRoleStatus(roles, statusId, duration, (ushort)stacks);
    }

    // True when a host now owns the cleanse.
    public static bool Dispelled(PartyRole role) => Plugin.Multiplayer?.ReportDispelled(role) ?? false;

    public static void Provoked(IReadOnlyList<SimEnemy> enemies) => Plugin.Multiplayer?.ReportProvokedEnemies(enemies);

    public static void Attacked(SimEnemy enemy) => Plugin.Multiplayer?.ReportAttackedEnemy(enemy);
}

// Leaf: remove the first dispellable status (Status sheet CanDispel) from each friendly recipient.
// The status is picked at the press and comes off 1.11s later, as the game's does. On a peer the
// host's copy, which holds the statuses, does both.
internal sealed class DispelEffect : IActionEffect
{
    private const float Delay = 1.11f;

    public void Apply(ActionContext ctx)
    {
        foreach (var member in ActionTargets.Friendly(ctx))
            if (member is not ISimPartyMember slot || !HostReport.Dispelled(slot.Role))
                Dispel(member);
    }

    public static void Dispel(SimCharacter member)
    {
        if (member.ActiveStatuses.FirstOrDefault(s => Natives.Data.CanDispel(s.StatusId)) is not { } status) return;
        void Remove() => member.RemoveStatus(status.StatusId, status.SourceObject);
        if (Plugin.Game is { } game) game.Events.Add(Delay, Remove);
        else Remove();
    }
}

// Leaf: tell the targeted enemy who provoked it; what that does is the scenario's to decide
// (SimEnemy.Provoked).
internal sealed class ProvokeEffect : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        var enemies = ActionTargets.Hostile(ctx);
        foreach (var enemy in enemies) enemy.Provoke(ctx.Caster);
        HostReport.Provoked(enemies);
    }
}

// Leaf: grant a status to the action's friendly recipients (see ActionTargets).
internal sealed class TargetStatusEffect(ushort statusId, float duration, int stacks = 0) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        var roles = new List<PartyRole>();
        foreach (var member in ActionTargets.Friendly(ctx))
        {
            member.RemoveStatus(statusId);
            member.AddStatusParam(statusId, stacks, duration);
            if (member is ISimPartyMember slot) roles.Add(slot.Role);
        }
        HostReport.RoleStatus(roles, statusId, duration, stacks);
    }
}

// When a staggered status lands: on the caster OnCaster seconds after the action, then on each other
// recipient EachNext seconds after the one before.
public readonly record struct StatusLanding(float OnCaster, float EachNext);

// Leaf: a tank LB3's status, on the friendly recipients at the press unless the zone staggers it
// (IZone.TankLimitBreakLanding).
internal sealed class LimitBreakStatusEffect(ushort statusId, float duration) : IActionEffect
{
    private readonly TargetStatusEffect atThePress = new(statusId, duration);

    public void Apply(ActionContext ctx)
    {
        if (Plugin.Game is not { Zone.TankLimitBreakLanding: { } landing } game)
        {
            atThePress.Apply(ctx);
            return;
        }
        var members = ActionTargets.Friendly(ctx).OrderBy(m => !ReferenceEquals(m, ctx.Caster)).ToList();
        for (var k = 0; k < members.Count; k++)
        {
            var member = members[k];
            game.Events.Add(landing.OnCaster + landing.EachNext * k, () =>
            {
                if (!member.IsAlive()) return;
                member.RemoveStatus(statusId);
                member.AddStatusParam(statusId, 0, duration);
                if (member is ISimPartyMember slot) HostReport.RoleStatus([slot.Role], statusId, duration, 0);
            });
        }
    }
}

// Leaf: debuff the enemies the action hits (Reprisal).
internal sealed class EnemyStatusEffect(ushort statusId, float duration, int stacks = 0) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        var enemies = ActionTargets.Hostile(ctx);
        foreach (var enemy in enemies)
        {
            enemy.RemoveStatus(statusId);
            enemy.AddStatusParam(statusId, stacks, duration);
        }
        Plugin.Multiplayer?.ReportAppliedEnemyStatus(enemies, statusId, duration, (ushort)stacks);
    }
}

// Wrapper: apply the inner effects only when the action lands as a valid combo continuation.
internal sealed class ComboEffect(IActionEffect[] inner) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (!ctx.CasterIsPlayer || !PlayerCombo.IsActiveContinuation(ctx.ActionId)) return;
        foreach (var e in inner) e.Apply(ctx);
    }
}

// Wrapper: apply the inner effects with `chance` probability (procs, e.g. DNC feathers).
internal sealed class RandomEffect(float chance, IActionEffect[] inner) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (ctx.Rng.NextSingle() >= chance) return;
        foreach (var e in inner) e.Apply(ctx);
    }
}
