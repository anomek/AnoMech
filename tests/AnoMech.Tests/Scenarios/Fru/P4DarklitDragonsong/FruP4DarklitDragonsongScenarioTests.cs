using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: chained RH-C-MT-R as a bowtie, Dark Water on RH and SH, Spirit Taker on M1, the wing east.
// Everyone starts with three Lightsteeped stacks at 21.9; a tower and a Path of Light cone add one each.
//
//    6.6  Akh Rhai under everyone at the centre; they spread out by 9.
//   28.92 curses (break under 9.5y). 32.22 towers: MT (2.2,-8) + RH (-2.3,-8.2) north, C (2,8.1) + R (-2.4,8) south.
//   33.07 Path of Light at the four nearest the centre: SH (-6.1,2.9), M1 (-6.1,-3), M2 (6.2,2.9), OT (6.4,-2.7).
//         The Fragment of Fate stands at (0,-16).
//   35.5  RH (-7.3,-8), C (7.2,8.3), MT (7.1,-8), R (-7.3,8).
//   40.03 Hallowed Wings over the east half; 40.17 Dark Water: RH M1 MT OT at (-6.6,-7.5), SH R M2 C at (-6.8,7.2).
//   43.82 Somber Dance on the farthest from the Oracle at (-1.6,4): MT at (18.4,-0.1); the Oracle lands at (11.1,1.4).
//   47.02 Somber Dance on the nearest: MT at (13.1,1.6).
//   57.77 Akh Morn: MT alone at (-9.1,0) takes the Usurper's, everyone else the Oracle's at the centre.
public class FruP4DarklitDragonsongScenarioTests
{
    private static NegativeRun<FruP4DarklitDragonsongScenario> Darklit(PartyRole player, Action<FruP4DarklitDragonsongStateOverrides>? tweak = null)
        => Negative<FruP4DarklitDragonsongScenario>(player)
            .Overrides<FruP4DarklitDragonsongStateOverrides>(o =>
            {
                o.EastWing = true;
                o.ChainedTank = MainTank;
                o.ChainedHealer = RegenHealer;
                o.DpsOrder = [PhysRangedDps, CasterDps, MeleeDpsA, MeleeDpsB];
                o.Shape = 0;
                o.TetherWaterIndex = 0;
                o.FreeWaterIndex = 0;
                o.SpiritTakerTarget = MeleeDpsA;
                tweak?.Invoke(o);
            });

    // The first repeat lands before the cast's own hit.
    [Test]
    public void StayingInAkhRhaiDies()
        => Darklit(MainTank)
            .FreezeAt(6.7f)
            .ShouldKill(ActionId.AkhRhai, MainTank);

    [Test]
    public void CurseTooShortWipes()
        => Darklit(RegenHealer)
            .TeleportAt(36f, to: new(6, 6))
            .ShouldKill(ActionId.RefulgentFate, PerRole.All);

    // The leaver keeps clear of both curse partners; the soaker left alone dies and snaps the chain.
    [TestCase(RegenHealer, -8f, -12f, MainTank)]
    [TestCase(CasterDps, 8f, 12f, PhysRangedDps)]
    public void TowerSoakedAloneKillsSoaker(PartyRole leaver, float x, float z, PartyRole soaker)
        => Darklit(leaver)
            .TeleportAt(32.1f, to: new(x, z))
            .ShouldKill(ActionId.BrightHunger, soaker)
            .ShouldKill(ActionId.RefulgentFate, AllBut(soaker));

    [Test]
    public void TowerSoakerBaitingPathOfLightWipes()
        => Darklit(OffTank)
            .TeleportAt(32f, to: new(0, -8))
            .TeleportAt(32.5f, to: new(6.43f, -2.7f))
            .ShouldKill(ActionId.Lightsteep, PerRole.All);

    // In front of OT on the same ray, M2 and OT are each in both cones.
    [Test]
    public void TwoPathOfLightConesWipe()
        => Darklit(MeleeDpsB)
            .TeleportAt(32.9f, to: new(3.2f, -1.35f))
            .ShouldKill(ActionId.Lightsteep, PerRole.All);

    [Test]
    public void PathOfLightOnFragmentFails()
        => Darklit(ShieldHealer)
            .TeleportAt(32.9f, to: new(0, -4))
            .ShouldFail();

    // Out on the wing's half as it snapshots, back in the water stack before that resolves.
    [TestCase(true, 8f, -6.8f, -7.2f, ActionId.HallowedWingsRight)]
    [TestCase(false, -8f, 6.35f, -7.2f, ActionId.HallowedWingsLeft)]
    public void StandingOnWingSideDies(bool eastWing, float wingX, float stackX, float stackZ, uint wing)
        => Darklit(OffTank, o => o.EastWing = eastWing)
            .TeleportAt(39.9f, to: new(wingX, stackZ))
            .TeleportAt(40.1f, to: new(stackX, stackZ))
            .ShouldKill(wing, OffTank);

    [Test]
    public void DarkWaterShortABodyKillsStackers()
        => Darklit(OffTank)
            .TeleportAt(40f, to: new(-4, -15))
            .ShouldKill(ActionId.DarkWater, RegenHealer, MeleeDpsA, MainTank);

    [Test]
    public void NonTankFarthestTakesSomberDance()
        => Darklit(MeleeDpsA)
            .TeleportAt(43.6f, to: new(13.5f, -13.5f))
            .ShouldKill(ActionId.SomberDanceFar, MeleeDpsA);

    [Test]
    public void NonTankNearestTakesSomberDance()
        => Darklit(MeleeDpsA)
            .TeleportAt(46.9f, to: new(11.1f, 1.4f))
            .ShouldKill(ActionId.SomberDanceNear, MeleeDpsA);

    // Two in MT's stack is still under four, so it lands as a tank buster on each.
    [Test]
    public void NonTankJoiningSoloAkhMornDies()
        => Darklit(MeleeDpsA)
            .TeleportAt(57.6f, to: new(-9.08f, 0.03f))
            .ShouldKill(ActionId.AkhMornUsurperHit, MeleeDpsA);
}
