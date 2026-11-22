using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

public enum LightRampantDuty { Tether, Bait }

// User-forced rolls for FruP2LightRampantState; null leaves a field randomized.
public sealed class FruP2LightRampantStateOverrides
{
    public bool? NorthOrbsFirst { get; set; }
    public bool? Pairs { get; set; }
    public bool? SupportPairs { get; set; }
    // The four who start with a Lightsteeped stack.
    public IReadOnlyList<PartyRole>? InitiallySteeped { get; set; }
    // Loop slot 0-5 of the first Powerful Light target; the second is the next slot.
    public int? WeightSlot { get; set; }

    // Bait = one of the two Luminous Hammer baiters; Tether = one of the six chained.
    public PerRoleSetting<LightRampantDuty> Duty { get; set; } = new();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        conflicts.AtMost(2, PerRole.All.Where(r => Duty[r] == LightRampantDuty.Bait).ToList(), "a Luminous Hammer bait");
        conflicts.AtMost(6, PerRole.All.Where(r => Duty[r] == LightRampantDuty.Tether).ToList(), "a tether");
        return conflicts;
    }
}
