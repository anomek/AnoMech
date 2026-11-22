using System.Collections.Generic;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P4CrystallizeTime;

// User-forced rolls for FruP4CrystallizeTimeState; null leaves a field randomized.
public sealed class FruP4CrystallizeTimeStateOverrides
{
    // The player gets one of these.
    public IReadOnlyList<CrystallizeDebuff>? PlayerDebuffs { get; set; }

    public bool? SlowNorthwest { get; set; }
    public CrystallizeCorner? Corner { get; set; }

    // Every role's debuff, overriding PlayerDebuffs: two of each red, one of each blue.
    public IReadOnlyDictionary<PartyRole, CrystallizeDebuff>? Debuffs { get; set; }
    public IReadOnlyList<PartyRole>? Quietus { get; set; }
    public PartyRole? SpiritTakerTarget { get; set; }
}
