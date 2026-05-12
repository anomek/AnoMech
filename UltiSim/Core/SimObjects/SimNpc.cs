using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace UltiSim.Core.SimObjects;

// SimCharacter backed by a BattleChara we allocated via ClientObjectManager.
// Identified by its CO index. Overlay state (VFX, statuses) lives on the base;
// this layer adds Index-based pointer lookup, movement, and the "free the
// handle on Despawn" lifecycle.
public unsafe class SimNpc : SimPartySlot
{
    public const uint InvalidIndex = 0xFFFFFFFF;

    public uint Index { get; private set; }
    private bool pendingDraw;
    private float pendingDespawnTimer = -1f;
    private Vector3? moveTarget;
    private float moveSpeed;
    private float? moveFinalRotation;
    private ushort moveTimelineId;
    private bool moveAnimActive;

    // ActionTimeline rows: 22 = normal/run (looping). PlayerCharacter rigs in normal
    // stance use this for the run loop; setting Timeline.BaseOverride forces the
    // base animation slot to play it while we drive position via SetPosition.
    public const ushort DefaultRunTimelineId = 22;

    protected SimNpc(uint index)
    {
        Index = index;
        pendingDraw = index != InvalidIndex;
    }

    public override bool IsAlive => !Dead && Index != InvalidIndex && GetGameObject() != null;

    public void PlayActionTimeline(ushort timelineId)
    {
        var chara = GetBattleChara();
        if (chara == null) return;
        if (chara->Timeline.TimelineSequencer.Parent == null) return;
        chara->Timeline.PlayActionTimeline(timelineId);
    }

    // Writes Timeline.ModelState then forces a draw rebuild via DisableDraw +
    // deferred EnableDraw. UpdateRender (vfunc 4) and NotifyTransformChanged
    // (dirty-flag) were both tried first and didn't trigger a visual commit;
    // the proven path is the same DisableDraw + pendingDraw shape that
    // SetModelCharaId uses — the Tick reconciler at the bottom of this file
    // re-enables once IsReadyToDraw flips true again. Cost is one Tick of
    // invisibility on every commit, which is acceptable at well-defined
    // 003F packet timestamps.
    public void SetModelState(byte value)
    {
        var chara = GetBattleChara();
        if (chara == null) return;
        var obj = (GameObject*)chara;
        obj->DisableDraw();
        chara->Timeline.ModelState = value;
        pendingDraw = true;
    }

    public void SetAnimationState(byte hi, byte lo)
    {
        var chara = GetBattleChara();
        if (chara == null) return;
        chara->Timeline.AnimationState[0] = hi;
        chara->Timeline.AnimationState[1] = lo;
    }

    // Server-side boss appearance change arrives as three ActorControlExtra
    // packets within ~90ms: 0x0031 = SetModeAttributeFlags (writes
    // ModelContainer.ModeAttributeFlags), 0x003F = SetModelState (writes
    // Timeline.ModelState), 0x0197 = PlayActionTimeline. Empirically verified
    // on Omega-M's Synthetic Shield: ModeAttributeFlags 0x10 -> 0x31 and
    // ModelState 0x00 -> 0x04 are the only fields that change when the shield
    // becomes visible. Both setters force a draw rebuild via DisableDraw +
    // pendingDraw -> deferred EnableDraw; the lighter UpdateRender and
    // NotifyTransformChanged paths were tried first and did not commit the
    // change visually.
    public void ApplyPoseChange(byte modelState, byte animStateHi, byte animStateLo, ushort commitTimelineId)
    {
        var chara = GetBattleChara();
        if (chara == null) return;
        chara->Timeline.ModelState = modelState;
        chara->Timeline.AnimationState[0] = animStateHi;
        chara->Timeline.AnimationState[1] = animStateLo;
        byte* animState = &chara->Timeline.ModelState + 1;
        animState[0] = animStateHi;
        animState[1] = animStateLo;
        if (chara->Timeline.TimelineSequencer.Parent == null) return;
        chara->Timeline.PlayActionTimeline(commitTimelineId);
    }

    public void SetModeAttributeFlags(byte value)
    {
        var chara = GetBattleChara();
        if (chara == null) return;
        var obj = (GameObject*)chara;
        obj->DisableDraw();
        chara->ModelContainer.ModeAttributeFlags = value;
        pendingDraw = true;
    }

    // Disables draw, writes the new ModelCharaId, and defers EnableDraw to the
    // Tick reconciler — the engine rebuilds the skeleton asynchronously, so
    // re-enabling in the same frame fires before IsReadyToDraw flips and the
    // swap is lost. pendingDraw routes us through the same readiness gate the
    // initial spawn flow uses.
    public void SetModelCharaId(int modelCharaId)
    {
        var chara = GetBattleChara();
        if (chara == null) return;
        var obj = (GameObject*)chara;
        obj->DisableDraw();
        chara->ModelContainer.ModelCharaId = modelCharaId;
        pendingDraw = true;
    }

    // CharacterData.TransformationId (short at +0x24 of the CharacterData
    // sub-object, flattened onto BattleChara via Inherits<CharacterData>).
    // Bosses drive form swaps through this field; the engine normally derives
    // it from a buff Param (e.g. Superfluid carries Param=493), but our direct
    // StatusManager writes bypass that derivation, so scenarios set it
    // explicitly. Same deferred-EnableDraw shape as SetModelCharaId: the
    // skeleton rebuild is async, so re-enabling inline races IsReadyToDraw.
    public void SetTransformationId(short transformationId)
    {
        var chara = GetBattleChara();
        if (chara == null) return;
        var obj = (GameObject*)chara;
        // obj->DisableDraw();
        chara->TransformationId = transformationId;
        // obj->GetDrawObject()->UpdateRender();
        // pendingDraw = true;
    }

    public uint EntityId
    {
        get
        {
            var obj = GetGameObject();
            return obj == null ? 0u : obj->EntityId;
        }
    }

    public override GameObjectId GameObjectId
    {
        get
        {
            var obj = GetGameObject();
            return obj == null ? default : obj->GetGameObjectId();
        }
    }

    public override Vector3 Position
    {
        get
        {
            var obj = GetGameObject();
            return obj == null ? default : obj->Position;
        }
    }

    public override float Rotation
    {
        get
        {
            var obj = GetGameObject();
            return obj == null ? 0f : obj->Rotation;
        }
    }

    public override float HitboxRadius
    {
        get
        {
            var chara = GetBattleChara();
            return chara == null ? 0f : chara->HitboxRadius;
        }
    }

    // GameObject.SetPosition only updates the logical Position (+0xB0); the engine
    // catches the DrawObject up to it over multiple frames, which reads as the
    // model "walking" the gap for big jumps (small per-frame deltas in MoveTo's
    // tick hide it). Mirroring the write into DrawObject.Position forces the
    // visible model to snap.
    public override void SetPosition(Vector3 position)
    {
        var obj = GetGameObject();
        if (obj == null) return;
        obj->SetPosition(position.X, position.Y, position.Z);
        if (obj->DrawObject != null) obj->DrawObject->Object.Position = position;
    }

    public override void SetPosition(Placement placement)
    {
        var obj = GetGameObject();
        if (obj == null) return;
        obj->SetPosition(placement.Position.X, placement.Position.Y, placement.Position.Z);
        obj->SetRotation(MathUtil.NormalizeRotation(placement.Rotation));
        if (obj->DrawObject != null) obj->DrawObject->Object.Position = placement.Position;
    }

    // Snaps the NPC's facing toward `target` on the XZ plane. No-op when the
    // target is at the same XZ position. Does not affect movement state.
    public void Face(Vector3 target)
    {
        var obj = GetGameObject();
        if (obj == null) return;
        var dx = target.X - obj->Position.X;
        var dz = target.Z - obj->Position.Z;
        if (dx * dx + dz * dz < 1e-6f) return;
        obj->SetRotation(MathUtil.NormalizeRotation(MathF.Atan2(dx, dz)));
    }

    public void Face(IPositioned target) => Face(target.Position);

    // Plays `timelineId` immediately and despawns after `delay` seconds. No-op
    // when already despawned. Useful for warp-out / death animations.
    public void Despawn(ushort timelineId, float delay)
    {
        if (!IsAlive) return;
        PlayActionTimeline(timelineId);
        pendingDespawnTimer = MathF.Max(0f, delay);
    }

    // tmp
    public void MoveTo(Placement p) => MoveTo(p.Position);
    // Sets a destination the Tick loop walks toward at `speed` units/sec, facing
    // the direction of travel. Plays `timelineId` (default normal/run = 22) on the
    // base animation slot for the duration of the motion so the model actually
    // animates instead of sliding. When `finalRotation` is set, the entity snaps
    // to that facing on arrival instead of holding the last direction-of-travel
    // angle (used when callers need an explicit pose at the destination).
    // Cleared on arrival or when MoveTo is called again. Cancels with StopMoving().
    public override void MoveTo(Vector3 target, float speed = 6f, float? finalRotation = null, ushort timelineId = DefaultRunTimelineId)
    {
        moveTarget = target;
        moveSpeed = MathF.Max(0f, speed);
        moveFinalRotation = finalRotation;
        moveTimelineId = timelineId;
        StartMoveAnim();
    }

    public override void StopMoving()
    {
        moveTarget = null;
        StopMoveAnim();
    }

    private void StartMoveAnim()
    {
        var chara = GetBattleChara();
        if (chara == null) return;
        chara->Timeline.BaseOverride = moveTimelineId;
        if (chara->Timeline.TimelineSequencer.Parent != null)
            chara->Timeline.TimelineSequencer.PlayTimeline(moveTimelineId);
        moveAnimActive = true;
    }

    private void StopMoveAnim()
    {
        if (!moveAnimActive) return;
        var chara = GetBattleChara();
        if (chara != null) chara->Timeline.BaseOverride = 0;
        moveAnimActive = false;
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);

        if (pendingDraw)
        {
            var obj = GetGameObject();
            if (obj == null) { pendingDraw = false; }
            else if (obj->IsReadyToDraw())
            {
                obj->EnableDraw();
                pendingDraw = false;
            }
        }

        if (moveTarget is { } target)
        {
            var current = Position;
            var dx = target.X - current.X;
            var dz = target.Z - current.Z;
            var distSq = dx * dx + dz * dz;
            var step = moveSpeed * deltaSeconds;
            if (distSq <= step * step || step <= 0f)
            {
                var rot = moveFinalRotation
                          ?? (distSq > 1e-6f ? MathF.Atan2(dx, dz) : Rotation);
                SetPosition(new Placement(target, rot));
                moveTarget = null;
                moveFinalRotation = null;
                StopMoveAnim();
            }
            else
            {
                var dist = MathF.Sqrt(distSq);
                var nx = current.X + dx / dist * step;
                var nz = current.Z + dz / dist * step;
                SetPosition(new Placement(new Vector3(nx, current.Y, nz), MathF.Atan2(dx, dz)));
            }
        }

        if (pendingDespawnTimer >= 0f)
        {
            pendingDespawnTimer -= deltaSeconds;
            if (pendingDespawnTimer <= 0f) { pendingDespawnTimer = -1f; Despawn(); }
        }
    }

    public override void Despawn()
    {
        if (Index == InvalidIndex) return;
        base.Despawn(); // clears VFX + pinned statuses
        var obj = GetGameObject();
        if (obj != null)
        {
            obj->DisableDraw();
            ClientObjectManager.Instance()->DeleteObjectByIndex((ushort)Index, 0);
        }
        Index = InvalidIndex;
        pendingDraw = false;
    }

    protected GameObject* GetGameObject()
        => Index == InvalidIndex ? null : (GameObject*)ClientObjectManager.Instance()->GetObjectByIndex((ushort)Index);

    protected BattleChara* GetBattleChara() => (BattleChara*)GetGameObject();

    internal override BattleChara* BattleCharaPtr => GetBattleChara();
}
