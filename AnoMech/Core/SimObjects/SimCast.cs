using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
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
public sealed class SimCast : ISimObject
{
    private readonly SimCharacter parent;
    private readonly Coordinates coordinates;

    private bool casting;
    private float elapsed;
    private float total;
    private float omenDelay;
    private float omenRotate;
    // Retail resolves an NPC action ~0.3s after its bar fills: callers pass the real bar as
    // castTime and that gap as fireDelay, counted on our own clock since the engine may clear
    // CastInfo once the bar completes.
    private float fireDelay;
    private float fireDelayElapsed;
    private float castClock;
    private bool barComplete;
    private Vector3? targetLocation;   // scenario-local coords
    private GameObjectId? targetId;
    private byte animationVariation;

    private float animationLock;
    private float remainingAnimationLock;

    // Set by NativeCast, consumed by the next NativeActionEffect on this caster: "the resolve
    // of the telegraph just started" versus "no telegraph behind this".
    private bool pendingNativeResolve;

    public bool IsCasting => parent.Proxy?.IsCasting ?? false;

    public uint ActionId { get; private set; }
    // Read live from CastInfo: a bare NativeCast never enters Tick's `casting` path, so
    // `elapsed` would stay frozen for it.
    public float Progress
    {
        get
        {
            if (parent.Proxy is not { Exists: true } chara || total <= 0f) return 0f;
            return Math.Clamp(chara.CurrentCastTime / total, 0f, 1f);
        }
    }

    // Scenario-local ground target, sampled for peers so a replayed Cast() lands in the same spot.
    public Vector3? TargetLocation => targetLocation;

    // Entity target. A raw GameObjectId isn't portable across the network (each client spawns
    // its own doppels), so MultiplayerManager resolves it to a role/NetId; without an entity
    // target the hit-react animation never plays on a peer.
    public GameObjectId? TargetId => targetId;

    // The duration this cast runs with, sampled for peers: many scripted casts use synthetic ids
    // the Action sheet lacks, or whose Cast100ms doesn't match the script.
    public float Total => total;

    // Sampled for peers; the ActorCast packet is the only thing that controls when the omen
    // fades in.
    public float OmenDelay => omenDelay;
    // Sampled for peers: folded into the native cast rotation, so the actor's facing doesn't
    // carry it and a peer would draw the omen unrotated.
    public float OmenRotate => omenRotate;

    // The telegraph CastSeq names, sampled for peers so a replayed bar releases as late and holds
    // its animation as long. A bare NativeCast's resolve is its caller's, so it carries only what
    // the caller declares.
    public float CastAnimationLock { get; private set; } = 0.6f;
    public float CastFireDelay { get; private set; }
    // How far along the bar was when it began, for a cast already under way as a scenario opens.
    public float CastStartElapsed { get; private set; }

    // Bumped per telegraphed cast. A peer dedupes on this changing rather than on IsCasting's
    // rising edge, which compared two independent clocks and replayed a cast twice.
    public int CastSeq { get; private set; }

    // An instant cast never sets IsCasting and clears ActionId within the tick, so a
    // level-sampled snapshot can't see it; the counter plus a snapshot of what it was is what
    // a peer replays.
    public int LastInstantCastSeq { get; private set; }
    public uint LastInstantCastActionId { get; private set; }
    public Vector3? LastInstantCastTargetLocation { get; private set; }
    public GameObjectId? LastInstantCastTargetId { get; private set; }
    // A bare NativeActionEffect (no Cast around it) is replayed by a peer field for field: a
    // Cast() would re-face the caster at the target position and use its own lock.
    public bool LastInstantCastIsNativeEffect { get; private set; }
    public float LastInstantCastAnimationLock { get; private set; } = 0.6f;
    public GameObjectId? LastInstantCastActionTargetId { get; private set; }
    // Set instead when the resolve was delivered as a captured raw packet, so a peer delivers
    // its own copy the same way (see Core.Native.Implementations.RawActionEffect).
    public string? LastInstantCastRawPacket { get; private set; }

    // Records a raw-packet delivery the caller performed: the packet bypasses this class
    // entirely, so nothing else would mark the caster as having just acted.
    public void NoteRawActionEffect(uint actionId, string captureName, float animationLock)
    {
        LastInstantCastActionId = actionId;
        LastInstantCastTargetLocation = null;
        LastInstantCastTargetId = null;
        LastInstantCastActionTargetId = null;
        LastInstantCastIsNativeEffect = true;
        LastInstantCastAnimationLock = animationLock;
        LastInstantCastRawPacket = captureName;
        LastInstantCastSeq++;
        remainingAnimationLock = animationLock;
    }

    // True while the cast bar is up (or a delayed fire is still pending) or the release
    // animation is still playing. A following boss roots itself while busy so the action
    // animation finishes in place instead of sliding.
    public bool IsBusy => IsCasting || casting || remainingAnimationLock > 0f;

    // Owned and ticked by its SimEnemy, never reaped by liveness.
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
        if (parent.Proxy is not { Exists: true } chara) return false;

        if (castTime == null)
        {
            if (Natives.Data.Action(actionId) is not { } action)
            {
                Plugin.Log.Warning($"[SimCast.Start] Action Row {actionId} not found");
                return false;
            }

            castTime = action.CastSeconds;
        }

        this.animationLock = animationLock;
        var castTimeValue = castTime.Value;


        // Instant actions get only the ActionEffect (retail sends no cast packet for them):
        // a cast-begin on the same frame clobbers the body animation, which is the whole show
        // for a VFX-less auto-attack.
        if (castTimeValue > 0)
        {
            var target = targetId ?? chara.GameObjectId;
            NativeCast(actionId, ActionType.Action, omenDelay, castTimeValue, false, parent.Rotation + omenRotate, localTargetLocation, target);
            total = chara.TotalCastTime;
            CastAnimationLock = animationLock;
            CastFireDelay = fireDelay ?? 0f;
            CastSeq++;
        }
        else
        {
            total = 0f;
        }

        elapsed = 0f;
        castClock = 0f;
        barComplete = false;

        casting = true;
        targetLocation = localTargetLocation;
        this.targetId = targetId;
        this.animationVariation = animationVariation;
        ActionId = actionId;
        this.omenDelay = omenDelay;
        this.omenRotate = omenRotate;
        this.fireDelay = fireDelay ?? 0;
        fireDelayElapsed = 0f;

        if (castTimeValue <= 0)
        {
            FaceTarget(chara);
            FireActionEffect(chara, actionId, ActionType.Action, animationLock, targetLocation, targetId, animationVariation);
            LastInstantCastActionId = actionId;
            LastInstantCastTargetLocation = targetLocation;
            LastInstantCastTargetId = targetId;
            LastInstantCastActionTargetId = targetId;
            LastInstantCastIsNativeEffect = false;
            LastInstantCastAnimationLock = animationLock;
            LastInstantCastRawPacket = null;
            LastInstantCastSeq++;
            ResetCastState();
        }

        return true;
    }

    public void NativeCast(uint actionId, ActionType actionType, float omenDelay, float castTime, bool interruptible, float? rotation = null, Vector3? position = null, GameObjectId? targetId = null, GameObjectId? ballistaId = null, float animationLock = 0.6f, float fireDelay = 0f)
    {
        parent.Proxy?.ReceiveActorCast(new ActorCastData(
            actionId, actionType, castTime, omenDelay, interruptible,
            rotation ?? parent.Rotation, coordinates.ToGlobal(position ?? parent.Position), targetId, ballistaId));

        // The bookkeeping Start() would do. `casting` stays false: the caller schedules its own
        // NativeActionEffect for the resolve (pendingNativeResolve), or Tick would fire the
        // release twice.
        ActionId = actionId;
        total = castTime;
        this.omenDelay = omenDelay;
        targetLocation = position;
        this.targetId = targetId;
        CastAnimationLock = animationLock;
        CastFireDelay = fireDelay;
        CastStartElapsed = 0f;
        CastSeq++;
        pendingNativeResolve = true;
    }

    public void SkipAhead(float seconds)
    {
        if (parent.Proxy is not { IsCasting: true } chara) return;
        CastStartElapsed = Math.Clamp(seconds, 0f, total);
        chara.SetCurrentCastTime(CastStartElapsed);
        elapsed = CastStartElapsed;
        castClock = CastStartElapsed;
    }

    public void NativeActionEffect(uint actionId, float animationLock, ushort spellId, byte animationVariaton, ActionType actionType, byte flags, float? rotation = null, Vector3? position = null, GameObjectId? animationTargetId = null, GameObjectId? actionTargetId = null, GameObjectId? ballistaId = null)
    {
        if (parent.Proxy is not { Exists: true } chara)
        {
            return;
        }

        chara.ReceiveActionEffect(new ActionEffectData(
            actionId, actionType, animationLock, spellId, animationVariaton, flags,
            rotation ?? parent.Rotation, coordinates.ToGlobal(position ?? Vector3.Zero),
            animationTargetId, actionTargetId, ballistaId));

        remainingAnimationLock = animationLock;

        // The resolve of a telegraph just started is already covered by CastSeq; a standalone
        // effect gets the instant-cast bookkeeping instead. FireActionEffect's own call is
        // recorded by Start/Tick as a Cast, so `firingCast` keeps it out of here.
        if (pendingNativeResolve)
        {
            pendingNativeResolve = false;
        }
        else if (!firingCast)
        {
            LastInstantCastActionId = actionId;
            LastInstantCastTargetLocation = position;
            LastInstantCastTargetId = animationTargetId ?? actionTargetId;
            LastInstantCastActionTargetId = actionTargetId;
            LastInstantCastIsNativeEffect = true;
            LastInstantCastAnimationLock = animationLock;
            LastInstantCastRawPacket = null;
            LastInstantCastSeq++;
        }
    }

    private bool firingCast;

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

        if (parent.Proxy is not { Exists: true } chara)
        {
            casting = false;
            return;
        }

        elapsed = chara.CurrentCastTime;
        castClock += deltaSeconds;

        // With a fire delay our own clock also counts as completion: the engine may clear
        // CastInfo once the bar fills.
        if (elapsed >= total || (fireDelay > 0f && castClock >= total)) barComplete = true;
        if (!barComplete) return;

        if (fireDelay > 0f)
        {
            fireDelayElapsed += deltaSeconds;
            if (fireDelayElapsed < fireDelay) return;
        }

        FaceTarget(chara);
        FireActionEffect(chara, ActionId, ActionType.Action, animationLock, targetLocation, targetId, animationVariation);
        ResetCastState();
    }

    // Stops the cast short: nothing fires. False when there was nothing to stop.
    public bool Cancel()
    {
        var chara = parent.Proxy;
        if (!casting && !pendingNativeResolve && chara is not { IsCasting: true }) return false;
        chara?.ClearCast();
        pendingNativeResolve = false;
        ResetCastState();
        return true;
    }

    // Teardown for caster despawn: drop the telegraph, stop any pending delayed
    // spawn, and clear CastInfo. Clearing CastInfo matters because despawn deletes
    // the BattleChara via DeleteObjectByIndex -> Character::Terminate, whose
    // scheduler teardown reads a still-live cast/action timeline and crashes on
    // freed state (C0000005 at TimelineGroup.PlayAction; see crash dump
    // 20260529_193455).
    public void Despawn()
    {
        parent.Proxy?.ClearCast();
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
        castClock = 0f;
        barComplete = false;
    }

    // targetLocation is stored scenario-local; lift to world only for native
    // ActionEffect delivery (Receive / CastInfo expect world coords).
    private Vector3? WorldTargetLocation => targetLocation is { } loc ? coordinates.ToGlobal(loc) : null;

    // Targeted casts (ground location now; entity targets later) snap to face the target
    // on the final tick so the release animation plays in the intended direction even if
    // the target moved during the cast. FireActionEffect snapshots Rotation into the
    // packet header, so this must run first.
    private void FaceTarget(IBattleCharaProxy chara)
    {
        if (WorldTargetLocation is not { } loc) return;
        var dx = loc.X - chara.Position.X;
        var dz = loc.Z - chara.Position.Z;
        if (dx * dx + dz * dz < 1e-6f) return;
        chara.Rotation = MathUtil.NormalizeRotation(MathF.Atan2(dx, dz));
    }

    // Mimics the server's ActionEffect packet so the game plays the action's release
    // animation/VFX on the caster. When deliverTo is set, the packet carries
    // NumTargets=1 with that GameObjectId and a zeroed no-op effect block; some
    // actions only animate on the caster if the engine sees at least one target to
    // deliver to. When deliverTo is null, NumTargets=0 (used for self-targeted
    // casts and cast releases without an entity target) — the release animation
    // still plays.
    private void FireActionEffect(IBattleCharaProxy chara, uint actionId, ActionType actionType, float animationLock, Vector3? localTargetLocation = null, GameObjectId? deliverTo = null, byte animationVariation = 0)
    {
        if (deliverTo is { } id && !Natives.BattleCharas.IsInCharacterManager(id.ObjectId))
        {
            Plugin.Log.Warning(
                $"FireActionEffect: target {id.ObjectId:X} for action {actionId:X} on caster {chara.EntityId:X} not in CharacterManager._battleCharas; dropping deliverTo to avoid ApplyAll null-deref");
            deliverTo = null;
        }

        var pos = localTargetLocation ?? parent.Position;
        firingCast = true;
        try
        {
            NativeActionEffect(actionId, animationLock, (ushort)actionId, animationVariation, actionType, 0, chara.Rotation, pos, deliverTo, deliverTo);
        }
        finally
        {
            firingCast = false;
        }
    }
}
