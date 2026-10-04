using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnoMech.Core;

namespace AnoMech.Multiplayer;

internal enum SimAssetKind { Action, BNpcBase, EObj, Lockon, Tether, Timeline, OmenPath, Layout, EventId, ModelChara, BattleTalk }

// A peer rebuilds the world from raw engine ids the host sends and runs no scenario logic, so
// it cannot judge whether an id belongs to the fight. Handing the engine an id loads that
// content's files, and bad asset loads crash on the file thread (see CLAUDE.md), so an
// unconstrained id field would let a malicious host make someone else's client load anything.
//
// The allowlist is harvested by reflection: every constant in a class named for an id kind
// (UmadConstants.ActionId, ...) plus any static field whose own name names one
// (BlasterLockons). Declaring a constant is registering it; there is no manifest to drift.
//
// A keyword must be the id-suffixed form, never a bare noun: the field name wins over the class,
// so a plain word also claims any descriptively-named constant containing it.
//
// StatusId is not enforced: its sources are open-ended (a peer's real Rampart, chart
// abilities, Sprint) and a status id only resolves an icon.
internal static class SimAssets
{
    private static readonly Dictionary<SimAssetKind, string[]> Keywords = new()
    {
        [SimAssetKind.Action] = ["ActionId"],
        [SimAssetKind.BNpcBase] = ["BNpcBase"],
        [SimAssetKind.EObj] = ["EObjId"],
        [SimAssetKind.Lockon] = ["Lockon"],
        [SimAssetKind.Tether] = ["TetherId"],
        [SimAssetKind.Timeline] = ["TimelineId"],
        [SimAssetKind.OmenPath] = ["VfxPath", "OmenPath"],
        [SimAssetKind.Layout] = ["LayoutId"],
        [SimAssetKind.EventId] = ["EventId"],
        [SimAssetKind.ModelChara] = ["ModelCharaId"],
        [SimAssetKind.BattleTalk] = ["BattleTalkId"],
    };

    private static Dictionary<SimAssetKind, HashSet<ulong>>? numbers;
    private static HashSet<string>? paths;
    private static readonly HashSet<(SimAssetKind, string)> warned = new();

    private static void EnsureHarvested()
    {
        if (numbers != null) return;
        numbers = Keywords.Keys.ToDictionary(k => k, _ => new HashSet<ulong>());
        paths = new HashSet<string>(StringComparer.Ordinal);

        // A type that fails to load must not blank the whole harvest.
        Type?[] types;
        try { types = typeof(SimAssets).Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types; DiagnosticLog.Warn($"[SimAssets] Some types failed to load: {e.Message}"); }

        foreach (var type in types)
        {
            if (type == null) continue;
            var typeKind = KindFor(type.Name);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                var kind = KindFor(field.Name) ?? typeKind;
                if (kind is not { } k) continue;
                object? value;
                try { value = field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null); }
                catch (Exception) { continue; }
                Absorb(k, value);
            }
        }

        DiagnosticLog.Info("[SimAssets] Harvested allowlist: "
            + string.Join(", ", numbers.Select(kv => $"{kv.Key}={kv.Value.Count}"))
            + $", OmenPath={paths.Count}.");
    }

    private static SimAssetKind? KindFor(string name)
    {
        foreach (var (kind, keywords) in Keywords)
            if (keywords.Any(k => name.Contains(k, StringComparison.Ordinal)))
                return kind;
        return null;
    }

    private static void Absorb(SimAssetKind kind, object? value)
    {
        switch (value)
        {
            case null: return;
            case string s when kind == SimAssetKind.OmenPath: paths!.Add(s); return;
            case string: return;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                var n = Convert.ToInt64(value);
                if (n >= 0) numbers![kind].Add((ulong)n);
                return;
            case IEnumerable items:
                foreach (var item in items) Absorb(kind, item);
                return;
        }
    }

    public static bool IsKnown(SimAssetKind kind, ulong id)
    {
        EnsureHarvested();
        return numbers![kind].Contains(id);
    }

    public static bool IsKnownOmenPath(string path)
    {
        EnsureHarvested();
        return paths!.Contains(path);
    }

    // Warned once per value: snapshots repeat at the frame rate.
    public static bool Allow(SimAssetKind kind, ulong id, string context)
    {
        if (IsKnown(kind, id)) return true;
        WarnOnce(kind, id.ToString(), context, $"0x{id:X} ({id})");
        return false;
    }

    public static bool AllowOmenPath(string path, string context)
    {
        if (IsKnownOmenPath(path)) return true;
        WarnOnce(SimAssetKind.OmenPath, path, context, $"'{path}'");
        return false;
    }

    private static void WarnOnce(SimAssetKind kind, string key, string context, string shown)
    {
        if (!warned.Add((kind, key))) return;
        DiagnosticLog.Warn($"[SimAssets] {context}: {kind} {shown} is not referenced anywhere in this build -- rejected. "
            + "If this is a real scenario asset, it needs to be a constant in a class or field named for its kind.");
    }

    // Host side: a harvest gap surfaces on the scenario developer's own machine rather than
    // as a missing visual on a peer.
    public static void WarnIfUnknown(SimAssetKind kind, ulong id, string context)
    {
        if (id == 0 || IsKnown(kind, id)) return;
        WarnOnce(kind, id.ToString(), $"Host: {context}", $"0x{id:X} ({id})");
    }

    public static void WarnIfUnknownPath(string path, string context)
    {
        if (string.IsNullOrEmpty(path) || IsKnownOmenPath(path)) return;
        WarnOnce(SimAssetKind.OmenPath, path, $"Host: {context}", $"'{path}'");
    }
}
