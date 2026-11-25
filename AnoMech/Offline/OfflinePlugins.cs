using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnoMech.Core;
using Dalamud.Plugin;

namespace AnoMech.Offline;

// Other plugins while offline mode runs. The blacklisted ones are turned off with Dalamud's temporary
// disable, an override it keeps only in memory: a crash or a restart loads them again by itself, and
// returning to the title turns them back on.
internal static class OfflinePlugins
{
    // Internal names. Each logs in, travels or keeps records of the character on its own.
    private static readonly string[] Blacklist = ["AutoRetainer", "Lifestream", "Altoholic", "vsatisfy"];

    // Dalamud starts its plugin auto-update 20 s after it sees a login, which the offline player's
    // arrival is to it, and counts a client that isn't logged in as idle. Nothing public pauses it, so
    // its login time is kept unset while offline mode runs; a real login later starts it as usual.
    private static readonly (object Manager, FieldInfo LoginTime)? AutoUpdate = FindAutoUpdate();

    public static bool CanHoldAutoUpdate => AutoUpdate != null;

    // Display names of the blacklisted plugins loaded now.
    public static List<string> Loaded() => Blacklisted().Where(p => p.IsLoaded).Select(p => p.Name).ToList();

    public static void TurnOff()
    {
        var names = Blacklisted().Where(p => p.IsLoaded).Select(p => p.InternalName).ToArray();
        OfflineProcessState.TurnedOffPlugins = [.. OfflineProcessState.TurnedOffPlugins.Union(names)];
        foreach (var name in names) Run("/xldisableplugintemp", name);
    }

    // Only those still off: one turned on again from the plugin installer meanwhile is already loaded.
    public static void TurnBackOn()
    {
        var names = OfflineProcessState.TurnedOffPlugins;
        if (names.Length == 0) return;
        OfflineProcessState.TurnedOffPlugins = [];
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins.Where(p => !p.IsLoaded && names.Contains(p.InternalName)))
            Run("/xlenableplugintemp", plugin.InternalName);
    }

    public static void HoldAutoUpdate()
    {
        if (AutoUpdate is not { } autoUpdate || autoUpdate.LoginTime.GetValue(autoUpdate.Manager) == null) return;
        autoUpdate.LoginTime.SetValue(autoUpdate.Manager, null);
        DiagnosticLog.Info("[Offline] Held off Dalamud's plugin auto-update.");
    }

    private static IEnumerable<IExposedPlugin> Blacklisted()
        => Plugin.PluginInterface.InstalledPlugins.Where(p => Blacklist.Contains(p.InternalName, StringComparer.OrdinalIgnoreCase));

    // Dalamud queues these and works through them a frame at a time.
    private static void Run(string command, string internalName)
    {
        DiagnosticLog.Info($"[Offline] {command} {internalName}");
        try
        {
            Plugin.CommandManager.ProcessCommand($"{command} {internalName}");
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Offline] {command} {internalName} threw: {e.Message}");
        }
    }

    private static (object, FieldInfo)? FindAutoUpdate()
    {
        try
        {
            var dalamud = typeof(IDalamudPluginInterface).Assembly;
            var type = dalamud.GetType("Dalamud.Plugin.Internal.AutoUpdate.AutoUpdateManager");
            var loginTime = type?.GetField("loginTime", BindingFlags.Instance | BindingFlags.NonPublic);
            var get = type == null ? null : dalamud.GetType("Dalamud.Service`1")?.MakeGenericType(type).GetMethod("Get", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
            if (loginTime?.FieldType == typeof(DateTime?) && get?.Invoke(null, null) is { } manager) return (manager, loginTime);
            DiagnosticLog.Warn("[Offline] Dalamud's plugin auto-update wasn't found.");
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Offline] Dalamud's plugin auto-update wasn't found: {e.Message}");
        }
        return null;
    }
}
