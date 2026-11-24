using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P3Wormhole;

public sealed class TeaP3WormholeAi : IScenarioAi<TeaP3WormholeState>
{
    public string Name => "NAUR";

    private const float Exact = 0f;
    private const float NumberSpotsBy = 25.39f;

    private static readonly Vector2 NorthWestDot = new(-13f, -13f);
    private static readonly Vector2 NorthEastDot = new(13f, -13f);
    private static readonly Vector2 SouthWestBait = new(-12.4f, 12.3f);
    private static readonly Vector2 SouthEastBait = new(12.4f, 12.3f);
    private static readonly float FacingNorthWest = MathF.Atan2(-1f, -1f);
    private static readonly float FacingSouthWest = MathF.Atan2(-1f, 1f);

    private TeaP3WormholeState state = null!;
    private AiManager ai = null!;

    public void Run(TeaP3WormholeState stateParam, SimWorld world)
    {
        state = stateParam;
        ai = new AiManager(world);

        ai.Move(0f, UnderAlexanderPrime);
        ai.Move(8.3f, StackInCentre);

        ai.Move(19.6f, TakeNumberSpots, Exact, NumberSpotsBy);
        ai.Kbi(22.4f, [Number(1)]);
        ai.Kbi(23.8f, [Number(2)]);
        ai.Move(25.45f, FiveAndSixSoakFirstTowers, Exact);
        ai.Kbi(26.8f, [Number(3)]);
        if (FourOne) ai.Move(26.9f, FourToChargeSpot, Exact);
        else ai.Move(26.9f, ThreeToSlashSpot, Exact, facing: FacingSouthWest);
        ai.Kbi(28.2f, [Number(4)]);
        ai.Move(Mirrored ? 31.4f : 31.05f, ThreeToWestWall, Exact);
        ai.Kbi(31.2f, [Number(5)]);
        ai.Move(31.4f, FiveToNorthWestDot, Exact, sprint: true, facing: FacingNorthWest);
        ai.Move(31.4f, SevenAndEightSoakSecondTowers, Exact);
        ai.Kbi(32.4f, [Number(6)]);
        ai.Move(32.5f, FourToEastWall, Exact);
        ai.Kbi(35.4f, [Number(7)]);
        ai.Move(35.65f, SevenToSlashSpot, Exact, sprint: true, facing: FacingSouthWest);
        ai.Move(35.65f, EightToChargeSpot, Exact);
        ai.Kbi(36.8f, [Number(8)]);
        ai.Move(36.8f, FiveAndSixBackToCentre);
        ai.Move(37.1f, OneAndTwoSoakThirdTowers, Exact);
        ai.Move(39.9f, OneAndTwoBackToCentre);
        ai.Move(41.05f, ThreeFourSevenAndEightBackToCentre);

        ai.Move(46.0f, EnumerationSpots, Exact);
    }

    private static IAiMove UnderAlexanderPrime() => AiMove.Create(
            new Vector2(-0.4f, -6.2f), new Vector2(-0.7f, -7.0f), new Vector2(-0.7f, 6.0f), new Vector2(-2.3f, 7.6f),
            new Vector2(-3.9f, 7.3f), new Vector2(1.1f, 7.6f), new Vector2(0.5f, 5.9f), new Vector2(2.2f, 8.4f))
        .NaturalOrder();

    private static IAiMove StackInCentre() => AiMove.All(Vector2.Zero);

    private IAiMove TakeNumberSpots() => AiMove.Create(
            NorthWestDot,
            NorthEastDot,
            FourOne ? SouthWestSlashSpot : SouthWestBait,
            FourOne ? SouthEastBait : SouthEastChargeSpot,
            new Vector2(-15.8f, -8.6f),
            new Vector2(14.8f, -7.2f),
            new Vector2(-17.3f, -6.0f),
            new Vector2(16.6f, -5.6f))
        .Assignments(state.Numbers);

    private IAiMove FiveAndSixSoakFirstTowers()
        => AiMove.Create(null, null, null, null, FirstSoakWest, FirstSoakEast, SevenWaitsForSecondSoak, EightWaitsForSecondSoak).Assignments(state.Numbers);

    private IAiMove FourToChargeSpot() => AiMove.Single(Number(4), SouthEastChargeSpot);

    private IAiMove ThreeToSlashSpot() => AiMove.Single(Number(3), SouthWestSlashSpot);

    private IAiMove ThreeToWestWall() => AiMove.Single(Number(3), ThreeWaitsOnTheWestWall);

    private IAiMove FiveToNorthWestDot() => AiMove.Single(Number(5), NorthWestDot);

    private IAiMove SevenAndEightSoakSecondTowers()
        => AiMove.Create(OneWaitsForThirdSoak, TwoWaitsForThirdSoak, null, null, null, NorthEastDot, SecondSoakWest, SecondSoakEast).Assignments(state.Numbers);

    private IAiMove FourToEastWall() => AiMove.Single(Number(4), FourWaitsOnTheEastWall);

    private IAiMove SevenToSlashSpot() => AiMove.Single(Number(7), SouthWestSlashSpot);

    private IAiMove EightToChargeSpot() => AiMove.Single(Number(8), SouthEastChargeSpot);

    private IAiMove FiveAndSixBackToCentre() => AiMove.Create(null, null, null, null, Vector2.Zero, Vector2.Zero).Assignments(state.Numbers);

    private IAiMove OneAndTwoSoakThirdTowers() => AiMove.Create(ThirdSoakWest, ThirdSoakEast).Assignments(state.Numbers);

    private IAiMove OneAndTwoBackToCentre() => AiMove.Create(Vector2.Zero, Vector2.Zero).Assignments(state.Numbers);

    private IAiMove ThreeFourSevenAndEightBackToCentre()
        => AiMove.Create(null, null, Vector2.Zero, Vector2.Zero, null, null, Vector2.Zero, Vector2.Zero).Assignments(state.Numbers);

    private PartyRole Number(int number) => state.Numbers[number - 1];

    private bool FourOne => state.BruteJusticeNorthWest;
    private bool Mirrored => state.PuddlesMirrored;
    private Vector2 SouthWestSlashSpot => Mirrored ? new(-14.0f, 12.8f) : new(-13.4f, 12.6f);
    private Vector2 SouthEastChargeSpot => Mirrored ? new(13.4f, 12.6f) : new(14.0f, 12.8f);
    private Vector2 FirstSoakWest => Mirrored ? new(-17.0f, 1.0f) : new(-17.0f, -5.0f);
    private Vector2 FirstSoakEast => Mirrored ? new(17.0f, -4.7f) : new(17.0f, 1.4f);
    private Vector2 SecondSoakWest => Mirrored ? new(-15.5f, 5.0f) : new(-15.0f, -2.6f);
    private Vector2 SecondSoakEast => Mirrored ? new(16.0f, -3.6f) : new(16.0f, 3.6f);
    private Vector2 ThirdSoakWest => Mirrored ? new(-11.3f, 2.8f) : new(-11.3f, -4.2f);
    private Vector2 ThirdSoakEast => Mirrored ? new(11.4f, -4.2f) : new(11.4f, 3.2f);
    private Vector2 OneWaitsForThirdSoak => Mirrored ? new(-18.3f, -1.0f) : new(-18.4f, -2.4f);
    private Vector2 TwoWaitsForThirdSoak => Mirrored ? new(18.9f, 1.5f) : new(18.9f, -1.0f);
    private Vector2 ThreeWaitsOnTheWestWall => Mirrored ? new(-18.2f, -4.2f) : new(-17.4f, 1.0f);
    private Vector2 FourWaitsOnTheEastWall => Mirrored ? new(18.2f, 4.5f) : new(18.0f, 2.0f);
    private Vector2 SevenWaitsForSecondSoak => Mirrored ? new(-17.5f, -3.0f) : new(-18.0f, 1.5f);
    private Vector2 EightWaitsForSecondSoak => Mirrored ? new(18.5f, 2.5f) : new(18.3f, -3.0f);

    private IAiMove EnumerationSpots()
    {
        var marked = state.Enumerations.ToList();
        var melee = marked.Where(r => r is MeleeDpsA or MeleeDpsB).ToList();
        var east = melee.Count == 1 ? melee[0] : marked.Min();
        var west = marked.First(r => r != east);
        var free = TeaP3WormholeState.Dps.Except(marked).ToList();
        return AiMove.Create(
                new Vector2(4.4f, -0.3f), new Vector2(5.2f, 0.5f), new Vector2(5.2f, -1.1f),
                new Vector2(-5.2f, -0.3f), new Vector2(-6.0f, 0.5f), new Vector2(-6.0f, -1.1f),
                new Vector2(0f, -6.0f), new Vector2(-0.8f, -7.4f))
            .Assignments([east, MainTank, OffTank, west, RegenHealer, ShieldHealer, free[0], free[1]]);
    }
}
