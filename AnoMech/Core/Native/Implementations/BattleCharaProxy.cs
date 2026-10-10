using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using InteropGenerator.Runtime;

namespace AnoMech.Core.Native.Implementations;

internal sealed unsafe class BattleCharaProxy : IBattleCharaProxy
{
    private const uint NoTarget = 0xE0000000;

    // ABGR. Red, matching the game's own physical/unmitigated damage tint.
    private const uint DamageColorAbgr = 0xFF3030FFu;

    public static readonly BattleCharaProxy LocalPlayer = new(-1);

    public static BattleCharaProxy ForSlot(int slot) => new(slot);

    private BattleCharaProxy(int slot) => Slot = slot;

    public int Slot { get; }

    internal BattleChara* Ptr => Slot < 0
        ? (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0)
        : (BattleChara*)CharacterManager.Instance()->BattleCharas[Slot];

    public bool Exists => Ptr != null;

    public uint EntityId
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0u : obj->EntityId;
        }
    }

    public GameObjectId GameObjectId
    {
        get
        {
            var obj = Ptr;
            return obj == null ? default : obj->GetGameObjectId();
        }
    }

    public string Name
    {
        get
        {
            var obj = Ptr;
            return obj == null ? "" : ((GameObject*)obj)->GetName().ToString();
        }
    }

    public byte ClassJob
    {
        get
        {
            var obj = Ptr;
            return obj == null ? (byte)0 : obj->ClassJob;
        }
    }

    // ── Transform ────────────────────────────────────────────────────────────

    public Vector3 Position
    {
        get
        {
            var obj = Ptr;
            return obj == null ? default : (Vector3)obj->Position;
        }
    }

    public float Rotation
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0f : obj->Rotation;
        }
        set
        {
            var obj = Ptr;
            if (obj != null) obj->Rotation = value;
        }
    }

    public void SetPosition(Vector3 position)
    {
        var obj = Ptr;
        if (obj == null) return;
        obj->SetPosition(position.X, position.Y, position.Z);
        if (obj->DrawObject != null) obj->DrawObject->Object.Position = position;
    }

    public void SetRotation(float rotation)
    {
        var obj = Ptr;
        if (obj != null) obj->SetRotation(rotation);
    }

    public float HitboxRadius
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0f : obj->HitboxRadius;
        }
    }

    // ── Model ────────────────────────────────────────────────────────────────

    public bool IsReadyToDraw
    {
        get
        {
            var obj = Ptr;
            return obj != null && obj->IsReadyToDraw();
        }
    }

    public void EnableDraw()
    {
        var obj = Ptr;
        if (obj != null) obj->EnableDraw();
    }

    public void DisableDraw()
    {
        var obj = Ptr;
        if (obj != null) obj->DisableDraw();
    }

    public bool HasDrawObject
    {
        get
        {
            var obj = Ptr;
            return obj != null && obj->DrawObject != null;
        }
    }

    public bool IsDrawObjectVisible
    {
        get
        {
            var obj = Ptr;
            return obj != null && obj->DrawObject != null && obj->DrawObject->IsVisible;
        }
        set
        {
            var obj = Ptr;
            if (obj != null && obj->DrawObject != null) obj->DrawObject->IsVisible = value;
        }
    }

    public void SetModelHidden(bool hidden)
    {
        var obj = Ptr;
        if (obj == null) return;
        const VisibilityFlags bits = VisibilityFlags.Model | VisibilityFlags.Nameplate;
        var go = (GameObject*)obj;
        if (hidden) go->RenderFlags |= bits;
        else go->RenderFlags &= ~bits;
    }

    public byte ModelState
    {
        get
        {
            var obj = Ptr;
            return obj == null ? (byte)0 : obj->Timeline.ModelState;
        }
    }

    public bool? HasUnloadedModelSlot
    {
        get
        {
            var obj = Ptr;
            if (obj == null) return null;
            var draw = (CharacterBase*)obj->DrawObject;
            if (draw == null) return null;
            return Enumerable.Range(0, draw->SlotCount).Any(i => draw->ModelsSpan[i].Value == null);
        }
    }

    // ── Animation ────────────────────────────────────────────────────────────

    public void PlayActionTimeline(ushort timelineId, ushort loopId = 0, ushort? baseOverride = 0)
    {
        var obj = Ptr;
        if (obj == null) return;
        if (obj->Timeline.TimelineSequencer.Parent == null) return;
        if (baseOverride is { } value) obj->Timeline.BaseOverride = value;
        obj->Timeline.PlayActionTimeline(timelineId, loopId);
    }

    public void ResetActionTimeline()
    {
        var obj = Ptr;
        if (obj == null) return;
        obj->Timeline.BaseOverride = 0;
        obj->Timeline.ModelState = 0;
        obj->Timeline.AnimationState[0] = 0;
        obj->Timeline.AnimationState[1] = 0;
        // Sequencer ops need a live skeleton (Parent); guard before touching it.
        if (obj->Timeline.TimelineSequencer.Parent == null) return;
        obj->Timeline.TimelineSequencer.SetSlotTimeline(0, 0);
    }

    // Character::Terminate walks all 14 sequencer slots and crashes on a still-live one (a
    // mid-cast release animation occupies the UpperBody/Facial/Lips slots).
    public void QuiesceActionTimeline()
    {
        var obj = Ptr;
        if (obj == null) return;
        obj->Timeline.BaseOverride = 0;
        obj->Timeline.ModelState = 0;
        obj->Timeline.AnimationState[0] = 0;
        obj->Timeline.AnimationState[1] = 0;
        if (obj->Timeline.TimelineSequencer.Parent == null) return;
        for (uint slot = 0; slot < 14; slot++)
            obj->Timeline.TimelineSequencer.SetSlotTimeline(slot, 0);
    }

    public void SetBaseOverride(ushort timelineId)
    {
        var obj = Ptr;
        if (obj != null) obj->Timeline.BaseOverride = timelineId;
    }

    public ushort GetSlotTimeline(uint slot)
    {
        var obj = Ptr;
        if (obj == null || obj->Timeline.TimelineSequencer.Parent == null) return 0;
        return obj->Timeline.TimelineSequencer.GetSlotTimeline(slot);
    }

    public void SetSlotTimeline(uint slot, ushort timelineId)
    {
        var obj = Ptr;
        if (obj == null || obj->Timeline.TimelineSequencer.Parent == null) return;
        obj->Timeline.TimelineSequencer.SetSlotTimeline(slot, timelineId);
    }

    public void PlayTimelineDirect(ushort timelineId)
    {
        var obj = Ptr;
        if (obj == null || obj->Timeline.TimelineSequencer.Parent == null) return;
        obj->Timeline.TimelineSequencer.PlayTimeline(timelineId);
    }

    public ulong LoadBaseTimelineResources()
    {
        var obj = Ptr;
        if (obj == null || obj->Timeline.TimelineSequencer.Parent == null) return 0;
        var scheduler = obj->Timeline.TimelineSequencer.GetSchedulerTimeline(0);
        return scheduler == null ? 0 : scheduler->LoadTimelineResources();
    }


    // ── Casting ──────────────────────────────────────────────────────────────

    public bool IsCasting
    {
        get
        {
            var obj = Ptr;
            return obj != null && obj->CastInfo.IsCasting;
        }
    }

    public uint CastActionId
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0u : obj->CastInfo.ActionId;
        }
    }

    public float CurrentCastTime
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0f : obj->CastInfo.CurrentCastTime;
        }
    }

    public float TotalCastTime
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0f : obj->CastInfo.TotalCastTime;
        }
    }

    public void ClearCast()
    {
        var obj = Ptr;
        if (obj == null) return;
        obj->CastInfo.IsCasting = false;
        obj->CastInfo.ActionId = 0;
        obj->CastInfo.ActionType = 0;
    }

    public void ReceiveActorCast(ActorCastData cast)
    {
        var obj = Ptr;
        if (obj == null) return;
        var packet = new ActorCastPacket
        {
            ActionId = (ushort)cast.ActionId,
            ActionType = (byte)cast.ActionType,
            OmenDelay = (byte)(cast.OmenDelay * 10),
            ActionId_2 = cast.ActionId,
            CastTime = cast.CastTime,
            TargetEntityId = cast.Target?.ObjectId ?? NoTarget,
            RotationInt = MathUtil.QuantizeRotation(cast.Rotation),
            Interruptible = cast.Interruptible,
            BallistaEntityId = cast.Ballista?.ObjectId ?? NoTarget,
            PositionX = MathUtil.QuantizePosition(cast.Position.X),
            PositionY = MathUtil.QuantizePosition(cast.Position.Y),
            PositionZ = MathUtil.QuantizePosition(cast.Position.Z),
        };
        PacketDispatcherPointers.HandleActorCastPacket(obj->GetGameObjectId().ObjectId, &packet);
    }

    public void ReceiveActionEffect(ActionEffectData effect)
    {
        var obj = Ptr;
        if (obj == null) return;
        var noTarget = new GameObjectId { ObjectId = NoTarget, Type = 0 };
        var actionTarget = effect.ActionTarget ?? noTarget;
        var header = new ActionEffectHandler.Header
        {
            AnimationTargetId = effect.AnimationTarget ?? noTarget,
            ActionId = effect.ActionId,
            GlobalSequence = 0,
            AnimationLock = effect.AnimationLock,
            BallistaEntityId = effect.Ballista?.ObjectId ?? NoTarget,
            SourceSequence = 0,
            RotationInt = MathUtil.QuantizeRotation(effect.Rotation),
            SpellId = effect.SpellId,
            AnimationVariation = effect.AnimationVariation,
            ActionType = (byte)effect.ActionType,
            Flags = effect.Flags,
            NumTargets = (byte)(effect.ActionTarget == null ? 0 : 1),
        };
        var targetEffects = new ActionEffectHandler.TargetEffects();
        var position = effect.Position;
        ActionEffectHandler.Receive(obj->GetGameObjectId().ObjectId, (Character*)obj, &position, &header, &targetEffects, &actionTarget);
    }

    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
    {
        var obj = Ptr;
        if (obj == null) return;
        PacketDispatcher.HandleActorControlPacket(obj->EntityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, NoTarget, false);
    }

    // ── Combat state ─────────────────────────────────────────────────────────

    public uint Health
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0u : obj->Health;
        }
        set
        {
            var obj = Ptr;
            if (obj != null) obj->Health = value;
        }
    }

    public uint MaxHealth
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0u : obj->MaxHealth;
        }
        set
        {
            var obj = Ptr;
            if (obj != null) obj->MaxHealth = value;
        }
    }

    public void ApplyDeadState()
    {
        var obj = Ptr;
        if (obj == null) return;
        obj->Health = 0;
        obj->Mana = 0;
        obj->Mode = CharacterModes.Dead;
    }

    public void ClearShield()
    {
        var obj = Ptr;
        if (obj != null) obj->ShieldValue = 0;
    }

    public void SetTarget(GameObjectId target)
    {
        var obj = Ptr;
        if (obj != null) obj->TargetId = target;
    }

    public byte TargetableStatus
    {
        get
        {
            var obj = Ptr;
            return obj == null ? (byte)0 : (byte)obj->TargetableStatus;
        }
    }

    // ── Statuses ─────────────────────────────────────────────────────────────

    public void AddStatusInit(ushort statusId, ushort param, GameObjectId source = default)
        => Statuses.AddStatusInit((Character*)Ptr, statusId, param, source);

    public void ApplyStatus(ushort statusId, float remainingTime, ushort param, GameObjectId source = default)
        => Statuses.Apply((Character*)Ptr, statusId, remainingTime, param, source);

    public void RemoveStatus(ushort statusId, GameObjectId source = default)
        => Statuses.Remove((Character*)Ptr, statusId, source);

    // ── Effects ──────────────────────────────────────────────────────────────

    public IActorVfxProxy? AttachVfx(string path)
    {
        var obj = Ptr;
        if (string.IsNullOrEmpty(path) || obj == null) return null;
        var bytes = System.Text.Encoding.UTF8.GetBytes(path + "\0");
        VfxData* vfx;
        fixed (byte* p = bytes)
            vfx = VfxDataPointers.ActorVfxCreate(p, (GameObject*)obj, (GameObject*)obj, -1f, 0, 0, 0);
        return vfx == null ? null : new ActorVfxProxy(vfx);
    }

    public ushort GetTetherId(byte slot)
    {
        var obj = Ptr;
        return obj == null ? (ushort)0 : obj->Vfx.Tethers[slot].Id;
    }

    public void ShowFlyText(uint amount, string label, uint damageTypeIcon = 0)
    {
        var obj = Ptr;
        if (obj == null) return;
        // val1 is the number, text1 the label: putting the number in both prints it twice.
        Plugin.FlyText.AddFlyText(FlyTextKind.Damage, ((GameObject*)obj)->ObjectIndex, amount, 0,
            new SeString(new TextPayload(label)), new SeString(), DamageColorAbgr, 0, damageTypeIcon);
    }

    public void Despawn()
    {
        if (Slot < 0) return;
        var obj = Ptr;
        if (obj == null) return;
        // DeleteObjectByIndex runs Character::Terminate (see QuiesceActionTimeline).
        QuiesceActionTimeline();
        obj->DisableDraw();
        if (CharacterManager.Instance() == null)
        {
            Plugin.Log.Warning("[BattleCharaProxy.Despawn] CharacterManager.Instance() was null.");
            return;
        }
        var packet = new DespawnCharacterPacket { Index = (byte)Slot };
        PacketDispatcherPointers.HandleDespawnCharacterPacket(0, &packet);
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────

    public string DescribeDrawState()
    {
        var obj = Ptr;
        if (obj == null) return "no BattleChara";
        var go = (GameObject*)obj;
        var draw = obj->DrawObject;
        return $"DrawObject={(draw == null ? "null" : draw->IsVisible ? "visible" : "hidden")} "
            + $"RenderFlags={go->RenderFlags} ModelCharaId={obj->ModelContainer.ModelCharaId} "
            + $"VfxScale={go->VfxScale:F2} Height={go->Height:F2} Scale={go->Scale:F2} "
            + $"ObjectKind={obj->ObjectKind} SubKind={obj->BattleNpcSubKind} Mode={obj->Mode}/{obj->ModeParam} "
            + $"ModelSkeletonId={obj->ModelContainer.ModelSkeletonId} Race={obj->DrawData.CustomizeData.Race}";
    }

    // Which ActionTimeline id each sequencer slot holds, with its playback position, so a
    // timeline that ran out reads differently from one cut short.
    public string DescribeActionTimeline()
    {
        var obj = Ptr;
        if (obj == null) return "no BattleChara";
        if (obj->Timeline.TimelineSequencer.Parent == null) return "no sequencer";
        var slots = new System.Text.StringBuilder();
        for (uint slot = 0; slot < 14; slot++)
        {
            var id = obj->Timeline.TimelineSequencer.GetSlotTimeline(slot);
            if (id == 0) continue;
            slots.Append($"[{slot}]={id}");
            var scheduler = obj->Timeline.TimelineSequencer.GetSchedulerTimeline(slot);
            if (scheduler != null)
            {
                slots.Append($"@{scheduler->CurrentTimestamp:F2}");
                if (slot == 0)
                {
                    slots.Append($"({scheduler->ActionTimelineKey})");
                    // Base-slot internals: load state, group, resolved resource name and the
                    // sequencer's shadow id arrays.
                    var state = *(int*)((byte*)scheduler + 0x78);
                    slots.Append($"[state={state} group=0x{(nint)scheduler->OwningGroup:X}");
                    var resource = scheduler->SchedulerResource;
                    if (resource == null) slots.Append(" res=none");
                    else
                    {
                        var name = resource->Name.DataPointer != null ? ((CStringPointer)resource->Name.DataPointer).ToString() : "(inline)";
                        slots.Append($" res=\"{name}\" handle={(resource->Resource == null ? "none" : $"LoadState={resource->Resource->LoadState}")}");
                    }
                    slots.Append($" ids2/3/4={obj->Timeline.TimelineSequencer.TimelineIds2[0]}/{obj->Timeline.TimelineSequencer.TimelineIds3[0]}/{obj->Timeline.TimelineSequencer.TimelineIds4[0]}]");
                }
            }
            slots.Append(' ');
        }
        return $"slots {(slots.Length == 0 ? "(all empty)" : slots.ToString().TrimEnd())} slot0speed={obj->Timeline.TimelineSequencer.GetSlotSpeed(0):F2} BaseOverride={obj->Timeline.BaseOverride} Speed={obj->Timeline.OverallSpeed:F2} DrawObject={(obj->DrawObject == null ? "null" : obj->DrawObject->IsVisible ? "visible" : "hidden")} Mode={obj->Mode}/{obj->ModeParam} RenderFlags={((GameObject*)obj)->RenderFlags} Casting={obj->CastInfo.IsCasting} rot={obj->Rotation:F3}";
    }

    public string DescribeTimelineSlotIds()
    {
        var obj = Ptr;
        if (obj == null || obj->Timeline.TimelineSequencer.Parent == null) return "-";
        var ids = new System.Text.StringBuilder();
        for (uint slot = 0; slot < 14; slot++)
        {
            var id = obj->Timeline.TimelineSequencer.GetSlotTimeline(slot);
            if (id != 0) ids.Append($"[{slot}]={id} ");
        }
        return ids.Length == 0 ? "(all empty)" : ids.ToString();
    }

    // PerSlotStagingArea is the in-progress load record; its resource handle carries the file
    // path and the engine's own load/read/IO state, which says where a stuck slot stopped.
    public string DescribeModelSlots()
    {
        var obj = Ptr;
        if (obj == null) return "no BattleChara";
        var draw = (CharacterBase*)obj->DrawObject;
        if (draw == null) return "DrawObject null";
        var loaded = Enumerable.Range(0, draw->SlotCount).Select(i => draw->ModelsSpan[i].Value != null).ToList();
        var text = new System.Text.StringBuilder(
            $"SlotCount={draw->SlotCount} HasModelInSlotLoaded=0x{draw->HasModelInSlotLoaded:X} HasModelFilesInSlotLoaded=0x{draw->HasModelFilesInSlotLoaded:X} "
            + $"slots=[{string.Join(",", loaded.Select((l, i) => l ? $"{i}:loaded" : $"{i}:null"))}]");
        for (var i = 0; i < draw->SlotCount; i++)
        {
            string resolvedPath;
            try { resolvedPath = draw->ResolveMdlPath((uint)i); }
            catch (Exception ex) { resolvedPath = $"<ResolveMdlPath threw: {ex.Message}>"; }

            text.Append($"\n  slot {i}: ");
            if (draw->PerSlotStagingArea == null)
            {
                text.Append($"PerSlotStagingArea=null, ResolveMdlPath={resolvedPath}");
                continue;
            }
            var staging = draw->PerSlotStagingArea[i];
            if (staging.ModelResourceHandle == null)
            {
                text.Append($"staging.Flags={staging.Flags}, ModelResourceHandle=null, ResolveMdlPath={resolvedPath}");
                continue;
            }
            var rh = (ResourceHandle*)staging.ModelResourceHandle;
            text.Append($"staging.Flags={staging.Flags}, handle.FileName=\"{rh->FileName}\", LoadState={rh->LoadState}, ReadState={rh->ReadState}, OtherState={rh->OtherState}, LastIOResult={rh->LastIOResult}, RefCount={rh->RefCount}, ResolveMdlPath={resolvedPath}");
        }
        return text.ToString();
    }
}
