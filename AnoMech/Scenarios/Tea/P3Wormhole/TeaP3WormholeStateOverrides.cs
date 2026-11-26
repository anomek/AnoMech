using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Tea.P3Wormhole;

public sealed class TeaP3WormholeStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    // Null = random.
    public bool? BruteJusticeNorthWest { get; set; }
    public bool? PuddlesMirrored { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    // 1..8.
    public PerRoleSetting<int> Number { get; set; } = new();
    // DPS only.
    public PerRoleSetting<bool> Enumeration { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        foreach (var number in Enumerable.Range(1, 8))
            conflicts.AtMost(1, PerRole.All.Where(r => Number[r] == number).ToList(), $"number {number}");
        conflicts.AtMost(2, PerRole.All.Where(r => Enumeration[r] == true).ToList(), "an Enumeration");
        var supportsAsking = PerRole.All.Where(r => !r.IsDps() && Enumeration[r] == true).ToList();
        if (supportsAsking.Count > 0)
            conflicts.Add($"{SettingsConflicts.Seats(supportsAsking)} asked for a DPS-only marker.");
        return conflicts;
    }
}
