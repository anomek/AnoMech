using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P4CrystallizeTime;

// What a player can ask to hold: Ice, Water and Eruption on blue are one pick.
public enum CrystallizeDebuffChoice { RedIce, RedAero, BlueDarkness, BlueIceWaterEruption }

// User-forced rolls for FruP4CrystallizeTimeState; null leaves a field randomized.
public sealed class FruP4CrystallizeTimeStateOverrides
{
    public bool? SlowNorthwest { get; set; }
    public CrystallizeCorner? Corner { get; set; }

    // Every role's debuff, overriding PlayerDebuffs: two of each red, one of each blue.
    public IReadOnlyDictionary<PartyRole, CrystallizeDebuff>? Debuffs { get; set; }
    public IReadOnlyList<PartyRole>? Quietus { get; set; }
    public PartyRole? SpiritTakerTarget { get; set; }

    public PerRoleSetting<CrystallizeDebuffChoice> PlayerDebuffs { get; set; } = new();

    public static CrystallizeDebuff[] Matching(CrystallizeDebuffChoice choice) => choice switch
    {
        CrystallizeDebuffChoice.RedIce => [CrystallizeDebuff.RedIce],
        CrystallizeDebuffChoice.RedAero => [CrystallizeDebuff.RedAero],
        CrystallizeDebuffChoice.BlueDarkness => [CrystallizeDebuff.BlueDarkness],
        _ => [CrystallizeDebuff.BlueIce, CrystallizeDebuff.BlueWater, CrystallizeDebuff.BlueEruption],
    };

    public static string Label(CrystallizeDebuffChoice choice) => choice switch
    {
        CrystallizeDebuffChoice.RedIce => "Red + Ice",
        CrystallizeDebuffChoice.RedAero => "Red + Aero",
        CrystallizeDebuffChoice.BlueDarkness => "Blue + Darkness",
        _ => "Blue + Ice / Water / Eruption",
    };

    // Each pick has as many places as the deal has matching debuffs.
    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        foreach (var choice in Enum.GetValues<CrystallizeDebuffChoice>())
            conflicts.AtMost(FruP4CrystallizeTimeState.Dealt.Count(Matching(choice).Contains),
                PerRole.All.Where(r => PlayerDebuffs[r] == choice).ToList(), Label(choice));
        return conflicts;
    }
}
