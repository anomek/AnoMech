using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.EnemyActions;

// Runs EnemyActions for one SimEnemy: the visuals go through SimEnemy.NativeCast /
// NativeActionEffect, everything mechanical goes on the scenario's EventScheduler.
internal sealed class EnemyActionHandler(SimEnemy caster, SimWorld world)
{
    // `target` and `location` are exclusive; neither = centred on the caster.
    public void Start(EnemyAction action, SimCharacter? target, Vector3? location)
    {
        var id = action.ActionId;
        var castTime = action.Cast.CastTime ?? Natives.Data.Action(id)?.CastSeconds ?? 0f;
        GameObjectId? castTarget = location is null ? (target ?? caster).GameObjectId : null;
        DiagnosticLog.Info(
            $"[EnemyAction] Cast: {ActionLookup.Name(id)} ({id}) by {caster.DisplayName} from ({caster.Position.X:F1},{caster.Position.Z:F1}) castSeconds={castTime:F2}.");

        if (castTime > 0f)
            caster.NativeCast(id, ActionType.Action, action.Cast.OmenDelay, castTime, interruptible: false,
                rotation: caster.Rotation + action.Area.Rotation, position: location, targetId: castTarget,
                animationLock: action.Cast.AnimationLock, fireDelay: action.Timing.VfxOffset);

        Schedule(castTime + action.Timing.VfxOffset, () => Release(action, target, location, castTarget));
        if (action.Effects.Count > 0)
            Schedule(castTime + action.Timing.ResolveOffset, () => Resolve(action, target, location));
    }

    // Faces a target first: the packet carries the caster's rotation. A ground location doesn't turn
    // the caster, since the game aims some lines from behind it. Some actions animate only when
    // delivered to a target, but one outside CharacterManager null-derefs ApplyAll.
    private void Release(EnemyAction action, SimCharacter? target, Vector3? location, GameObjectId? castTarget)
    {
        var id = action.ActionId;
        if (target == caster) target = null;
        var aim = location ?? target?.Position;
        caster.Face(target?.Position);
        GameObjectId? deliverTo = target is not null && Natives.BattleCharas.IsInCharacterManager(target.GameObjectId.ObjectId)
            ? target.GameObjectId
            : null;
        caster.NativeActionEffect(
            id, action.Cast.AnimationLock, (ushort)id, action.Cast.Variation, ActionType.Action, 0,
            position: aim ?? caster.Position, animationTargetId: castTarget, actionTargetId: deliverTo);
    }

    private void Resolve(EnemyAction action, SimCharacter? target, Vector3? location)
    {
        var party = world.Party;
        var origin = target is not null && target != caster && IsDirectional(action.ActionId)
            ? caster.Placement().Face(target.Position)
            : target?.Placement()
              ?? (location is { } at ? new Placement(at, caster.Rotation) : caster.Placement());
        var ctx = new EnemyActionContext(action, caster, target, origin, party);

        var query = new AoeQuery(action.ActionId, origin, action.Area.Rotation, action.Area.Size);
#if DEBUG
        AnoMech.Windows.DamageDebugWindow.Instance?.Record(query);
#endif
        var hits = query.Run(party.Find);
        if (action.Area.AdjustTargets is { } adjust) hits = adjust(ctx, hits);
        ctx.Hits = hits;
        DiagnosticLog.Info(
            $"[EnemyAction] Resolve: {ActionLookup.Name(action.ActionId)} at ({origin.Position.X:F1},{origin.Position.Z:F1}) rot={origin.Rotation:F3} -- {hits.Count} target(s): "
            + string.Join(", ", hits.Select(t => (t as ISimPartyMember)?.Role.ToString() ?? "?")));

        foreach (var effect in action.Effects)
            effect.Apply(ctx);

        var name = ActionLookup.Name(action.ActionId);
        foreach (var (who, amount, icon) in ctx.DamageShown)
            Schedule(action.Timing.DamageDelay, () => ShowFlyText(who, amount, icon, name));
        foreach (var (who, explanation) in ctx.Killed)
            Schedule(action.Timing.DeathDelay, () => who.Die(action.ActionId, Explain(action.DeathExplanation, explanation)));
        foreach (var run in ctx.AfterResolveActions)
            run();
    }

    // Cones and lines (InsideActionAoe's CastTypes 3, 4, 8, 12, 13).
    private static bool IsDirectional(uint actionId)
        => Natives.Data.Action(actionId)?.CastType is 3 or 4 or 8 or 12 or 13;

    private static string? Explain(string? action, string? hit)
        => action is null ? hit : hit is null ? action : $"{action}; {hit}";

    // The game shows a player only the damage they take, never a party member's.
    private static void ShowFlyText(SimCharacter who, uint amount, FlyTextIcon icon, string name)
    {
        if (who is SimPlayer && who.IsAlive() && who.Proxy is { Exists: true } chara)
            chara.ShowFlyText(amount, name, (uint)icon);
    }

    // Anything due now runs inline: an instant action resolves inside the Cast() call, so casts
    // issued in one timeline event see each other's statuses in call order.
    private void Schedule(float delay, Action run)
    {
        if (delay <= 0f) run();
        else world.Events.Add(delay, run);
    }
}
