using System;
using System.Collections.Generic;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AnoMech.Core.Native.Implementations;

internal sealed unsafe class BattleCharas : IBattleCharas
{
    private const byte RaceLalafell = 3;
    private const byte TribePlainsfolk = 5;
    private const byte SexFemale = 1;
    private const byte BodyTypeAdult = 1;

    private const uint DoppelMaxHealth = 100_000;
    private const uint EnemyMaxHealth = 1_000_000;

    // Outside the engine's player/server-actor ranges and CreateCharacter's 0xE00000xx ids.
    private const uint PacketSpawnEntityIdBase = 0x4000FE00u;

    // In the player-kind id range, above the ids real characters carry.
    private const uint DoppelEntityIdBase = 0x10FFFE00u;

    public IBattleCharaProxy LocalPlayer => BattleCharaProxy.LocalPlayer;

    // Only per-instance fields are patched into the built packet: slot, position/rotation, the
    // English name, and a dangling owner reference.
    public IBattleCharaProxy? SpawnBattleNpcFromPacket(EnemySpawnConfig config, Placement placement, out uint entityId)
    {
        entityId = 0;
        if (BuildSpawnPacket(config) is not { } packet) return null;
        if (CharacterManager.Instance() == null) return null;
        var idx = CharacterManagerHelper.FindFreeIndex();
        if (idx < 0)
        {
            DiagnosticLog.Warn("[BattleCharas.SpawnBattleNpcFromPacket] no free BattleChara slot.");
            return null;
        }

        var id = PacketSpawnEntityIdBase + (uint)idx;
        packet.Common.SpawnIndex = (byte)idx;
        packet.Common.Position = placement.Position;
        packet.Common.Rotation = MathUtil.QuantizeRotation(MathUtil.NormalizeRotation(placement.Rotation));
        // The capture's owner reference names an actor that doesn't exist here.
        if (packet.Common.ObjectType is >= 0x40000000 and < 0xE0000000) packet.Common.ObjectType = 0xE0000000;

        var displayName = BNpcName(config.NameId) ?? $"BNpc {config.BNpcBaseId:X}";
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(displayName);
        var nameField = (byte*)&packet + 0x10 + 0x232;   // SpawnNpcPacket.Common (+0x10) . _name (+0x232, 32 bytes)
        for (var i = 0; i < 32; i++) nameField[i] = i < nameBytes.Length && i < 31 ? nameBytes[i] : (byte)0;

        DiagnosticLog.Info($"[BattleCharas.SpawnBattleNpcFromPacket] BNpcBase {packet.Common.BaseId} NameId {packet.Common.NameId} ModelChara {packet.Common.ModelChara} "
            + $"DisplayFlags=0x{packet.Common.DisplayFlags:X} Kind={packet.Common.ObjectKind}/{packet.Common.SubKind} Mode={packet.Common.CharacterMode} "
            + $"Level={packet.Common.Level} EventId=0x{packet.Common.EventId:X} LayoutId=0x{packet.Common.LayoutId:X} -> index {idx}, entity 0x{id:X}, "
            + $"pos {placement.Position}, rot {placement.Rotation:F3}.");
        try
        {
            PacketDispatcher.HandleSpawnNpcPacket(id, &packet);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[BattleCharas.SpawnBattleNpcFromPacket] HandleSpawnNpcPacket threw {e.GetType().Name}: {e.Message}");
            return null;
        }
        // The actor arrives a few frames later; hold the slot for it.
        CharacterManagerHelper.Reserve(idx);
        entityId = id;
        return BattleCharaProxy.ForSlot(idx);
    }

    // The values every captured server spawn of a combatant BNpc carries.
    private const byte SpawnCharacterDataFlags = 0x3;
    private const byte SpawnCharacterDataFlagsHelper = 0x1;
    private const byte SpawnLinkRange = 0x14;
    private const ushort SpawnResourcePoints = 10000;
    // Retail sends the BNpcBase row's own Battalion: 4 for every enemy, 0 for allies.
    private const byte HostileBattalion = 4;
    private const byte FriendlyBattalion = 0;

    private static SpawnNpcPacket? BuildSpawnPacket(EnemySpawnConfig config)
    {
        if (!Plugin.DataManager.GetExcelSheet<BNpcBase>().TryGetRow(config.BNpcBaseId, out var bnpc))
        {
            Plugin.Log.Warning($"BNpcBase row {config.BNpcBaseId} (0x{config.BNpcBaseId:X}) not found");
            return null;
        }
        var modelCharaId = config.ModelCharaId != 0 ? config.ModelCharaId : bnpc.ModelChara.RowId;
        var helper = config.Visibility == SpawnVisibility.InvisibleHelper;
        var packet = new SpawnNpcPacket
        {
            CharacterDataFlags = helper ? SpawnCharacterDataFlagsHelper : SpawnCharacterDataFlags,
            LinkRange = SpawnLinkRange,
        };
        ref var common = ref packet.Common;
        common.TargetId = 0xE0000000;
        common.OwnerId = 0xE0000000;
        common.TetherTargetId = 0xE0000000;
        common.BaseId = config.BNpcBaseId;
        common.NameId = config.NameId;
        common.LayoutId = config.LayoutId;
        // Retail binds every duty BNpc to the duty's director.
        common.EventId = InstanceContentDirectorHelper.DirectorEventId();
        common.MaxHealthPoints = EnemyMaxHealth;
        common.HealthPoints = EnemyMaxHealth;
        common.DisplayFlags = config.DisplayFlags ?? (uint)config.Visibility;
        common.MaxResourcePoints = helper ? (ushort)0 : SpawnResourcePoints;
        common.ResourcePoints = common.MaxResourcePoints;
        common.ModelChara = (ushort)modelCharaId;
        common.CharacterMode = CharacterModes.Normal;
        common.ObjectKind = ObjectKind.BattleNpc;
        common.SubKind = (byte)BattleNpcSubKind.Combatant;
        common.Battalion = config.IsHostile ? HostileBattalion : FriendlyBattalion;
        common.Level = config.Level;
        common.ModelAttributeFlags = config.InitialModeAttributeFlags ?? 0;
        common.ModelState = config.ModelState;
        common.ModelScaleId = config.ModelScale;
        common.AnimationState = config.AnimationState;
        if (config.Appearance is { } appearance) WriteAppearance(&packet.Common, appearance);
        if (config.Statuses is { } statuses) WriteStatuses(&packet.Common, statuses);
        return packet;
    }

    private const int SpawnStatusSlots = 30;

    private static void WriteStatuses(CommonSpawnData* common, IReadOnlyList<SpawnStatus> statuses)
    {
        var slots = (CommonSpawnData.StatusEffect*)((byte*)common + 0x88);
        for (var i = 0; i < Math.Min(SpawnStatusSlots, statuses.Count); i++)
        {
            slots[i].StatusId = statuses[i].StatusId;
            slots[i].Param = statuses[i].Param;
            slots[i].SourceObjectId = 0xE0000000;
        }
    }

    private const int EquipmentSlotCount = 10;
    private const int CustomizeLength = 26;

    private static void WriteAppearance(CommonSpawnData* common, NpcAppearance appearance)
    {
        common->MainhandWeaponModel.Value = appearance.MainHand;
        common->OffhandWeaponModel.Value = appearance.OffHand;
        var equipment = (uint*)((byte*)common + 0x1FC);
        for (var i = 0; i < Math.Min(EquipmentSlotCount, appearance.Equipment?.Length ?? 0); i++)
            equipment[i] = appearance.Equipment![i];
        var customize = (byte*)common + 0x252;
        for (var i = 0; i < Math.Min(CustomizeLength, appearance.Customize?.Length ?? 0); i++)
            customize[i] = appearance.Customize![i];
    }

    // The values every captured server spawn of another player carries.
    private const uint SpawnPlayerDisplayFlags = 0x40020;
    private const byte SpawnPlayerSubKind = 4;

    public IBattleCharaProxy? SpawnDoppel(PartyMemberPreset preset, byte level, Placement placement, out uint entityId)
    {
        entityId = 0;
        if (CharacterManager.Instance() == null) return null;
        var idx = CharacterManagerHelper.FindFreeIndex();
        if (idx < 0)
        {
            DiagnosticLog.Warn("[BattleCharas.SpawnDoppel] no free BattleChara slot.");
            return null;
        }

        var packet = new SpawnPlayerPacket();
        if (Plugin.ObjectTable.LocalPlayer is { } player)
        {
            var localChara = (Character*)player.Address;
            packet.HomeWorldId = localChara->HomeWorld;
            packet.CurrentWorldId = localChara->CurrentWorld;
        }
        ref var common = ref packet.Common;
        common.TargetId = 0xE0000000;
        common.OwnerId = 0xE0000000;
        common.TetherTargetId = 0xE0000000;
        common.MaxHealthPoints = DoppelMaxHealth;
        common.HealthPoints = DoppelMaxHealth;
        common.DisplayFlags = SpawnPlayerDisplayFlags;
        common.MaxResourcePoints = SpawnResourcePoints;
        common.ResourcePoints = SpawnResourcePoints;
        common.SpawnIndex = (byte)idx;
        common.CharacterMode = CharacterModes.Normal;
        common.ObjectKind = ObjectKind.Pc;
        common.SubKind = SpawnPlayerSubKind;
        common.Level = level;
        common.ClassJobId = preset.ClassJob;
        common.Position = placement.Position;
        common.Rotation = MathUtil.QuantizeRotation(MathUtil.NormalizeRotation(placement.Rotation));
        WriteCustomize(ref common.CustomizeData);
        WriteEquipment(&packet, preset, Plugin.DataManager.GetExcelSheet<Item>());
        // SpawnPlayerPacket.Common (+0x20) . _name (+0x232, 32 bytes)
        GameObjectHelper.WriteName((byte*)&packet + 0x20 + 0x232, 32, preset.Name);

        var id = DoppelEntityIdBase + (uint)idx;
        DiagnosticLog.Info($"[BattleCharas.SpawnDoppel] job {preset.ClassJob} level {level} -> index {idx}, entity 0x{id:X}, pos {placement.Position}, rot {placement.Rotation:F3}.");
        try
        {
            PacketDispatcher.HandleSpawnPlayerPacket(id, &packet);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[BattleCharas.SpawnDoppel] HandleSpawnPlayerPacket threw {e.GetType().Name}: {e.Message}");
            return null;
        }
        CharacterManagerHelper.Reserve(idx);
        entityId = id;
        return BattleCharaProxy.ForSlot(idx);
    }

    private static void WriteCustomize(ref CustomizeData c)
    {
        c.Race = RaceLalafell;
        c.Sex = SexFemale;
        c.BodyType = BodyTypeAdult;
        c.Height = 50;
        c.Tribe = TribePlainsfolk;
        c.Face = 1;
        c.Hairstyle = 1;
        c.SkinColor = 1;
        c.EyeColorRight = 1;
        c.EyeColorLeft = 1;
        c.HairColor = 1;
        c.HighlightsColor = 1;
        c.TattooColor = 1;
        c.Eyebrows = 1;
        c.Nose = 1;
        c.Jaw = 1;
        c.LipColorFurPattern = 1;
        c.MuscleMass = 50;
        c.TailShape = 1;
        c.BustSize = 50;
        c.FacePaintColor = 1;
    }

    private static void WriteEquipment(SpawnPlayerPacket* packet, PartyMemberPreset preset, ExcelSheet<Item> itemSheet)
    {
        ApplyItem(packet, DrawDataContainer.EquipmentSlot.Head, preset.Head, itemSheet);
        ApplyItem(packet, DrawDataContainer.EquipmentSlot.Body, preset.Body, itemSheet);
        ApplyItem(packet, DrawDataContainer.EquipmentSlot.Hands, preset.Hands, itemSheet);
        ApplyItem(packet, DrawDataContainer.EquipmentSlot.Legs, preset.Legs, itemSheet);
        ApplyItem(packet, DrawDataContainer.EquipmentSlot.Feet, preset.Feet, itemSheet);
    }

    private static void ApplyItem(SpawnPlayerPacket* packet, DrawDataContainer.EquipmentSlot slot, uint itemRowId, ExcelSheet<Item> itemSheet)
    {
        if (itemRowId == 0) return;
        if (!itemSheet.TryGetRow(itemRowId, out var item))
        {
            Plugin.Log.Warning($"BattleCharas: Item row {itemRowId} for slot {slot} not found");
            return;
        }
        // SpawnPlayerPacket.Common (+0x20) . _equipmentModelIds (+0x1FC, 10 x LegacyEquipmentModelId)
        var model = (LegacyEquipmentModelId*)((byte*)packet + 0x20 + 0x1FC) + (int)slot;
        model->Id = (ushort)item.ModelMain;
        model->Variant = (byte)(item.ModelMain >> 16);
    }

    private static string? BNpcName(uint nameId)
    {
        if (nameId == 0 || !Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcName>().TryGetRow(nameId, out var row)) return null;
        var name = row.Singular.ExtractText();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    public bool IsInCharacterManager(uint entityId)
    {
        var characterManager = CharacterManager.Instance();
        return characterManager != null && characterManager->LookupBattleCharaByEntityId(entityId) != null;
    }

    public void ReleaseSlot(int slot) => CharacterManagerHelper.Release(slot);
}
