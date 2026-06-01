using System;
using System.Numerics;
using AnoMech.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

// Drives a single simulated cast on a SimCharacter's BattleChara. Writing
// CastInfo and ticking CurrentCastTime ourselves (instead of calling
// Character::StartCast) is what lets the simulator replay arbitrary boss
// abilities; on completion SimCast fires a synthetic ActionEffectHandler.Receive
// with a server-shaped header so the release animation/VFX play. It owns the
// cast's SimOmen telegraph and the post-release animation lock that roots the
// caster.
//
// One SimCast per caster, constructed once and reused. Start() begins a cast (or
// fires instantly when the cast time resolves to <= 0); Tick() advances it. The
// owning SimEnemy reads IsCasting/Progress/ActionId for the cast-bar HUD and
// IsBusy to decide when to root a following boss. All target coordinates handled
// here are world-space — the SimEnemy adapter converts from scenario-local.
public sealed unsafe class SimCast : ISimObject
{
    private readonly SimCharacter parent;
    private readonly Coordinates coordinates;
    private SimOmen? omen;

    private bool casting;
    private float elapsed;
    private float total;
    private Vector3? targetLocation;   // scenario-local coords
    private GameObjectId? targetId;
    private byte animationVariation;

    private bool omenScheduled;
    private float omenDelay;
    private float omenRotate;
    private uint omenActionId;

    // Engine doesn't expose post-action animation-lock duration via EXD — the
    // real value only ships in the server's ActionEffect packet. 0.6s is a
    // reasonable approximation for most boss abilities; if a scenario needs
    // tighter timing we can derive per-action values from captured ACT logs.
    private const float DefaultAnimationLockSeconds = 0.6f;
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
    public bool Start(uint actionId, Vector3? localTargetLocation, float? castSeconds, GameObjectId? targetId, float omenDelay, float omenRotate, byte animationVariation)
    {
        var chara = parent.BattleCharaPtr;
        if (chara == null) return false;

        Lumina.Excel.Sheets.Action action;
        if (castSeconds is null)
        {
            var actionSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (!actionSheet.TryGetRow(actionId, out action))
            {
                Plugin.Log.Warning($"Action row {actionId} not found");
                return false;
            }
            castSeconds = action.Cast100ms / 10f;
        }

        var seconds = castSeconds.Value;
        chara->CastInfo.ActionType = 1; // ActionType.Action
        chara->CastInfo.ActionId = actionId;
        if (targetId is { } tid)
            chara->CastInfo.TargetId = tid;
        else
            chara->CastInfo.TargetId = chara->GetGameObjectId();
        if (localTargetLocation is { } loc) chara->CastInfo.TargetLocation = coordinates.ToGlobal(loc);

        targetLocation = localTargetLocation;
        this.targetId = targetId;
        this.animationVariation = animationVariation;
        ActionId = actionId;
        if (seconds <= 0f)
        {
            FaceTarget(chara);
            FireActionEffect(chara, WorldTargetLocation, targetId, animationVariation);
            remainingAnimationLock = DefaultAnimationLockSeconds;
            ResetCastState();
            return true;
        }

        chara->CastInfo.IsCasting = true;
        chara->CastInfo.Interruptible = false;
        chara->CastInfo.CurrentCastTime = 0f;
        chara->CastInfo.BaseCastTime = seconds;
        chara->CastInfo.TotalCastTime = seconds;

        casting = true;
        elapsed = 0f;
        total = seconds;
        omenScheduled = false;
        this.omenDelay = MathF.Max(0f, omenDelay);
        this.omenRotate = omenRotate;
        omenActionId = actionId;
        if (this.omenDelay <= 0f)
            SpawnOmen(chara);
        else
            omenScheduled = true;
        return true;
    }

    public void Tick(float deltaSeconds)
    {
        omen?.Tick(deltaSeconds);
        if (omen is { IsActive: false })
        {
            omen.Despawn();
            omen = null;
        }

        if (remainingAnimationLock > 0f)
            remainingAnimationLock = MathF.Max(0f, remainingAnimationLock - deltaSeconds);

        if (!casting) return;

        var chara = parent.BattleCharaPtr;
        if (chara == null) { casting = false; return; }

        elapsed += deltaSeconds;
        if (omenScheduled && elapsed >= omenDelay)
        {
            omenScheduled = false;
            SpawnOmen(chara);
        }
        if (elapsed >= total)
        {
            chara->CastInfo.CurrentCastTime = total;
            FaceTarget(chara);
            FireActionEffect(chara, WorldTargetLocation, targetId, animationVariation);
            remainingAnimationLock = DefaultAnimationLockSeconds;
            chara->CastInfo.IsCasting = false;
            omen?.Despawn();
            omen = null;
            ResetCastState();
        }
        else
        {
            chara->CastInfo.CurrentCastTime = elapsed;
        }
    }

    // Suppress the default telegraph so a scenario can render a custom omen (e.g.
    // SwivelCannon's 210° cone covers the whole arena from the edge; the scenario
    // draws a half-disc at arena center instead).
    public void HideOmen()
    {
        omen?.Despawn();
        omen = null;
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
        omenScheduled = false;
        omen?.Despawn();
        omen = null;
    }

    private void ResetCastState()
    {
        casting = false;
        omenScheduled = false;
        targetLocation = null;
        targetId = null;
        animationVariation = 0;
        ActionId = 0;
    }

    // targetLocation is stored scenario-local; lift to world only for native
    // ActionEffect delivery (Receive / CastInfo expect world coords).
    private Vector3? WorldTargetLocation => targetLocation is { } loc ? coordinates.ToGlobal(loc) : null;

    private void SpawnOmen(BattleChara* chara)
    {
        // Origin in scenario-local coords (parent.Position is local); SimOmen converts
        // to world at its native spawn boundary.
        var origin = targetLocation ?? parent.Position;
        var rotation = MathUtil.NormalizeRotation(chara->Rotation + omenRotate);
        omen?.Despawn();
        // Persistent (no duration): SimCast clears it on cast completion / hide / despawn.
        omen = new SimOmen(coordinates, omenActionId, origin, rotation);
    }

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
    private static void FireActionEffect(BattleChara* chara, Vector3? targetLocation = null, GameObjectId? deliverTo = null, byte animationVariation = 0)
    {
        var actionId = chara->CastInfo.ActionId;

        // Engine ApplyAll dereferences whatever LookupBattleCharaByEntityId returns for each
        // targetEntityIds[i] without a null check. A captured GameObjectId can outlive the
        // BC behind it (target killed/despawned mid-cast, scenario reset, lifetime expiry),
        // and a stale id resolves to null in _battleCharas → ApplyAll+0x45 null-deref. Drop
        // to the no-target path in that case; release animation still plays off casterEntityId.
        var safeDeliver = deliverTo;
        if (safeDeliver is { } probeId)
        {
            var cm = CharacterManager.Instance();
            if (cm == null || cm->LookupBattleCharaByEntityId((uint)probeId.Id) == null)
            {
                Plugin.Log.Warning(
                    $"FireActionEffect: target {probeId.Id:X} for action {actionId:X} on caster {chara->EntityId:X} not in CharacterManager._battleCharas; dropping deliverTo to avoid ApplyAll null-deref");
                safeDeliver = null;
            }
        }

        var rotationInt = (ushort)Math.Clamp(
            (int)((chara->Rotation / MathF.PI) * 32767f + 32767f), 0, 65535);

        // Derive AnimationTargetId from the sanitized delivery state, not from CastInfo.TargetId
        // (snapshotted at Start() and possibly stale). When there's no valid entity target, use
        // the benign 0xE0000000 "no target" sentinel — never the caster's own spawned id. Our
        // spawned BattleCharas get ids in the reserved 0xE0000001+ pronoun range (BattleCharaSpawn),
        // and Receive/ApplyAll resolves AnimationTargetId through the placeholder resolver with no
        // accompanying pointer, null-derefing on a reserved id (crash dump 20260529_210128). In solo
        // mode the boss is the first spawned BC (index 0 -> 0xE0000001), so a self-cast with no
        // target reliably hit that path. The release animation still plays off casterEntityId.
        var header = default(ActionEffectHandler.Header);
        if (safeDeliver is { } sd)
            header.AnimationTargetId = sd;
        else
            header.AnimationTargetId = 0xE0000000;
        header.ActionId = actionId;
        header.GlobalSequence = 0;
        header.AnimationLock = 0f;
        header.BallistaEntityId = 0xE0000000;
        header.SourceSequence = 0;
        header.RotationInt = rotationInt;
        header.SpellId = (ushort)actionId;
        header.AnimationVariation = animationVariation;
        header.ActionType = chara->CastInfo.ActionType;
        header.NumTargets = (byte)(safeDeliver.HasValue ? 1 : 0);
        header.ForceAnimationLock = true;

        var pos = targetLocation ?? new Vector3(chara->Position.X, chara->Position.Y, chara->Position.Z);

        if (safeDeliver is { } targetId)
        {
            var effectsBlock = default(ActionEffectHandler.TargetEffects);
            ActionEffectHandler.Receive(
                chara->EntityId,
                (Character*)chara,
                &pos,
                &header,
                &effectsBlock,
                &targetId);
        }
        else
        {
            ActionEffectHandler.Receive(
                chara->EntityId,
                (Character*)chara,
                &pos,
                &header,
                null,
                null);
        }
    }
}
