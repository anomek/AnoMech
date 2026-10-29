using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P3UltimateRelativity;

// Which wave a player's fire goes off in, or would for the one holding Dark Blizzard.
public enum RelativityFire { Short, Medium, Long }

// User-forced rolls for FruP3UltimateRelativityState; null leaves a field randomized.
public sealed class FruP3UltimateRelativityStateOverrides
{
    // Dark Blizzard on the long DPS (true) or the short support (false).
    public bool? DpsIce { get; set; }
    // Every role's debuff; two short DPS, a short, medium and two long supports, a medium and a long DPS.
    // Overrides Fire.
    public IReadOnlyDictionary<PartyRole, RelativityDebuff>? Debuffs { get; set; }
    // Eighths of a turn the hourglass layout is turned.
    public int? North { get; set; }
    // Bit n: hourglass clock n sweeps clockwise.
    public int? ClockwiseMask { get; set; }
    // Unholy Darkness per fire wave; each must not hold that wave's fire.
    public IReadOnlyList<PartyRole>? Darkness { get; set; }
    public PartyRole? ShellCrusherTarget { get; set; }

    public PerRoleSetting<RelativityFire> Fire { get; set; } = new();

    // Places are indexed by RelativityFire.
    private static readonly (PartyRole[] Seats, string Group, int[] Places)[] Groups =
    [
        ([PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer], "supports", [1, 1, 2]),
        ([PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps], "DPS", [2, 1, 1]),
    ];

    public static string Label(RelativityFire fire) => fire.ToString().ToLowerInvariant();

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        foreach (var (seats, group, places) in Groups)
            foreach (var fire in Enum.GetValues<RelativityFire>())
                conflicts.AtMost(places[(int)fire], seats.Where(r => Fire[r] == fire).ToList(), $"{Label(fire)} fire among the {group}");
        return conflicts;
    }
}
