using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

// SimCharacter backed by a BattleChara we allocated via ClientObjectManager.
// Identified by its CO index. Overlay state (VFX, statuses) lives on the base;
// this layer adds Index-based pointer lookup, movement, and the "free the
// handle on Despawn" lifecycle.
public unsafe class SimNpc : SimCharacter
{
    public const int InvalidIndex = -1;

    private int index;
    private bool pendingDraw;
    private int pendingDrawFrames;

    // The CharacterManager slot this wrapper reads (InvalidIndex once despawned).
    protected int Index => index;

    // Forget the slot without touching what it holds: a packet spawn whose slot ended up with
    // somebody else's actor.
    protected void DetachSlot()
    {
        index = InvalidIndex;
        pendingDraw = false;
    }

    // Re-arm the deferred EnableDraw for an actor created without one (see
    // EnemySpawnConfig.PacketSpawnEnableDraw).
    protected void RequestDraw()
    {
        pendingDraw = index != InvalidIndex;
        pendingDrawFrames = 0;
    }
    private float? visualHeight;

    private protected override Movement Movement => field ??= new Movement(this);

    internal override BattleChara* BattleCharaPtr => (BattleChara*)(index == InvalidIndex ? null : CharacterManager.Instance()->BattleCharas[index]);

    // pendingDraw=false for an actor the engine's own spawn handler created: it enables the
    // draw itself, as for a real server spawn.
    protected SimNpc(int index, Coordinates coordinates, bool pendingDraw = true) : base(coordinates)
    {
        this.index = index;
        this.pendingDraw = pendingDraw && index != InvalidIndex;
    }

    public override bool IsActive => index != InvalidIndex && BattleCharaPtr != null;

    // Elevates the native model independently of the actor's ground position.
    // Retained across asynchronous draw creation/rebuilds and reapplied after
    // movement: SetPosition alone is not a persistent visual suspension.
    public void SetVisualHeight(float height)
    {
        if (!float.IsFinite(height)) throw new ArgumentOutOfRangeException(nameof(height));
        visualHeight = height;
        ApplyVisualHeight();
    }

    private void ApplyVisualHeight()
    {
        var obj = BattleCharaPtr;
        if (obj == null || visualHeight is not { } height) return;
        obj->SetDrawOffset(0, height, 0);
    }

    public void SetModelState(byte value)
    {
        var chara = BattleCharaPtr;
        if (chara == null) return;
        TimelineFunctions.SetModelState(&chara->Timeline, value);
    }

    // Sampled for peers.
    public byte ModelState
    {
        get
        {
            var chara = BattleCharaPtr;
            return chara == null ? (byte)0 : chara->Timeline.ModelState;
        }
    }

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
        var chara = BattleCharaPtr;
        if (chara == null) return;
        chara->ModelContainer.ModeAttributeFlags = value;
        ReloadModel();
    }

    // Keep the native actor and its object-table entry alive during a size
    // transition. Despawning and reusing its slot in the same frame can leave
    // other object-table readers observing the old, terminated actor.
    public void SetScale(float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        var chara = BattleCharaPtr;
        if (chara == null) return;
        chara->Scale = scale;
        chara->HitboxRadius = scale * chara->ModelContainer.UnscaledRadius;
        ReloadModel();
    }

    // Forces a model rebuild via DisableDraw -> EnableDraw so the engine re-reads
    // ModeAttributeFlags and rebuilds the sub-meshes. The re-enable is deferred through the
    // pendingDraw path (the rebuild is async, gated on IsReadyToDraw). Only cycles draw when
    // the model is currently drawn: a hidden NPC keeps the written flags and applies them on
    // its next EnableDraw from the visibility system, so we never force it visible.
    protected void ReloadModel()
    {
        var obj = BattleCharaPtr;
        if (obj == null) return;
        var draw = obj->DrawObject;
        if (draw == null) return; //|| !draw->IsVisible) return;
        obj->DisableDraw();
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
    public int PlayedActionSeq { get; private set; }

    public void PlayAction(uint actionId, float animationLock = 0.6f)
    {
        PlayedActionId = actionId;
        PlayedActionAnimationLock = animationLock;
        PlayedActionSeq++;
        actionCast ??= new SimCast(this, Coordinates);
        actionCast.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0,
            animationTargetId: GameObjectId, actionTargetId: GameObjectId);
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);

        if (pendingDraw)
        {
            var obj = BattleCharaPtr;
            if (obj == null)
            {
                pendingDraw = false;
            }
            else if (obj->IsReadyToDraw())
            {
                obj->EnableDraw();
                pendingDraw = false;
                DiagnosticLog.Info($"[SimNpc] EnableDraw fired for goid {obj->GetGameObjectId()} at pos {Position}.");
            }
            else
            {
                pendingDrawFrames++;
                // Once, well past a normal model load, for an IsReadyToDraw stuck false.
                if (pendingDrawFrames == 300)
                    DiagnosticLog.Warn($"[SimNpc] still pendingDraw after {pendingDrawFrames} ticks, goid {obj->GetGameObjectId()} -- IsReadyToDraw() never returned true.");
            }
        }
        ApplyVisualHeight();
    }

    public override void Despawn()
    {
        if (index == InvalidIndex) return;
        base.Despawn();
        var obj = BattleCharaPtr;
        if (obj != null)
        {
            // DeleteObjectByIndex runs Character::Terminate, which walks all 14 sequencer slots;
            // a still-live one crashes on freed scheduler state (see QuiesceActionTimeline).
            QuiesceActionTimeline();
            obj->DisableDraw();

            var characterManager = CharacterManager.Instance();

            if (characterManager == null)
            {
                Plugin.Log.Warning("[SimNpc.Despawn] CharacterManager.Instance() was null.");
            }
            else
            {
                var packet = new DespawnCharacterPacket
                {
                    Index = (byte)index
                };

                PacketDispatcherPointers.HandleDespawnCharacterPacket(0, &packet);
            }
        }
        index = InvalidIndex;
        pendingDraw = false;
        visualHeight = null;
    }
}
