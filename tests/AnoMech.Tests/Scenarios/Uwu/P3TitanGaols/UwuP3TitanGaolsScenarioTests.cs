using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Uwu.P3TitanGaols;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: Titan jumps east to (14, 0), so the AI's jump-frame coordinates are the world's; safe side
// south; gaols on OffTank, MeleeDpsB, CasterDps (chain order 0, 1, 2); both Landslides on MT.
//   8.38  Geocrush from (14, 0); party at (-13.5, 0). 9.70 floor shrinks to 16y.
//  14.15  Bury (3y) on bombs (-11, 11), (-11, -11), (-5, 5), (-5, -5), (-12, -5).
//  16.93  Upheaval from (14, 0): party at (10.2, 0.6) flies 24y to (-13.5, 4).
//  19.45  Burst (5y) on the same five bombs.
//  22.17  Bury on bomb 5 at (-12, 5). 23.42 Landslide from (14, 0) aimed at MT; the five non-gaol
//         players bait it stacked at (-11, 0) and leave.
//  24.26  Jail: OffTank (5.3, 0.9), MeleeDpsB (0, 0.9), CasterDps (-6.9, 1.7).
//  27.48  Bomb 5's Burst breaks CasterDps's gaol. 27.88 Landslide aimed at MT, the five stacked
//         again at (-11, 0), then dodging to (-8, -6).
//         Freefire 28.10, 28.73, 29.37, gaol 2 to gaol 0.
//  39.09  Titan follows MT to (8, -6), charging on OffTank's sludge; Woken 44.07.
//  44.40  Granite Impact from any unbroken gaol. 45.00 awakening check.
public class UwuP3TitanGaolsScenarioTests
{
    private static NegativeRun<UwuP3TitanGaolsScenario> Gaols(PartyRole player)
        => Negative<UwuP3TitanGaolsScenario>(player).Overrides<UwuP3TitanGaolsStateOverrides>(Pin);

    private static void Pin(UwuP3TitanGaolsStateOverrides o)
    {
        o.FirstJump = Direction.E;
        o.SafeSide = 1;
        o.GaolTargets = [OffTank, MeleeDpsB, CasterDps];
        o.FirstLandslideBait = MainTank;
        o.SecondLandslideBait = MainTank;
    }

    [Test]
    public void StayingInTheMiddleDiesToTheJumpGeocrush()
        => Gaols(RegenHealer)
            .TeleportAt(7f, to: Vector2.Zero)
            .ShouldKill(ActionId.GeocrushJump, RegenHealer);

    [Test]
    public void StandingOffTheShrunkenFloorFalls()
        => Gaols(RegenHealer)
            .TeleportAt(9.8f, to: new(-17, 0))
            .ShouldKill(TheEnvironment, RegenHealer);

    [TestCase(-11f, 11f, 14f)]
    [TestCase(-11f, -11f, 14f)]
    [TestCase(-5f, 5f, 14f)]
    [TestCase(-5f, -5f, 14f)]
    [TestCase(-12f, -5f, 14f)]
    [TestCase(-12f, 5f, 22f)]
    public void StandingOnABombDiesToBury(float x, float z, float at)
        => Gaols(RegenHealer)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.Bury, RegenHealer);

    // After Upheaval's knockback has landed.
    [TestCase(-11f, 11f, 19.2f)]
    [TestCase(-11f, -11f, 19.2f)]
    [TestCase(-5f, 5f, 19.2f)]
    [TestCase(-5f, -5f, 19.2f)]
    [TestCase(-12f, -5f, 19.2f)]
    [TestCase(-12f, 5f, 26f)]
    public void StandingOnABombDiesToBurst(float x, float z, float at)
        => Gaols(RegenHealer)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.Burst, RegenHealer);

    [Test]
    public void UpheavalFromTheWrongSideThrowsYouIntoABurst()
        => Gaols(RegenHealer)
            .TeleportAt(16.5f, to: new(10.2f, -0.6f))
            .ShouldKill(ActionId.Burst, RegenHealer);

    [Test]
    public void UpheavalFromFarAwayThrowsYouOffTheFloor()
        => Gaols(RegenHealer)
            .TeleportAt(16.5f, to: new(6, 0))
            .ShouldKill(TheEnvironment, RegenHealer);

    [Test]
    public void StayingOnTheLandslideBaitSpotFalls()
        => Gaols(RegenHealer)
            .TeleportAt(22f, to: new(-11, 0))
            .ShouldKill(TheEnvironment, RegenHealer);

    // Backed up along the line, clear of bomb 5's Burst and gaol 2's Freefire.
    [Test]
    public void MainTankStayingInTheAimedLandslideFalls()
        => Gaols(MainTank)
            .TeleportAt(26f, to: new(-15.5f, -1))
            .ShouldKill(TheEnvironment, MainTank);

    [TestCase(-6.9f)]
    [TestCase(0f)]
    [TestCase(5.3f)]
    public void StandingBesideABreakingGaolDiesToFreefire(float gaolX)
        => Gaols(RegenHealer)
            .TeleportAt(27.95f, to: new(gaolX, -4))
            .ShouldKill(ActionId.Freefire, RegenHealer);

    [TestCase(OffTank, 5.4f)]
    [TestCase(MeleeDpsB, 0f)]
    public void PrisonerOutOfTheChainWipesToGraniteImpact(PartyRole prisoner, float x)
        => Gaols(prisoner)
            .TeleportAt(23.8f, to: new(x, 8))
            .ShouldKill(ActionId.GraniteImpactLong, PerRole.All);

    [Test]
    public void SixthBombMissingTheChainWipesToGraniteImpact()
        => Gaols(CasterDps)
            .TeleportAt(23.8f, to: new(-6.7f, -6))
            .ShouldKill(ActionId.GraniteImpactLong, PerRole.All);

    [Test]
    public void FreedPrisonerStayingInTheSludgeDies()
        => Gaols(CasterDps)
            .FreezeAt(25f)
            .ShouldKill(TheEnvironment, CasterDps);

    // Titan picks up his third charge on the way but stops clear of every sludge before the tick that wakes him.
    [Test]
    public void MainTankPullingTitanOutOfTheSludgeFailsTheAwakening()
        => Gaols(MainTank)
            .TeleportAt(39.2f, to: new(0, -15))
            .ShouldFail();
}
