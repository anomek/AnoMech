using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea;
using AnoMech.Scenarios.Tea.I1LimitCut;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: numbers 1 H2, 2 MT, 3 OT, 4 H1, 5 C, 6 M1, 7 M2, 8 R; the first Hawk Blasters
// on E/W.
//
//    0.11  Cruise Chaser's line; Brute Justice's at 31.14.
//    3.19  The numbers go out, 1 to 8 over 0.21s.
//    5.27  Hawk Blasters every 2.2s: pairs 14y out from E/W, turning 45° clockwise; the centre at 14.05
//          and 25.07; from 16.25 the pairs start over on E/W.
//   11.55  Cruise Chaser warps 2y behind H2 (#1), facing H2's way; Alpha Sword on H2 at 12.64, Super
//          Blassty Charge on MT (#2) at 14.14. OT and H1 at 16.16 / 17.31 / 18.80, C and M1 at
//          20.80 / 21.95 / 23.42, M2 and R at 25.50 / 26.58 / 28.05.
//   31.26  J Kick; Brute Justice and Cruise Chaser targetable at 34.29.
public class TeaI1LimitCutScenarioTests
{
    private static readonly PartyRole[] PinnedNumbers =
        [ShieldHealer, MainTank, OffTank, RegenHealer, CasterDps, MeleeDpsA, MeleeDpsB, PhysRangedDps];

    private static void Pin(TeaI1LimitCutStateOverrides o) => Pin(o, axis: 2);

    private static void Pin(TeaI1LimitCutStateOverrides o, int axis)
    {
        o.FirstBlasterAxis = axis;
        for (var k = 0; k < PinnedNumbers.Length; k++) o.Number[PinnedNumbers[k]] = k + 1;
    }

    private static NegativeRun<TeaI1LimitCutScenario> LimitCut(PartyRole player)
        => Negative<TeaI1LimitCutScenario>(player).Overrides<TeaI1LimitCutStateOverrides>(Pin);

    private static ScenarioRun Run(PartyRole player, float stopAt, Action<ScenarioProbe>? probe = null, params Takeover[] takeovers)
        => Run(player, stopAt, probe, takeovers, music: false);

    private static ScenarioRun Run(PartyRole player, float stopAt, Action<ScenarioProbe>? probe, Takeover[] takeovers, bool music)
        => ScenarioRun.Execute(typeof(TeaI1LimitCutScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = player,
            StopAt = stopAt,
            Overrides = o => Pin((TeaI1LimitCutStateOverrides)o),
            Takeovers = takeovers,
            Probe = probe,
            Configure = config => config.SuppressBgm = !music,
        });

    private static SimEnemy CruiseChaser(ScenarioProbe p)
        => p.World.Children.OfType<SimEnemy>().Single(e => e.BNpcBaseId == BNpcBaseId.CruiseChaser);

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static IReadOnlyList<Dictionary<string, string>> Timeline() => ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows;

    private static float At(Dictionary<string, string> row) => float.Parse(row["t"], System.Globalization.CultureInfo.InvariantCulture);

    private static Vector2 OnCompass(float degrees, float radius) => Flat(Geometry.OnCompass(degrees, radius));

    // The line aimed outward also kills whoever waits on the rim beyond the even, so only the
    // player is judged.
    private static void AssertSurvived(ScenarioRun run, PartyRole player)
        => Assert.That(run.Deaths.Where(d => d.Role == player), Is.Empty, run.ToString);

    private static void StandOutsideCruiseChaser(ScenarioProbe p, PartyRole role, bool facingCruiseChaser)
        => StandOutsideCruiseChaser(p, role, facingOffDegrees: facingCruiseChaser ? 0f : 180f);

    // 3y further out than Cruise Chaser, so the 20y knockback has nowhere to go but off the arena.
    private static void StandOutsideCruiseChaser(ScenarioProbe p, PartyRole role, float facingOffDegrees)
    {
        var cruiseChaser = Flat(CruiseChaser(p).Position);
        var outward = Vector2.Normalize(cruiseChaser);
        var member = p.Member(role)!;
        var spot = cruiseChaser + outward * 3f;
        member.SetPosition(new Vector3(spot.X, 0f, spot.Y));
        member.SetRotation(MathF.Atan2(-outward.X, -outward.Y) + facingOffDegrees * MathF.PI / 180f);
    }

    [Test]
    public void DiesWalkingIntoWall()
        => LimitCut(MeleeDpsB)
            .TeleportAt(1f, to: new(0, 20.5f))
            .ShouldKill(TheEnvironment, MeleeDpsB);

    // The far end of the first pair, on each axis.
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void DiesToFirstHawkBlasters(int axis)
        => Negative<TeaI1LimitCutScenario>(MeleeDpsB)
            .Overrides<TeaI1LimitCutStateOverrides>(o => Pin(o, axis))
            .TeleportAt(5f, to: OnCompass(axis * 45f + 180f, 14f))
            .ShouldKill(ActionId.HawkBlaster, MeleeDpsB);

    [Test]
    public void DiesToCentreHawkBlaster()
        => LimitCut(PhysRangedDps)
            .TeleportAt(13.8f, to: new(0, 0))
            .ShouldKill(ActionId.HawkBlaster, PhysRangedDps);

    // Wave 6 lands on wave 1's E/W again, not on the next axis round.
    [Test]
    public void SecondRunOfHawkBlastersStartsOverOnFirstAxis()
        => LimitCut(MeleeDpsB)
            .TeleportAt(16f, to: new(14, 0))
            .ShouldKill(ActionId.HawkBlaster, MeleeDpsB);

    // 12.64 + Alpha Sword's 0.31.
    [Test]
    public void AlphaSwordKillLandsAfterItsDelay()
    {
        var run = Run(ShieldHealer, 13.5f, p =>
        {
            if (p.Crossed(12.3f)) p.Member(ShieldHealer)!.AddStatus(StatusId.PhysicalVulnerabilityUp, 17f);
        });
        Assert.That(run.Deaths, Is.Not.Empty, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths.First().Role, Is.EqualTo(ShieldHealer));
            Assert.That(run.Deaths.First().ActionId, Is.EqualTo(ActionId.AlphaSword));
            Assert.That(run.Deaths.First().Time, Is.EqualTo(12.95f).Within(0.02f));
        });
    }

    // 14.05 + Hawk Blaster's 0.63.
    [Test]
    public void HawkBlasterKillLandsAfterItsDelay()
    {
        var run = Run(PhysRangedDps, 15f, null, new Takeover(13.8f, new(0, 0)));
        Assert.That(run.Deaths, Is.Not.Empty, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths.First().Time, Is.EqualTo(14.68f).Within(0.02f));
            Assert.That(run.Deaths.First().ActionId, Is.EqualTo(ActionId.HawkBlaster));
        });
    }

    [Test]
    public void AlphaSwordOnAlreadyVulnerableOddKills()
        => LimitCut(ShieldHealer)
            .At(12.3f, p => p.Member(ShieldHealer)!.AddStatus(StatusId.PhysicalVulnerabilityUp, 17f))
            .ShouldKill(ActionId.AlphaSword, ShieldHealer);

    [Test]
    public void SuperBlasstyChargeOnAlreadyVulnerableEvenKills()
        => LimitCut(MainTank)
            .At(13.8f, p => p.Member(MainTank)!.AddStatus(StatusId.PhysicalVulnerabilityUp, 17f))
            .ShouldKill(ActionId.SuperBlasstyCharge, MainTank);

    // OT steps in front of H2 as H2 takes #1, and dies to that very hit.
    [Test]
    public void StandingInFrontOfOddDiesToAlphaSword()
    {
        var run = Run(OffTank, 13.5f, p =>
        {
            if (!p.Crossed(12.4f)) return;
            var odd = p.Member(ShieldHealer)!;
            p.Member(OffTank)!.SetPosition(odd.Position + new Vector3(MathF.Sin(odd.Rotation), 0f, MathF.Cos(odd.Rotation)) * 3f);
        }, new Takeover(12.3f, null));
        Assert.That(run.Deaths, Is.Not.Empty, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(run.Deaths.First().Role, Is.EqualTo(OffTank));
            Assert.That(run.Deaths.First().ActionId, Is.EqualTo(ActionId.AlphaSword));
            Assert.That(run.Deaths.First().Time, Is.EqualTo(12.95f).Within(0.02f));
        });
    }

    // OT steps between Cruise Chaser and MT as MT takes #2.
    [Test]
    public void StandingInSuperBlasstyChargeDies()
        => LimitCut(OffTank)
            .FreezeAt(13.9f)
            .At(14.0f, p => p.Member(OffTank)!.SetPosition((CruiseChaser(p).Position + p.Member(MainTank)!.Position) / 2f))
            .ShouldKill(ActionId.SuperBlasstyCharge, OffTank);

    [Test]
    public void EachHitFollowsCruiseChasersTurnToItsBait()
    {
        string? cruiseChaser = null;
        var run = Run(MainTank, 28.5f, p => cruiseChaser ??= p.Game.Catalog.Scenarios.OfType<TeaI1LimitCutScenario>().Single().LastState?.Objects.CruiseChaser
            ?.EntityId.ToString("X8"));
        Assert.That(run.Passed, run.ToString);
        var rows = Timeline().Where(r => r["actor"] == cruiseChaser).ToList();
        var hits = rows.Where(r => r["kind"] == "ability"
                                   && (r["id"] == ActionId.AlphaSword.ToString() || r["id"] == ActionId.SuperBlasstyCharge.ToString())).ToList();
        Assert.That(hits, Has.Count.EqualTo(8));
        Assert.That(hits.Select(h => h["targets"]).Distinct().Count(), Is.EqualTo(8), "each hit on its own player");
        foreach (var hit in hits)
        {
            var turn = rows.Take(rows.IndexOf(hit)).Last(r => r["kind"] is "set_pos" or "move");
            Assert.Multiple(() =>
            {
                Assert.That(hit["target"], Is.EqualTo(cruiseChaser));
                Assert.That(turn["kind"], Is.EqualTo("set_pos"));
                Assert.That(Number(turn["rot"]), Is.EqualTo(Number(hit["rot"])).Within(0.011f));
                Assert.That(At(hit) - At(turn), Is.EqualTo(0.13f).Within(0.02f));
            });
        }
    }

    private static float Number(string value) => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    // The rectangle only runs forward from Cruise Chaser.
    [Test]
    public void StandingBehindCruiseChaserMissesSuperBlasstyCharge()
    {
        var hit = false;
        var run = Run(OffTank, 14.5f, p =>
        {
            if (p.Crossed(14f))
            {
                var cruiseChaser = Flat(CruiseChaser(p).Position);
                var even = Flat(p.Member(MainTank)!.Position);
                var behind = cruiseChaser + Vector2.Normalize(cruiseChaser - even) * 2.5f;
                p.Member(OffTank)!.SetPosition(new Vector3(behind.X, 0f, behind.Y));
            }
            if (p.Crossed(14.4f)) hit = p.Member(OffTank)!.FindStatus(StatusId.PhysicalVulnerabilityUp) != null;
        }, new Takeover(13.9f, null));
        Assert.That(run.Passed, run.ToString);
        Assert.That(hit, Is.False, "OT behind Cruise Chaser was hit by Super Blassty Charge");
    }

    // Taken off the AI before the bot would press Arm's Length.
    [Test]
    public void SuperBlasstyChargeThrowsEvenFacingAwayOffArena()
        => LimitCut(MainTank)
            .FreezeAt(10.9f)
            .At(13.9f, p => StandOutsideCruiseChaser(p, MainTank, facingCruiseChaser: false))
            .ShouldKill(TheEnvironment, MainTank);

    [Test]
    public void EvenFacingCruiseChaserIsNotKnockedBack()
    {
        var run = Run(MainTank, 16f, p =>
        {
            if (p.Crossed(13.9f)) StandOutsideCruiseChaser(p, MainTank, facingCruiseChaser: true);
        }, new Takeover(10.9f, null));
        AssertSurvived(run, MainTank);
    }

    // Well inside the arc.
    [Test]
    public void EvenFacing25DegreesOffCruiseChaserIsNotKnockedBack()
    {
        var run = Run(MainTank, 16f, p =>
        {
            if (p.Crossed(13.9f)) StandOutsideCruiseChaser(p, MainTank, facingOffDegrees: 25f);
        }, new Takeover(10.9f, null));
        AssertSurvived(run, MainTank);
    }

    // Past the arc's 68° outer bound.
    [Test]
    public void EvenFacing72DegreesOffCruiseChaserIsThrownOffArena()
        => LimitCut(MainTank)
            .FreezeAt(10.9f)
            .At(13.9f, p => StandOutsideCruiseChaser(p, MainTank, facingOffDegrees: 72f))
            .ShouldKill(TheEnvironment, MainTank);

    [TestCase(KnockbackImmunity.ArmsLength)]
    [TestCase(KnockbackImmunity.Surecast)]
    [TestCase(KnockbackImmunity.InnerStrength)]
    public void KnockbackImmunityKeepsEvenOnArena(ushort statusId)
    {
        var run = Run(MainTank, 16f, p =>
        {
            if (!p.Crossed(13.9f)) return;
            StandOutsideCruiseChaser(p, MainTank, facingCruiseChaser: false);
            p.Member(MainTank)!.AddStatus(statusId, 6f);
        }, new Takeover(10.9f, null));
        AssertSurvived(run, MainTank);
    }

    // #1 is gone before the warp: Cruise Chaser warps in place and swings at the player picked for it,
    // killing whoever else is in it.
    [Test]
    public void DeadNumbersAlphaSwordGoesToAnotherPlayer()
    {
        var hit = false;
        var run = ScenarioRun.Execute(typeof(TeaI1LimitCutScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            StopAt = 13f,
            Overrides = o =>
            {
                Pin((TeaI1LimitCutStateOverrides)o);
                ((TeaI1LimitCutStateOverrides)o).DeadNumbersHitGoesTo = PhysRangedDps;
            },
            Probe = p =>
            {
                if (p.Crossed(10f)) p.Member(ShieldHealer)!.Die(ActionId.HawkBlaster);
                if (p.Crossed(12.9f)) hit = p.Member(PhysRangedDps)!.FindStatus(StatusId.PhysicalVulnerabilityUp) != null;
            },
        });
        Assert.That(run.Deaths.First().Role, Is.EqualTo(ShieldHealer), run.ToString);
        Assert.That(hit, "Alpha Sword missed the player it was sent to");
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void PinnedRollClears(int axis)
    {
        var run = ScenarioRun.Execute(typeof(TeaI1LimitCutScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            Overrides = o => Pin((TeaI1LimitCutStateOverrides)o, axis),
        });
        Assert.That(run.Passed, run.ToString);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    public void RandomRollClears(int seed)
    {
        var run = ScenarioRun.Execute(typeof(TeaI1LimitCutScenario), 0, seed);
        Assert.That(run.Passed, run.ToString);
    }

    [Test]
    public void BotsSprintWhereRealNumbersDo()
    {
        static bool Sprinting(ScenarioProbe p, PartyRole role) => p.Member(role)!.FindStatus(JobActions.SprintStatus) != null;
        bool? first = null, fifth = null, seventh = null, third = null, fourth = null;
        var run = Run(MainTank, 22f, p =>
        {
            if (p.Crossed(8f)) first = Sprinting(p, ShieldHealer);
            if (p.Crossed(15f)) fifth = Sprinting(p, CasterDps);
            if (!p.Crossed(21f)) return;
            seventh = Sprinting(p, MeleeDpsB);
            third = Sprinting(p, OffTank);
            fourth = Sprinting(p, RegenHealer);
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True, "#1");
            Assert.That(fifth, Is.True, "#5");
            Assert.That(seventh, Is.True, "#7");
            Assert.That(third, Is.False, "#3");
            Assert.That(fourth, Is.False, "#4");
        });
    }

    // An instant cast from a helper the engine hasn't built yet is lost, so each pair goes up early.
    [Test]
    public void HawkBlasterHelpersStandReadyBeforeTheirWave()
    {
        int? before = null, ready = null;
        var run = Run(RegenHealer, 5.5f, p =>
        {
            int Helpers() => p.World.Children.OfType<SimEnemy>().Count(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.Helper);
            if (p.Crossed(4.2f)) before = Helpers();
            if (p.Crossed(4.5f)) ready = Helpers();
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Zero);
            Assert.That(ready, Is.EqualTo(2));
        });
    }

    [Test]
    public void EveryHawkBlasterHelperIsGoneByJKick()
    {
        var left = -1;
        var run = Run(RegenHealer, 31.5f, p =>
        {
            if (p.Crossed(31.4f))
                left = p.World.Children.OfType<SimEnemy>().Count(e => e.IsActive && e.BNpcBaseId == BNpcBaseId.Helper);
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(left, Is.Zero);
    }

    [Test]
    public void TheP2TrackStartsWithJKick()
    {
        var run = Run(RegenHealer, 31.5f, null, [], music: true);
        Assert.That(run.Passed, run.ToString);
        var switches = Timeline().Where(r => r["kind"] == "bgm" && r["detail"] == "switch").ToList();
        Assert.That(switches.Select(r => r["id"]), Is.EqualTo(new[] { Bgm.P2.ToString() }));
        Assert.That(At(switches[0]), Is.EqualTo(31.26f).Within(0.02f));
    }

    [Test]
    public void BossesSpeakTheirLines()
    {
        var run = Run(MainTank, 32f);
        Assert.That(run.Passed, run.ToString);
        var lines = Timeline().Where(r => r["kind"] == "battle_talk").ToList();
        Assert.That(lines.Select(l => (l["detail"].Split(' ')[0], l["id"])), Is.EqualTo(new[]
        {
            ($"speaker={BNpcNameId.CruiseChaser}", BattleTalkId.DesignationBlassty.ToString()),
            ($"speaker={BNpcNameId.BruteJustice}", BattleTalkId.BruteJusticeArrives.ToString()),
        }));
        Assert.That(lines.Select(At), Is.EqualTo(new[] { 0.11f, 31.14f }).Within(0.05f));
    }

    [Test]
    public void ReplayStateRoundTrips()
    {
        TeaI1LimitCutState? host = null;
        TeaI1LimitCutState? replayed = null;
        ScenarioRun.Execute(typeof(TeaI1LimitCutScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            StopAt = 1f,
            Probe = probe =>
            {
                if (!probe.Crossed(0.5f)) return;
                var scenario = probe.Game.Catalog.Scenarios.OfType<TeaI1LimitCutScenario>().Single();
                host = scenario.LastState;
                replayed = (TeaI1LimitCutState?)scenario.StartReplay(scenario.BuildReplayStateMessage()!, 0, MainTank, probe.World);
            },
        });

        Assert.That(host, Is.Not.Null);
        Assert.That(replayed, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(replayed!.Numbers, Is.EqualTo(host!.Numbers));
            Assert.That(replayed.FirstBlasterAxis, Is.EqualTo(host.FirstBlasterAxis));
        });
    }

    [Test]
    public void ReplayStateRejectsBadRolls()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TeaI1LimitCutState.FromNetworkReplay(PinnedNumbers, 4), Is.Null);
            Assert.That(TeaI1LimitCutState.FromNetworkReplay(PinnedNumbers[..7], 0), Is.Null);
            Assert.That(TeaI1LimitCutState.FromNetworkReplay([.. PinnedNumbers[..7], ShieldHealer], 0), Is.Null);
            Assert.That(TeaI1LimitCutState.FromNetworkReplay(PinnedNumbers, 3), Is.Not.Null);
        });
    }
}
