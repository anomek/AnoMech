using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Helpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
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

    // Fallback for every role, and for tank roles when IScenario.TankMaxHealth is null (see
    // Populate's tankMaxHealth param).
    private const uint DoppelMaxHealth = 100_000;

    private const float RingRadius = 2.5f;
    private const float RadiusJitter = 0.6f;
    private const float AngleJitter = 0.4f;

    private static readonly Random Rng = new();

    // networkRoles: slots held by other real participants, spawned as SimNetworkPuppet (position
    // from the network, not AiManager); takes priority over `solo`. networkSeats: their lobby
    // name and job for the nameplate and party list, each falling back to the role preset's.
    public static void Populate(SimParty party, SimPlayer player, uint playerJob, SimWorld world, uint? tankMaxHealth = null, PartyRole? roleOverride = null, bool solo = false, IReadOnlySet<PartyRole>? networkRoles = null, IReadOnlyDictionary<PartyRole, NetworkSeat>? networkSeats = null, byte? levelOverride = null)
    {
        var presets = roleOverride is { } skip
            ? PartyPresets.ForRole(skip, levelOverride)
            : PartyPresets.ForPlayerJob(playerJob, levelOverride);
        var itemSheet = Plugin.DataManager.GetExcelSheet<Item>();

        for (int i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var role = (PartyRole)i;
            if (preset == null)
            {
                // Player's own job slot — wire the SimPlayer in directly so
                // Party.Get(role) returns a uniform SimCharacter.
                party.SetSlot(role, player);
                if (role.IsTank() && tankMaxHealth is { } realHp) player.OverrideMaxHealthForTankRole(realHp);
                continue;
            }

            if (networkRoles != null && networkRoles.Contains(role))
            {
                var angle0 = (i / (float)presets.Count) * MathF.Tau;
                var localPos0 = new Vector3(MathF.Sin(angle0) * RingRadius, 0f, MathF.Cos(angle0) * RingRadius);
                var puppetPreset = networkSeats?.GetValueOrDefault(role) is { } seat
                    ? preset with
                    {
                        Name = seat.Name.Length > 0 ? seat.Name : preset.Name,
                        ClassJob = seat.ClassJob != 0 ? seat.ClassJob : preset.ClassJob,
                    }
                    : preset;
                var puppet = SpawnPuppet(puppetPreset, world, role, new Placement(localPos0, MathF.Atan2(-localPos0.X, -localPos0.Z)), itemSheet, tankMaxHealth);
                if (puppet != null) party.SetSlot(role, puppet);
                continue;
            }

            // Solo mode: only the player's own slot (and any network puppets) is filled.
            if (solo) continue;

            var angle = (i / (float)presets.Count) * MathF.Tau
                        + ((float)Rng.NextDouble() - 0.5f) * AngleJitter;
            var distance = RingRadius + ((float)Rng.NextDouble() - 0.5f) * RadiusJitter;
            // Local ring around the scenario origin. Y stays at 0 (local floor);
            // Coordinates.ToGlobal lifts it to origin.Y at spawn.
            var localPos = new Vector3(MathF.Sin(angle) * distance, 0f, MathF.Cos(angle) * distance);
            var facingPlayer = MathF.Atan2(-localPos.X, -localPos.Z);

            var member = Spawn(preset, world, role, new Placement(localPos, facingPlayer), itemSheet, tankMaxHealth);
            if (member != null) party.SetSlot(role, member);
        }
    }

    private static SimPartyNpc? Spawn(PartyMemberPreset preset, SimWorld world, PartyRole role, Placement placement, ExcelSheet<Item> itemSheet, uint? tankMaxHealth)
    {
        if (!SpawnNative(preset, world, role, placement, itemSheet, tankMaxHealth, out var idx)) return null;

        Plugin.Log.Info($"PartyCreator: spawned {preset.Name} ({role}, job {preset.ClassJob}) at index {idx}");
        var member = new SimPartyNpc(idx, world.Coordinates, role, preset.ClassJob, preset.Name);
        // Bots steer around the scenario's geometry; only doppels get the live
        // field (bosses/player/puppets keep ObstacleField.Empty and move in straight lines).
        member.Obstacles = world.Obstacles;
        // Seed the stored Position/Rotation to match the spawn placement so
        // anything reading SimCharacter.Position before the first Tick sees
        // the correct value (the Tick re-sync only kicks in next frame).
        member.SetPosition(placement);
        return member;
    }

    // Same visuals as a bot, but excluded from the Obstacles field only steering doppels need.
    private static SimNetworkPuppet? SpawnPuppet(PartyMemberPreset preset, SimWorld world, PartyRole role, Placement placement, ExcelSheet<Item> itemSheet, uint? tankMaxHealth)
    {
        if (!SpawnNative(preset, world, role, placement, itemSheet, tankMaxHealth, out var idx)) return null;

        Plugin.Log.Info($"PartyCreator: spawned network puppet {preset.Name} ({role}, job {preset.ClassJob}) at index {idx}");
        var puppet = new SimNetworkPuppet(idx, world.Coordinates, role, preset.ClassJob, preset.Name);
        puppet.SetPosition(placement);
        return puppet;
    }

    // Shared native BattleChara setup for both a bot doppel and a network puppet
    // — identical visuals, only the wrapper type and movement behaviour differ.
    private static bool SpawnNative(PartyMemberPreset preset, SimWorld world, PartyRole role, Placement placement, ExcelSheet<Item> itemSheet, uint? tankMaxHealth, out int idx)
    {
        idx = -1;
        if (!CharacterManagerHelper.CreateCharacter(out idx, out var obj)) return false;

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
        var maxHealth = role.IsTank() && tankMaxHealth is { } real ? real : DoppelMaxHealth;
        chara->MaxHealth = maxHealth;
        chara->Health = maxHealth;
        chara->MaxMana = 10_000;
        chara->Mana = 10_000;
        chara->Battalion = 0;
        chara->IsHostile = false;
        chara->InCombat = false;
        chara->IsPartyMember = true;
        chara->IsAllianceMember = false;
        chara->IsFriend = false;
        chara->LifeSkillContainer.IsOffhandDrawn = false;
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

        return true;
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
