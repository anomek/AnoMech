using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

// Four players chained in a loop (a tank, a healer and two DPS, linked as a bowtie, box or
// hourglass), Dark Water on one chained and one free player, Spirit Taker on a random player and
// the Usurper's wing on a random side.
//
// LineupOrder is the NA lineup the chains and waters are rolled against: 0-3 chained
// (NW, NE, SE, SW), 4-7 free.
public sealed class FruP4DarklitDragonsongState
{
    public const float ChainsAt = 21.9f;
    public const float CursesAt = 28.9f;
    public const float CursesEndAt = 42.9f;
    public const float TowersAt = 32.2f;
    public const float DarkWaterAt = 40.15f;
    public const float TowerRadius = 4f;

    private static readonly PartyRole[] LineupDps = [PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];
    private static readonly PartyRole[] BaitDps = [PartyRole.CasterDps, PartyRole.PhysRangedDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];

    private readonly PartyRole[] links;

    public IReadOnlyList<PartyRole> LineupOrder { get; }
    public int TetherWaterIndex { get; }
    public int FreeWaterIndex { get; }
    public PartyRole SpiritTakerTarget { get; }
    public bool EastWing { get; }

    public FruP4DarklitDragonsongState(Rng rng, FruP4DarklitDragonsongStateOverrides overrides)
    {
        var tank = overrides.ChainedTank ?? rng.NextTankRole();
        var healer = overrides.ChainedHealer ?? rng.NextHealerRole();
        var dps = overrides.DpsOrder?.ToArray() ?? rng.Shuffle(LineupDps);
        var shape = overrides.Shape ?? rng.Next(3);
        TetherWaterIndex = overrides.TetherWaterIndex ?? rng.Next(4);
        FreeWaterIndex = overrides.FreeWaterIndex ?? rng.Next(4);
        SpiritTakerTarget = overrides.SpiritTakerTarget ?? rng.NextRole();
        EastWing = overrides.EastWing ?? rng.NextBool();

        var pair = LineupDps.Where(r => r == dps[0] || r == dps[1]).ToArray();
        var free = BaitDps.Where(r => r != dps[0] && r != dps[1]).ToArray();
        links = shape switch
        {
            0 => [healer, pair[1], tank, pair[0]],
            1 => [healer, tank, pair[1], pair[0]],
            _ => [healer, tank, pair[0], pair[1]],
        };
        PartyRole[] lineup = [healer, tank, pair[1], pair[0], (PartyRole)(5 - (int)healer), (PartyRole)(1 - (int)tank), free[1], free[0]];
        if (shape == 1) (lineup[1], lineup[2]) = (lineup[2], lineup[1]);
        if (shape == 2) (lineup[1], lineup[3]) = (lineup[3], lineup[1]);
        LineupOrder = lineup;
    }

    public PartyRole TetherWater => LineupOrder[TetherWaterIndex];
    public PartyRole FreeWater => LineupOrder[4 + FreeWaterIndex];
    public PartyRole[] Waters => [TetherWater, FreeWater];

    public (PartyRole, PartyRole) ChainLink(int index) => (links[index], links[(index + 1) % 4]);
}
