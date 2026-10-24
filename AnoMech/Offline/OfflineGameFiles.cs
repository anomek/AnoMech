using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using AnoMech.Core;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Common.Configuration;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using GearsetEntry = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.GearsetEntry;
using GearsetFlag = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.GearsetFlag;
using UserFileEvent = FFXIVClientStructs.FFXIV.Client.UI.Misc.UserFileManager.UserFileEvent;

namespace AnoMech.Offline;

public sealed class OfflineGearset
{
    public const int SlotCount = 14;
    public const int MateriaPerSlot = 5;

    public int Id { get; init; }
    public string Name { get; init; } = "";
    public byte ClassJobId { get; init; }
    public short ItemLevel { get; init; }
    public byte Flags { get; set; }
    // RaptureGearsetModule.GearsetItemIndex order, which is also the equipped container's; high
    // quality items carry the game's +1,000,000 offset.
    public uint[] Items { get; } = new uint[SlotCount];
    public uint[] Glamours { get; } = new uint[SlotCount];
    public byte[] Stain0 { get; } = new byte[SlotCount];
    public byte[] Stain1 { get; } = new byte[SlotCount];
    public ushort[] Materia { get; } = new ushort[SlotCount * MateriaPerSlot];
    public byte[] MateriaGrades { get; } = new byte[SlotCount * MateriaPerSlot];
    public ushort[] Glasses { get; } = new ushort[2];
}

internal sealed record OfflineLocalCharacter(ulong ContentId, string Folder, DateTime LastPlayed, IReadOnlyList<OfflineGearset> Gearsets, int CurrentGearset, string? GearsetProblem)
{
    public string Key { get; } = ContentId.ToString("X16", CultureInfo.InvariantCulture);
}

internal sealed record OfflineAppearance(string File, byte[] Customize, byte Voice, DateTime Saved, string Comment);

internal sealed record OfflineInnRoom(uint TerritoryId, string Name, Vector3 Position, float Rotation);

// The game's own local files and data, only ever read: each character's config folder, the saved
// appearance data and the inn rooms' entrances.
internal static unsafe class OfflineGameFiles
{
    private const string CharacterFolderPrefix = "FFXIV_CHR";
    private const int HeaderLength = 16;
    private const int MaxFileLength = 4 << 20;
    private const ushort AddonFileType = 0, MacroFileType = 1, HotbarFileType = 2, KeybindFileType = 3, GearsetFileType = 5;
    // From these versions on the game copies its gearset array straight from decoded offset 4.
    private const ushort FirstGearsetVersion = 0x6D, LastGearsetVersion = 0x6E;
    private const int GearsetEntriesOffset = 4, CurrentGearsetOffset = 1;
    private const int GearsetEntrySize = 0x1C4;
    // The file holds one entry past the last gearset, which the game uses as scratch.
    private const int LastGearsetIndex = 99;
    private const byte GearsetKey = 0x73;
    private const byte GearsetFlagFixedIn0x6E = 0x40;
    private const uint AppearanceMagic = 0x2013FF14;
    private const int AppearanceCustomizeOffset = 0x10, AppearanceVoiceOffset = 0x2A, AppearanceSavedOffset = 0x2C, AppearanceCommentOffset = 0x30;
    private const uint InnIntendedUse = 2;
    private static readonly string[] EntranceLayouts = ["planevent", "planmap"];

    private delegate byte UserFileLoadedDelegate(UserFileEvent* file, byte ok, byte* body, ushort version, uint length);
    private const string UserFileLoadedSignature = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B D9 41 0F B7 F9 8B 49 40";

    private static List<OfflineInnRoom>? innRooms;

    public static string GameVersion()
    {
        var framework = Framework.Instance();
        return framework == null ? "" : new string(framework->GameVersionString);
    }

    public static string UserFolder()
    {
        var framework = Framework.Instance();
        var path = framework == null ? null : framework->UserPathString;
        return string.IsNullOrWhiteSpace(path)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "FINAL FANTASY XIV - A Realm Reborn")
            : path;
    }

    // Shared access, so a read never keeps the game from writing or replacing its own files.
    private static byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaxFileLength) throw new InvalidDataException($"{Path.GetFileName(path)} is {stream.Length} bytes");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    // A user file is a 16-byte header (type, version, then the body length at 8) and the body.
    private static bool TryBody(byte[] file, ushort type, out ushort version, out byte[] body)
    {
        version = 0;
        body = [];
        if (file.Length < HeaderLength || BitConverter.ToUInt16(file, 0) != type) return false;
        version = BitConverter.ToUInt16(file, 2);
        var length = BitConverter.ToUInt32(file, 8);
        if (length == 0 || length > file.Length - HeaderLength) return false;
        body = file.AsSpan(HeaderLength, (int)length).ToArray();
        return true;
    }

    public static List<OfflineLocalCharacter> ScanCharacters()
    {
        var characters = new List<OfflineLocalCharacter>();
        try
        {
            var root = new DirectoryInfo(UserFolder());
            if (!root.Exists) return characters;
            foreach (var folder in root.EnumerateDirectories(CharacterFolderPrefix + "*"))
            {
                if (!ulong.TryParse(folder.Name.AsSpan(CharacterFolderPrefix.Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var contentId) || contentId == 0)
                    continue;
                try
                {
                    var gearsets = ReadGearsets(Path.Combine(folder.FullName, "GEARSET.DAT"), out var current, out var problem);
                    characters.Add(new OfflineLocalCharacter(contentId, folder.FullName, LastPlayed(folder), gearsets, current, problem));
                }
                catch (Exception e)
                {
                    DiagnosticLog.Warn($"[Offline] Skipped {folder.Name}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Offline] Couldn't list the character folders: {e.Message}");
        }
        characters.Sort((a, b) => b.LastPlayed.CompareTo(a.LastPlayed));
        return characters;
    }

    // The game rewrites a character's files whenever it logs out.
    private static DateTime LastPlayed(DirectoryInfo folder)
    {
        var latest = folder.LastWriteTimeUtc;
        foreach (var file in folder.EnumerateFiles("*.DAT"))
            if (file.LastWriteTimeUtc > latest) latest = file.LastWriteTimeUtc;
        return latest;
    }

    // The game decodes a gearset body by dropping its first byte and XORing the rest.
    private static List<OfflineGearset> ReadGearsets(string path, out int current, out string? problem)
    {
        current = -1;
        problem = null;
        var sets = new List<OfflineGearset>();
        byte[] file;
        try
        {
            file = ReadShared(path);
        }
        catch (FileNotFoundException)
        {
            problem = "no gearsets saved";
            return sets;
        }
        catch (Exception e)
        {
            problem = $"its gearsets couldn't be read ({e.Message})";
            return sets;
        }
        if (!TryBody(file, GearsetFileType, out var version, out var body) || body.Length < 1 + GearsetEntriesOffset)
        {
            problem = "its gearset file is damaged";
            return sets;
        }
        if (version is < FirstGearsetVersion or > LastGearsetVersion || sizeof(GearsetEntry) != GearsetEntrySize)
        {
            problem = $"its gearsets are from a game version offline mode can't read (file version {version:X})";
            return sets;
        }

        var decoded = new byte[body.Length - 1];
        for (var i = 0; i < decoded.Length; i++) decoded[i] = (byte)(body[i + 1] ^ GearsetKey);
        if (decoded[CurrentGearsetOffset] <= LastGearsetIndex) current = decoded[CurrentGearsetOffset];
        var count = Math.Min((decoded.Length - GearsetEntriesOffset) / GearsetEntrySize, LastGearsetIndex + 1);
        fixed (byte* data = decoded)
        {
            for (var i = 0; i < count; i++)
            {
                var entry = (GearsetEntry*)(data + GearsetEntriesOffset + i * GearsetEntrySize);
                if (!entry->Flags.HasFlag(GearsetFlag.Exists)) continue;
                var name = entry->Name;
                var end = name.IndexOf((byte)0);
                var set = new OfflineGearset
                {
                    Id = i,
                    Name = Encoding.UTF8.GetString(end >= 0 ? name[..end] : name),
                    ClassJobId = entry->ClassJob,
                    ItemLevel = entry->ItemLevel,
                    Flags = (byte)entry->Flags,
                };
                if (version < LastGearsetVersion) set.Flags &= unchecked((byte)~GearsetFlagFixedIn0x6E);
                for (var slot = 0; slot < OfflineGearset.SlotCount; slot++)
                {
                    ref var item = ref entry->Items[slot];
                    set.Items[slot] = item.ItemId;
                    set.Glamours[slot] = item.GlamourId;
                    set.Stain0[slot] = item.Stain0Id;
                    set.Stain1[slot] = item.Stain1Id;
                    for (var m = 0; m < OfflineGearset.MateriaPerSlot; m++)
                    {
                        set.Materia[slot * OfflineGearset.MateriaPerSlot + m] = item.Materia[m];
                        set.MateriaGrades[slot * OfflineGearset.MateriaPerSlot + m] = item.MateriaGrades[m];
                    }
                }
                set.Glasses[0] = entry->GlassesIds[0];
                set.Glasses[1] = entry->GlassesIds[1];
                sets.Add(set);
            }
        }
        return sets;
    }

    // FFXIV_CHARA_xx.dat: the appearance data the character creator saves.
    public static List<OfflineAppearance> ScanAppearances()
    {
        var appearances = new List<OfflineAppearance>();
        try
        {
            foreach (var file in new DirectoryInfo(UserFolder()).EnumerateFiles("FFXIV_CHARA_*.dat"))
            {
                if (!file.Extension.Equals(".dat", StringComparison.OrdinalIgnoreCase)) continue;
                byte[] bytes;
                try
                {
                    bytes = ReadShared(file.FullName);
                }
                catch
                {
                    continue;
                }
                if (bytes.Length < AppearanceCommentOffset || BitConverter.ToUInt32(bytes, 0) != AppearanceMagic) continue;
                var customize = bytes.AsSpan(AppearanceCustomizeOffset, OfflineLoadouts.CustomizeLength).ToArray();
                if (!OfflineLoadouts.IsKnownAppearance(customize)) continue;
                var comment = bytes.AsSpan(AppearanceCommentOffset);
                var end = comment.IndexOf((byte)0);
                if (end >= 0) comment = comment[..end];
                appearances.Add(new OfflineAppearance(
                    file.Name,
                    customize,
                    bytes[AppearanceVoiceOffset],
                    DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToUInt32(bytes, AppearanceSavedOffset)).UtcDateTime,
                    Encoding.UTF8.GetString(comment).Trim()));
            }
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Offline] Couldn't list the saved appearances: {e.Message}");
        }
        appearances.Sort((a, b) => b.Saved.CompareTo(a.Saved));
        return appearances;
    }

    // Where the innkeeper's warp puts a character: a PopRange in the room's own layout.
    public static IReadOnlyList<OfflineInnRoom> InnRooms()
    {
        if (innRooms != null) return innRooms;
        var rooms = new List<OfflineInnRoom>();
        try
        {
            var territories = Plugin.DataManager.GetExcelSheet<TerritoryType>();
            foreach (var warp in Plugin.DataManager.GetExcelSheet<Warp>())
            {
                if (territories.GetRowOrDefault(warp.TerritoryType.RowId) is not { TerritoryIntendedUse.RowId: InnIntendedUse } territory) continue;
                if (rooms.Any(r => r.TerritoryId == territory.RowId) || FindEntrance(territory, warp.PopRange.RowId) is not { } entrance) continue;
                var room = territory.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
                var city = territory.PlaceNameZone.ValueNullable?.Name.ExtractText() ?? "";
                rooms.Add(new OfflineInnRoom(territory.RowId, city.Length > 0 ? $"{room} ({city})" : room, entrance.Position, entrance.Rotation));
            }
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Offline] Couldn't read the inn rooms: {e.Message}");
        }
        rooms.Sort((a, b) => a.TerritoryId.CompareTo(b.TerritoryId));
        return innRooms = rooms;
    }

    private static (Vector3 Position, float Rotation)? FindEntrance(TerritoryType territory, uint instanceId)
    {
        var bg = territory.Bg.ExtractText();
        var slash = bg.LastIndexOf('/');
        if (slash < 0) return null;
        foreach (var layout in EntranceLayouts)
        {
            LgbFile? file;
            try
            {
                file = Plugin.DataManager.GetFile<LgbFile>($"bg/{bg[..slash]}/{layout}.lgb");
            }
            catch
            {
                continue;
            }
            if (file == null) continue;
            foreach (var layer in file.Layers)
                foreach (var instance in layer.InstanceObjects)
                {
                    if (instance.InstanceId != instanceId) continue;
                    var translation = instance.Transform.Translation;
                    var rotation = instance.Transform.Rotation;
                    // Stored as Euler angles; a half turn about X and Z together is a half turn about Y.
                    var yaw = MathF.Abs(rotation.X) > MathF.PI / 2 ? MathF.PI - rotation.Y : rotation.Y;
                    return (new Vector3(translation.X, translation.Y, translation.Z), MathF.IEEERemainder(yaw, 2 * MathF.PI));
                }
        }
        return null;
    }

    // Every per-character file module keeps CharacterContentId 0 until a real login stamps it, and
    // saves nothing to disk while it is 0.
    // Null once every per-character module is free for offline mode. A real logout hands them back
    // to no character, but a save it started runs asynchronously and holds its buffer in
    // TempDataPtr until the write is done; offline mode's file guard would refuse that write.
    public static string? FileModulesBusy()
    {
        if (UIModule.Instance() == null) return "The game's interface isn't ready yet.";
        foreach (var (_, address) in FileModules())
            if (((UserFileEvent*)address)->TempDataPtr != 0)
                return "The game is still saving your last session; offline mode can start in a moment.";
        foreach (var (name, address) in FileModules())
            if (name is "hotbars" or "gearsets" or "macros" && ((UserFileEvent*)address)->CharacterContentId != 0)
                return $"The game still has a character's {name} loaded. Restart the game to use offline mode.";
        return null;
    }

    private static List<(string Name, nint Module)> FileModules()
    {
        var modules = new List<(string, nint)>();
        void Add(string name, UserFileEvent* module)
        {
            if (module != null) modules.Add((name, (nint)module));
        }
        if (RaptureHotbarModule.Instance() is var hotbars && hotbars != null) Add("hotbars", &hotbars->UserFileEvent);
        if (RaptureGearsetModule.Instance() is var gearsets && gearsets != null) Add("gearsets", &gearsets->UserFileEvent);
        if (RaptureMacroModule.Instance() is var macros && macros != null) Add("macros", &macros->UserFileEvent);
        if (AddonConfig.Instance() is var addons && addons != null) Add("HUD layout", &addons->UserFileEvent);
        if (UIInputData.Instance() is var input && input != null) Add("keybinds", &input->UserFileEvent);
        if (ItemOrderModule.Instance() is var itemOrder && itemOrder != null) Add("item order", &itemOrder->UserFileEvent);
        if (ItemFinderModule.Instance() is var itemFinder && itemFinder != null) Add("item finder", &itemFinder->UserFileEvent);
        if (UiSavePackModule.Instance() is var savePack && savePack != null) Add("interface state", &savePack->UserFileEvent);
        if (LogFilterConfig.Instance() is var logFilter && logFilter != null) Add("log filters", &logFilter->UserFileEvent);
        if (AcquaintanceModule.Instance() is var acquaintances && acquaintances != null) Add("acquaintances", &acquaintances->UserFileEvent);
        return modules;
    }

    // What a character with no files on this PC starts with. After a real login the modules and
    // settings still hold that character's; the loader's missing-file path resets each module the
    // way the game does for a new character, and every setting goes back to its default.
    public static void LoadDefaults(Action<string> trace)
    {
        var framework = Framework.Instance();
        if (framework != null)
        {
            var config = &framework->SystemConfig.SystemConfigBase;
            ResetSettings("COMMON.DAT", &config->UiConfig, trace);
            ResetSettings("CONTROL0.DAT", &config->UiControlConfig, trace);
            ResetSettings("CONTROL1.DAT", &config->UiControlGamepadConfig, trace);
        }
        if (!CanLoadConfig(out var why))
        {
            trace($"[Offline] Hotbars, keybinds, HUD layout and macros left as they were ({why}).");
            return;
        }
        var loaded = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<UserFileLoadedDelegate>(Plugin.SigScanner.ScanText(UserFileLoadedSignature));
        foreach (var (name, address) in FileModules())
        {
            if (name is not ("hotbars" or "macros" or "HUD layout" or "keybinds")) continue;
            var module = (UserFileEvent*)address;
            if (module->CharacterContentId != 0)
            {
                trace($"[Offline] {name}: the module belongs to a character; left alone.");
                continue;
            }
            loaded(module, 0, null, 0, 0);
            module->HasChanges = false;
            module->IsSavePending = false;
            trace($"[Offline] Reset {name} to the game's defaults.");
        }
    }

    private static void ResetSettings(string name, ConfigBase* config, Action<string> trace)
    {
        if (config->ConfigEntry == null) return;
        var changed = 0;
        for (var i = 0u; i < config->ConfigCount; i++)
        {
            var entry = config->ConfigEntry + i;
            switch ((ConfigType)entry->Type)
            {
                case ConfigType.UInt when entry->Value.UInt != entry->Properties.UInt.DefaultValue:
                    entry->SetValueUInt(entry->Properties.UInt.DefaultValue);
                    changed++;
                    break;
                case ConfigType.Float when BitConverter.SingleToInt32Bits(entry->Value.Float) != BitConverter.SingleToInt32Bits(entry->Properties.Float.DefaultValue):
                    entry->SetValueFloat(entry->Properties.Float.DefaultValue);
                    changed++;
                    break;
            }
        }
        trace($"[Offline] {name} settings back to the game's defaults: {changed} changed.");
    }

    public static bool CanLoadConfig(out string why)
    {
        try
        {
            Plugin.SigScanner.ScanText(UserFileLoadedSignature);
            why = "";
            return true;
        }
        catch (Exception e)
        {
            why = $"the game's config loader wasn't found ({e.Message})";
            return false;
        }
    }

    // Offline only, into modules no character owns: each file goes to the module's own parser the
    // way a login's file read does, and a module that belongs to a character is left alone. None of
    // it is needed to play, so a file that fails is logged and skipped.
    public static void LoadConfig(string folder, Action<string> trace)
    {
        var loaded = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<UserFileLoadedDelegate>(Plugin.SigScanner.ScanText(UserFileLoadedSignature));
        var framework = Framework.Instance();
        if (framework != null)
        {
            var config = &framework->SystemConfig.SystemConfigBase;
            LoadSettings(folder, "COMMON.DAT", &config->UiConfig, trace);
            LoadSettings(folder, "CONTROL0.DAT", &config->UiControlConfig, trace);
            LoadSettings(folder, "CONTROL1.DAT", &config->UiControlGamepadConfig, trace);
        }
        if (AddonConfig.Instance() is var addons && addons != null) LoadUserFile(loaded, &addons->UserFileEvent, folder, "ADDON.DAT", AddonFileType, trace);
        if (UIInputData.Instance() is var input && input != null) LoadUserFile(loaded, &input->UserFileEvent, folder, "KEYBIND.DAT", KeybindFileType, trace);
        if (RaptureHotbarModule.Instance() is var hotbars && hotbars != null) LoadUserFile(loaded, &hotbars->UserFileEvent, folder, "HOTBAR.DAT", HotbarFileType, trace);
        if (RaptureMacroModule.Instance() is var macros && macros != null) LoadUserFile(loaded, &macros->UserFileEvent, folder, "MACRO.DAT", MacroFileType, trace);
    }

    // Parsers may decode the buffer in place, so they get a copy of the file's body.
    private static void LoadUserFile(UserFileLoadedDelegate loaded, UserFileEvent* module, string folder, string name, ushort type, Action<string> trace)
    {
        if (module->CharacterContentId != 0)
        {
            trace($"[Offline] {name}: the module belongs to a character; left alone.");
            return;
        }
        try
        {
            if (!TryBody(ReadShared(Path.Combine(folder, name)), type, out var version, out var body))
            {
                trace($"[Offline] {name}: not a file of this kind; not loaded.");
                return;
            }
            trace($"[Offline] Loading {name} (version {version:X}, {body.Length} bytes)...");
            fixed (byte* data = body) loaded(module, 1, data, version, (uint)body.Length);
            module->HasChanges = false;
            module->IsSavePending = false;
            trace($"[Offline] Loaded {name}.");
        }
        catch (Exception e)
        {
            trace($"[Offline] {name}: not loaded ({e.Message}).");
        }
    }

    // COMMON.DAT and CONTROL0/1.DAT are "Name<TAB>Value" lines under <section> headings, applied by
    // name through the game's own setters, which range-check and notify like the config window.
    private static void LoadSettings(string folder, string name, ConfigBase* config, Action<string> trace)
    {
        if (config->ConfigEntry == null) return;
        try
        {
            ApplySettings(Encoding.UTF8.GetString(ReadShared(Path.Combine(folder, name))), name, config, trace);
        }
        catch (Exception e)
        {
            trace($"[Offline] {name}: not loaded ({e.Message}).");
        }
    }

    // A few names belong to two entries (LockonDefaultZoom); the file lists entries in table order.
    private static void ApplySettings(string text, string name, ConfigBase* config, Action<string> trace)
    {
        var entries = new Dictionary<string, List<nint>>();
        for (var i = 0u; i < config->ConfigCount; i++)
        {
            var entry = config->ConfigEntry + i;
            if (!entry->Name.HasValue) continue;
            var key = entry->Name.ToString();
            if (!entries.TryGetValue(key, out var named)) entries[key] = named = [];
            named.Add((nint)entry);
        }
        var seen = new Dictionary<string, int>();
        var changed = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var tab = line.IndexOf('\t');
            if (tab <= 0 || !entries.TryGetValue(line[..tab], out var named)) continue;
            seen.TryGetValue(line[..tab], out var occurrence);
            seen[line[..tab]] = occurrence + 1;
            if (occurrence >= named.Count) continue;
            var entry = (ConfigEntry*)named[occurrence];
            var value = line[(tab + 1)..];
            if ((ConfigType)entry->Type == ConfigType.UInt
                && uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                && number != entry->Value.UInt && number >= entry->Properties.UInt.MinValue && number <= entry->Properties.UInt.MaxValue)
            {
                trace($"[Offline] {name} {line[..tab]} = {number}");
                entry->SetValueUInt(number);
                changed++;
            }
            else if ((ConfigType)entry->Type == ConfigType.Float
                     && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && float.IsFinite(real)
                     && real != entry->Value.Float && real >= entry->Properties.Float.MinValue && real <= entry->Properties.Float.MaxValue)
            {
                trace($"[Offline] {name} {line[..tab]} = {real}");
                entry->SetValueFloat(real);
                changed++;
            }
        }
        trace($"[Offline] Loaded {name}: {changed} settings changed.");
    }
}
