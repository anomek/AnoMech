using System;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

public enum DefamationRot { Red, Blue }

public sealed class TopP3HelloWorldStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    // The rot colour carried by the defamations for the whole mechanic; null = random.
    public DefamationRot? DefamationColor { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    // The role for the first patch; two seats start in each.
    public PerRoleSetting<HelloWorldRole> Role { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;

        foreach (var role in Enum.GetValues<HelloWorldRole>())
            conflicts.AtMost(2, PerRole.All.Where(r => Role[r] == role).ToList(), $"a first-patch {role}");
        return conflicts;
    }
}
