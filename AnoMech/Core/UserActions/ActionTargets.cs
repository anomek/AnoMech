using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.UserActions;

// Who a player action lands on, read from the Action sheet: its single target, or a circle of
// EffectRange around the caster. The sheet does not say which side an action affects
// (Reprisal and Heart of Light differ only in radius), so the caller picks Friendly or Hostile.
internal static class ActionTargets
{
    public static IReadOnlyList<SimCharacter> Friendly(ActionContext ctx)
    {
        if (Plugin.GameInstance is not { } game || !TryGetAction(ctx.ActionId, out var action)) return [];
        var members = game.World.Party.ActiveMembers();
        switch (action.CastType)
        {
            case CastType.SingleTarget:
                var target = members.FirstOrDefault(m => (ulong)m.GameObjectId == ctx.TargetId);
                if (target != null && !ReferenceEquals(target, ctx.Caster) && action.CanTargetParty) return [target];
                return action.CanTargetSelf ? [ctx.Caster] : [];
            case CastType.Circle:
                return members.Where(m => InRange(ctx.Caster, m, action.EffectRange)).ToList();
            default:
                Plugin.Log.Warning($"ActionTargets: action {ctx.ActionId} has unsupported CastType {action.CastType}; applied to the caster only");
                return [ctx.Caster];
        }
    }

    public static IReadOnlyList<SimEnemy> Hostile(ActionContext ctx)
    {
        if (Plugin.GameInstance is not { } game || !TryGetAction(ctx.ActionId, out var action)) return [];
        var enemies = game.World.Children.OfType<SimEnemy>().Where(e => e.IsActive);
        switch (action.CastType)
        {
            case CastType.SingleTarget:
                return enemies.Where(e => (ulong)e.GameObjectId == ctx.TargetId).ToList();
            case CastType.Circle:
                return enemies.Where(e => InRange(ctx.Caster, e, action.EffectRange)).ToList();
            default:
                Plugin.Log.Warning($"ActionTargets: action {ctx.ActionId} has unsupported CastType {action.CastType}; no enemy hit");
                return [];
        }
    }

    private static bool TryGetAction(uint actionId, [NotNullWhen(true)] out ActionRow? action)
        => (action = Natives.Data.Action(actionId)) != null;

    // An AoE reaches a target whose hitbox edge is inside it, not just its centre.
    private static bool InRange(SimCharacter caster, SimCharacter target, float range)
    {
        var dx = target.Position.X - caster.Position.X;
        var dz = target.Position.Z - caster.Position.Z;
        var reach = range + target.HitboxRadius;
        return dx * dx + dz * dz <= reach * reach;
    }
}
