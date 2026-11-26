using System;
using System.Reflection;
using System.Text.Json;
using AnoMech.Core;

namespace AnoMech.Scenarios;

// Carries the host's scenario overrides to every peer, so the knobs a peer's own code reads
// (IScenario.RunInstanceEvents, anything else outside the host-only Run) match what
// the host set. ScenarioSettingsSummary shows the same values; this is what makes them true.
//
// The JSON is host-controlled, so it is never deserialized into anything but the peer's own
// overrides type: a plain settings POCO of bools, nullable ints and enums. Enum members the
// build doesn't define are dropped rather than handed on to native calls.
public static class ScenarioSettingsSync
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = false };

    public static string? Serialize(object? overrides)
    {
        if (overrides == null) return null;
        try
        {
            return JsonSerializer.Serialize(overrides, overrides.GetType(), Options);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[ScenarioSettingsSync] Could not serialize {overrides.GetType().Name}: {e.Message}");
            return null;
        }
    }

    // Copies every writable property of `json` onto `target` in place: the scenario holds that
    // instance for the plugin's lifetime, so replacing it is not an option.
    public static bool Apply(object target, string json)
    {
        object? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(json, target.GetType(), Options);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[ScenarioSettingsSync] Could not read {target.GetType().Name} settings from the host: {e.Message}");
            return false;
        }
        if (parsed == null) return false;

        var applied = 0;
        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0) continue;
            object? value;
            try { value = property.GetValue(parsed); }
            catch { continue; }
            if (!IsAcceptable(property.PropertyType, value))
            {
                DiagnosticLog.Warn($"[ScenarioSettingsSync] {target.GetType().Name}.{property.Name} arrived as '{value}', which this build has no such value for -- keeping ours.");
                continue;
            }
            try
            {
                // A per-player setting carries eight seats of its own, none of them validated by
                // the enum check above.
                (value as IPerRoleSetting)?.Sanitize();
                property.SetValue(target, value);
                applied++;
            }
            catch (Exception e) { DiagnosticLog.Warn($"[ScenarioSettingsSync] Could not set {property.Name}: {e.Message}"); }
        }
        DiagnosticLog.Info($"[ScenarioSettingsSync] Applied {applied} setting(s) from the host to {target.GetType().Name}.");
        return true;
    }

    // System.Text.Json takes any number for an enum, defined or not.
    private static bool IsAcceptable(Type declared, object? value)
    {
        if (value == null) return true;
        var type = Nullable.GetUnderlyingType(declared) ?? declared;
        return !type.IsEnum || Enum.IsDefined(type, value);
    }
}
