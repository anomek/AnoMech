using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Helpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Numerics;

namespace AnoMech.Core.Game.Party;

// Spawns and configures the eight party members around the scenario origin.
// Reads PartyPresets for the player's job (the player's own slot is null in
// the preset list — that role gets the SimPlayer reference instead), builds
// each non-player BattleChara as a Lalafell PC, and stores the resulting
// SimPartyNpc (or SimPlayer) into the supplied SimParty. Doppels are
// inserted into CharacterManager._battleCharas so row-click targeting and
// mouseover tooltips resolve through the engine's normal lookup path; the
// matching unregister lives in SimPartyNpc.Despawn. Scenarios are
// inn-gated upstream (Game.RunScenarioInternal). Game is the entry point —
// it computes the origin and delegates here.
internal static unsafe class PartyCreator
{
    private const byte RaceLalafell = 3;
    private const byte TribePlainsfolk = 5;
    private const byte SexFemale = 1;
    private const byte BodyTypeAdult = 1;

    // Status-loop-VFX size is driven by GameObject.Height (and possibly VfxScale). The engine
    // derives both from a real PC's Customize, but a client-spawned doppel leaves them at the 1.0
    // default — making status VFX render oversized vs the small Lalafell model. There's no cheap
    // way to recompute them (CalculateHeight is a different, larger value), so we hardcode the
    // Lalafell values observed on a live Lalafell player to match the hardcoded Customize below.
    private const float LalafellVfxScale = 0.4f;
    private const float LalafellHeight = 0.6f;

    private const float RingRadius = 2.5f;
    private const float RadiusJitter = 0.6f;
    private const float AngleJitter = 0.4f;

    private static readonly Random Rng = new();

    public static void Populate(SimParty party, SimPlayer player, uint playerJob, SimWorld world, PartyRole? roleOverride = null, bool solo = false)
    {
        var presets = roleOverride is { } skip
            ? PartyPresets.ForRole(skip)
            : PartyPresets.ForPlayerJob(playerJob);
        var itemSheet = Plugin.DataManager.GetExcelSheet<Item>();

        for (int i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            if (preset == null)
            {
                // Player's own job slot — wire the SimPlayer in directly so
                // Party.Get(role) returns a uniform SimCharacter.
                party.SetSlot((PartyRole)i, player);
                continue;
            }

            // Solo mode: only the player's own slot is filled — skip every doppel.
            if (solo) continue;

            var angle = (i / (float)presets.Count) * MathF.Tau
                        + ((float)Rng.NextDouble() - 0.5f) * AngleJitter;
            var distance = RingRadius + ((float)Rng.NextDouble() - 0.5f) * RadiusJitter;
            // Local ring around the scenario origin. Y stays at 0 (local floor);
            // Coordinates.ToGlobal lifts it to origin.Y at spawn.
            var localPos = new Vector3(MathF.Sin(angle) * distance, 0f, MathF.Cos(angle) * distance);
            var facingPlayer = MathF.Atan2(-localPos.X, -localPos.Z);

            var member = Spawn(preset, world, (PartyRole)i, new Placement(localPos, facingPlayer), itemSheet);
            if (member != null) party.SetSlot((PartyRole)i, member);
        }
    }

    private static SimPartyNpc? Spawn(PartyMemberPreset preset, SimWorld world, PartyRole role, Placement placement, ExcelSheet<Item> itemSheet)
    {
        if (!CharacterManagerHelper.CreateCharacter(out var idx, out var obj)) return null;

        var gameObj = (GameObject*)obj;
        var chara = (BattleChara*)obj;
        chara->ObjectKind = ObjectKind.Pc;
        chara->Position = world.Coordinates.ToGlobal(placement.Position);
        chara->Rotation = MathUtil.NormalizeRotation(placement.Rotation);
        chara->Scale = 1f;
        chara->VfxScale = LalafellVfxScale;
        chara->Height = LalafellHeight;
        chara->ModelContainer.ModelCharaId = 0;
        chara->ModelContainer.ModelSkeletonId = 0;

        WriteCustomize(chara);
        WriteEquipment(chara, preset, itemSheet);
        GameObjectHelper.WriteName(gameObj, preset.Name);
        obj->RenderFlags = 0;

        chara->TargetableStatus = ObjectTargetableFlags.IsTargetable;
        chara->HitboxRadius = 0.5f;
        chara->MaxHealth = 100_000;
        chara->Health = 100_000;
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

        Plugin.Log.Info($"PartyCreator: spawned {preset.Name} ({role}, job {preset.ClassJob}) at index {idx}");
        var member = new SimPartyNpc(idx, world.Coordinates, role, preset.ClassJob, preset.Name);
        // Seed the stored Position/Rotation to match the spawn placement so
        // anything reading SimCharacter.Position before the first Tick sees
        // the correct value (the Tick re-sync only kicks in next frame).
        member.SetPosition(placement);
        return member;
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
            Plugin.Log.Warning($"PartyCreator: Item row {itemRowId} for slot {slot} not found");
            return;
        }
        chara->DrawData.Equipment(slot).Value = item.ModelMain;
    }
}
