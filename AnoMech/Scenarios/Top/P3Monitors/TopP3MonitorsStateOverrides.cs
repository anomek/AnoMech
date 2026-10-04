using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P3Monitors;

public enum MonitorCleave { Left, Right }

public sealed class TopP3MonitorsStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    // The side Final Omega's own monitor fires at; null = random.
    public MonitorCleave? BossSide { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    // Three players get a monitor.
    public PerRoleSetting<bool> Monitor { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;

        conflicts.AtMost(3, PerRole.All.Where(r => Monitor[r] == true).ToList(), "a monitor");
        conflicts.AtLeast(3, PerRole.All.Where(r => Monitor[r] == false).ToList(), 8, "a monitor");
        return conflicts;
    }
}
