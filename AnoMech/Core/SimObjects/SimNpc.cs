using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native;
using AnoMech.Pointers;
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

    private protected override Movement Movement => field ??= new Movement(this);
    
    internal override BattleChara* BattleCharaPtr => (BattleChara*)(index == InvalidIndex ? null : CharacterManager.Instance()->BattleCharas[index]);

    protected SimNpc(int index, Coordinates coordinates) : base(coordinates)
    {
        this.index = index;
        pendingDraw = index != InvalidIndex;
    }

    public override bool IsActive => index != InvalidIndex && BattleCharaPtr != null;


    public void SetModelState(byte value)
    {
        var chara = BattleCharaPtr;
        if (chara == null) return;
        TimelineFunctions.SetModelState(&chara->Timeline, value);
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

    // Forces a model rebuild via DisableDraw -> EnableDraw so the engine re-reads
    // ModeAttributeFlags and rebuilds the sub-meshes. The re-enable is deferred through the
    // pendingDraw path (the rebuild is async, gated on IsReadyToDraw). Only cycles draw when
    // the model is currently drawn: a hidden NPC keeps the written flags and applies them on
    // its next EnableDraw from the visibility system, so we never force it visible.
    private void ReloadModel()
    {
        var obj = BattleCharaPtr;
        if (obj == null) return;
        var draw = obj->DrawObject;
        if (draw == null) return; //|| !draw->IsVisible) return;
        obj->DisableDraw();
        pendingDraw = true;
    }

    public uint EntityId
    {
        get
        {
            var obj = BattleCharaPtr;
            return obj == null ? 0u : obj->EntityId;
        }
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);

        if (pendingDraw)
        {
            var obj = BattleCharaPtr;
            if (obj == null) { pendingDraw = false; }
            else if (obj->IsReadyToDraw())
            {
                obj->EnableDraw();
                pendingDraw = false;
            }
        }
    }

    public override void Despawn()
    {
        if (index == InvalidIndex) return;
        base.Despawn(); 
        var obj = BattleCharaPtr;
        if (obj != null)
        {
            // Quiesce the action timeline before deletion: DeleteObjectByIndex runs the native
            // Character::Terminate, whose scheduler teardown walks all 14 ActionTimelineSequencer
            // slots and calls TimelineGroup::PlayAction on each. A still-live timeline there crashes
            // on freed scheduler state (C0000005 at TimelineGroup.PlayAction). Clearing only slot 0
            // is not enough for a mid-cast / mid-release-animation boss (the release animation
            // occupies UpperBody/Facial/Lips slots), so quiesce every slot here. DisableDraw alone
            // does not close this window either. See crash dumps 20260529_193455 and 20260603_221355.
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
    }
}
