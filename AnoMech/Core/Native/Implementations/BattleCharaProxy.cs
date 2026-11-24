using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using InteropGenerator.Runtime;

namespace AnoMech.Core.Native.Implementations;

internal sealed unsafe class BattleCharaProxy : IBattleCharaProxy
{
    private const uint NoTarget = 0xE0000000;

    public static readonly BattleCharaProxy LocalPlayer = new(-1);

    public static BattleCharaProxy ForSlot(int slot) => new(slot);

    private BattleCharaProxy(int slot) => Slot = slot;

    public int Slot { get; }

    internal BattleChara* Ptr => PtrAt(Slot);

    // By slot, so a proxy wrapped by another IBattleCharaProxy (multiplayer's) reaches it too.
    internal static BattleChara* PtrAt(int slot) => slot < 0
        ? (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0)
        : (BattleChara*)CharacterManager.Instance()->BattleCharas[slot];

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
    }

    // Type 0 for the local player too. UNVERIFIED there: the server warps its own player with
    // types 25 and 5, which the client follows with camera and screen handling.
    public void SetPos(Vector3 position, float rotation)
    {
        var obj = Ptr;
        if (obj == null) return;
        var packet = new ActorSetPosPacket
        {
            RotationInt = MathUtil.QuantizeRotation(MathUtil.NormalizeRotation(rotation)),
            X = position.X,
            Y = position.Y,
            Z = position.Z,
        };
        PacketDispatcherPointers.HandleActorSetPosPacket(obj->EntityId, &packet);
    }

    public void Move(Vector3 position, float rotation, ActorMoveFlags flags, float speed)
    {
        var obj = Ptr;
        if (obj == null || Slot < 0) return;
        var packet = new ActorMovePacket
        {
            RotationInt = MathUtil.QuantizeRotation(MathUtil.NormalizeRotation(rotation)),
            MoveFlags = (byte)flags,
            State = (byte)((ushort)flags >> 8),
            // Tenths of a y/s, wrapping past 25.5 the way a knockback's does.
            Speed = (byte)((int)MathF.Round(speed * 10f) & 0xFF),
            PositionX = MathUtil.QuantizePosition(position.X),
            PositionY = MathUtil.QuantizePosition(position.Y),
            PositionZ = MathUtil.QuantizePosition(position.Z),
        };
        PacketDispatcherPointers.HandleActorMovePacket(obj->EntityId, &packet);
    }

    public void NpcKnockback(Vector3 position, float rotation, float durationSeconds)
    {
        var obj = Ptr;
        if (obj == null || Slot < 0) return;
        var packet = new NpcKnockbackPacket
        {
            RotationInt = MathUtil.QuantizeRotation(MathUtil.NormalizeRotation(rotation)),
            DurationSeconds = durationSeconds,
            PositionX = MathUtil.QuantizePosition(position.X),
            PositionY = MathUtil.QuantizePosition(position.Y),
            PositionZ = MathUtil.QuantizePosition(position.Z),
        };
        PacketDispatcherPointers.HandleNpcKnockbackPacket(obj->EntityId, &packet);
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
    }

    public byte ModelState
    {
        get
        {
            var obj = Ptr;
            return obj == null ? (byte)0 : obj->Timeline.ModelState;
        }
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

    // ActionEffect32 is the largest the server sends.
    private const int MaxEffectTargets = 32;
    private const int MaxEffectsPerTarget = 8;

    public uint ReceiveActionEffect(ActionEffectData effect)
    {
        var obj = Ptr;
        if (obj == null) return 0;
        var targets = effect.Targets ?? [];
        var count = Math.Min(targets.Count, MaxEffectTargets);
        var sequence = ActionEffects.NextGlobalSequence();
        var header = new ActionEffectHandler.Header
        {
            AnimationTargetId = effect.AnimationTarget ?? new GameObjectId { ObjectId = NoTarget, Type = 0 },
            ActionId = effect.ActionId,
            GlobalSequence = sequence,
            AnimationLock = effect.AnimationLock,
            BallistaEntityId = effect.Ballista?.ObjectId ?? NoTarget,
            SourceSequence = effect.SourceSequence,
            RotationInt = MathUtil.QuantizeRotation(effect.Rotation),
            SpellId = effect.SpellId,
            AnimationVariation = effect.AnimationVariation,
            ActionType = (byte)effect.ActionType,
            Flags = effect.Flags,
            NumTargets = (byte)count,
        };
        var targetEffects = stackalloc ActionEffectHandler.TargetEffects[Math.Max(count, 1)];
        var targetIds = stackalloc GameObjectId[Math.Max(count, 1)];
        targetEffects[0] = default;
        targetIds[0] = new GameObjectId { ObjectId = NoTarget, Type = 0 };
        for (var i = 0; i < count; i++)
        {
            targetIds[i] = targets[i].Id;
            targetEffects[i] = default;
            var entries = targets[i].Effects ?? [];
            for (var k = 0; k < entries.Count && k < MaxEffectsPerTarget; k++)
                targetEffects[i].Effects[k] = new ActionEffectHandler.Effect
                {
                    Type = entries[k].Type,
                    Param0 = entries[k].Param0,
                    Param1 = entries[k].Param1,
                    Param2 = entries[k].Param2,
                    Param3 = entries[k].Param3,
                    Param4 = entries[k].Param4,
                    Value = entries[k].Value,
                };
        }
        var position = effect.Position;
        ActionEffectHandler.Receive(obj->GetGameObjectId().ObjectId, (Character*)obj, &position, &header, targetEffects, targetIds);
        return sequence;
    }

    public void ReceiveEffectResult(uint globalSequence, byte targetIndex, uint health)
    {
        var obj = Ptr;
        if (obj == null) return;
        var entityId = ((GameObject*)obj)->EntityId;
        var packet = new EffectResultBasicPacket
        {
            Count = 1,
            GlobalSequence = globalSequence,
            EntityId = entityId,
            CurrentHP = health,
            TargetIndex = targetIndex,
        };
        PacketDispatcherPointers.HandleEffectResultBasicPacket(entityId, &packet, 0);
    }

    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
    {
        var obj = Ptr;
        if (obj == null) return;
        PacketDispatcher.HandleActorControlPacket(obj->EntityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, NoTarget, false);
    }

    public void ActorControlTarget(uint category, GameObjectId target, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0)
    {
        var obj = Ptr;
        if (obj == null) return;
        PacketDispatcher.HandleActorControlPacket(obj->EntityId, category, arg1, arg2, arg3, arg4, 0, 0, 0, 0, target, false);
    }

    // Within this many yalms of the target the approach counts as arrived. UNVERIFIED: retail's value.
    private const byte ForcedApproachRange = 1;

    // The controller holds one mode at a time: the face-only follow would replace the walk, so
    // the approach, which turns the player as it walks, goes out alone. Retail always ends the
    // previous one before starting the next.
    public void ForcedFollow(uint? targetEntityId)
    {
        PacketDispatcherPointers.HandleForcedFollowEndPacket(0);
        if (targetEntityId is not { } target) return;
        var packet = new ForcedApproachPacket { Range = ForcedApproachRange, TargetEntityId = target };
        PacketDispatcherPointers.HandleForcedApproachPacket(&packet);
    }

    public void StopForcedMove() => PacketDispatcherPointers.HandleForcedStopPacket(0);

    // ── Combat state ─────────────────────────────────────────────────────────

    public uint Health
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0u : obj->Health;
        }
    }

    public uint MaxHealth
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0u : obj->MaxHealth;
        }
    }

    public uint MaxMana
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0u : obj->MaxMana;
        }
    }

    public uint Mana
    {
        get
        {
            var obj = Ptr;
            return obj == null ? 0u : obj->Mana;
        }
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

    // Party members and enemies alike get the Double list retail sends to players; the handler
    // doesn't care which kind of character it is.
    public void ReceiveStatusList(IReadOnlyList<StatusSlot> statuses, uint health, uint mana, byte shield)
    {
        var obj = Ptr;
        if (obj == null) return;
        var packet = new StatusEffectListDoublePacket
        {
            ClassJob = obj->ClassJob,
            Level = obj->Level,
            Level_2 = obj->Level,
            CurrentHP = health,
            MaxHP = obj->MaxHealth,
            CurrentMP = (ushort)mana,
            MaxMP = (ushort)obj->MaxMana,
            DamageShield = shield,
        };
        var first = (StatusEffectListStatus*)((byte*)&packet + StatusEffectListDoublePacket.StatusesOffset);
        var second = (StatusEffectListStatus*)((byte*)&packet + StatusEffectListDoublePacket.ExtraStatusesOffset);
        for (var i = 0; i < statuses.Count && i < 2 * StatusEffectListDoublePacket.Capacity; i++)
        {
            var entry = i < StatusEffectListDoublePacket.Capacity ? &first[i] : &second[i - StatusEffectListDoublePacket.Capacity];
            entry->StatusId = statuses[i].StatusId;
            entry->Param = statuses[i].Param;
            entry->RemainingTime = statuses[i].RemainingTime;
            entry->SourceEntityId = statuses[i].SourceEntityId;
        }
        PacketDispatcherPointers.HandleStatusEffectListDoublePacket(((GameObject*)obj)->EntityId, &packet, 0);
    }

    // ── Effects ──────────────────────────────────────────────────────────────

    public ushort GetTetherId(byte slot)
    {
        var obj = Ptr;
        return obj == null ? (ushort)0 : obj->Vfx.Tethers[slot].Id;
    }

    // Sent even while the slot is still empty: the handler also cancels a spawn still queued for
    // the slot, so an actor despawned before it arrived never appears.
    public void Despawn()
    {
        if (Slot < 0) return;
        CharacterManagerHelper.Release(Slot);
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
