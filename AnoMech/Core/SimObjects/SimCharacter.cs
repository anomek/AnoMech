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

    public GameObjectId GameObjectId => BattleCharaPtr == null ? default : BattleCharaPtr->GetGameObjectId();
    public float HitboxRadius => BattleCharaPtr == null ? 0f : BattleCharaPtr->HitboxRadius;
    
    
    public virtual void Tick(float deltaSeconds)
    {
        var native = BattleCharaPtr;
        if (native != null)
        {
            Position = Coordinates.ToLocal(native->Position);
            Rotation = native->Rotation;
        }
        statusList.Update(deltaSeconds);
        vfx.Update(deltaSeconds);
        Movement.Tick(deltaSeconds);
    }

    public virtual void Despawn()
    {
        statusList.Despawn();
        vfx.Despawn();
    }
    
    // -------------------------
    // Location Subsystem
    // -------------------------
    
    // Character position in local coordinates. Updated every frame to be always in sync with game
    public Vector3 Position { get; private set; }
    public float Rotation { get; private set; }
    
    public void SetPosition(Vector3 position)
    {
        var obj = BattleCharaPtr;
        if (obj == null) return;
        var w = Coordinates.ToGlobal(position);
        obj->SetPosition(w.X, w.Y, w.Z);
        if (obj->DrawObject != null) obj->DrawObject->Object.Position = w;
        Position = position; // early update, will be updated on next tick anyway
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
    
    public void Face(Vector3? target) => Movement.Face(target);
    public void Face(IPositioned? target) => Face(target?.Position);
    public void MoveTo(Vector3 target, float speed = 6f, float? finalRotation = null)
        => Movement.MoveTo(target, speed, finalRotation);
    public void MoveTo(Placement p) => MoveTo(p.Position);
    protected void StopMoving() => Movement.Stop();

    public void Intercept(SimTether? tether, float margin = 3f) => Movement.Intercept(tether, margin);


    // -------------------------
    // VFX Subsystem
    // -------------------------
    
    // Self-attached actor VFX keyed by path.
    // persistent: true  → tracked by sim (might crash if we try to remove vfx after game already did that)
    // persistent: false → fire-and-forget (game is responsible for duration and cleaning of vfx)
    public void AddVfx(string path, float duration = 0f, bool persistent = true)
    {
        if (!VfxFunctions.VfxPathExists(path) || !IsActive) return;
        if (persistent && FindVfx(path) is {} existing)
        {
            existing.Refresh(duration);
            return;
        }
        var spawned = new SimVfx(this, path, duration);
        if (persistent && spawned.IsActive)
            vfx.Add(spawned);
    }
    
    public void AttachLockonVfx(uint lockonId, float duration = 0f, bool persistent = true)
    {
        if (VfxFunctions.LockonVfxIconName(lockonId) is {} iconName)
            AddVfx($"vfx/lockon/eff/{iconName}.avfx", duration, persistent);
    }

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

    public SimStatus? AddStatus(ushort statusId, float duration = 0f, int stacks = 1, bool overrideStacks = false)
    {
        if (FindStatus(statusId) is {} status)
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
        var s = new SimStatus(this, statusId, duration, (ushort)stacks);
        statusList.Add(s);
        return s;
    }

    public void RemoveStatus(ushort statusId)
    {
        FindStatus(statusId)?.Despawn();
    }

    public SimStatus? FindStatus(ushort statusId)
    {
        return statusList.Find(status => status.IsActive && status.StatusId == statusId);
    }

    public bool HasStatus(ushort statusId) => FindStatus(statusId) != null;
    
    
    // -------------------------
    // Other Subsystem
    // -------------------------
    
    public void PlayActionTimeline(ushort timelineId, ushort loopId = 0, ushort baseOverride = 0)
    {
        var chara = BattleCharaPtr;
        if (chara == null) return;
        if (chara->Timeline.TimelineSequencer.Parent == null) return;
        chara->Timeline.BaseOverride = baseOverride;
        chara->Timeline.PlayActionTimeline(timelineId, loopId);
    }
    
    public void ResetActionTimeline()
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

    // Despawn-only: fully stop the action-timeline sequencer before the BattleChara is
    // deleted. DeleteObjectByIndex -> Character::Terminate walks all 14 sequencer slots and
    // calls TimelineGroup::PlayAction on each; a still-live slot (mid-cast / release
    // animation) crashes on freed scheduler state (C0000005 at TimelineGroup.PlayAction;
    // dumps 20260529_193455, 20260603_221355). ResetActionTimeline only clears slot 0 (Base),
    // which is insufficient for a casting boss whose release animation occupies the
    // UpperBody/Facial/Lips slots. Sequencer ops need a live skeleton, so guard on Parent first.
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
