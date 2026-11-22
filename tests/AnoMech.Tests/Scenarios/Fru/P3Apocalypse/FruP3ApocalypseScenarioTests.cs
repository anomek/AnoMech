using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P3Apocalypse;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: lights from north, clockwise; Dark Water 1 on MT and M1, 2 on RH and M2, 3 on SH and R;
// Spirit Taker on M1.
//
//   22.82 Dark Water (6y, 4 to share): MT SH RH OT at (-5.2,0), M1 C R M2 at (5.1,0).
//   24.82 Spirit Taker on M1 at (3.8,3.6); M2 waits at (3.6,-3.2).
//   33.32 Apocalypse (9y): the centre at 33.32 and 35.32, the spokes rolling clockwise from N/S
//         every 2s (E/W at 37.32).
//   36.02 Dark Eruption (6y) on everyone: MT (-6.9,-6.7), OT (-9.5,0.1), M1 (6.9,7), M2 (9.6,0.3).
//   41.82 Dark Water: RH MT SH OT at (-2.8,-3.1), the DPS at (2.8,2.9).
//   44.02 Darkest Dance on the farthest from the Oracle at (0,0): OT at (-17.7,0).
//   46.82 its knockback from the Oracle at (-11.1,0); the party at (-9.3,±1).
//   50.82 Dark Water: OT RH SH MT at (-5.5,5.5), the DPS at (-5.5,-5.5).
public class FruP3ApocalypseScenarioTests
{
    private static NegativeRun<FruP3ApocalypseScenario> Apocalypse(PartyRole player)
        => Negative<FruP3ApocalypseScenario>(player)
            .Overrides<FruP3ApocalypseStateOverrides>(o =>
            {
                o.Rotation = 0;
                o.Clockwise = true;
                o.SpiritTakerTarget = MeleeDpsA;
                o.WaterDurations = [1, 0, 2, 3, 1, 2, 3, 0];
            });

    // The one leaving is never the holder, whose stack would leave with them.
    [TestCase(ShieldHealer, 22.6f, -5f, -8f, MainTank, RegenHealer, OffTank)]
    [TestCase(ShieldHealer, 41.6f, -6f, 3f, RegenHealer, MainTank, OffTank)]
    [TestCase(OffTank, 50.6f, -12f, 0f, RegenHealer, ShieldHealer, MainTank)]
    public void DarkWaterShortABodyKillsStackers(PartyRole leaver, float at, float x, float z, PartyRole a, PartyRole b, PartyRole c)
        => Apocalypse(leaver)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.DarkWater, a, b, c);

    // Between the two stacks, OT counts towards both, so neither runs short.
    [Test]
    public void InBothWaterStacksDies()
        => Apocalypse(OffTank)
            .TeleportAt(50.6f, to: new(-5.5f, 0))
            .ShouldKill(ActionId.DarkWater, OffTank);

    [TestCase(33.1f, 0f, -3f)]
    [TestCase(37.1f, 14f, 0f)]
    public void StandingInApocalypseDies(float at, float x, float z)
        => Apocalypse(MainTank)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.Apocalypse, MainTank);

    [Test]
    public void DarkEruptionsOverlappingDie()
        => Apocalypse(MainTank)
            .TeleportAt(35.9f, to: new(-9.53f, 0.1f))
            .ShouldKill(ActionId.DarkEruption, MainTank, OffTank);

    [Test]
    public void NonTankFarthestTakesDarkestDance()
        => Apocalypse(MeleeDpsA)
            .TeleportAt(43.8f, to: new(18, 5))
            .ShouldKill(ActionId.DarkestDance, MeleeDpsA);

    [Test]
    public void DarkestDanceKnockbackIntoWallDies()
        => Apocalypse(MeleeDpsA)
            .TeleportAt(46.6f, to: new(12, 0))
            .ShouldKill(TheEnvironment, MeleeDpsA);

    [Test]
    public void SpiritTakerKnockbackIntoWallDies()
        => Apocalypse(MeleeDpsB)
            .TeleportAt(24.6f, to: new(6.5f, 6))
            .ShouldKill(TheEnvironment, MeleeDpsB);
}
