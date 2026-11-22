using System;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Uwu.UltimateAnnihilation;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Uwu.UwuConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: Ifrit south-east (13.7, 13.7), Titan south-west (-13.7, 13.7); Garuda south (0, 19.5).
// Thermal Low on MT, both healers and PhysRanged; PhysRanged takes the first Mesohigh, the Searing
// Wind healer the second. Orbs spawn at (2, -4); MT (1.6, -3.4) and OT (2.6, -4.4) pop them.
//  17.00  Weight of the Land on the stack (-6, -4.5). 20.03 on (2.5, -3.7). 23.02 on (-6, -4.5).
//  20.18  Eye of the Storm (safe inside 12y). Again 37.20 and 54.22.
//  20.28  Flaming Crush on the stack at (-6, -4.5).
//  20.80  Orb 1 (two soakers), armed 21.60. Orb 2 at 26.78, armed 27.58.
//  22.52  Mesohigh on PhysRanged at (-1.3, 6.5).
//  24.60  Feather Rain targets five players; the party dodges and it lands 26.81.
//  29.37  Searing Wind from the holder at (0.2, 17.7). Then 35.38 (8.8, 11), 41.40 (-0.2, 10.6),
//         47.42 and 53.42 (0.3, 11.1).
//  32.18  Landslide from Titan, one line straight at the centre. 34.17 awakened, ±22° off it.
//  33.78  Awakened Crimson Cyclone along x = 0 from the south and along z = 0 from Ifrit's side.
//  38.83  Orb 4 (one soaker), armed 39.63; MT pops it at once. 39.93 Mesohigh on the healer at (-0.2, 10.6).
//  42.01  Feather Rain targets, party stacked at (-6.5, -7.5); lands 44.22. Tank Purge 42.43.
public class UltimateAnnihilationScenarioTests
{
    private static NegativeRun<UltimateAnnihilationScenario> Annihilation(PartyRole player, Action<UltimateAnnihilationStateOverrides>? tweak = null)
        => Negative<UltimateAnnihilationScenario>(player)
            .Overrides<UltimateAnnihilationStateOverrides>(o =>
            {
                o.IfritCorner = IfritCorner.SouthEast;
                tweak?.Invoke(o);
            });

    [TestCase(16.8f, -6f, -4.5f)]
    [TestCase(19.8f, 2.5f, -3.7f)]
    [TestCase(22.8f, -6f, -4.5f)]
    public void StandingInWeightOfTheLandDies(float at, float x, float z)
        => Annihilation(MeleeDpsA)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.WeightOfTheLand, MeleeDpsA);

    [Test]
    public void FlamingCrushTargetAloneDies()
        => Annihilation(CasterDps, o => o.FlamingCrushOnPlayer = true)
            .TeleportAt(19.2f, to: new(-3, 6))
            .ShouldKill(ActionId.FlamingCrush, CasterDps);

    [TestCase(22.3f, 0f, 12f)]
    [TestCase(39.7f, 0f, 14f)]
    public void TakingMesohighWithoutThermalLowDies(float at, float x, float z)
        => Annihilation(CasterDps)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.Mesohigh, CasterDps);

    // Just behind the taker: inside the hit, but not the closest to Garuda.
    [TestCase(22.4f, -1.3f, 5.5f)]
    [TestCase(39.8f, -0.2f, 9.5f)]
    public void StandingInMesohighWithoutThermalLowDies(float at, float x, float z)
        => Annihilation(CasterDps)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.Mesohigh, CasterDps);

    [TestCase(29.2f, 0f, 15f)]
    [TestCase(35.2f, 6.5f, 9f)]
    [TestCase(41.2f, 0f, 8f)]
    [TestCase(47.2f, 0f, 8.5f)]
    [TestCase(53.2f, 0f, 8.5f)]
    public void ClosingInOnSearingWindDies(float at, float x, float z)
        => Annihilation(MeleeDpsA)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.SearingWind, MeleeDpsA);

    [TestCase(19.9f)]
    [TestCase(36.9f)]
    [TestCase(53f)]
    public void LeavingTheEyeBeforeItResolvesDies(float at)
        => Annihilation(MeleeDpsA)
            .TeleportAt(at, to: new(-10.5f, -10.5f))
            .ShouldKill(ActionId.EyeOfTheStorm, MeleeDpsA);

    [TestCase(IfritCorner.SouthWest, 5f)]
    [TestCase(IfritCorner.SouthEast, -5f)]
    public void IfritDashesFromHisCornerAcrossTheCentreWhoeverHowlTurnedHimTo(IfritCorner corner, float x)
        => Annihilation(CasterDps, o => o.IfritCorner = corner)
            .TeleportAt(30.5f, to: new(x, -5f))
            .ShouldKill(ActionId.CrimsonCyclone, CasterDps);

    [TestCase(IfritCorner.SouthEast, 0f, -3f)]
    [TestCase(IfritCorner.SouthEast, 14f, 0f)]
    [TestCase(IfritCorner.SouthWest, -14f, 0f)]
    public void StandingInAwakenedCrimsonCycloneDies(IfritCorner corner, float x, float z)
        => Annihilation(CasterDps, o => o.IfritCorner = corner)
            .TeleportAt(33.5f, to: new(x, z))
            .ShouldKill(ActionId.CrimsonCycloneAwaken, CasterDps);

    // The push carries the player 30y, past the arena's edge.
    [TestCase(IfritCorner.SouthEast, 31.9f, -5f, 5f)]
    [TestCase(IfritCorner.SouthWest, 31.9f, 5f, 5f)]
    [TestCase(IfritCorner.SouthEast, 33.9f, -10.6f, 6.3f)]
    [TestCase(IfritCorner.SouthWest, 33.9f, 10.6f, 6.3f)]
    public void StandingInLandslideIsKnockedIntoTheWall(IfritCorner corner, float at, float x, float z)
        => Annihilation(CasterDps, o => o.IfritCorner = corner)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(TheEnvironment, CasterDps);

    // At least four of the five rains are aimed at the seven stacked here.
    [Test]
    public void FeatherRainOnTheStackDies()
        => Annihilation(MeleeDpsA)
            .FreezeAt(42f)
            .ShouldKill(ActionId.FeatherRain, MeleeDpsA);

    [TestCase(20.7f)]
    [TestCase(26.7f)]
    public void OrbPoppedByOneTankFails(float at)
        => Annihilation(OffTank)
            .TeleportAt(at, to: new(8, -8))
            .ShouldFail();

    [Test]
    public void PoppingTheLastOrbLateDiesToTankPurge()
        => Annihilation(MainTank)
            .TeleportAt(39f, to: new(-2.5f, -6))
            .TeleportAt(41.6f, to: new(2, -4))
            .ShouldKill(ActionId.TankPurge, MainTank);
}
