using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using AnoMech.Scenarios.Umad;

namespace AnoMech.Scenarios;

// Every captured NpcSpawn body by the name a spawn travels to peers under
// (EnemyState.NpcSpawnTemplate). Resolved by name on receipt, never from wire bytes.
internal static class NpcSpawnTemplates
{
    private static readonly Dictionary<string, byte[]> ByName = Merge(UmadRealPackets.NpcSpawnTemplates);

    public static bool TryGet(string name, [NotNullWhen(true)] out byte[]? template) => ByName.TryGetValue(name, out template);

    public static string? NameOf(byte[]? template)
    {
        if (template == null) return null;
        foreach (var (name, bytes) in ByName)
            if (ReferenceEquals(bytes, template)) return name;
        return null;
    }

    private static Dictionary<string, byte[]> Merge(params IReadOnlyDictionary<string, byte[]>[] families)
    {
        var merged = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var family in families)
            foreach (var (name, bytes) in family)
                merged.Add(name, bytes);
        return merged;
    }
}
