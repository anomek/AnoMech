using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: red aero MT M1, red ice OT M2, blue eruption RH, water SH, ice R, darkness C; the
// northwest pair slowed, Tidal Light leaving NE last, Quietus on RH SH R, Spirit Taker on R.
// The Fragment of Fate stands at (0,-16).
//
//   22.73 Maelstrom (12y) at (0,-11) (0,11); 28.23 (9.5,-5.5) (-9.5,5.5); 33.18 (-9.5,-5.5) (9.5,5.5).
//   23.92 Dark Water on SH with M1 R C at (12,15.4).
//   25.55 the heads catch OT at (-12.6,0) and M2 at (12.6,0); the west head flies on by (-11.1,5.2) at 28.
//   25.92 Dark Aero on MT (-9.7,16.6) and M1 (9.6,16.7), pushing SH R C at (8.6,15.1) inward;
//         Dark Eruption on RH (-12,-15.3); Dark Blizzard on OT, M2, R.
//   28.82 Unholy Darkness on C with OT SH RH R at (-12.9,-12.9); 28.82 is also when red ice runs out.
//   29.93 the heads' second catch: M1 (7.1,10.2), MT (-7.1,10.3).
//   32.03 Tidal Light west from x=20, a 10y strip every ~2s; 38.43 south from z=-20 the same way.
//   36.3-39.1 the blues step in the puddles; Wyrmfang and red aero's Wyrmclaw would run out at 51.82.
//   48.52 Spirit Taker on R at (0,0). 52.8 rewind: OT (9.6,-7.6), C (8.8,-6.4), MT (7.6,-9.6).
//   53.93 Hallowed Wings from (20,0) facing west: OT first, then C M2 SH; the party lands at x -10..-13.5.
//   58.53 Hallowed Wings from (0,-20) facing south: MT (-12.3,-9.6) first, then RH M1 R at z -8.6.
//   69.0  Akh Morn: MT alone at (-9.3,0.1).
public class FruP4CrystallizeTimeScenarioTests
{
    private static NegativeRun<FruP4CrystallizeTimeScenario> Crystallize(PartyRole player)
        => Negative<FruP4CrystallizeTimeScenario>(player)
            .Overrides<FruP4CrystallizeTimeStateOverrides>(o =>
            {
                o.Debuffs = new Dictionary<PartyRole, CrystallizeDebuff>
                {
                    [MainTank] = CrystallizeDebuff.RedAero,
                    [OffTank] = CrystallizeDebuff.RedIce,
                    [RegenHealer] = CrystallizeDebuff.BlueEruption,
                    [ShieldHealer] = CrystallizeDebuff.BlueWater,
                    [MeleeDpsA] = CrystallizeDebuff.RedAero,
                    [MeleeDpsB] = CrystallizeDebuff.RedIce,
                    [PhysRangedDps] = CrystallizeDebuff.BlueIce,
                    [CasterDps] = CrystallizeDebuff.BlueDarkness,
                };
                o.SlowNorthwest = true;
                o.Corner = CrystallizeCorner.NE;
                o.Quietus = [RegenHealer, ShieldHealer, PhysRangedDps];
                o.SpiritTakerTarget = PhysRangedDps;
            });

    [TestCase(22.5f, 0f, 8f)]
    [TestCase(28f, 8f, -3f)]
    [TestCase(33f, -8f, -4f)]
    public void StandingInMaelstromDies(float at, float x, float z)
        => Crystallize(MainTank)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.Maelstrom, MainTank);

    [Test]
    public void DarkWaterShortABodyKillsStackers()
        => Crystallize(CasterDps)
            .TeleportAt(23.7f, to: new(5, 10))
            .ShouldKill(ActionId.DarkWater, ShieldHealer, MeleeDpsA, PhysRangedDps);

    [Test]
    public void UnholyDarknessShortABodyKillsStackers()
        => Crystallize(OffTank)
            .TeleportAt(28.6f, to: new(-5, -15))
            .ShouldKill(ActionId.UnholyDarkness, CasterDps, ShieldHealer, RegenHealer, PhysRangedDps);

    // Inside the ring of OT's red ice and of R's blue ice.
    [TestCase(-12.6f, 6f)]
    [TestCase(8.6f, 9f)]
    public void StandingInDarkBlizzardRingDies(float x, float z)
        => Crystallize(MainTank)
            .TeleportAt(25.7f, to: new(x, z))
            .ShouldKill(ActionId.DarkBlizzard, MainTank);

    [Test]
    public void DarkAeroKnockbackIntoWallDies()
        => Crystallize(ShieldHealer)
            .TeleportAt(25.7f, to: new(11.5f, 15.8f))
            .ShouldKill(TheEnvironment, ShieldHealer);

    [TestCase(31.8f, 15f, 5f, ActionId.TidalLightFirst)]
    [TestCase(33.9f, 5f, 5f, ActionId.TidalLightRest)]
    [TestCase(38.2f, 5f, -15f, ActionId.TidalLightFirst)]
    [TestCase(40.3f, 5f, -5f, ActionId.TidalLightRest)]
    public void StandingInTidalLightDies(float at, float x, float z, uint strip)
        => Crystallize(MainTank)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(strip, MainTank);

    // Off the west head's path, OT keeps Wyrmclaw until it runs out as the darkness stack lands.
    [Test]
    public void RedNotCaughtByHeadDies()
        => Crystallize(OffTank)
            .TeleportAt(24.5f, to: new(-5, -5))
            .ShouldDieOfStatus(StatusId.Wyrmclaw, OffTank);

    [Test]
    public void WyrmclawRunningOutKills()
        => Crystallize(MainTank)
            .At(31f, p => p.Member(MainTank)!.AddStatus(StatusId.Wyrmclaw, 51.82f - 31f))
            .ShouldDieOfStatus(StatusId.Wyrmclaw, MainTank);

    [Test]
    public void WyrmfangRunningOutKills()
        => Crystallize(RegenHealer)
            .At(37f, p => p.Member(RegenHealer)!.AddStatus(StatusId.Wyrmfang, 51.82f - 37f))
            .ShouldDieOfStatus(StatusId.Wyrmfang, RegenHealer);

    [Test]
    public void BlueCaughtByHeadDies()
        => Crystallize(RegenHealer)
            .TeleportAt(24f, to: new(-11f, -5.5f))
            .ShouldKill(ActionId.LongingOfTheLost, RegenHealer);

    // The AI walks OT out of the head after its catch; stepping back in lets it catch again.
    [Test]
    public void RedCaughtTwiceDies()
        => Crystallize(OffTank)
            .TeleportAt(27.9f, to: new(-11.1f, 5.2f))
            .ShouldKill(ActionId.LongingOfTheLost, OffTank);

    [Test]
    public void SecondWingOnVulnDies()
        => Crystallize(OffTank)
            .TeleportAt(58.3f, to: new(-10.44f, -11f))
            .ShouldKill(ActionId.HallowedWingsKnockback, OffTank);

    [Test]
    public void NonTankFrontOfWingDies()
        => Crystallize(CasterDps)
            .TeleportAt(53.8f, to: new(10.5f, -7.5f))
            .ShouldKill(ActionId.HallowedWingsKnockback, CasterDps);

    [Test]
    public void WingKnockbackIntoWallDies()
        => Crystallize(MainTank)
            .TeleportAt(53.8f, to: new(-5, -8))
            .ShouldKill(TheEnvironment, MainTank);

    [Test]
    public void SpiritTakerKnockbackIntoWallDies()
        => Crystallize(ShieldHealer)
            .TeleportAt(48.3f, to: new(3, 0))
            .ShouldKill(TheEnvironment, ShieldHealer);

    [Test]
    public void NonTankJoiningSoloAkhMornDies()
        => Crystallize(MeleeDpsA)
            .TeleportAt(68.8f, to: new(-9.33f, 0.1f))
            .ShouldKill(ActionId.AkhMornUsurperHit, MeleeDpsA);

    [Test]
    public void FireworkOnFragmentFails()
        => Crystallize(RegenHealer)
            .TeleportAt(25.7f, to: new(0, -13))
            .ShouldFail();
}
