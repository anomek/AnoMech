using System;
using System.Linq;
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
    }
}

// Leaf: remove the first dispellable status (Status sheet CanDispel) from each friendly recipient.
// The status is picked at the press and comes off 1.11s later, as the game's does.
internal sealed class DispelEffect : IActionEffect
{
    private const float Delay = 1.11f;

    public void Apply(ActionContext ctx)
    {
        foreach (var member in ActionTargets.Friendly(ctx))
            Dispel(member);
    }

    public static void Dispel(SimCharacter member)
    {
        if (member.ActiveStatuses.FirstOrDefault(s => Natives.Data.Status(s.StatusId) is { CanDispel: true }) is not { } status) return;
        void Remove() => member.RemoveStatus(status.StatusId, status.SourceObject);
        if (Plugin.Game is { } game) game.Events.Add(Delay, Remove);
        else Remove();
    }
}

// Leaf: grant a status to the action's friendly recipients (see ActionTargets).
internal sealed class TargetStatusEffect(ushort statusId, float duration, int stacks = 0) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        foreach (var member in ActionTargets.Friendly(ctx))
        {
            member.RemoveStatus(statusId);
            member.AddStatusParam(statusId, stacks, duration);
        }
    }
}

// Leaf: debuff the enemies the action hits (Reprisal).
internal sealed class EnemyStatusEffect(ushort statusId, float duration, int stacks = 0) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        foreach (var enemy in ActionTargets.Hostile(ctx))
        {
            enemy.RemoveStatus(statusId);
            enemy.AddStatusParam(statusId, stacks, duration);
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

// Wrapper: apply the inner effects only once the player's level reaches the action they unlock. A
// trait's Ready status granted below it would turn a button into an action the player can't use yet.
internal sealed class UnlocksEffect(uint actionId, IActionEffect[] inner) : IActionEffect
{
    public void Apply(ActionContext ctx)
    {
        if (ctx.CasterIsPlayer && Plugin.ObjectTable.LocalPlayer?.Level < Natives.Data.Action(actionId)?.ClassJobLevel) return;
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
