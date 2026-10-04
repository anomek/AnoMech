using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

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
        var jgm = JobGaugeManager.Instance();
        if (jgm != null) gauge.Add(jgm, amount);
    }
}

// Leaf: set a job gauge to an absolute value, clamped like Add.
internal sealed unsafe class SetGaugeEffect(ResourceGauge gauge, int value) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (!ctx.CasterIsPlayer) return;
        var jgm = JobGaugeManager.Instance();
        if (jgm != null) gauge.Add(jgm, value - gauge.Read(jgm));
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
        if (ctx.Caster is ISimPartyMember member) HostReport.RoleStatus([member.Role], statusId, duration);
    }
}

// A peer runs no scenario logic: the host decides who lives, from the statuses on its own copy
// of each character. Only mitigation is sent, the one kind of status the host acts on.
internal static class HostReport
{
    public static void RoleStatus(IReadOnlyList<PartyRole> roles, ushort statusId, float duration)
    {
        if (Mitigation.ByStatusId.ContainsKey(statusId))
            Plugin.MultiplayerInstance?.ReportAppliedRoleStatus(roles, statusId, duration);
    }

    // The host's copy would otherwise keep counting what a dispel took off.
    public static void OwnStatusesCleared(List<ushort> statusIds) => Plugin.MultiplayerInstance?.ReportClearedOwnStatuses(statusIds);
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
        HostReport.RoleStatus(roles, statusId, duration);
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
        Plugin.MultiplayerInstance?.ReportAppliedEnemyStatus(enemies, statusId, duration);
    }
}

// Leaf: a tank LB3's party-wide status.
internal sealed class TankLimitBreakEffect(ushort statusId, float duration) : IActionEffect
{
    private const float FirstLands = 1.34f;
    private const float NextLands = 0.1335f;

    public void Apply(ActionContext ctx)
    {
        if (Plugin.GameInstance is not { } game) return;
        var order = ActionTargets.Friendly(ctx)
            .OrderBy(member => ReferenceEquals(member, ctx.Caster) ? 0 : 1)
            .ThenBy(_ => ctx.Rng.Next())
            .ToList();
        for (var k = 0; k < order.Count; k++)
        {
            var member = order[k];
            game.World.Events.Add(FirstLands + NextLands * k, () =>
            {
                if (!member.IsAlive()) return;
                member.RemoveStatus(statusId);
                member.AddStatusParam(statusId, 0, duration);
                if (member is ISimPartyMember slot) HostReport.RoleStatus([slot.Role], statusId, duration);
            });
        }
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
