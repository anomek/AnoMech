using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

// User-forced rolls for FruP4DarklitDragonsongState; null leaves a field randomized.
public sealed class FruP4DarklitDragonsongStateOverrides
{
    public bool? EastWing { get; set; }
    public PartyRole? ChainedTank { get; set; }
    public PartyRole? ChainedHealer { get; set; }
    // R, C, M1, M2 shuffled: the first two are chained.
    public IReadOnlyList<PartyRole>? DpsOrder { get; set; }
    // 0 bowtie, 1 box, 2 hourglass.
    public int? Shape { get; set; }
    // Lineup slot 0-3 of the chained and of the free Dark Water.
    public int? TetherWaterIndex { get; set; }
    public int? FreeWaterIndex { get; set; }
    public PartyRole? SpiritTakerTarget { get; set; }

    // True = one of the four chained: a tank, a healer and two DPS. Any of the deal's pins above
    // overrides this and DarkWater.
    public PerRoleSetting<bool> Chained { get; set; } = new();
    // One chained player and one free one carry it.
    public PerRoleSetting<bool> DarkWater { get; set; } = new();

    private static readonly (PartyRole[] Seats, int Chained, string What)[] Groups =
    [
        ([PartyRole.MainTank, PartyRole.OffTank], 1, "a tank's chain"),
        ([PartyRole.RegenHealer, PartyRole.ShieldHealer], 1, "a healer's chain"),
        ([PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps], 2, "a DPS chain"),
    ];

    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;
        foreach (var (seats, chained, what) in Groups)
        {
            conflicts.AtMost(chained, seats.Where(r => Chained[r] == true).ToList(), what);
            conflicts.AtLeast(chained, seats.Where(r => Chained[r] == false).ToList(), seats.Length, what);
        }
        var water = PerRole.All.Where(r => DarkWater[r] == true).ToList();
        conflicts.AtMost(2, water, "Dark Water");
        conflicts.AtMost(1, water.Where(r => Chained[r] == true).ToList(), "Dark Water on a chain");
        conflicts.AtMost(1, water.Where(r => Chained[r] == false).ToList(), "Dark Water off the chains");
        conflicts.AtLeast(2, PerRole.All.Where(r => DarkWater[r] == false).ToList(), 8, "Dark Water");
        if (!conflicts.Any && !FruP4DarklitDragonsongState.CanDeal(Forced(Chained), Forced(DarkWater)))
            conflicts.Add($"{SettingsConflicts.Seats(PerRole.All.Where(r => Chained[r] != null || DarkWater[r] != null))} "
                + "can't all get the chain and Dark Water they asked for.");
        return conflicts;
    }

    private static List<(PartyRole Role, bool Value)> Forced(PerRoleSetting<bool> setting)
        => PerRole.All.Where(r => setting[r] != null).Select(r => (r, setting[r]!.Value)).ToList();
}
