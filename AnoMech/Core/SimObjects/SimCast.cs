using AnoMech.Core.Game;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Numerics;

namespace AnoMech.Core.SimObjects;

// Drives a single simulated cast on a SimCharacter's BattleChara. Writing
// CastInfo and ticking CurrentCastTime ourselves (instead of calling
// Character::StartCast) is what lets the simulator replay arbitrary boss
// abilities; on completion SimCast fires a synthetic ActionEffectHandler.Receive
// with a server-shaped header so the release animation/VFX play. It owns the
// post-release animation lock that roots the caster.
//
// One SimCast per caster, constructed once and reused. Start() begins a cast (or
// fires instantly when the cast time resolves to <= 0). The
// owning SimEnemy reads IsCasting/Progress/ActionId for the cast-bar HUD and
// IsBusy to decide when to root a following boss. All target coordinates handled
// here are world-space — the SimEnemy adapter converts from scenario-local.
public sealed unsafe class SimCast : ISimObject
{
    private readonly SimCharacter parent;
    private readonly Coordinates coordinates;

    private bool casting;
    private float elapsed;
    private float total;
    private float fireDelay;
    private float fireDelayElapsed;
    private Vector3? targetLocation;   // scenario-local coords
    private GameObjectId? targetId;
    private byte animationVariation;

    private float animationLock;
    private float remainingAnimationLock;

    public bool IsCasting => casting;
    public uint ActionId { get; private set; }
    public float Progress => total <= 0f ? 0f : Math.Clamp(elapsed / total, 0f, 1f);

    // True while the cast bar is up or the release animation is still playing. A
    // following boss roots itself while busy so the action animation finishes in
    // place instead of sliding.
    public bool IsBusy => casting || remainingAnimationLock > 0f;

    // SimCast is a persistent subsystem of its caster: the owning SimEnemy holds it
    // as a direct field and ticks/despawns it explicitly, never reaping it by
    // liveness. Always active while it exists.
    public bool IsActive => true;

    internal SimCast(SimCharacter parent, Coordinates coordinates)
    {
        this.parent = parent;
        this.coordinates = coordinates;
    }

    // Begins a cast of `actionId`. When castSeconds resolves to <= 0 (passed
    // explicitly or read as Cast100ms=0 from the sheet) the action fires
    // immediately with no cast bar. localTargetLocation (scenario-local, converted
    // to world only where native fields demand it) drives the AOE landing point and
    // the pre-fire facing snap; targetId, if set, makes the packet carry NumTargets=1
    // (some actions only animate on the caster when an entity target is delivered).
    // omenRotate is an offset added to the caster's facing (0 = aligned with parent.Rotation).
    public bool Start(uint actionId, Vector3? localTargetLocation, float? castTime, GameObjectId? targetId, float omenDelay, float omenRotate, byte animationVariation, float animationLock, float? fireDelay = null)
    {
        var chara = parent.BattleCharaPtr;
        if (chara == null) return false;


        if (castTime == null)
        {
            var actionSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();

            if (!actionSheet.TryGetRow(actionId, out var action))
            {
                Plugin.Log.Warning($"[SimCast.Start] Action Row {actionId} not found");
                return false;
            }

            castTime = action.Cast100ms / 10f;
        }

        this.animationLock = animationLock;
        var castTimeValue = castTime.Value;


        var target = targetId ?? chara->GetGameObjectId();
        NativeCast(actionId, ActionType.Action, omenDelay, castTimeValue, false, parent.Rotation + omenRotate, localTargetLocation, target);

        elapsed = 0f;
        total = chara->CastInfo.TotalCastTime;

        casting = true;
        targetLocation = localTargetLocation;
        this.targetId = targetId;
        this.animationVariation = animationVariation;
        ActionId = actionId;
        this.fireDelay = fireDelay ?? 0;
        
        if (castTimeValue <= 0)
        {
            FaceTarget(chara);
            remainingAnimationLock = animationLock;
            FireActionEffect(chara, actionId, targetLocation, targetId, animationVariation, animationLock);
            ResetCastState();
        }
        
        return true;
    }

    public void NativeCast(uint actionId, ActionType actionType, float omenDelay, float castTime, bool interruptible, float? rotation = null, Vector3? position = null, GameObjectId? targetId = null, GameObjectId? ballistaId = null)
    {
        var omenDelayByte = (byte)(omenDelay * 10);

        var rot = rotation ?? parent.Rotation;
        var qRotation = MathUtil.QuantizeRotation(rot);

        var animationTargetId = targetId == null ? 0xE0000000 : targetId.Value.ObjectId;
        var ballistaTargetId = ballistaId == null ? 0xE0000000 : ballistaId.Value.ObjectId;

        var localPos = position ?? parent.Position;
        var globalPos = coordinates.ToGlobal(localPos);

        var qPosX = MathUtil.QuantizePosition(globalPos.X);
        var qPosY = MathUtil.QuantizePosition(globalPos.Y);
        var qPosZ = MathUtil.QuantizePosition(globalPos.Z);

        var actorCastPacket = new ActorCastPacket
        {
            ActionId = (ushort)actionId,
            ActionType = (byte)actionType,
            OmenDelay = omenDelayByte,
            ActionId_2 = actionId,
            CastTime = castTime,
            TargetEntityId = animationTargetId,
            RotationInt = qRotation,
            Interruptible = interruptible,
            BallistaEntityId = ballistaTargetId,
            PositionX = qPosX,
            PositionY = qPosY,
            PositionZ = qPosZ,
        };

        PacketDispatcherPointers.HandleActorCastPacket(parent.GameObjectId.ObjectId, &actorCastPacket);
    }

    public void NativeActionEffect(uint actionId, float animationLock, ushort spellId, byte animationVariaton, ActionType actionType, byte flags, float? rotation = null, Vector3? position = null, GameObjectId? animationTargetId = null, GameObjectId? actionTargetId = null, GameObjectId? ballistaId = null)
    {
        const uint NullObjectId = 0xE0000000;

        var chara = parent.BattleCharaPtr;

        if (chara == null)
        {
            return;
        }

        var nullActionTarget = actionTargetId == null;

        var animationTarget = animationTargetId == null ? new GameObjectId { ObjectId = NullObjectId, Type = 0 } : animationTargetId!.Value;
        var actionTarget = nullActionTarget ? new GameObjectId { ObjectId = NullObjectId, Type = 0 } : actionTargetId!.Value;
        var ballistaTarget = ballistaId == null ? NullObjectId : ballistaId.Value.ObjectId;

        var rot = rotation ?? parent.Rotation;
        var qRotation = MathUtil.QuantizeRotation(rot);

        var localPos = position ?? Vector3.Zero;
        var globalPos = coordinates.ToGlobal(localPos);

        var header = new ActionEffectHandler.Header
        {
            AnimationTargetId = animationTarget,
            ActionId = actionId,
            GlobalSequence = 0,
            AnimationLock = animationLock,
            BallistaEntityId = ballistaTarget,
            SourceSequence = 0,
            RotationInt = qRotation,
            SpellId = spellId,
            AnimationVariation = animationVariaton,
            ActionType = (byte)actionType,
            Flags = flags,
            NumTargets = (byte)(nullActionTarget ? 0 : 1)
        };

        var targetEffects = new ActionEffectHandler.TargetEffects();

        ActionEffectHandler.Receive(
            parent.GameObjectId.ObjectId,
            (Character*)chara,
            &globalPos,
            &header,
            &targetEffects,
            &actionTarget
            );
    }

    public void Tick(float deltaSeconds)
    {
        if (remainingAnimationLock > 0f)
        {
            remainingAnimationLock = MathF.Max(0f, remainingAnimationLock - deltaSeconds);
        }

        if (!casting)
        {
            return;
        }

        var chara = parent.BattleCharaPtr;
        if (chara == null)
        {
            casting = false;
            return;
        }

        var castInfo = chara->CastInfo;
        elapsed = castInfo.CurrentCastTime;

        if (elapsed >= total)
        {
            var fire = true;

            if (fireDelay > 0)
            {
                fireDelayElapsed += deltaSeconds;

                if (fireDelayElapsed < fireDelay)
                {
                    fire = false;
                }
            }

            if (fire)
            {
                FaceTarget(chara);
                remainingAnimationLock = animationLock;
                FireActionEffect(chara, ActionId, targetLocation, targetId, animationVariation, animationLock);
                ResetCastState();
            }
        }
    }

    // Teardown for caster despawn: drop the telegraph, stop any pending delayed
    // spawn, and clear CastInfo. Clearing CastInfo matters because despawn deletes
    // the BattleChara via DeleteObjectByIndex -> Character::Terminate, whose
    // scheduler teardown reads a still-live cast/action timeline and crashes on
    // freed state (C0000005 at TimelineGroup.PlayAction; see crash dump
    // 20260529_193455).
    public void Despawn()
    {
        var chara = parent.BattleCharaPtr;
        if (chara != null)
        {
            chara->CastInfo.IsCasting = false;
            chara->CastInfo.ActionId = 0;
            chara->CastInfo.ActionType = 0;
        }
        casting = false;
    }

    private void ResetCastState()
    {
        casting = false;
        targetLocation = null;
        targetId = null;
        animationVariation = 0;
        ActionId = 0;
        fireDelay = 0;
        fireDelayElapsed = 0;
    }

    // targetLocation is stored scenario-local; lift to world only for native
    // ActionEffect delivery (Receive / CastInfo expect world coords).
    private Vector3? WorldTargetLocation => targetLocation is { } loc ? coordinates.ToGlobal(loc) : null;

    // Targeted casts (ground location now; entity targets later) snap to face the target
    // on the final tick so the release animation plays in the intended direction even if
    // the target moved during the cast. FireActionEffect snapshots Rotation into the
    // packet header, so this must run first.
    private void FaceTarget(BattleChara* chara)
    {
        if (WorldTargetLocation is not { } loc) return;
        var dx = loc.X - chara->Position.X;
        var dz = loc.Z - chara->Position.Z;
        if (dx * dx + dz * dz < 1e-6f) return;
        chara->Rotation = MathUtil.NormalizeRotation(MathF.Atan2(dx, dz));
    }

    // Mimics the server's ActionEffect packet so the game plays the action's release
    // animation/VFX on the caster. When deliverTo is set, the packet carries
    // NumTargets=1 with that GameObjectId and a zeroed no-op effect block; some
    // actions only animate on the caster if the engine sees at least one target to
    // deliver to. When deliverTo is null, NumTargets=0 (used for self-targeted
    // casts and cast releases without an entity target) — the release animation
    // still plays.
    private void FireActionEffect(BattleChara* chara, uint actionId, Vector3? localTargetLocation = null, GameObjectId? deliverTo = null, byte animationVariation = 0, float animationLock = 0f)
    {
        if (deliverTo is { } id)
        {
            var characterManager = CharacterManager.Instance();
            var deliverToId = id.ObjectId;

            if (characterManager == null || characterManager->LookupBattleCharaByEntityId(deliverToId) == null)
            {
                Plugin.Log.Warning(
                    $"FireActionEffect: target {deliverToId:X} for action {actionId:X} on caster {chara->EntityId:X} not in CharacterManager._battleCharas; dropping deliverTo to avoid ApplyAll null-deref");
                deliverTo = null;
            }
        }

        var pos = localTargetLocation ?? parent.Position;
        NativeActionEffect(actionId, animationLock, (ushort)actionId, animationVariation, (ActionType)chara->CastInfo.ActionType, 0, chara->Rotation, pos, deliverTo, deliverTo);
    }
}
