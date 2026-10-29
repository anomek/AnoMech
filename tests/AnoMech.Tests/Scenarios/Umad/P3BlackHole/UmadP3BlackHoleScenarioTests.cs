using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Umad.P3BlackHole;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Tests.NegativeRun;
using ActionId = AnoMech.Scenarios.Umad.UmadConstants.ActionId;
using StatusId = AnoMech.Scenarios.Umad.UmadConstants.StatusId;

namespace AnoMech.Tests;

// Every roll is pinned: Kefka always north, slaps Right Left Right (cones on M1 MT RH, the stack on
// C), Longitudinal Implosion, active holes at (17,0) (0,17) (-17,0) every wave, stacks on SH then R,
// Edict facing MT, Thunder Set 1 MT invulns both and Set 2 shared MT first. Slot i holds PartyRole i,
// so the tethers go M1 MT SH, then M2 OT C, then R RH.
//
//   22.15  Slap rows at (10,-10) (10,0) (10,10), 0.65s apart; 24.45 Final Slap r=6 on the centre;
//          24.82 cones from the centre onto DPS (-7,7), tanks (-7,-7), healers (-9,0).
//   42.63  Thunder III on MT (-5.2,0.6), Exdeath at (-2.8,-2.8); again at 45.63.
//   50.28  Damning Edict from Chaos (-5.7,2.7) facing north; again at 77.57 from (-5.9,4.0).
//   52.60  Slap rows at x=-10; 54.91 Final Slap; 55.27 Shocking Impact on the party at (9,0).
//   62.83  M1 (5.5,12.1) spends its crust on the (17,0) hole; M2 takes that tether at (7.6,10.0),
//          M1 heads back to the centre at 66. Every 5.1s a shot, a crust spent each time.
//   78.99  Look upon Me and Despair, a 16y-wide band down the middle; again at 137.05.
//   83.95  Thunder III on MT (-3.8,3.3), Exdeath at (-7.2,6.0); 86.95 on OT.
//  102.22  OT (-10,7.6) spends its crust on the (0,17) hole; C (-7.6,-10) takes its second hit.
//  119.09  Shockwaves from Chaos (-1,1.1) along the NE-SW diagonal; 121.11 NW-SE.
//  120.58  Slap rows at x=10; 122.89 Final Slap; 123.26 the same cones as at 24.82.
//  146.68  Blizzard III on everyone at (0,-5) supports and (0,5) DPS, landing 149.38.
//  151.91  West tower (-10,0) M1 R; 152.14 Knock Down on SH, the supports stacked at the centre;
//          153.20 east tower (10,0) M2 C, 154.50 west RH MT, 155.84 east SH OT.
//
// Times are when a hit snapshots; deaths follow by each action's damage delay. A run pauses 5s
// after its first death.
public class UmadP3BlackHoleScenarioTests
{
    internal static void Pin(UmadP3BlackHoleStateOverrides o)
    {
        o.SeatLinesInRoleOrder = true;
        o.SlapAttacks[0] = ActionId.SlapHappy_Right;
        o.SlapAttacks[1] = ActionId.SlapHappy_Left;
        o.SlapAttacks[2] = ActionId.SlapHappy_Right;
        for (var i = 0; i < 5; i++) o.KefkaPositions[i] = Direction.N;
        o.ConeTargets[0] = [MeleeDpsA, MainTank, RegenHealer];
        o.ConeTargets[1] = [CasterDps];
        o.ConeTargets[2] = [MeleeDpsA, MainTank, RegenHealer];
        o.ImplosionAttack = ActionId.LongitudinalImplosion;
        for (var i = 0; i < 4; i++) o.BlackHoleDirections[i] = Direction.N;
        o.MiniBlackHoleInitialAngle = 0;
        o.MiniBlackHoleChirality = 1;
        o.StackTargets = [ShieldHealer, PhysRangedDps];
        o.EdictTarget = MainTank;
        o.ThunderSet1 = ThunderIIIAssignment.MtInvulnsBoth;
        o.ThunderSet2 = ThunderIIIAssignment.ShareMtFirst;
    }

    private static NegativeRun<UmadP3BlackHoleScenario> BlackHole(PartyRole player)
        => Negative<UmadP3BlackHoleScenario>(player)
            .Overrides<UmadP3BlackHoleStateOverrides>(Pin);

    [TestCase(22f, 10f)]
    [TestCase(52.4f, -10f)]
    [TestCase(120.4f, 10f)]
    public void StandingOnSlapSideDies(float at, float x)
        => BlackHole(MeleeDpsA)
            .TeleportAt(at, to: new(x, 0))
            .ShouldKill(ActionId.SlapHappy_Slap, MeleeDpsA);

    // East of centre, out of the slap cones that fan west from it.
    [TestCase(24f)]
    [TestCase(54.5f)]
    [TestCase(122.5f)]
    public void StandingUnderFinalSlapDies(float at)
        => BlackHole(MeleeDpsA)
            .TeleportAt(at, to: new(4, 0))
            .ShouldKill(ActionId.SlapHappy_FinalSlap, MeleeDpsA);

    [Test]
    public void ShockingImpactShortABodyKillsStackers()
        => BlackHole(MeleeDpsA)
            .TeleportAt(55f, to: new(-10, 0))
            .ShouldKill(ActionId.ShockingImpact, AllBut(MeleeDpsA));

    // Between the DPS and healer cones, which overlap by 15 degrees.
    [Test]
    public void StandingInTwoShockwaveConesDies()
        => BlackHole(OffTank)
            .TeleportAt(24.7f, to: new(-8.3f, 3.4f))
            .ShouldKill(ActionId.ShockwaveCone, OffTank);

    [TestCase(50f)]
    [TestCase(77.3f)]
    public void StandingInFrontOfChaosDiesToEdict(float at)
        => BlackHole(MeleeDpsA)
            .TeleportAt(at, to: new(0, -10))
            .ShouldKill(ActionId.DamningEdict, MeleeDpsA);

    [Test]
    public void NonTankClosestToExdeathTakesThunder()
        => BlackHole(CasterDps)
            .TeleportAt(42.5f, to: new(-2.8f, -2f))
            .ShouldKill(ActionId.ThunderIII_Resolve, CasterDps);

    // MT, still carrying the first hit's Lightning Resistance Down, stays on Exdeath instead of
    // handing the second hit to OT, with no invuln up.
    [Test]
    public void TankNotSwappingTakesSecondThunder()
        => BlackHole(MainTank)
            .WithMitigationChecks()
            .TeleportAt(84.4f, to: new(-6.5f, 5.4f))
            .ShouldKill(ActionId.ThunderIII_Resolve, MainTank);

    // M1 stays at its pull spot, so M2's tether runs through it.
    [Test]
    public void NothingnessAfterSpentCrustDies()
        => BlackHole(MeleeDpsA)
            .FreezeAt(63f)
            .ShouldKill(ActionId.Nothingness, MeleeDpsA);

    // MT, holding the (0,17) hole's tether at (-12.1,5.5), steps onto SH's beam from the (-17,0)
    // hole: it takes that tether too, holding two at once.
    [Test]
    public void APlayerHoldingATetherTakesASecond()
    {
        var shown = ShownRun.Execute(typeof(UmadP3BlackHoleScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 64.5f,
            Overrides = o => Pin((UmadP3BlackHoleStateOverrides)o),
            Takeovers = [new Takeover(63.5f, new Vector2(-8.95f, -8.47f))],
        });
        var mainTank = "0x" + shown.IdOf(MainTank);
        var tethers = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x23" && r["p"].Split(',')[1] == "0x54").ToList();
        var holding = tethers.Where(r => ShownRun.Time(r) < 64.4f).GroupBy(r => r["actor"]).Select(g => g.Last()).Count(r => r["p"].Split(',')[2] == mainTank);
        var takenAfter = tethers.Any(r => ShownRun.Time(r) > 63.5f && r["p"].Split(',')[2] == mainTank);
        Assert.Multiple(() =>
        {
            Assert.That(takenAfter, Is.True, "MT takes the second tether");
            Assert.That(holding, Is.EqualTo(2), "MT holds both");
        });
    }

    // C steps to the far end of OT's beam, beside its own hole so its own beam misses OT, and
    // OT's shot spends C's crust in the same frame as OT's.
    [Test]
    public void TwoCrustsSpentTogetherWipe()
        => BlackHole(CasterDps)
            .TeleportAt(101.9f, to: new(-16.5f, 2.5f))
            .ShouldKill(ActionId.Earthquake_Cleanse, PerRole.All);

    // Set back one Nothingness step just before the shot that would spend the crust, as if a
    // tether turn had been missed: the shot only brings them back to Meanest Existence, and the
    // crust runs out with its line (80.38, 114.38, 147.38).
    [TestCase(MeleeDpsA, 62.6f)]
    [TestCase(OffTank, 102f)]
    [TestCase(RegenHealer, 137.4f)]
    public void UnspentCrustDiesToStrayEarth(PartyRole holder, float at)
        => BlackHole(holder)
            .At(at, p =>
            {
                p.Member(holder)!.RemoveStatus(StatusId.MeanestExistence);
                p.Member(holder)!.AddStatus(StatusId.Unbecoming);
            })
            .ShouldKill(ActionId.StrayEarth, holder);

    [TestCase(78.7f)]
    [TestCase(136.7f)]
    public void StandingInLookUponMeDies(float at)
        => BlackHole(MeleeDpsA)
            .TeleportAt(at, to: new(0, 10))
            .ShouldKill(ActionId.LookUponMeAndDespair_Omen, MeleeDpsA);

    [TestCase(118.9f, -5.9f, 6.1f)]
    [TestCase(120.9f, -6f, -4f)]
    public void StandingInImplosionConeDies(float at, float x, float z)
        => BlackHole(MeleeDpsA)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.Shockwave, MeleeDpsA);

    [Test]
    public void StayingPutThroughBlizzardDies()
        => BlackHole(MeleeDpsA)
            .FreezeAt(146.9f)
            .ShouldKill(ActionId.BlizzardIII, MeleeDpsA);

    [Test]
    public void SoloTowerSoakDies()
        => BlackHole(MeleeDpsA)
            .MoveBotAt(151.7f, PhysRangedDps, to: new(0, 0))
            .ShouldKill(ActionId.StompAMole, MeleeDpsA);

    [Test]
    public void EmptyTowerWipes()
        => BlackHole(MeleeDpsA)
            .TeleportAt(151.7f, to: new(0, 0))
            .MoveBotAt(151.7f, PhysRangedDps, to: new(0, 0))
            .ShouldKill(ActionId.UnmitigatedImpact, PerRole.All);

    [Test]
    public void StayingOnTowerThroughSwapDies()
        => BlackHole(MeleeDpsA)
            .FreezeAt(152.3f)
            .ShouldKill(ActionId.StompAMole, MeleeDpsA);

    [Test]
    public void KnockDownShortABodyKillsStackers()
        => BlackHole(MainTank)
            .TeleportAt(152f, to: new(0, -8))
            .ShouldKill(ActionId.KnockDown, OffTank, RegenHealer, ShieldHealer);

    [Test]
    public void DiesWalkingOffArena()
        => BlackHole(MainTank)
            .DropBelowArenaAt(3f, to: new(0, 20.5f))
            .ShouldKill(TheEnvironment, MainTank);
}
