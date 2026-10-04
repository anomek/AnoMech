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

    // Outside the engine's player/server-actor ranges and CreateCharacter's 0xE00000xx ids.
    private const uint PacketSpawnEntityIdBase = 0x4000FE00u;

    // A Character-type mesh won't build from Customize alone. PartyPresets' White Mage set,
    // not Graven Image's real gear.
    private static readonly (DrawDataContainer.EquipmentSlot Slot, uint ItemId)[] Type0PlaceholderEquipment =
    [
        (DrawDataContainer.EquipmentSlot.Head, 2902),
        (DrawDataContainer.EquipmentSlot.Body, 3225),
        (DrawDataContainer.EquipmentSlot.Hands, 3687),
        (DrawDataContainer.EquipmentSlot.Legs, 3463),
        (DrawDataContainer.EquipmentSlot.Feet, 3894),
    ];

    public IBattleCharaProxy LocalPlayer => BattleCharaProxy.LocalPlayer;

    public IBattleCharaProxy? SpawnBattleNpc(EnemySpawnConfig config, Placement placement)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return null;

        var bnpcSheet = Plugin.DataManager.GetExcelSheet<BNpcBase>();
        if (!bnpcSheet.TryGetRow(config.BNpcBaseId, out var bnpc))
        {
            Plugin.Log.Warning($"BNpcBase row {config.BNpcBaseId} (0x{config.BNpcBaseId:X}) not found");
            return null;
        }

        var modelCharaId = config.ModelCharaId != 0 ? config.ModelCharaId : bnpc.ModelChara.RowId;
        var modelCharaSheet = Plugin.DataManager.GetExcelSheet<ModelChara>();
        if (!modelCharaSheet.TryGetRow(modelCharaId, out var modelChara))
        {
            Plugin.Log.Warning($"ModelChara row {config.BNpcBaseId} (0x{config.BNpcBaseId:X}) not found");
            return null;
        }

        if (!CharacterManagerHelper.CreateCharacter(out var idx, out var obj)) return null;

        var gameObj = (GameObject*)obj;
        var chara = (BattleChara*)obj;
        // SetupBNpc populates ModelContainer (incl. ModeAttributeFlags) from BNpcBase and must
        // run before the overrides below. Skipped for a Type 0 row with a Customize: a PC-style
        // actor is Customize+equipment driven and SetupBNpc left it permanently un-rendered.
        // Gated on Customize, not Type 0 alone: the invisible Type 0 helpers rely on the
        // BattleNpc path loading no mesh, and routing them through the Pc path built a player
        // mesh out of the reused slot's stale CustomizeData.
        var pcStyle = modelChara.Type == 0 && config.Customize is not null;
        if (pcStyle)
        {
            chara->ObjectKind = ObjectKind.Pc;
            // Match SpawnDoppel field for field: a reused slot's stale skeleton id and equipment
            // compete with the engine's Race/Tribe resolution and half-load a broken mesh.
            chara->ModelContainer.ModelCharaId = 0;
            chara->ModelContainer.ModelSkeletonId = 0;
            chara->VfxScale = LalafellVfxScale;
            chara->Height = LalafellHeight;
            chara->Mode = CharacterModes.Normal;
            chara->ModeParam = 0;
        }
        else
        {
            chara->CharacterSetup.SetupBNpc(config.BNpcBaseId, config.NameId);
            chara->ObjectKind = ObjectKind.BattleNpc;
            chara->ModelContainer.ModelCharaId = (int)modelCharaId;
        }
        chara->Position = placement.Position;
        chara->SetRotation(MathUtil.NormalizeRotation(placement.Rotation));
        var scale = config.Scale > 0f ? config.Scale : bnpc.Scale;
        chara->Scale = scale;
        chara->SEPack = bnpc.SEPack;

        var nativeHitbox = true;

        // From Client::Game::Character::CharacterSetupContainer_SetupRaw
        switch (modelChara.Type)
        {
            // A Customize-less Type 0 (invisible helper) matches no case and keeps nativeHitbox.
            case 0 when pcStyle:
                // The engine resolves a PC skeleton from Race/Tribe once CustomizeData is
                // written; the hitbox is the doppels' fixed 0.5.
                if (config.Customize is { } customize)
                {
                    chara->DrawData.CustomizeData = customize;
                    // Zero every slot first: the reused slot's previous occupant leaves stale ids
                    // in the slots the placeholder set doesn't write.
                    foreach (DrawDataContainer.EquipmentSlot slot in Enum.GetValues<DrawDataContainer.EquipmentSlot>())
                        chara->DrawData.Equipment(slot).Value = 0;
                    // An all-zero CustomizeData is the real invisible helpers' own spawn data and
                    // must stay bare.
                    if (customize.Race != 0)
                    {
                        var itemSheet = Plugin.DataManager.GetExcelSheet<Item>();
                        foreach (var (slot, itemId) in Type0PlaceholderEquipment)
                            if (itemSheet.TryGetRow(itemId, out var item))
                                chara->DrawData.Equipment(slot).Value = item.ModelMain;
                    }
                }
                chara->HitboxRadius = config.HitboxRadius > 0f ? config.HitboxRadius : 0.5f;
                nativeHitbox = false;
                break;
            case 1:
                // TODO: This Type in the game's .exe is a bit complex, for now we just fallback to the previous solving method
                chara->HitboxRadius = config.HitboxRadius > 0f ? config.HitboxRadius : ResolveHitboxRadius(modelCharaId, scale);
                nativeHitbox = false;
                break;
            case 2:
                chara->ModelContainer.ModelSkeletonId = modelChara.Model + 10000;
                break;
            case 3:
                chara->ModelContainer.ModelSkeletonId = modelChara.Model;
                break;
        }

        if (nativeHitbox)
        {
            chara->ModelContainer.UnscaledRadius = ModelContainerPointers.CalculateUnscaledRadius(&chara->ModelContainer);
            chara->HitboxRadius = chara->Scale * chara->ModelContainer.UnscaledRadius; // From Client::Game::Character::ModelContainer_UpdateHitboxRadius
        }

        // Engine-resolved name (vfunc 6), same source as the nameplate. Empty for Type 0 (it
        // resolves from state SetupBNpc sets up), so fall back to the BNpcName sheet.
        var displayName = gameObj->GetName().ToString();
        if (string.IsNullOrEmpty(displayName)) displayName = BNpcName(config.NameId) ?? $"BNpc {config.BNpcBaseId:X}";
        GameObjectHelper.WriteName(gameObj, displayName);
        obj->RenderFlags = 0;

        chara->CharacterSetup.CopyFromCharacter((Character*)chara, CharacterSetupContainer.CopyFlags.None);

        chara->BattleNpcSubKind = BattleNpcSubKind.Combatant;
        chara->MaxHealth = 1_000_000;
        chara->Health = 1_000_000;
        chara->Battalion = 4;
        chara->IsHostile = true;
        chara->InCombat = true;
        chara->CombatTagType = 1;
        chara->CombatTaggerId = ((GameObject*)player.Address)->GetGameObjectId();
        chara->Mode = CharacterModes.Normal;
        chara->ModeParam = 0;
        if (config.InitialModeAttributeFlags is { } maf)
            chara->ModelContainer.ModeAttributeFlags = maf;
        // What the spawn handler's TimelineContainer weapon setter writes: bit 7 marks the state
        // as set, bit 6 is IsWeaponDrawn.
        if (config.WeaponDrawn)
            chara->Timeline.Flags3 |= 0xC0;
        chara->CastInfo.IsCasting = false;
        if (config.NameId != 0) chara->NameId = config.NameId;
        if (config.Level != 0) chara->Level = config.Level;

        DiagnosticLog.Info($"[BattleCharas.SpawnBattleNpc] BNpcBase {config.BNpcBaseId}: resolved modelCharaId={modelCharaId} (sheet default {bnpc.ModelChara.RowId}), scale={scale} (sheet default {bnpc.Scale}), hitboxRadius={chara->HitboxRadius} (nativeHitbox={nativeHitbox}), modelChara.Type={modelChara.Type}, ModelSkeletonId={chara->ModelContainer.ModelSkeletonId}, ModeAttributeFlags=0x{chara->ModelContainer.ModeAttributeFlags:X2} -- at index {idx}, goid {gameObj->GetGameObjectId()}, pos {placement.Position}, visible {config.IsVisible}.");
        return BattleCharaProxy.ForSlot(idx);
    }

    private static float ResolveHitboxRadius(uint modelCharaId, float scale)
    {
        const float DefaultUnscaledRadius = 0.5f;
        var sheet = Plugin.DataManager.GetExcelSheet<ModelChara>();
        var unscaled = DefaultUnscaledRadius;
        if (sheet.TryGetRow(modelCharaId, out var row) && row.Unknown0 > 0f)
            unscaled = row.Unknown0;
        return unscaled * scale;
    }

    // Only per-instance fields are patched: slot, position/rotation, the English name, and a
    // dangling owner reference.
    public IBattleCharaProxy? SpawnBattleNpcFromPacket(EnemySpawnConfig config, Placement placement, out uint entityId)
    {
        entityId = 0;
        if (config.NpcSpawnTemplate is not { } template) return null;
        if (template.Length != sizeof(SpawnNpcPacket))
        {
            DiagnosticLog.Warn($"[BattleCharas.SpawnBattleNpcFromPacket] template is {template.Length} bytes, expected {sizeof(SpawnNpcPacket)}.");
            return null;
        }
        if (CharacterManager.Instance() == null) return null;
        var idx = CharacterManagerHelper.FindFreeIndex();
        if (idx < 0)
        {
            DiagnosticLog.Warn("[BattleCharas.SpawnBattleNpcFromPacket] no free BattleChara slot.");
            return null;
        }

        var packet = new SpawnNpcPacket();
        fixed (byte* src = template) Buffer.MemoryCopy(src, &packet, sizeof(SpawnNpcPacket), template.Length);
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
