using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P1ProgramLoop;

public sealed class TopP1ProgramLoopStateOverrides
{
    // --- Per player: everyone has their own ---------------------------------------------
    // In Line 1..4; two seats carry each number.
    public PerRoleSetting<int> Number { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;

        foreach (var number in Enumerable.Range(1, 4))
            conflicts.AtMost(2, PerRole.All.Where(r => Number[r] == number).ToList(), $"In Line {number}");
        return conflicts;
    }
}
