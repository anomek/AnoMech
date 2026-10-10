using System;
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

    // Status-loop-VFX size is driven by GameObject.Height (and possibly VfxScale). The engine
    // derives both from a real PC's Customize, but a client-spawned doppel leaves them at the 1.0
    // default — making status VFX render oversized vs the small Lalafell model. There's no cheap
    // way to recompute them (CalculateHeight is a different, larger value), so these are the
    // values observed on a live Lalafell player, matching the hardcoded Customize below.
    private const float LalafellVfxScale = 0.4f;
    private const float LalafellHeight = 0.6f;

    private const uint DoppelMaxHealth = 100_000;
    private const uint EnemyMaxHealth = 1_000_000;

    // Outside the engine's player/server-actor ranges and CreateCharacter's 0xE00000xx ids.
    private const uint PacketSpawnEntityIdBase = 0x4000FE00u;

    public IBattleCharaProxy LocalPlayer => BattleCharaProxy.LocalPlayer;

    // Without a template the packet is built from the config. Only per-instance fields are
    // patched: slot, position/rotation, the English name, and a dangling owner reference.
    public IBattleCharaProxy? SpawnBattleNpcFromPacket(EnemySpawnConfig config, Placement placement, out uint entityId)
    {
        entityId = 0;
        if (config.NpcSpawnTemplate is { Length: var length } && length != sizeof(SpawnNpcPacket))
        {
            DiagnosticLog.Warn($"[BattleCharas.SpawnBattleNpcFromPacket] template is {length} bytes, expected {sizeof(SpawnNpcPacket)}.");
            return null;
        }
        SpawnNpcPacket packet;
        if (config.NpcSpawnTemplate is { } template)
        {
            packet = new SpawnNpcPacket();
            fixed (byte* src = template) Buffer.MemoryCopy(src, &packet, sizeof(SpawnNpcPacket), template.Length);
        }
        else if (BuildSpawnPacket(config) is { } built) packet = built;
        else return null;
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
    private const byte SpawnBattalion = 4;

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
        common.MaxHealthPoints = EnemyMaxHealth;
        common.HealthPoints = EnemyMaxHealth;
        common.DisplayFlags = (uint)config.Visibility;
        common.MaxResourcePoints = helper ? (ushort)0 : SpawnResourcePoints;
        common.ResourcePoints = common.MaxResourcePoints;
        common.ModelChara = (ushort)modelCharaId;
        common.CharacterMode = CharacterModes.Normal;
        common.ObjectKind = ObjectKind.BattleNpc;
        common.SubKind = (byte)BattleNpcSubKind.Combatant;
        common.Battalion = SpawnBattalion;
        common.Level = config.Level;
        common.ModelAttributeFlags = config.InitialModeAttributeFlags ?? 0;
        return packet;
    }

    public IBattleCharaProxy? SpawnDoppel(PartyMemberPreset preset, Placement placement)
    {
        if (!CharacterManagerHelper.CreateCharacter(out var idx, out var obj)) return null;

        var gameObj = (GameObject*)obj;
        var chara = (BattleChara*)obj;
        chara->ObjectKind = ObjectKind.Pc;
        chara->Position = placement.Position;
        chara->Rotation = MathUtil.NormalizeRotation(placement.Rotation);
        chara->Scale = 1f;
        chara->VfxScale = LalafellVfxScale;
        chara->Height = LalafellHeight;
        chara->ModelContainer.ModelCharaId = 0;
        chara->ModelContainer.ModelSkeletonId = 0;

        WriteCustomize(chara);
        WriteEquipment(chara, preset, Plugin.DataManager.GetExcelSheet<Item>());
        GameObjectHelper.WriteName(gameObj, preset.Name);
        obj->RenderFlags = 0;

        chara->TargetableStatus = ObjectTargetableFlags.IsTargetable;
        chara->HitboxRadius = 0.5f;
        chara->MaxHealth = DoppelMaxHealth;
        chara->Health = DoppelMaxHealth;
        chara->MaxMana = 10_000;
        chara->Mana = 10_000;
        chara->Battalion = 0;
        chara->IsHostile = false;
        chara->InCombat = false;
        chara->IsPartyMember = true;
        chara->IsAllianceMember = false;
        chara->IsFriend = false;
        chara->IsOffhandDrawn = false;
        chara->Timeline.IsWeaponDrawn = false;
        chara->CastInfo.IsCasting = false;
        chara->Mode = CharacterModes.Normal;
        chara->ModeParam = 0;
        chara->ClassJob = preset.ClassJob;
        chara->Level = preset.Level;

        var player = Plugin.ObjectTable.LocalPlayer;
        if (player != null)
        {
            var localChara = (Character*)player.Address;
            chara->HomeWorld = localChara->HomeWorld;
            chara->CurrentWorld = localChara->CurrentWorld;
        }

        return BattleCharaProxy.ForSlot(idx);
    }

    private static void WriteCustomize(BattleChara* chara)
    {
        ref var c = ref chara->DrawData.CustomizeData;
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

    private static void WriteEquipment(BattleChara* chara, PartyMemberPreset preset, ExcelSheet<Item> itemSheet)
    {
        ApplyItem(chara, DrawDataContainer.EquipmentSlot.Head, preset.Head, itemSheet);
        ApplyItem(chara, DrawDataContainer.EquipmentSlot.Body, preset.Body, itemSheet);
        ApplyItem(chara, DrawDataContainer.EquipmentSlot.Hands, preset.Hands, itemSheet);
        ApplyItem(chara, DrawDataContainer.EquipmentSlot.Legs, preset.Legs, itemSheet);
        ApplyItem(chara, DrawDataContainer.EquipmentSlot.Feet, preset.Feet, itemSheet);
    }

    private static void ApplyItem(BattleChara* chara, DrawDataContainer.EquipmentSlot slot, uint itemRowId, ExcelSheet<Item> itemSheet)
    {
        if (itemRowId == 0) return;
        if (!itemSheet.TryGetRow(itemRowId, out var item))
        {
            Plugin.Log.Warning($"BattleCharas: Item row {itemRowId} for slot {slot} not found");
            return;
        }
        chara->DrawData.Equipment(slot).Value = item.ModelMain;
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

    public void NoteOrphan(int slot, uint entityId) => CharacterManagerHelper.NoteOrphan(slot, entityId);

    public void SweepOrphans() => CharacterManagerHelper.SweepOrphans();
}
