using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.EnemyActions;

// Runs EnemyActions for one caster, an enemy or a party member: the visuals go out as its
// SimCast's packets, everything mechanical goes on the scenario's EventScheduler.
internal sealed class EnemyActionHandler(SimCharacter caster, SimCast cast, SimWorld world)
{
    private string CasterName => caster switch
    {
        SimEnemy enemy => enemy.DisplayName,
        ISimPartyMember member => member.Role.ToString(),
        _ => caster.GetType().Name,
    };

    private EnemyActionCast? casting;
    private EnemyActionCast? releasing;

    // From a cast's start until its ActionEffect: the bar ends ActionEffectOffset before it, and
    // the caster stays put through that gap.
    public bool AwaitingRelease => releasing is not null;

    public void Start(EnemyAction action, CastTarget at, byte animationVariation)
    {
        if (caster.Dead) return;
        var handle = new EnemyActionCast();
        var id = action.ActionId;
        var target = at.Character;
        var location = at.Location;
        var sheetRow = Natives.Data.Action(id);
        var sheetCastTime = sheetRow?.CastSeconds ?? 0f;
        var castTime = action.Cast.CastSecondsOverride ?? MathF.Max(0f, sheetCastTime - CastSpec.ActionEffectOffset);
        // Only a ground-target action goes out without a target; any other action aimed at a point
        // still targets its caster, or the client never applies the effect (no flytext until despawn).
        GameObjectId? castTarget = location is not null && sheetRow?.TargetArea == true ? null : (target ?? caster).GameObjectId;
        DiagnosticLog.Info(
            $"[EnemyAction] Cast: {Natives.Data.ActionName(id)} ({id}) by {CasterName} from ({caster.Position.X:F1},{caster.Position.Z:F1}) castSeconds={castTime:F2}.");

        if (castTime > 0f)
        {
            casting = handle;
            releasing = handle;
            cast.NativeCast(id, ActionType.Action, action.Cast.OmenDelay, castTime, interruptible: false,
                rotation: CastRotation(action, target, location), position: location, targetId: castTarget);
        }

        // Resolve goes first: an instant action runs both inline, and the ActionEffect carries the hits.
        var delivery = new Delivery();
        var resolveAt = castTime + action.Timing.ResolveSnapshotOffset;
        var releaseAt = action.Cast.CastSecondsOverride is { } bar ? bar + CastSpec.ActionEffectOffset : sheetCastTime;
        if (action.Effects.Count > 0)
            Schedule(resolveAt, () =>
            {
                if (handle.IsCancelled) return;
                if (casting == handle) casting = null;
                Resolve(action, target, location, delivery, MathF.Max(0f, releaseAt - resolveAt));
            }, handle);
        Schedule(releaseAt, () =>
        {
            if (releasing == handle) releasing = null;
            if (handle.IsCancelled) return;
            if (casting == handle) casting = null;
            Release(action, target, location, castTarget, animationVariation, delivery);
        }, handle);
    }

    public void CancelCast(CastCancelReason reason)
    {
        if (casting is not { } handle) return;
        casting = null;
        if (releasing == handle) releasing = null;
        handle.Cancelled();
        cast.Cancel(reason);
        foreach (var step in handle.PendingSteps) world.Events.Remove(step);
        handle.PendingSteps.Clear();
    }

    // The server aims a cast on another character at it, whichever way the caster faces.
    private float CastRotation(EnemyAction action, SimCharacter? target, Vector3? location)
    {
        if (location is null && target is not null && target != caster) return caster.Placement().Face(target.Position).Rotation;
        return caster.Rotation + action.Cast.Rotation;
    }

    // Faces a target first: the packet carries the caster's rotation. A ground location doesn't turn
    // the caster, since the game aims some lines from behind it, and an action with Cast.FaceTarget
    // off keeps its facing for any target. Some actions animate only when delivered to a target, but one outside
    // CharacterManager null-derefs ApplyAll.
    private void Release(EnemyAction action, SimCharacter? target, Vector3? location, GameObjectId? castTarget, byte animationVariation, Delivery delivery)
    {
        var id = action.ActionId;
        if (target == caster) target = null;
        var aim = location ?? target?.Position;
        // The client plays the release along the caster's model facing, not the packet's rotation, and
        // a turn reaches the model only on the next flush, so an unseen helper snaps to its target.
        if (action.Cast.FaceTarget && target is not null)
        {
            if (caster is SimEnemy { SpawnConfig.Visibility: SpawnVisibility.InvisibleHelper } helper)
                helper.SetRotation(caster.Placement().Face(target.Position).Rotation);
            else
                caster.Face(target.Position);
        }
        var outcomes = delivery.Outcomes.Where(o => o.Damage is not null || o.Push is not null || o.Dodged || o.StatusGains.Count > 0).ToList();
        var listed = new List<SimCharacter>();
        if (target is not null) listed.Add(target);
        listed.AddRange(outcomes.Select(o => o.Target));
        var targets = new List<ActionEffectTarget>();
        var confirmations = new List<(SimCharacter Target, byte Index)>();
        foreach (var who in listed.Distinct())
        {
            if (!Natives.BattleCharas.IsInCharacterManager(who.GameObjectId.ObjectId)) continue;
            var outcome = outcomes.FirstOrDefault(o => o.Target == who);
            var effects = new List<ActionEffectEntry>();
            if (outcome?.Damage is { } damage)
            {
                effects.Add(ActionEffectEntry.Damage(damage.Amount, damage.Kind.AttackType()));
                confirmations.Add((who, (byte)targets.Count));
            }
            // A push that deals nothing still gets a typeless zero hit, and no EffectResult; an
            // NPC's gets only its Knockback entry.
            else if (outcome?.Push is not null && who is ISimPartyMember)
                effects.Add(ActionEffectEntry.Damage(0, 0));
            else if (outcome?.Dodged == true)
                effects.Add(ActionEffectEntry.Dodge());
            foreach (var (statusId, entryParam) in outcome?.StatusGains.Take(MaxEffectsPerTarget - 3) ?? [])
                effects.Add(ActionEffectEntry.StatusGain(statusId, entryParam));
            if (outcome?.Push?.KnockbackRow is { } row)
                effects.Add(ActionEffectEntry.Knockback(row));
            effects.Add(ActionEffectEntry.ActionMarker(id));
            targets.Add(new ActionEffectTarget(who.GameObjectId, effects));
        }
        delivery.Sequence = cast.NativeActionEffect(
            id, action.Cast.AnimationLock, (ushort)id, animationVariation, ActionType.Action, 0,
            rotation: caster.Rotation + action.Cast.Rotation, position: aim ?? caster.Position, animationTargetId: castTarget, targets: targets);
        delivery.Confirmations = confirmations;
        if (delivery.ImpactDue) ConfirmDamage(delivery);
    }

    private void Resolve(EnemyAction action, SimCharacter? target, Vector3? location, Delivery delivery, float releaseDelay)
    {
        var party = world.Party;
        var origin = target is not null && target != caster && IsDirectional(action)
            ? caster.Placement().Face(target.Position)
            : target?.Placement()
              ?? (location is { } at ? new Placement(at, caster.Rotation) : caster.Placement());
        var ctx = new EnemyActionContext(action, caster, target, origin, party, releaseDelay, world.Events);

        // A party member's own action never hits them.
        IReadOnlyList<SimCharacter> hits = IsSingleTarget(action)
            ? party.ActiveMembers().Where(m => ReferenceEquals(m, target)).ToList()
            : AreaHits(action, origin, origin.Position == caster.Position ? caster.HitboxRadius : 0f, party)
                .Where(h => h != caster).ToList();
        if (action.Area.ExcludeCaster
            && hits.MinBy(h => DistanceXZ(h.Position, caster.Position)) is { } baiter
            && DistanceXZ(baiter.Position, caster.Position) <= ExcludeCasterThreshold)
            hits = hits.Where(h => h != baiter).ToList();
        if (action.Area.AdjustTargets is { } adjust) hits = adjust(ctx, hits);
        ctx.Hits = hits;
        if (!IsSingleTarget(action))
            ctx.NpcHits = NpcAreaHits(action, origin, origin.Position == caster.Position ? caster.HitboxRadius : 0f);
        DiagnosticLog.Info(
            $"[EnemyAction] Resolve: {Natives.Data.ActionName(action.ActionId)} at ({origin.Position.X:F1},{origin.Position.Z:F1}) rot={origin.Rotation:F3} -- {hits.Count} target(s): "
            + string.Join(", ", hits.Select(t => (t as ISimPartyMember)?.Role.ToString() ?? "?")));

        foreach (var effect in action.Effects)
            effect.Apply(ctx, hits);
        world.SimEvents.Raise(new ActionResolved(action, caster, origin, hits, ctx.NpcHits, hits.Where(ctx.IsKilled).ToList(), releaseDelay));

        delivery.Outcomes = ctx.Outcomes;
        // Ahead of the deaths it shares a time with, so the killing hit lands on a living target.
        if (ctx.Outcomes.Any(o => o.Damage is not null))
            Schedule(action.Timing.DamageDelay, () => ConfirmDamage(delivery));
        foreach (var (status, revealAt) in ctx.HiddenStatuses)
            Schedule(revealAt, status.Reveal);
        foreach (var (who, statusId) in ctx.ImpactRemovals)
            Schedule(action.Timing.DamageDelay, () =>
            {
                if (who.IsAlive()) who.RemoveStatus(statusId);
            });
        foreach (var outcome in ctx.Outcomes)
        {
            var who = outcome.Target;
            if (outcome.Push is { } push)
            {
                // A player's client slides itself by the row; an NPC goes as far as the server lets it.
                Schedule(push.Delay ?? action.Timing.DamageDelay, () =>
                {
                    if (push.KnockbackRow is { } row && who is ISimPartyMember) who.Knockback(push.From, row);
                    else if (push.Speed > 0f)
                    {
                        var distance = who is ISimPartyMember ? push.Distance : world.NpcPushDistance(who.Position, push.From, push.Distance);
                        who.Push(push.From, distance, distance / push.Speed);
                    }
                });
            }
            if (outcome.Killed)
                Schedule(action.Timing.DamageDelay, () => who.Die(action.ActionId, Explain(action.DeathExplanation, outcome.Explanation)));
        }
    }

    private const float ExcludeCasterThreshold = 0.01f;

    // The ActionEffect's limit; damage, knockback and the marker take three.
    private const int MaxEffectsPerTarget = 8;

    private static IReadOnlyList<SimCharacter> AreaHits(EnemyAction action, Placement origin, float casterHitboxRadius, SimParty party)
    {
        var query = new AoeQuery(action.ActionId, origin, action.Cast.Rotation + action.Area.Rotation, action.Area.Size, action.Area.CastTypeOverride, casterHitboxRadius);
#if DEBUG
        AnoMech.Windows.DamageDebugWindow.Instance?.Record(query);
#endif
        return query.Run(party.Find);
    }

    // Unlike a player, whose center has to be inside, an NPC is hit anywhere on its hitbox.
    private IReadOnlyList<SimEnemy> NpcAreaHits(EnemyAction action, Placement origin, float casterHitboxRadius)
    {
        var query = new AoeQuery(action.ActionId, origin, action.Cast.Rotation + action.Area.Rotation, action.Area.Size, action.Area.CastTypeOverride, casterHitboxRadius);
        return world.Children.OfType<SimEnemy>()
            .Where(e => e != caster && e.IsAlive() && query.SignedDistance(e.Position) <= e.HitboxRadius)
            .ToList();
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    // No area, only the cast target is hit.
    private static bool IsSingleTarget(EnemyAction action)
        => (action.Area.CastTypeOverride ?? Natives.Data.Action(action.ActionId)?.CastType) is CastType.SingleTarget;

    // Cones and lines.
    private static bool IsDirectional(EnemyAction action)
        => (action.Area.CastTypeOverride ?? Natives.Data.Action(action.ActionId)?.CastType)
            is CastType.Cone2 or CastType.Cone or CastType.Rectangle2 or CastType.Rectangle or CastType.Charge;

    private static string? Explain(string? action, string? hit)
        => action is null ? hit : hit is null ? action : $"{action}; {hit}";

    // The EffectResult that makes the client apply a target's damage entry, flytext included. An
    // impact due before the ActionEffect went out waits for it.
    private static void ConfirmDamage(Delivery delivery)
    {
        delivery.ImpactDue = delivery.Sequence == 0;
        if (delivery.ImpactDue) return;
        foreach (var (who, index) in delivery.Confirmations)
            if (who.Proxy is { Exists: true } chara)
                chara.ReceiveEffectResult(delivery.Sequence, index, chara.Health);
    }

    // Resolve's outcomes, for the ActionEffect; the ActionEffect's ids, for the EffectResults.
    private sealed class Delivery
    {
        public IReadOnlyCollection<EnemyActionContext.TargetOutcome> Outcomes = [];
        public uint Sequence;
        public IReadOnlyList<(SimCharacter Target, byte Index)> Confirmations = [];
        public bool ImpactDue;
    }

    // Anything due now runs inline: an instant action resolves inside the Cast() call, so casts
    // issued in one timeline event see each other's statuses in call order. `owner`: the cast whose
    // cancel takes the step off the queue.
    private void Schedule(float delay, Action run, EnemyActionCast? owner = null)
    {
        if (delay <= 0f) run();
        else
        {
            world.Events.Add(delay, run);
            owner?.PendingSteps.Add(run);
        }
    }
}
