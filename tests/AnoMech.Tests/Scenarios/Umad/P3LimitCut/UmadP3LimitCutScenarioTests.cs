using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Umad.P3LimitCut;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Tests.NegativeRun;
using Umad = AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Tests;

// Every roll is pinned: clones start S going clockwise (so the charges walk S SE E NE N NW W SW),
// bosses held NE, R baits Umbra Smash, MT invulns both Thunders. Numbers in role order (MT = 1 ..
// C = 8); supports carry Headwind, DPS Tailwind.
//
//    4.50  R runs out to (-12.6,12.7); the party stacks around (4.5,-5), Chaos (6,-5.9).
//    8.00  Umbra Smash locks onto R; lands 13.00, lethal within 20y.
//   15.83  Vacuum Wave reads facings from Exdeath (5.6,-7.0); the pushes land 16.61.
//   19.98  Cyclone on each of the eight, stacked around (-3.5,4).
//   30.97  Charge 1 from (0,20) onto MT (-7.2,-17.6), then every 0.22s: OT (-17.5,-7.5),
//          RH (-17.4,7.5), SH (-7.1,17.6), M1 (7.5,17.6), M2 (17.6,7.6), R (17.6,-7.3) at 32.30
//          from (-20,0), C (7.3,-17.3) at 32.52 from (-14.1,14.1). Lethal within 35y.
//   38.58  Thunder III on the closest to Exdeath (-14,-5.3): MT at (-11.9,-9.0).
//
// A run pauses 5s after its first death.
public class UmadP3LimitCutScenarioTests
{
    private const float FacingEast = MathF.PI / 2f;
    private const float FacingWest = -MathF.PI / 2f;

    internal static void Pin(UmadP3LimitCutStateOverrides o)
    {
        o.StartSpot = 0;
        o.Clockwise = true;
        o.BossSpot = 3;
        foreach (var role in PerRole.All)
        {
            o.Number[role] = (int)role + 1;
            o.Wind[role] = role is MainTank or OffTank or RegenHealer or ShieldHealer ? Wind.Headwind : Wind.Tailwind;
        }
    }

    private static NegativeRun<UmadP3LimitCutScenario> LimitCut(PartyRole player)
        => Negative<UmadP3LimitCutScenario>(player)
            .Overrides<UmadP3LimitCutStateOverrides>(Pin);

    [Test]
    public void BaitStayingInStackKillsParty()
        => LimitCut(PhysRangedDps)
            .FreezeAt(4.4f)
            .ShouldKill(Umad.ActionId.UmbraSmash, PerRole.All);

    [Test]
    public void StandingNearUmbraBaitDies()
        => LimitCut(CasterDps)
            .TeleportAt(12.5f, to: new(-8, 8))
            .ShouldKill(Umad.ActionId.UmbraSmash, CasterDps);

    // West of Exdeath, so the push runs west: Headwind wants west, Tailwind east.
    [TestCase(RegenHealer, FacingEast)]
    [TestCase(CasterDps, FacingWest)]
    public void WrongFacingForWindPushesOffArena(PartyRole player, float facing)
        => Assert.That(PushedTo(player, new(0, -7), facing).Length(), Is.GreaterThan(Umad.Geometry.ArenaRadius));

    [Test]
    public void CorrectFacingAtEdgePushesOffArena()
        => Assert.That(PushedTo(CasterDps, new(-15, -7), FacingEast).Length(), Is.GreaterThan(Umad.Geometry.ArenaRadius));

    // A headless run has no terrain to fall from, so where the push lands stands in for the fall.
    private static Vector2 PushedTo(PartyRole player, Vector2 from, float facing)
    {
        var landed = Vector2.Zero;
        ScenarioRun.Execute(typeof(UmadP3LimitCutScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = player,
            StopAt = 18f,
            Overrides = o => Pin((UmadP3LimitCutStateOverrides)o),
            Takeovers = [new Takeover(15.5f, from, facing)],
            Probe = p => { if (p.Crossed(17.9f)) landed = new(p.Member(player)!.Position.X, p.Member(player)!.Position.Z); },
        });
        return landed;
    }

    [Test]
    public void CycloneAloneDies()
        => LimitCut(CasterDps)
            .TeleportAt(19.8f, to: new(10, 10))
            .ShouldKill(Umad.ActionId.Cyclone, CasterDps);

    [Test]
    public void StandingCloseToOwnCloneDies()
        => LimitCut(CasterDps)
            .TeleportAt(32.35f, to: new(0, 0))
            .ShouldKill(Umad.ActionId.UltimaBlasterCharge, CasterDps);

    // On R's line 36.5y from its clone: both take R's charge with no harm but the vuln, and
    // C's own charge, aimed through R, lands on both vulns.
    [Test]
    public void TakingChargeWithVulnDies()
        => LimitCut(CasterDps)
            .TeleportAt(32.2f, to: new(15.8f, -7))
            .ShouldKill(Umad.ActionId.UltimaBlasterCharge, CasterDps, PhysRangedDps);

    [Test]
    public void NonTankClosestToExdeathTakesThunder()
        => LimitCut(CasterDps)
            .TeleportAt(38.4f, to: new(-14, -4.5f))
            .ShouldKill(Umad.ActionId.ThunderIII_Resolve, CasterDps);

    [Test]
    public void DiesWalkingOffArena()
        => LimitCut(MainTank)
            .DropBelowArenaAt(3f, to: new(0, 20.5f))
            .ShouldKill(TheEnvironment, MainTank);

    private static readonly string[] TankLimitBreaks = ["199", "4240", "4241", "17105"];
    private static readonly string[] TankLimitBreakStatuses = ["196", "863", "864", "1931"];
    private static float Number(string value) => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    // Outside TEA a tank LB3's status lands on everyone at the press: the bot main tank's, at 14.94s.
    [Test]
    public void TheBotTanksLimitBreakLandsOnEveryoneAtThePress()
    {
        string? mainTank = null;
        var run = ScenarioRun.Execute(typeof(UmadP3LimitCutScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = CasterDps,
            UserActionsEnabled = true,
            StopAt = 18f,
            Overrides = o => Pin((UmadP3LimitCutStateOverrides)o),
            Probe = p => mainTank ??= p.Member(MainTank)!.EntityId.ToString("X8"),
        });
        var rows = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows;
        var limitBreaks = rows.Where(r => r["kind"] == "ability" && TankLimitBreaks.Contains(r["id"])).ToList();
        var lands = rows.Where(r => r["kind"] == "status_gain" && TankLimitBreakStatuses.Contains(r["id"])).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, run.ToString);
            Assert.That(limitBreaks, Has.Count.EqualTo(1));
            Assert.That(lands.Select(r => r["actor"]).Distinct().Count(), Is.EqualTo(PerRole.All.Length));
            Assert.That(lands, Has.Count.EqualTo(PerRole.All.Length));
            if (limitBreaks.Count != 1) return;
            Assert.That(limitBreaks.Single()["actor"], Is.EqualTo(mainTank));
            var pressed = Number(limitBreaks.Single()["t"]);
            foreach (var land in lands)
                Assert.That(Number(land["t"]), Is.EqualTo(pressed).Within(0.02f), $"landing on {land["actor"]}");
        });
    }
}
