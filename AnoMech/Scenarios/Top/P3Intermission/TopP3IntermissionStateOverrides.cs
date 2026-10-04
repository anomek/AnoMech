using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P3Intermission;

// The first three arm units: north, southeast and southwest, or northeast, south and northwest.
public enum FirstHands { North, South }

public enum SniperDebuff { Spread, Stack, None }

public sealed class TopP3IntermissionStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    // null = random.
    public FirstHands? Hands { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    public PerRoleSetting<SniperDebuff> Debuff { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;

        conflicts.AtMost(4, Wanting(SniperDebuff.Spread), "a spread");
        conflicts.AtMost(2, Wanting(SniperDebuff.Stack), "a stack");
        conflicts.AtMost(2, Wanting(SniperDebuff.None), "no debuff");
        return conflicts;
    }

    private PartyRole[] Wanting(SniperDebuff debuff) => PerRole.All.Where(r => Debuff[r] == debuff).ToArray();
}
