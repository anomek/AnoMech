using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P5ParadiseRegained;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: towers unturned with the east one second, dark wing first.
//
//   15.72 tower (0,7): RH (-0.65,7) + SH (0.65,7); dark cleave (120° towards (-0.87,-0.5)) on MT (-6,-3.5).
//   16.52 dark buster on the nearest: OT at (0,1).
//   19.22 tower (6.1,-3.5): M2 + C; 19.42 light cleave (towards (-0.87,0.5)) on OT (-6.1,3.5).
//   20.22 light buster on the farthest: MT at (0,-14). 22.72 tower (-6.1,-3.5): M1 + R.
//   37.13 Cruel Paths from (0,0), 6y wide: light at MT (-4,4) with RH M1 R at (-6,6), dark at OT (4,4)
//         with SH M2 C at (6,6). MT keeps Light Resistance Down for the next round.
//   39.52 their echo where they went. 41.73 light at M1 (-4,4) with OT RH R, dark at M2 (4,4) with MT SH C.
public class FruP5ParadiseRegainedScenarioTests
{
    private static NegativeRun<FruP5ParadiseRegainedScenario> Paradise(PartyRole player)
        => Negative<FruP5ParadiseRegainedScenario>(player)
            .Overrides<FruP5ParadiseRegainedStateOverrides>(o =>
            {
                o.Rotation = 0;
                o.EastSecond = true;
                o.DarkFirst = true;
            });

    [TestCase(ShieldHealer, 15.5f, 3f, 12f)]
    [TestCase(CasterDps, 19f, 6f, -10f)]
    [TestCase(PhysRangedDps, 22.5f, -6f, -10f)]
    public void TowerSoakedAloneWipes(PartyRole leaver, float at, float x, float z)
        => Paradise(leaver)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.ParadiseTowerFailure, PerRole.All);

    [Test]
    public void SoakingTwoTowersDies()
        => Paradise(RegenHealer)
            .TeleportAt(19f, to: new(6.1f, -3.5f))
            .ShouldKill(ActionId.ParadiseTowerExplosion, RegenHealer);

    [TestCase(15.5f, -7f, -4f, ActionId.WingsCleaveDark)]
    [TestCase(19.2f, -7f, 4f, ActionId.WingsCleaveLight)]
    public void NonTankInWingCleaveDies(float at, float x, float z, uint cleave)
        => Paradise(MeleeDpsA)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(cleave, MeleeDpsA);

    [Test]
    public void SameTankTakingBothCleavesDies()
        => Paradise(MainTank)
            .TeleportAt(19.2f, to: new(-7, 4))
            .ShouldKill(ActionId.WingsCleaveLight, MainTank);

    [TestCase(16.3f, 0.3f, 0.3f, ActionId.WingsBusterDark)]
    [TestCase(20f, 0f, -18f, ActionId.WingsBusterLight)]
    public void NonTankTakingWingBusterDies(float at, float x, float z, uint buster)
        => Paradise(MeleeDpsA)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(buster, MeleeDpsA);

    [Test]
    public void CruelPathShortABodyKillsStackers()
        => Paradise(PhysRangedDps)
            .TeleportAt(36.9f, to: new(-8, 0))
            .ShouldKill(ActionId.CruelPathOfLight, MainTank, RegenHealer, MeleeDpsA);

    // MT leaving the dark line also leaves it a body short.
    [Test]
    public void FrontBaitTwiceSameColourDies()
        => Paradise(MainTank)
            .TeleportAt(41.6f, to: new(-6, 6))
            .ShouldKill(ActionId.CruelPathOfLight, MainTank)
            .ShouldKill(ActionId.CruelPathOfDarkness, MeleeDpsB, ShieldHealer, CasterDps);

    // After the lines are aimed, RH stands where they cross and counts towards both.
    [Test]
    public void InBothLinesDies()
        => Paradise(RegenHealer)
            .TeleportAt(37f, to: new(0, 1))
            .ShouldKill(ActionId.CruelPathOfDarkness, RegenHealer);

    [Test]
    public void StandingInEchoDies()
        => Paradise(RegenHealer)
            .TeleportAt(39.3f, to: new(-6, 6))
            .ShouldKill(ActionId.CruelPathOfLightEcho, RegenHealer);
}
