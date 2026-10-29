using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Tea.P3AlexanderPrime;

public enum InceptionDebuff { RestrainingOrder, SharedSentence, AggravatedAssault }

public sealed class TeaP3AlexanderPrimeStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    public P3Practice Practice { get; set; }
    // Null = random.
    public PartyRole? RestrainingOrderTank { get; set; }
    public bool? AlexanderPrimeNorth { get; set; }
    // 0..7: Alexander Prime's Sacrament spot, 22.5° + 45° × k clockwise from north.
    public int? SacramentSpot { get; set; }
    public bool? BruteJusticeNorthWest { get; set; }
    public bool? PuddlesMirrored { get; set; }

    // Headless runs pin the rest of the roll. Internal, so settings sync and the summary skip them.
    // The kill falls short: the enrage that check guards goes off.
    internal bool CruiseChaserSurvives { get; set; }
    internal bool AlexanderPrimeSurvives { get; set; }
    // Every player's damage over time ticks this far into each 3s.
    internal float? TickClock { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    // True = a Plasmasphere's tether, false = a Judgment Crystal.
    public PerRoleSetting<bool> Orb { get; set; } = new();
    // DPS only; the Restraining Order's tank is the fight-wide row.
    public PerRoleSetting<InceptionDebuff> Debuff { get; set; } = new();
    // 1..8.
    public PerRoleSetting<int> Number { get; set; } = new();
    // DPS only.
    public PerRoleSetting<bool> Enumeration { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        conflicts.AtMost(4, PerRole.All.Where(r => Orb[r] == true).ToList(), "a Plasmasphere");
        conflicts.AtMost(4, PerRole.All.Where(r => Orb[r] == false).ToList(), "a Judgment Crystal");
        conflicts.AtMost(1, PerRole.All.Where(r => Debuff[r] == InceptionDebuff.RestrainingOrder).ToList(), "the DPS Restraining Order");
        conflicts.AtMost(1, PerRole.All.Where(r => Debuff[r] == InceptionDebuff.SharedSentence).ToList(), "Shared Sentence");
        conflicts.AtMost(2, PerRole.All.Where(r => Debuff[r] == InceptionDebuff.AggravatedAssault).ToList(), "an Aggravated Assault");
        foreach (var number in Enumerable.Range(1, 8))
            conflicts.AtMost(1, PerRole.All.Where(r => Number[r] == number).ToList(), $"number {number}");
        conflicts.AtMost(2, PerRole.All.Where(r => Enumeration[r] == true).ToList(), "an Enumeration");
        var supportsAsking = PerRole.All.Where(r => !r.IsDps() && (Debuff[r] is not null || Enumeration[r] == true)).ToList();
        if (supportsAsking.Count > 0)
            conflicts.Add($"{SettingsConflicts.Seats(supportsAsking)} asked for a DPS-only debuff or marker.");
        return conflicts;
    }
}
