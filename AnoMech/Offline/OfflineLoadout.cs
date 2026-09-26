using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

namespace AnoMech.Offline;

public sealed class OfflineLook
{
    // WeaponModelId.Value and EquipmentModelId.Value, in DrawDataContainer slot order.
    public ulong MainHand { get; set; }
    public ulong OffHand { get; set; }
    public ulong[] Equipment { get; } = new ulong[10];
    public ushort[] Glasses { get; } = new ushort[2];
    public bool HatHidden { get; init; }
    public bool VisorToggled { get; init; }
    public bool WeaponHidden { get; init; }
}

internal sealed record OfflineLoadout(
    OfflineLocalCharacter Character,
    string Name,
    byte ClassJobId,
    byte Level,
    uint MaxHp,
    uint MaxMp,
    int[] Attributes,
    OfflineLook Look,
    OfflineGearset Equipment,
    byte[] Customize,
    byte Voice,
    OfflineInnRoom Inn,
    string Summary);

internal static class OfflineLoadouts
{
    public const uint HqItemOffset = 1_000_000;
    public const int CustomizeLength = 26;
    public const int MaxNameLength = 20;
    public const string DefaultName = "Adventurer";
    // Stored as the chosen appearance for the built-in look; never a file name.
    public const string BuiltInAppearance = "default";
    // A plain Midlander, for a PC that never saved appearance data.
    private static readonly byte[] DefaultCustomize = [1, 0, 1, 50, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 50, 0, 0, 1, 0];
    private const byte DefaultVoice = 1;
    private const uint DefaultMaxMp = 10000;
    private const int AttributeCount = 74;
    private const int SkillSpeed = 45, SpellSpeed = 46;

    // RaptureGearsetModule.GearsetItemIndex -> DrawDataContainer.EquipmentSlot.
    private static readonly (int Item, int Slot)[] ArmorSlots =
        [(2, 0), (3, 1), (4, 2), (6, 3), (7, 4), (8, 5), (9, 6), (10, 7), (11, 8), (12, 9)];

    public static List<OfflineGearset> CombatGearsets(OfflineLocalCharacter character)
        => character.Gearsets.Where(g => JobRow(g.ClassJobId) is { Role: > 0 }).ToList();

    // The gearset picked for this character, else the one it last wore, else its first.
    public static OfflineGearset? ChosenGearset(OfflineLocalCharacter character, OfflineChoice choice)
    {
        var sets = CombatGearsets(character);
        return sets.Find(g => g.Id == choice.GearsetId) ?? sets.Find(g => g.Id == character.CurrentGearset) ?? sets.FirstOrDefault();
    }

    public static OfflineAppearance? ChosenAppearance(OfflineChoice choice, IReadOnlyList<OfflineAppearance> appearances)
        => choice.Appearance == BuiltInAppearance ? null : appearances.FirstOrDefault(a => a.File == choice.Appearance) ?? appearances.FirstOrDefault();

    public static string ChosenName(OfflineChoice choice)
    {
        var name = new string(choice.Name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return name.Length > 0 ? name[..Math.Min(name.Length, MaxNameLength)] : DefaultName;
    }

    public static OfflineLoadout? Resolve(OfflineLocalCharacter character, OfflineChoice choice, IReadOnlyList<OfflineAppearance> appearances,
                                          IReadOnlyList<OfflineInnRoom> inns, out string error)
    {
        error = "";
        if (character.Gearsets.Count == 0)
        {
            error = $"This character can't be used offline: {character.GearsetProblem ?? "it has no gearsets"}.";
            return null;
        }
        if (ChosenGearset(character, choice) is not { } set)
        {
            error = "This character has no gearsets for a combat job.";
            return null;
        }
        if (inns.Count == 0)
        {
            error = "The inn rooms couldn't be read from the game data.";
            return null;
        }

        var appearance = ChosenAppearance(choice, appearances);
        var equipment = Sanitize(set);
        var level = GearLevel(equipment);
        var maxHp = EstimatedMaxHp(level, JobRow(set.ClassJobId) is { Role: 1 });
        var inn = inns[0];
        var summary = $"{JobName(set.ClassJobId)} level {level}, i{set.ItemLevel}, about {maxHp:N0} HP, in {inn.Name}";
        return new OfflineLoadout(character, ChosenName(choice), set.ClassJobId, level, maxHp, DefaultMaxMp, Attributes(equipment, level), LookFromGearset(set),
                                  equipment, appearance?.Customize ?? DefaultCustomize, appearance?.Voice ?? DefaultVoice, inn, summary);
    }

    public static ClassJob? JobRow(byte classJobId)
        => classJobId == 0 ? null : Plugin.DataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(classJobId);

    public static string JobName(byte classJobId)
        => JobRow(classJobId)?.Name.ExtractText() is { Length: > 0 } name ? Capitalize(name) : $"Job {classJobId}";

    public static string JobAbbreviation(byte classJobId)
        => JobRow(classJobId)?.Abbreviation.ExtractText() is { Length: > 0 } abbreviation ? abbreviation : "?";

    private static string Capitalize(string name) => char.ToUpperInvariant(name[0]) + name[1..];

    public static bool IsKnownLevel(int level)
        => level > 0 && Plugin.DataManager.GetExcelSheet<ParamGrow>().GetRowOrDefault((uint)level) is { LevelModifier: > 0 };

    public static byte LatestExpansion()
        => (byte)Math.Min(Plugin.DataManager.GetExcelSheet<ExVersion>().Max(row => row.RowId), byte.MaxValue);

    // Race, sex and tribe pick the model files; a value the game has no row for would load none.
    // Each race owns tribes 2r-1 and 2r.
    public static bool IsKnownAppearance(byte[] customize)
        => customize.Length == CustomizeLength
           && customize[1] <= 1
           && (customize[4] + 1) / 2 == customize[0]
           && Plugin.DataManager.GetExcelSheet<Race>().GetRowOrDefault(customize[0]) is { RowId: > 0 }
           && Plugin.DataManager.GetExcelSheet<Tribe>().GetRowOrDefault(customize[4]) is { RowId: > 0 };

    public static string Describe(OfflineAppearance appearance)
    {
        var race = Plugin.DataManager.GetExcelSheet<Tribe>().GetRowOrDefault(appearance.Customize[4]) is { } tribe
            ? (appearance.Customize[1] == 0 ? tribe.Masculine : tribe.Feminine).ExtractText()
            : "?";
        var comment = appearance.Comment.Length > 0 ? $"\"{appearance.Comment}\" - " : "";
        return $"{comment}{race}, saved {appearance.Saved.ToLocalTime():d}";
    }

    // The level the gear was made for; the game's own tables bound it.
    private static byte GearLevel(OfflineGearset set)
    {
        var items = Plugin.DataManager.GetExcelSheet<Item>();
        var level = set.Items.Select(id => id % HqItemOffset).Where(id => id != 0)
                       .Select(id => items.GetRowOrDefault(id)?.LevelEquip ?? 0).DefaultIfEmpty((byte)0).Max();
        while (level > 1 && !IsKnownLevel(level)) level--;
        return IsKnownLevel(level) ? level : (byte)1;
    }

    // Only skill and spell speed change what the game does offline (the GCD), so only those carry
    // the level's base value; the rest is what the gear and materia give.
    private static int[] Attributes(OfflineGearset set, byte level)
    {
        var attributes = new int[AttributeCount];
        void Add(uint param, int value)
        {
            if (param > 0 && param < attributes.Length) attributes[param] += value;
        }

        var items = Plugin.DataManager.GetExcelSheet<Item>();
        var materia = Plugin.DataManager.GetExcelSheet<Materia>();
        for (var slot = 0; slot < OfflineGearset.SlotCount; slot++)
        {
            var id = set.Items[slot];
            if (id == 0 || items.GetRowOrDefault(id % HqItemOffset) is not { } item) continue;
            for (var i = 0; i < item.BaseParam.Count; i++) Add(item.BaseParam[i].RowId, item.BaseParamValue[i]);
            if (id >= HqItemOffset)
                for (var i = 0; i < item.BaseParamSpecial.Count; i++) Add(item.BaseParamSpecial[i].RowId, item.BaseParamValueSpecial[i]);
            for (var m = 0; m < OfflineGearset.MateriaPerSlot; m++)
            {
                var index = slot * OfflineGearset.MateriaPerSlot + m;
                if (set.Materia[index] != 0 && materia.GetRowOrDefault(set.Materia[index]) is { } row && set.MateriaGrades[index] < row.Value.Count)
                    Add(row.BaseParam.RowId, row.Value[set.MateriaGrades[index]]);
            }
        }
        var baseSpeed = Plugin.DataManager.GetExcelSheet<ParamGrow>().GetRowOrDefault(level)?.BaseSpeed ?? 0;
        attributes[SkillSpeed] += baseSpeed;
        attributes[SpellSpeed] += baseSpeed;
        return attributes;
    }

    private static uint EstimatedMaxHp(byte level, bool tank)
        => (uint)Math.Max(1000, (tank ? 300_000 : 150_000) * Math.Min((int)level, 100) / 100);

    private static byte Dye(ulong stain)
        => stain is > 0 and <= byte.MaxValue && Plugin.DataManager.GetExcelSheet<Stain>().GetRowOrDefault((uint)stain) != null ? (byte)stain : (byte)0;

    private static ushort Facewear(ushort glasses)
        => glasses != 0 && Plugin.DataManager.GetExcelSheet<Glasses>().GetRowOrDefault(glasses) != null ? glasses : (ushort)0;

    // The equipped container is read by game code and by item level sync, which index the item and
    // materia sheets directly: anything those have no row for is left out.
    private static OfflineGearset Sanitize(OfflineGearset set)
    {
        var items = Plugin.DataManager.GetExcelSheet<Item>();
        var materia = Plugin.DataManager.GetExcelSheet<Materia>();
        var clean = new OfflineGearset { Id = set.Id, Name = set.Name, ClassJobId = set.ClassJobId, ItemLevel = set.ItemLevel, Flags = set.Flags };
        for (var slot = 0; slot < OfflineGearset.SlotCount; slot++)
        {
            var id = set.Items[slot] % HqItemOffset;
            if (id == 0 || items.GetRowOrDefault(id) is not { EquipSlotCategory.RowId: > 0 }) continue;
            clean.Items[slot] = set.Items[slot];
            clean.Glamours[slot] = items.GetRowOrDefault(set.Glamours[slot]) is { EquipSlotCategory.RowId: > 0 } ? set.Glamours[slot] : 0;
            clean.Stain0[slot] = Dye(set.Stain0[slot]);
            clean.Stain1[slot] = Dye(set.Stain1[slot]);
            for (var m = 0; m < OfflineGearset.MateriaPerSlot; m++)
            {
                var index = slot * OfflineGearset.MateriaPerSlot + m;
                var grade = set.MateriaGrades[index];
                if (set.Materia[index] == 0 || materia.GetRowOrDefault(set.Materia[index]) is not { } row
                    || grade >= row.Value.Count || grade >= row.Item.Count || row.Item[grade].RowId == 0)
                    continue;
                clean.Materia[index] = set.Materia[index];
                clean.MateriaGrades[index] = grade;
            }
        }
        return clean;
    }

    private const byte HeadgearVisible = 0x08, WeaponsVisible = 0x10, VisorEnabled = 0x20;

    private static OfflineLook LookFromGearset(OfflineGearset set)
    {
        var look = new OfflineLook
        {
            HatHidden = (set.Flags & HeadgearVisible) == 0,
            VisorToggled = (set.Flags & VisorEnabled) != 0,
            WeaponHidden = (set.Flags & WeaponsVisible) == 0,
        };
        look.Glasses[0] = Facewear(set.Glasses[0]);
        look.Glasses[1] = Facewear(set.Glasses[1]);

        var main = Shown(set, 0);
        look.MainHand = main is { } m ? Weapon(m.ModelMain, Dye(set.Stain0[0]), Dye(set.Stain1[0])) : 0;
        var off = Shown(set, 1);
        look.OffHand = off is { } o
            ? Weapon(o.ModelMain, Dye(set.Stain0[1]), Dye(set.Stain1[1]))
            : main is { ModelSub: not 0 } dual ? Weapon(dual.ModelSub, Dye(set.Stain0[0]), Dye(set.Stain1[0])) : 0;

        foreach (var (item, slot) in ArmorSlots)
            look.Equipment[slot] = Shown(set, item) is { } row ? Armor(row.ModelMain, Dye(set.Stain0[item]), Dye(set.Stain1[item])) : 0;
        return look;
    }

    private static Item? Shown(OfflineGearset set, int index)
    {
        var id = set.Glamours[index] != 0 ? set.Glamours[index] : set.Items[index] % HqItemOffset;
        return id == 0 ? null : Plugin.DataManager.GetExcelSheet<Item>().GetRowOrDefault(id);
    }

    // WeaponModelId: Id, Type, Variant (16 bits each), then the two dyes.
    private static ulong Weapon(ulong model, byte stain0, byte stain1)
        => (model & 0xFFFF_FFFF_FFFF) | ((ulong)stain0 << 48) | ((ulong)stain1 << 56);

    // EquipmentModelId: Id (16 bits), Variant (8 bits), then the two dyes.
    private static ulong Armor(ulong model, byte stain0, byte stain1)
        => (model & 0xFF_FFFF) | ((ulong)stain0 << 24) | ((ulong)stain1 << 32);
}
