using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Native;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;

namespace AnoMech.Core.SimObjects;

// Common base for anything in the simulated world that has a BattleChara behind it
public abstract unsafe class SimCharacter(Coordinates coordinates) : ISimObject, IPositioned
{
    private readonly List<SimVfx> vfx = [];
    private readonly List<SimStatus> statusList = [];

    internal abstract BattleChara* BattleCharaPtr { get; }

    private protected abstract Movement Movement { get; }

    protected readonly Coordinates Coordinates = coordinates;

    // Obstacles this character's Movement steers around. Defaults to the shared
    // empty field (no avoidance — straight lines); PartyCreator points party
    // doppels at world.Obstacles so only bots avoid geometry.
    internal ObstacleField Obstacles { get; set; } = ObstacleField.Empty;

    public virtual bool IsActive => BattleCharaPtr != null;

    // True while the character is rooted by an in-progress action (cast bar up or
    // release animation still playing). The Movement subsystem reads this to hold
    // an active follow in place until the animation finishes. Default false; types
    // that simulate casts (SimEnemy) override it.
    public virtual bool AnimationLock => false;

    public bool IsForcedMoving => Movement.IsForcedMoving;
    public void Slide(Vector3 direction, float distance, float speed) => Movement.Slide(direction, distance, speed);

    public GameObjectId GameObjectId => BattleCharaPtr == null ? default : BattleCharaPtr->GetGameObjectId();
    public float HitboxRadius => BattleCharaPtr == null ? 0f : BattleCharaPtr->HitboxRadius;

    public uint EntityId
    {
        get
        {
            var obj = BattleCharaPtr;
            return obj == null ? 0u : obj->EntityId;
        }
    }

    public virtual void Tick(float deltaSeconds)
    {
        var native = BattleCharaPtr;
        if (native != null)
        {
            position = Coordinates.ToLocal(native->Position);
            Rotation = native->Rotation;
        }
        statusList.Update(deltaSeconds);
        vfx.Update(deltaSeconds);
        Movement.Tick(deltaSeconds);
        TickCarry(deltaSeconds);
    }

    // The real client's carry: still for 0.2s, settled by 0.9s. The client ignores an injected
    // one, so a native carry that hasn't moved by the check is finished by the sim's slide.
    private const float CarryStartDelay = 0.2f;
    private const float CarrySlideSeconds = 0.7f;
    private const float CarryCheckSeconds = 0.35f;
    private const float CarryMinProgress = 0.5f;
    private Vector3 carryStart;
    private Vector3? carryDestination;
    private float carryElapsed;

    public virtual void CarryTo(Vector3 destination, CarryMode mode = CarryMode.Native)
    {
        if (mode == CarryMode.Push)
        {
            Movement.Carry(destination, CarryStartDelay, CarrySlideSeconds);
            return;
        }
        ForcedMovement.CarryTo(EntityId, Coordinates.ToGlobal(destination), Rotation, selfTarget: mode == CarryMode.NativeSelfTarget);
        carryStart = Position;
        carryDestination = destination;
        carryElapsed = 0f;
    }

    private void TickCarry(float deltaSeconds)
    {
        if (carryDestination is not { } destination) return;
        carryElapsed += deltaSeconds;
        if (carryElapsed < CarryCheckSeconds) return;
        carryDestination = null;
        if (Vector3.Distance(Position, carryStart) >= CarryMinProgress) return;
        DiagnosticLog.Warn($"[SimCharacter] {GetType().Name} 0x{EntityId:X} was not carried by the client ({Vector3.Distance(Position, carryStart):F2}y in {CarryCheckSeconds:F2}s) -- sliding it the remaining {Vector3.Distance(Position, destination):F1}y instead.");
        Movement.Carry(destination, 0f, CarryStartDelay + CarrySlideSeconds - CarryCheckSeconds);
    }

    public virtual void Despawn()
    {
        statusList.Despawn();
        vfx.Despawn();
    }

    // -------------------------
    // Location Subsystem
    // -------------------------

    // Character position in local coordinates. Updated every frame to be always in sync with game.
    // Virtual so SimNetworkPuppet can report the peer's real position while the model catches up.
    private Vector3 position;
    public virtual Vector3 Position => position;
    public float Rotation { get; private set; }

    public void SetPosition(Vector3 newPosition)
    {
        var obj = BattleCharaPtr;
        if (obj == null) return;
        var w = Coordinates.ToGlobal(newPosition);
        obj->SetPosition(w.X, w.Y, w.Z);
        if (obj->DrawObject != null) obj->DrawObject->Object.Position = w;
        position = newPosition; // early update, will be updated on next tick anyway
    }

    public void SetRotation(float rotation)
    {
        var obj = BattleCharaPtr;
        if (obj == null) return;
        obj->SetRotation(MathUtil.NormalizeRotation(rotation));
        Rotation = rotation; // early update, will be updated on next tick anyway
    }

    public void SetPosition(Placement placement)
    {
        SetPosition(placement.Position);
        SetRotation(placement.Rotation);
    }

    // Transform for an actor the engine hasn't created yet (a packet spawn in flight, see
    // SimEnemy.SpawnFromPacket); SetPosition returns early without a native object.
    protected void SeedTransform(Vector3 newPosition, float rotation)
    {
        position = newPosition;
        Rotation = rotation;
    }

    public void Face(Vector3? target) => Movement.Face(target);
    public void Face(IPositioned? target) => Face(target?.Position);
    public void MoveTo(Vector3 target, float speed = 6f, float? finalRotation = null)
        => Movement.MoveTo(target, speed, finalRotation);
    public void MoveTo(Placement p) => MoveTo(p.Position);
    public void StopMoving() => Movement.Stop();

    public void Intercept(SimTether? tether, float margin = 3f) => Movement.Intercept(tether, margin);
    public bool IsIntercepting => Movement.IsIntercepting;
    public bool IsEasedMoving => Movement.IsEasedMoving;

    // forced: the mechanic is taking control, not a strat positioning a bot (see
    // Movement.Follow). Virtual so SimNetworkPuppet can hand a forced follow to its owner.
    public virtual void Follow(SimCharacter? target = null, float speed = 6f, bool forced = false)
        => Movement.Follow(target, speed, forced);


    // -------------------------
    // VFX Subsystem
    // -------------------------

    // Self-attached actor VFX keyed by path.
    // persistent: true  → tracked by sim (might crash if we try to remove vfx after game already did that)
    // persistent: false → fire-and-forget (game is responsible for duration and cleaning of vfx)
    public void AddVfx(string path, float duration = 0f, bool persistent = true)
        => AddVfx(path, duration, persistent, fromLockon: false);

    // fromLockon keeps a marker out of both VFX replication channels: its own id is what travels
    // (see AttachLockonVfx), and the derived path names no VfxPath constant a peer would accept.
    private void AddVfx(string path, float duration, bool persistent, bool fromLockon)
    {
        if (!VfxFunctions.VfxPathExists(path) || !IsActive) return;
        if (persistent && FindVfx(path) is { } existing)
        {
            existing.Refresh(duration);
            return;
        }
        var spawned = new SimVfx(this, path, duration, fromLockon);
        if (persistent && spawned.IsActive)
            vfx.Add(spawned);
        else if (!persistent && !fromLockon)
        {
            if (pendingVfx.Count < AnoMech.Multiplayer.NetGuard.MaxVfxPerEntity) pendingVfx.Add((path, duration));
            else droppedPendingVfx++;
        }
    }

    // Every non-persistent AddVfx since the last drain, sampled for peers like the lockons.
    private readonly List<(string Path, float Duration)> pendingVfx = [];
    private int droppedPendingVfx;

    public IReadOnlyList<(string Path, float Duration)> DrainPendingVfx(out int dropped)
    {
        dropped = droppedPendingVfx;
        droppedPendingVfx = 0;
        if (pendingVfx.Count == 0) return [];
        var result = pendingVfx.ToArray();
        pendingVfx.Clear();
        return result;
    }

    // Fire-and-forget marker of the last attached lockon (every call site uses persistent: false).
    public uint? LastLockonVfxId { get; private set; }

    // Every lockon attached since the last drain, so two in one tick (P4's Blizzard+Lightning
    // orbs) both replicate.
    private readonly List<uint> pendingLockonVfxIds = [];
    private int droppedPendingLockonVfxIds;

    public IReadOnlyList<uint> DrainPendingLockonVfxIds(out int dropped)
    {
        dropped = droppedPendingLockonVfxIds;
        droppedPendingLockonVfxIds = 0;
        if (pendingLockonVfxIds.Count == 0) return [];
        var result = pendingLockonVfxIds.ToArray();
        pendingLockonVfxIds.Clear();
        return result;
    }

    public void AttachLockonVfx(uint lockonId, float duration = 0f, bool persistent = true)
    {
        if (VfxFunctions.LockonVfxIconName(lockonId) is not { } iconName) return;
        AddVfx($"vfx/lockon/eff/{iconName}.avfx", duration, persistent, fromLockon: true);
        LastLockonVfxId = lockonId;
        if (pendingLockonVfxIds.Count < AnoMech.Multiplayer.NetGuard.MaxLockonVfxPerEntity) pendingLockonVfxIds.Add(lockonId);
        else droppedPendingLockonVfxIds++;
    }

    // Sampled for peers and reconciled there like the statuses: unlike the fire-and-forget ones
    // above, a persistent VFX ends by removal, which no one-shot event could carry.
    public IReadOnlyList<string> ActivePersistentVfxPaths
        => vfx.Count == 0 ? [] : vfx.Where(v => v.IsActive && !v.FromLockon).Select(v => v.Path).Distinct().ToList();

    public SimVfx? FindVfx(string path)
    {
        return vfx.Find(v => v.IsActive && v.Path == path);
    }

    public void RemoveVfx(string path)
    {
        FindVfx(path)?.Despawn();
    }

    // FIXME: minor, keep track of tethers and slots attached to character
    public bool HasTetherInSlot0(ushort tetherId)
        => BattleCharaPtr != null && VfxFunctions.GetTetherId((Character*)BattleCharaPtr, 0) == tetherId;

    // -------------------------
    // Status Subsystem
    // -------------------------

    // sourceObject distinguishes independent same-id instances (UMAD P1 Tele-portent applies
    // the same id twice with separate expiries).
    public SimStatus? AddStatus(ushort statusId, float duration = 0f, int stacks = 1, bool overrideStacks = false, GameObjectId sourceObject = default)
    {
        Core.DiagnosticLog.Info($"[SimCharacter] AddStatus: {DiagnosticName} gets status {statusId} (duration={duration:F1}, stacks={stacks}, overrideStacks={overrideStacks}, source={sourceObject}).");
        if (FindStatus(statusId, sourceObject) is { } status)
        {
            // overrideStacks: stacks is the absolute target; otherwise it's a
            // relative delta (negative consumes stacks).
            int delta = overrideStacks ? stacks - status.Stacks : stacks;
            status.Reapply(duration, delta);
            if (status.Stacks == 0)
            {
                status.Despawn();   // last stack consumed → remove the status
                return null;
            }
            return status;
        }

        // No existing status: a non-positive request has nothing to remove.
        if (stacks <= 0) return null;
        var s = new SimStatus(this, statusId, duration, (ushort)stacks, sourceObject);
        statusList.Add(s);
        return s;
    }

    // For logging: the party role when available, else the type name.
    private string DiagnosticName => (this as ISimPartyMember)?.Role.ToString() ?? GetType().Name;

    public SimStatus AddStatusParam(ushort statusId, int param, float duration = 0f)
    {
        var s = new SimStatus(this, statusId, duration, (ushort)param);
        statusList.Add(s);
        return s;
    }

    public void RemoveStatus(ushort statusId)
    {
        RemoveStatus(statusId, default);
    }

    // Source-aware: an id the character holds twice (two appliers) needs the right one named.
    public void RemoveStatus(ushort statusId, GameObjectId sourceObject)
    {
        if (FindStatus(statusId, sourceObject) is not { } status) return;
        Core.DiagnosticLog.Info($"[SimCharacter] RemoveStatus: {DiagnosticName} loses status {statusId}.");
        status.Despawn();
    }

    public SimStatus? FindStatus(ushort statusId, GameObjectId sourceObject = default)
    {
        return statusList.Find(status => status.IsActive && status.StatusId == statusId && status.SourceObject == sourceObject);
    }

    public bool HasStatus(ushort statusId) => FindStatus(statusId) != null;

    // Sampled for peers; AddStatus is otherwise entirely local.
    public IReadOnlyList<(ushort StatusId, ushort Stacks, float RemainingTime)> ActiveStatusSnapshot =>
        statusList.Where(s => s.IsActive).Select(s => (s.StatusId, s.Stacks, s.RemainingTime)).ToList();

    public IReadOnlyList<SimStatus> ActiveStatuses => statusList.Where(s => s.IsActive).ToList();


    // -------------------------
    // Other Subsystem
    // -------------------------

    // Sampled by MultiplayerManager for scripted animation cues (a boss warp, a sleep pose).
    // Movement and network interpolation use the *Native entry points, so the run cycle isn't
    // broadcast. A reset is id 0 with its own seq bump.
    public ushort? AnimationTimelineId { get; private set; }
    public ushort AnimationTimelineLoopId { get; private set; }

    // Same reasoning as SimCast.CastSeq: a repeat of the same id must read as a change.
    public int AnimationTimelineSeq { get; private set; }

    public void PlayActionTimeline(ushort timelineId, ushort loopId = 0, ushort baseOverride = 0)
    {
        AnimationTimelineId = timelineId;
        AnimationTimelineLoopId = loopId;
        AnimationTimelineSeq++;
        PlayActionTimelineNative(timelineId, loopId, baseOverride);
    }

    internal void PlayActionTimelineNative(ushort timelineId, ushort loopId = 0, ushort baseOverride = 0)
    {
        var chara = BattleCharaPtr;
        if (chara == null) return;
        if (chara->Timeline.TimelineSequencer.Parent == null) return;
        chara->Timeline.BaseOverride = baseOverride;
        chara->Timeline.PlayActionTimeline(timelineId, loopId);
    }

    public void ResetActionTimeline()
    {
        AnimationTimelineId = 0;
        AnimationTimelineLoopId = 0;
        AnimationTimelineSeq++;
        ResetActionTimelineNative();
    }

    internal void ResetActionTimelineNative()
    {
        var bc = BattleCharaPtr;
        if (bc == null) return;
        bc->Timeline.BaseOverride = 0;
        bc->Timeline.ModelState = 0;
        bc->Timeline.AnimationState[0] = 0;
        bc->Timeline.AnimationState[1] = 0;
        // Sequencer ops need a live skeleton (Parent); guard before touching it.
        if (bc->Timeline.TimelineSequencer.Parent == null) return;
        bc->Timeline.TimelineSequencer.SetSlotTimeline(0, 0);
    }

    // Despawn-only: Character::Terminate walks all 14 sequencer slots and crashes on a still-live
    // one (a mid-cast release animation occupies the UpperBody/Facial/Lips slots);
    // ResetActionTimeline only clears slot 0.
    public void QuiesceActionTimeline()
    {
        var bc = BattleCharaPtr;
        if (bc == null) return;
        bc->Timeline.BaseOverride = 0;
        bc->Timeline.ModelState = 0;
        bc->Timeline.AnimationState[0] = 0;
        bc->Timeline.AnimationState[1] = 0;
        if (bc->Timeline.TimelineSequencer.Parent == null) return;
        for (uint slot = 0; slot < 14; slot++)
            bc->Timeline.TimelineSequencer.SetSlotTimeline(slot, 0);
    }
}
