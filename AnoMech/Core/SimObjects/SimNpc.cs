using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

// SimCharacter backed by a BattleChara we allocated in a CharacterManager slot. Overlay state
// (VFX, statuses) lives on the base; this layer adds movement and the "free the slot on
// Despawn" lifecycle.
public class SimNpc : SimCharacter
{
    private IBattleCharaProxy? proxy;
    private bool pendingDraw;
    private int pendingDrawFrames;

    internal override IBattleCharaProxy? Proxy => proxy;

    // Forget the slot without touching what it holds: a packet spawn whose slot ended up with
    // somebody else's actor.
    protected void DetachSlot()
    {
        proxy = null;
        pendingDraw = false;
    }

    // Re-arm the deferred EnableDraw for an actor created without one (see
    // EnemySpawnConfig.PacketSpawnEnableDraw).
    protected void RequestDraw()
    {
        pendingDraw = proxy != null;
        pendingDrawFrames = 0;
    }

    private protected override Movement Movement => field ??= new Movement(this);

    // pendingDraw=false for an actor the engine's own spawn handler created: it enables the
    // draw itself, as for a real server spawn.
    protected SimNpc(IBattleCharaProxy proxy, Coordinates coordinates, bool pendingDraw = true) : base(coordinates)
    {
        this.proxy = proxy;
        this.pendingDraw = pendingDraw;
    }

    // Some real hits carry the new model state in their effect, on the hit frame; only the
    // ActorControl that follows (0.14-0.8s later) is sent here.
    public void SetModelState(byte value) => proxy?.SetModelState(value);

    // Sampled for peers.
    public byte ModelState => proxy?.ModelState ?? 0;

    // ModelContainer.ModeAttributeFlags (e.g. Omega-M's shield: 0x00 = shield, 0x10 = none)
    // is an INPUT the engine reads only while building the monster model
    // (CharacterSetup.SetupBNpc / Monster::SetupFromData). A bare field write has no visible
    // effect, and nothing lighter re-applies it — writing the per-frame mask
    // (Model.EnabledAttributeIndexMask), replaying the ActorControl 0x31 packet, a
    // SetModelState rebuild, and CharacterBase::SetupSlotModel were all confirmed inert on
    // our doppels. The only thing that works is a full model rebuild, so we write the field
    // and force a redraw. The redraw is visibility-aware (see ReloadModel), so setting flags
    // during an invisible warp window — as the real fight does — doesn't pop the boss into
    // view early.
    public void SetModeAttributeFlags(byte value)
    {
        if (proxy is not { Exists: true } chara) return;
        chara.SetModeAttributeFlags(value);
        ReloadModel();
    }

    // Sampled for peers: a runtime write has no other signal.
    public byte? ModeAttributeFlags => Proxy is { Exists: true } chara ? chara.ModeAttributeFlags : null;

    // Forces a model rebuild via DisableDraw -> EnableDraw so the engine re-reads
    // ModeAttributeFlags and rebuilds the sub-meshes. The re-enable is deferred through the
    // pendingDraw path (the rebuild is async, gated on IsReadyToDraw). Only cycles draw when
    // the model is currently drawn: a hidden NPC keeps the written flags and applies them on
    // its next EnableDraw from the visibility system, so we never force it visible.
    protected void ReloadModel()
    {
        if (proxy is not { HasDrawObject: true } chara) return;
        chara.DisableDraw();
        pendingDraw = true;
    }

    // Plays an action's own animation and VFX on this doppel through the same synthetic
    // ActionEffect a boss cast fires -- what lets a bot tank visibly pop its LB3 in Umad P3
    // Limit Cut. Effects (statuses, damage) stay the caller's job, exactly as for an enemy Cast.
    //
    // Here rather than on SimPartyNpc because the same seat is a SimNetworkPuppet on every peer,
    // which has to play it too; SimEnemy inherits it but drives its own richer cast instead.
    private SimCast? actionCast;

    // Sampled for peers, same edge trigger as SimCast.LastInstantCastSeq.
    public uint PlayedActionId { get; private set; }
    public float PlayedActionAnimationLock { get; private set; }
    public float PlayedActionCastSeconds { get; private set; }
    public float PlayedActionEffectDelay { get; private set; }
    public SimCharacter? PlayedActionTarget { get; private set; }
    public Vector3? PlayedActionLocation { get; private set; }
    public bool PlayedActionHoldsStill { get; private set; }
    public int PlayedActionSeq { get; private set; }

    private (uint ActionId, float AnimationLock, SimCharacter? Target, Vector3? Location)? pendingActionEffect;
    private float pendingActionEffectIn;
    private float heldStillFor;

    public override bool AnimationLock => PlayedActionHoldsStill && (pendingActionEffect != null || heldStillFor > 0f);

    // castSeconds > 0 puts the bar up first. A player's cast resolves before its bar fills, so
    // the effect lands effectDelay into it and the bar goes with it. A ground-targeted action goes
    // to `location` instead of a target. holdStill roots the user through the bar and the
    // animation lock, as a limit break does; a move ordered meanwhile waits for it.
    public void PlayAction(uint actionId, float animationLock = 0.6f, float castSeconds = 0f, float effectDelay = 0f, SimCharacter? target = null,
        Vector3? location = null, bool holdStill = false)
    {
        PlayedActionId = actionId;
        PlayedActionAnimationLock = animationLock;
        PlayedActionCastSeconds = castSeconds;
        PlayedActionEffectDelay = effectDelay;
        PlayedActionTarget = target;
        PlayedActionLocation = location;
        PlayedActionHoldsStill = holdStill;
        PlayedActionSeq++;
        heldStillFor = 0f;
        actionCast ??= new SimCast(this, Coordinates);
        if (holdStill) PauseMoveAnimationForAction();
        if (location is { } at) Face(at);
        else if (target != null) Face(target);
        if (castSeconds <= 0f)
        {
            FireAction(actionId, animationLock, target, location);
            return;
        }
        actionCast.NativeCast(actionId, ActionType.Action, 0f, castSeconds, false, position: location,
            targetId: location != null ? null : TargetOrSelf(target));
        pendingActionEffect = (actionId, animationLock, target, location);
        pendingActionEffectIn = effectDelay > 0f ? MathF.Min(effectDelay, castSeconds) : castSeconds;
    }

    private protected virtual void PauseMoveAnimationForAction() => PauseMoveAnimation();

    private void FireAction(uint actionId, float animationLock, SimCharacter? target, Vector3? location)
    {
        if (PlayedActionHoldsStill) heldStillFor = animationLock;
        if (location is { } at)
        {
            actionCast!.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0, position: at);
            return;
        }
        var targetId = TargetOrSelf(target);
        actionCast!.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0,
            position: target != null && targetId.ObjectId != GameObjectId.ObjectId ? target.Position : null,
            animationTargetId: targetId, actionTargetId: targetId);
    }

    // A target that left the world can't take the effect: ActionEffectHandler dereferences it.
    private GameObjectId TargetOrSelf(SimCharacter? target)
    {
        return target is { IsActive: true, Proxy.Exists: true } ? target.GameObjectId : GameObjectId;
    }

    // Death takes the cast with it, bar and all.
    protected void AbortPlayedAction()
    {
        heldStillFor = 0f;
        if (pendingActionEffect == null) return;
        pendingActionEffect = null;
        Proxy?.ClearCast();
    }

    private void TickPendingAction(float deltaSeconds)
    {
        if (heldStillFor > 0f) heldStillFor = MathF.Max(0f, heldStillFor - deltaSeconds);
        if (pendingActionEffect is not { } pending) return;
        pendingActionEffectIn -= deltaSeconds;
        if (pendingActionEffectIn > 0f) return;
        pendingActionEffect = null;
        FireAction(pending.ActionId, pending.AnimationLock, pending.Target, pending.Location);
        Proxy?.ClearCast();
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        TickPendingAction(deltaSeconds);

        if (pendingDraw)
        {
            if (proxy is not { Exists: true } chara)
            {
                pendingDraw = false;
            }
            else if (chara.IsReadyToDraw)
            {
                chara.EnableDraw();
                pendingDraw = false;
                DiagnosticLog.Info($"[SimNpc] EnableDraw fired for goid 0x{chara.GameObjectId.ObjectId:X} at pos {Position}.");
            }
            else
            {
                pendingDrawFrames++;
                // Once, well past a normal model load, for an IsReadyToDraw stuck false.
                if (pendingDrawFrames == 300)
                    DiagnosticLog.Warn($"[SimNpc] still pendingDraw after {pendingDrawFrames} ticks, goid 0x{chara.GameObjectId.ObjectId:X} -- IsReadyToDraw() never returned true.");
            }
        }
    }

    public override void Despawn()
    {
        if (proxy == null) return;
        base.Despawn();
        proxy.Despawn();
        proxy = null;
        pendingDraw = false;
    }
}
