using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
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
    // The Oracle's jumps land this short of their target, and don't move her when it's closer.
    private const float JumpLandingShort = 7.5f;

    private static readonly PartyRole[] LineupDps = [PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];
    private static readonly PartyRole[] BaitDps = [PartyRole.CasterDps, PartyRole.PhysRangedDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];

    private readonly PartyRole[] links;

    public IReadOnlyList<PartyRole> LineupOrder { get; }
    public int TetherWaterIndex { get; }
    public int FreeWaterIndex { get; }
    public PartyRole SpiritTakerTarget { get; }
    public bool EastWing { get; }

    public FruP4DarklitDragonsongState(Rng rng, PartyRole playerRole, FruP4DarklitDragonsongStateOverrides overrides)
    {
        var tank = overrides.ChainedTank ?? rng.NextTankRole();
        var healer = overrides.ChainedHealer ?? rng.NextHealerRole();
        var dps = overrides.DpsOrder?.ToArray() ?? rng.Shuffle(LineupDps);
        var shape = overrides.Shape ?? rng.Next(3);
        var tetherWaterIndex = overrides.TetherWaterIndex ?? rng.Next(4);
        var freeWaterIndex = overrides.FreeWaterIndex ?? rng.Next(4);
        SpiritTakerTarget = overrides.SpiritTakerTarget ?? rng.NextRole();
        EastWing = overrides.EastWing ?? rng.NextBool();

        var rolled = Loop(tank, healer, dps[0], dps[1], shape).Lineup;
        var deal = new Deal(tank, healer, dps[0], dps[1], rolled[tetherWaterIndex], rolled[4 + freeWaterIndex]);
        if (overrides is { ChainedTank: null, ChainedHealer: null, DpsOrder: null, TetherWaterIndex: null, FreeWaterIndex: null })
            deal = Honour(rng, deal, overrides.Chained.Resolve(playerRole), overrides.DarkWater.Resolve(playerRole));
        var (dealtLinks, lineup) = Loop(deal.Tank, deal.Healer, deal.DpsA, deal.DpsB, shape);
        links = dealtLinks;
        LineupOrder = lineup;
        TetherWaterIndex = Array.IndexOf(lineup, deal.ChainedWater);
        FreeWaterIndex = Array.IndexOf(lineup, deal.FreeWater) - 4;
    }

    // Who the chains and Dark Water go to.
    private sealed record Deal(PartyRole Tank, PartyRole Healer, PartyRole DpsA, PartyRole DpsB, PartyRole ChainedWater, PartyRole FreeWater)
    {
        public bool Honours(IEnumerable<(PartyRole Role, bool Value)> chained, IEnumerable<(PartyRole Role, bool Value)> water)
            => chained.All(c => (c.Role == Tank || c.Role == Healer || c.Role == DpsA || c.Role == DpsB) == c.Value)
               && water.All(w => (w.Role == ChainedWater || w.Role == FreeWater) == w.Value);
    }

    private static IEnumerable<Deal> Deals()
    {
        foreach (var tank in new[] { PartyRole.MainTank, PartyRole.OffTank })
        foreach (var healer in new[] { PartyRole.RegenHealer, PartyRole.ShieldHealer })
        for (var a = 0; a < LineupDps.Length; a++)
        for (var b = a + 1; b < LineupDps.Length; b++)
        {
            PartyRole[] chained = [tank, healer, LineupDps[a], LineupDps[b]];
            foreach (var chainedWater in chained)
            foreach (var freeWater in Enum.GetValues<PartyRole>().Except(chained))
                yield return new(tank, healer, LineupDps[a], LineupDps[b], chainedWater, freeWater);
        }
    }

    public static bool CanDeal(IReadOnlyList<(PartyRole Role, bool Value)> chained, IReadOnlyList<(PartyRole Role, bool Value)> water)
        => Deals().Any(d => d.Honours(chained, water));

    // The roll stands when it honours every forced seat; otherwise one of the deals that do is
    // drawn, so each stays as likely as the roll makes it.
    private static Deal Honour(Rng rng, Deal rolled, IReadOnlyList<(PartyRole Role, bool Value)> chained, IReadOnlyList<(PartyRole Role, bool Value)> water)
    {
        if (rolled.Honours(chained, water)) return rolled;
        var deals = Deals().Where(d => d.Honours(chained, water)).ToList();
        if (deals.Count > 0) return deals[rng.Next(deals.Count)];
        DiagnosticLog.Warn("[FruP4DarklitDragonsong] No deal gives every seat the chain and Dark Water it asked for -- leaving them to the roll.");
        return rolled;
    }

    private static (PartyRole[] Links, PartyRole[] Lineup) Loop(PartyRole tank, PartyRole healer, PartyRole dpsA, PartyRole dpsB, int shape)
    {
        var pair = LineupDps.Where(r => r == dpsA || r == dpsB).ToArray();
        var free = BaitDps.Where(r => r != dpsA && r != dpsB).ToArray();
        PartyRole[] links = shape switch
        {
            0 => [healer, pair[1], tank, pair[0]],
            1 => [healer, tank, pair[1], pair[0]],
            _ => [healer, tank, pair[0], pair[1]],
        };
        PartyRole[] lineup = [healer, tank, pair[1], pair[0], (PartyRole)(5 - (int)healer), (PartyRole)(1 - (int)tank), free[1], free[0]];
        if (shape == 1) (lineup[1], lineup[2]) = (lineup[2], lineup[1]);
        if (shape == 2) (lineup[1], lineup[3]) = (lineup[3], lineup[1]);
        return (links, lineup);
    }

    // Who baits Somber Dance, as the strat picked it on the host: it reads who plays which seat
    // there, so a replay takes the host's pick.
    public PartyRole? SomberTank { get; set; }

    // A peer's shadow of the host's roll (see IMultiplayerReplayable): only what the Ai reads.
    private FruP4DarklitDragonsongState(IReadOnlyList<PartyRole> lineupOrder, int tetherWaterIndex, int freeWaterIndex, bool eastWing, PartyRole somberTank)
    {
        links = [];
        LineupOrder = lineupOrder;
        TetherWaterIndex = tetherWaterIndex;
        FreeWaterIndex = freeWaterIndex;
        EastWing = eastWing;
        SomberTank = somberTank;
    }

    public static FruP4DarklitDragonsongState? FromNetworkReplay(IReadOnlyList<PartyRole> lineupOrder, int tetherWaterIndex, int freeWaterIndex,
        bool eastWing, PartyRole somberTank)
        => lineupOrder.Count == 8 && lineupOrder.All(Enum.IsDefined) && lineupOrder.Distinct().Count() == 8
           && tetherWaterIndex is >= 0 and < 4 && freeWaterIndex is >= 0 and < 4 && somberTank.IsTank()
            ? new(lineupOrder.ToList(), tetherWaterIndex, freeWaterIndex, eastWing, somberTank)
            : null;

    public PartyRole TetherWater => LineupOrder[TetherWaterIndex];
    public PartyRole FreeWater => LineupOrder[4 + FreeWaterIndex];
    public PartyRole[] Waters => [TetherWater, FreeWater];

    public (PartyRole, PartyRole) ChainLink(int index) => (links[index], links[(index + 1) % 4]);

    public static Vector3 JumpLanding(Vector3 oracle, Vector3 target)
    {
        var offset = target - oracle;
        return offset.Length() <= JumpLandingShort ? oracle : target - Vector3.Normalize(offset) * JumpLandingShort;
    }
}
