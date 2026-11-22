using System.Collections.Generic;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P3UltimateRelativity;

// User-forced rolls for FruP3UltimateRelativityState; null leaves a field randomized.
public sealed class FruP3UltimateRelativityStateOverrides
{
    // Dark Blizzard on the long DPS (true) or the short support (false).
    public bool? DpsIce { get; set; }
    // Every role's debuff; two short DPS, a short, medium and two long supports, a medium and a long DPS.
    public IReadOnlyDictionary<PartyRole, RelativityDebuff>? Debuffs { get; set; }
    // Eighths of a turn the hourglass layout is turned.
    public int? North { get; set; }
    // Bit n: hourglass clock n sweeps clockwise.
    public int? ClockwiseMask { get; set; }
    // Unholy Darkness per fire wave; each must not hold that wave's fire.
    public IReadOnlyList<PartyRole>? Darkness { get; set; }
    public PartyRole? ShellCrusherTarget { get; set; }
}
