using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P2DiamondDust;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: Axe Kick, Twin Stillness, first icicles N/S, Frigid Stone on the supports, the Mirror north
// at (0,-10), the gaze east at (20,0).
//
//   20.68 Axe Kick (16y) from (0,0); everyone out at 17-19y.
//   21.72 House of Light on the four DPS, cones from (0,0) at W/S/N/E as they walk in from 14y.
//   23.32 Frigid Stone on MT (-13.5,-13.3), OT (13.3,-13.4), RH (-13.3,13.5), SH (13.5,13.7).
//   23.38 icicles (0,-16) (0,16); 27.43 the four intercards at 16y; 31.43 (16,0) (-16,0).
//   27.62 Heavenly Strike from (0,0); the party at (0,-3) and (0,3) is pushed to ~17y.
//   30.07 Frigid Needle stars on the four stone spots.
//   31.72 Sinbound Holy x4, 1.57s apart: RH MT M1 R north, SH OT M2 C south, walking from
//         (4.6,-17.5) to (18,-2.5) and mirrored. Wave 0's north puddle sits at (4.6,-17.5).
//   40.07 Shining Armor from (20,0); MT's group at (13.7,10.4).
//   46.23 Twin Stillness's front cleave from the Mirror facing south; the party behind it at (-3,-17.8).
//   48.67 its back cleave; the party south at (2,13.5).
public class FruP2DiamondDustScenarioTests
{
    private static NegativeRun<FruP2DiamondDustScenario> DiamondDust(PartyRole player, Action<FruP2DiamondDustStateOverrides>? tweak = null)
        => Negative<FruP2DiamondDustScenario>(player)
            .Overrides<FruP2DiamondDustStateOverrides>(o =>
            {
                o.Axe = true;
                o.Stillness = true;
                o.FirstIcicle = 0;
                o.SupportsMarked = true;
                o.MirrorOctant = 0;
                o.GazeOctant = 2;
                tweak?.Invoke(o);
            });

    [Test]
    public void StayingInForAxeKickDies()
        => DiamondDust(MainTank)
            .TeleportAt(20.4f, to: new(0, 10))
            .ShouldKill(ActionId.AxeKick, MainTank);

    [Test]
    public void StayingOutForScytheKickDies()
        => DiamondDust(MainTank, o => o.Axe = false)
            .TeleportAt(20.4f, to: new(0, 15))
            .ShouldKill(ActionId.ScytheKick, MainTank);

    // Wave 1's hit lands after Heavenly Strike has pushed anyone on its spots off the arena.
    [TestCase(23.2f, 0f, -16f)]
    [TestCase(31.3f, 16f, 0f)]
    public void StandingUnderIcicleDies(float at, float x, float z)
        => DiamondDust(MainTank)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.IcicleImpact, MainTank);

    // Behind M1 on the same line from the centre, M2 is in both cones.
    [Test]
    public void TwoHouseOfLightBaitsStackedDie()
        => DiamondDust(MeleeDpsB)
            .TeleportAt(21.6f, to: new(-13, 0))
            .ShouldKill(ActionId.HouseOfLightDiamondDust, MeleeDpsA, MeleeDpsB);

    [Test]
    public void FrigidStonesStackedDie()
        => DiamondDust(MainTank)
            .TeleportAt(23.1f, to: new(13.31f, -13.36f))
            .ShouldKill(ActionId.FrigidStone, MainTank, OffTank);

    [Test]
    public void StandingOnFrigidNeedleArmDies()
        => DiamondDust(MainTank)
            .TeleportAt(29.9f, to: new(-3.5f, -13.3f))
            .ShouldKill(ActionId.FrigidNeedleCross, MainTank);

    // The cross runs through the star's centre too, and resolves before the circle.
    [Test]
    public void StandingOnFrigidNeedleStarDies()
        => DiamondDust(MainTank)
            .TeleportAt(29.9f, to: new(-13.5f, -13.3f))
            .ShouldKill(ActionId.FrigidNeedleCross, MainTank);

    [Test]
    public void HeavenlyStrikeIntoTheWallDies()
        => DiamondDust(MainTank)
            .TeleportAt(27.4f, to: new(0, -12))
            .ShouldKill(TheEnvironment, MainTank);

    [Test]
    public void SinboundHolyShortABodyKillsStackers()
        => DiamondDust(MainTank)
            .TeleportAt(31.5f, to: new(0, 0))
            .ShouldKill(ActionId.SinboundHoly, RegenHealer, MeleeDpsA, PhysRangedDps);

    [Test]
    public void SteppingInHolyPuddleBleedsOut()
        => DiamondDust(MainTank)
            .TeleportAt(36.6f, to: new(4.6f, -17.5f))
            .ShouldDieOfStatus(StatusId.Bleeding, MainTank);

    [Test]
    public void LookingAtShiningArmorDies()
        => DiamondDust(MainTank)
            .TeleportAt(39.9f, to: new(13.7f, 10.4f), facing: MathF.Atan2(6.3f, -10.4f))
            .ShouldKill(ActionId.ShiningArmor, MainTank);

    [TestCase(true, 46f, 0f, 0f, ActionId.TwinStillnessFirst)]
    [TestCase(true, 48.4f, 0f, -17f, ActionId.TwinStillnessSecond)]
    [TestCase(false, 46f, 0f, -17f, ActionId.TwinSilenceFirst)]
    [TestCase(false, 48.4f, 0f, 0f, ActionId.TwinSilenceSecond)]
    public void TwinCleaveCatchesWrongSide(bool stillness, float at, float x, float z, uint cleave)
        => DiamondDust(MainTank, o => o.Stillness = stillness)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(cleave, MainTank);
}
