using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Scenarios.Umad.P3LimitCut;

public enum Wind { Headwind, Tailwind }

public sealed class UmadP3LimitCutStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    // 0..7 = S, SE, E, NE, N, NW, W, SW; null = random (all eight observed).
    public int? StartSpot { get; set; } = null;
    // null = an even coin flip.
    public bool? Clockwise { get; set; } = null;
    // Intercardinal the tanks hold both bosses at (1 = SE, 3 = NE, 5 = NW, 7 = SW); null = random.
    public int? BossSpot { get; set; } = null;
    // Who the bots send out as the Umbra Smash bait; null = the physical ranged. One seat is
    // named, but the choice is the fight's, not that player's own setting.
    public PartyRole? BaitRole { get; set; } = null;
    // Black Hole's Thunder III plan for the one set that follows the charges (see
    // ThunderIIIAssignment). The default is what the fight almost always does: the Chaos tank
    // steps onto Exdeath and invulns both.
    public ThunderIIIAssignment ThunderPlan { get; set; } = ThunderIIIAssignment.MtInvulnsBoth;

    // --- Per player: everyone has their own ---------------------------------------------
    // 1..8. Two seats can't hold the same number; the later one is left random and logged.
    public PerRoleSetting<int> Number { get; set; } = new();
    // Headwind/Tailwind. Forcing these shifts the empirical 4/4, 5/3, 3/5, 2/6 split.
    public PerRoleSetting<Wind> Wind { get; set; } = new();

    // The eight numbers are a permutation, so no two seats can share one. The wind split is
    // always 2, 3, 4 or 5 Headwinds, so forcing past either end is a split the fight never rolls.
    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;

        foreach (var number in Enumerable.Range(1, 8))
            conflicts.AtMost(1, PerRole.All.Where(r => Number[r] == number).ToList(), $"number {number}");

        var headwinds = PerRole.All.Where(r => Wind[r] == P3LimitCut.Wind.Headwind).ToList();
        var tailwinds = PerRole.All.Where(r => Wind[r] == P3LimitCut.Wind.Tailwind).ToList();
        conflicts.AtMost(5, headwinds, "Headwind");
        conflicts.AtMost(6, tailwinds, "Tailwind");
        return conflicts;
    }
}
