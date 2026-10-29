using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AnoMech.Core;

namespace AnoMech.Offline;

// What was picked for one character folder; the folder itself is only ever read.
public sealed class OfflineChoice
{
    public string Name { get; set; } = "";
    public int GearsetId { get; set; } = -1;
    public string? Appearance { get; set; }
}

internal sealed class OfflineStoreData
{
    public int Version { get; set; } = 2;
    public string? SelectedKey { get; set; }
    public Dictionary<string, OfflineChoice> Choices { get; set; } = [];
    public bool UseConfig { get; set; } = true;
}

// Offline choices live in their own file beside the plugin config, so nothing here can touch
// Configuration or its versioning.
internal sealed class OfflineStore
{
    private const string FileName = "offline.json";
    // Why offline mode last closed the game; the process ends right after, so the next load says it.
    private const string ClosedFileName = "offline-closed.txt";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string path;
    private OfflineStoreData data = new();
    private bool readOnly;

    public string? LoadProblem { get; private set; }

    public OfflineStore(string directory)
    {
        path = Path.Combine(directory, FileName);
        Load();
    }

    public string? SelectedKey => data.SelectedKey;

    public bool UseConfig
    {
        get => data.UseConfig;
        set
        {
            data.UseConfig = value;
            Save();
        }
    }

    public void Select(string key)
    {
        data.SelectedKey = key;
        Save();
    }

    public static void RecordClose(string why)
    {
        try
        {
            File.WriteAllText(Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, ClosedFileName), $"{DateTime.Now:g}: {why}");
        }
        catch
        {
            // Only the next load's explanation is lost.
        }
    }

    public string? TakeCloseRecord()
    {
        var closed = Path.Combine(Path.GetDirectoryName(path)!, ClosedFileName);
        try
        {
            if (!File.Exists(closed)) return null;
            var record = File.ReadAllText(closed).Trim();
            File.Delete(closed);
            return record.Length > 0 ? record : null;
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Offline] Reading {ClosedFileName} failed: {e.Message}");
            return null;
        }
    }

    public string NameOf(OfflineLocalCharacter character)
        => data.Choices.TryGetValue(character.Key, out var choice) && choice != null ? choice.Name : "";

    public OfflineChoice ChoiceFor(OfflineLocalCharacter character)
    {
        if (data.Choices.TryGetValue(character.Key, out var choice) && choice != null) return choice;
        choice = new OfflineChoice();
        data.Choices[character.Key] = choice;
        return choice;
    }

    private void Load()
    {
        if (!File.Exists(path)) return;
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            Disable($"{FileName} couldn't be read ({e.Message})");
            return;
        }

        OfflineStoreData? loaded;
        try
        {
            loaded = JsonSerializer.Deserialize<OfflineStoreData>(text, Json);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            var backup = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bad";
            try
            {
                File.Move(path, backup);
                LoadProblem = $"{FileName} was damaged and has been set aside as {Path.GetFileName(backup)}.";
                DiagnosticLog.Warn($"[Offline] {LoadProblem} ({e.Message})");
            }
            catch (Exception moveError)
            {
                Disable($"{FileName} is damaged and couldn't be set aside ({moveError.Message})");
            }
            return;
        }
        if (loaded == null) return;
        loaded.Choices ??= [];
        foreach (var choice in loaded.Choices.Values)
            if (choice != null)
                choice.Name = choice.Name is { Length: > 0 } name ? name[..Math.Min(name.Length, OfflineLoadouts.MaxNameLength)] : "";
        data = loaded;
    }

    // Saving over a file that couldn't be read would lose it.
    private void Disable(string reason)
    {
        readOnly = true;
        LoadProblem = $"{reason}. Offline choices won't be saved until the plugin reloads.";
        DiagnosticLog.Warn($"[Offline] {LoadProblem}");
    }

    public void Save()
    {
        if (readOnly) return;
        try
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(data, Json));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Offline] Saving {FileName} failed: {e.Message}");
        }
    }
}
