using System;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Tea.I2TemporalStasis;

public sealed class TeaI2TemporalStasisStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    // Null = random.
    public bool? BruteJusticeEast { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    public PerRoleSetting<StasisDebuff> Debuff { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        foreach (var debuff in Enum.GetValues<StasisDebuff>())
        {
            conflicts.AtMost(1, PerRole.All.Where(r => !r.IsDps() && Debuff[r] == debuff).ToList(), $"a support's {debuff}");
            conflicts.AtMost(1, PerRole.All.Where(r => r.IsDps() && Debuff[r] == debuff).ToList(), $"a DPS's {debuff}");
        }
        return conflicts;
    }
}
