using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Tea.I1LimitCut;

public sealed class TeaI1LimitCutStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    // The first Hawk Blaster pair's axis: 0..3 = N/S, NE/SW, E/W, SE/NW; null = random.
    public int? FirstBlasterAxis { get; set; } = null;

    // Who takes a dead number's hit; null = a random living player, rolled at the hit.
    internal PartyRole? DeadNumbersHitGoesTo { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    // 1..8. Two seats can't hold the same number; the later one is left random and logged.
    public PerRoleSetting<int> Number { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        foreach (var number in Enumerable.Range(1, 8))
            conflicts.AtMost(1, PerRole.All.Where(r => Number[r] == number).ToList(), $"number {number}");
        return conflicts;
    }
}
