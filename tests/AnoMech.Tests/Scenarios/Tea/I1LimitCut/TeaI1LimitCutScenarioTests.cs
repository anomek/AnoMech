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

    // The client plays a hit along the model's facing: each one goes out on Cruise Chaser himself, his
    // model on screen already turned along it.
    [Test]
    public void EachHitFollowsCruiseChasersTurnToItsBait()
    {
        string? cruiseChaser = null;
        var run = Run(MainTank, 28.5f, p => cruiseChaser ??= p.Game.Catalog.Scenarios.OfType<TeaI1LimitCutScenario>().Single().LastState?.Objects.CruiseChaser
            ?.EntityId.ToString("X8"));
        Assert.That(run.Passed, run.ToString);
        var rows = Timeline();
        var hits = rows.Where(r => r["actor"] == cruiseChaser && r["kind"] == "ability"
                                   && (r["id"] == ActionId.AlphaSword.ToString() || r["id"] == ActionId.SuperBlasstyCharge.ToString())).ToList();
        Assert.That(hits, Has.Count.EqualTo(8));
        Assert.Multiple(() =>
        {
            Assert.That(hits.Select(h => h["targets"]).Distinct().Count(), Is.EqualTo(8), "each hit on its own player");
            Assert.That(hits.Select(h => h["target"]), Is.All.EqualTo(cruiseChaser));
            TeaShown.FacesEachHitOnScreen(rows, cruiseChaser!, [ActionId.AlphaSword, ActionId.SuperBlasstyCharge], "Cruise Chaser");
        });
    }

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
        static bool Sprinting(ScenarioProbe p, PartyRole role) => p.Member(role)!.FindStatus(SprintHandler.SprintStatusId) != null;
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

    private static TeaI1LimitCutState State(ScenarioProbe p) => p.Game.Catalog.Scenarios.OfType<TeaI1LimitCutScenario>().Single().LastState!;

    private static readonly float[] HitTimes = [12.64f, 14.14f, 17.31f, 18.80f, 21.95f, 23.42f, 26.58f, 28.05f];

    // On any roll: each number's marker on the player the roll gave it, from 3.19s 0.03s apart, and
    // Cruise Chaser's eight hits in number order and on time, each on its number alone with his model
    // facing along it.
    [Test]
    public void EveryNumberIsMarkedOnItsPlayerAndHitInTurn()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaI1LimitCutState? state = null;
            string? cruiseChaser = null;
            var shown = ShownRun.Execute(typeof(TeaI1LimitCutScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 29f,
                Probe = p =>
                {
                    state ??= State(p);
                    cruiseChaser ??= State(p).Objects.CruiseChaser is { } chaser ? TeaShown.Id(chaser) : null;
                },
            });
            var what = $"seed {seed}";
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var numbers = state!.Numbers.Select(shown.IdOf).ToList();
            var markers = shown.Rows.Where(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x22").ToList();
            var rows = shown.Rows.Where(r => r["actor"] == cruiseChaser).ToList();
            var hits = rows.Where(r => r["kind"] == "ability" && (r["id"] == ActionId.AlphaSword.ToString() || r["id"] == ActionId.SuperBlasstyCharge.ToString())).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(markers.Select(r => r["p"].Split(',')[..2]), Is.EqualTo(numbers.Select((id, k) => new[] { $"0x{LimitCutLockons[k]:X}", $"0x{id}" })),
                    $"{what}: the number markers, in order");
                Assert.That(markers.Select(ShownRun.Time), Is.EqualTo(Enumerable.Range(0, 8).Select(k => 3.19f + 0.03f * k)).Within(0.05f), what);
                Assert.That(hits.Select(h => h["id"]), Is.EqualTo(Enumerable.Range(0, 8).Select(k => (k % 2 == 0 ? ActionId.AlphaSword : ActionId.SuperBlasstyCharge).ToString())), what);
                Assert.That(hits.Select(h => ShownRun.Hits(h).Single()), Is.EqualTo(numbers), $"{what}: each hit on its number alone, in turn");
                Assert.That(hits.Select(ShownRun.Time), Is.EqualTo(HitTimes).Within(0.05f), what);
                TeaShown.FacesEachHitOnScreen(shown.Rows, cruiseChaser!, [ActionId.AlphaSword, ActionId.SuperBlasstyCharge], what);
            });
        }
    }

    private static readonly float[] WaveTimes = [5.27f, 7.49f, 9.69f, 11.90f, 14.05f, 16.25f, 18.49f, 20.68f, 22.83f, 25.07f];

    // From each first axis: every wave's two blasts 14y out either side, on time, each wave 45 degrees
    // clockwise of the last, the second run starting over on the first axis, waves 5 and 10 the centre.
    [Test]
    public void EveryHawkBlasterWaveGoesOffWhereItShould([Values(0, 1, 2, 3)] int axis)
    {
        var shown = ShownRun.Execute(typeof(TeaI1LimitCutScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            StopAt = 26f,
            Overrides = o => ((TeaI1LimitCutStateOverrides)o).FirstBlasterAxis = axis,
        });
        Assert.That(shown.Run.Passed, shown.Run.ToString);
        var blasts = shown.Of("ability", ActionId.HawkBlaster).ToList();
        Assert.Multiple(() =>
        {
            for (var wave = 0; wave < WaveTimes.Length; wave++)
            {
                var these = blasts.Where(b => MathF.Abs(ShownRun.Time(b) - WaveTimes[wave]) < 0.05f).ToList();
                Vector2[] spots = wave is 4 or 9
                    ? [Vector2.Zero]
                    : [OnCompass((axis + wave % 5) * 45f, Geometry.BlasterRadius), OnCompass((axis + wave % 5) * 45f + 180f, Geometry.BlasterRadius)];
                foreach (var spot in spots)
                    Assert.That(these.Any(b => Vector2.Distance(new(ShownRun.Number(b, "x") - 100f, ShownRun.Number(b, "z") - 100f), spot) < 0.05f), Is.True,
                        $"wave {wave + 1}: no blast at {spot}");
                Assert.That(these.All(b => spots.Any(spot => Vector2.Distance(new(ShownRun.Number(b, "x") - 100f, ShownRun.Number(b, "z") - 100f), spot) < 0.05f)), Is.True,
                    $"wave {wave + 1}: a blast off its spots");
            }
            Assert.That(blasts, Has.Count.EqualTo(18));
        });
    }

    // The pair whose turn it isn't stands between the baits and the wall at every hit on its side,
    // on any roll: numbers 1, 2, 5 and 6 bait on one side, 3, 4, 7 and 8 on the other.
    [Test]
    public void ThePairNotBaitingOnEachSideHugsTheWall()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            var radii = new Dictionary<(int Hit, int Number), float>();
            var shown = ShownRun.Execute(typeof(TeaI1LimitCutScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 29f,
                Probe = p =>
                {
                    for (var k = 0; k < HitTimes.Length; k++)
                    {
                        if (!p.Crossed(HitTimes[k])) continue;
                        var number = k + 1;
                        var other = number switch { 1 or 2 => 5, 5 or 6 => 1, 3 or 4 => 7, _ => 3 };
                        foreach (var waiting in new[] { other, other + 1 })
                            radii[(number, waiting)] = Flat(p.Member(State(p).Numbers[waiting - 1])!.Position).Length();
                    }
                },
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            Assert.That(radii.Where(r => r.Value < OnTheWall).Select(r => $"#{r.Key.Number} {r.Value:F2}y out at #{r.Key.Hit}'s hit"), Is.Empty, $"seed {seed}");
        }
    }

    // On a 20y arena, past the 13-14y the baits take.
    private const float OnTheWall = 15f;

    // Each bot follows its number's real track round the circle at the speed that keeps it on time:
    // few sharp turns or stops shorter than half a second, as they now run. A bot walking from point
    // to point instead turns and stutters at every one.
    [Test]
    public void BotsCircleWithoutSharpTurnsOrStutters()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            var tracks = new Dictionary<PartyRole, List<(float T, Vector2 At)>>();
            var shown = ShownRun.Execute(typeof(TeaI1LimitCutScenario), 0, seed, new ScenarioRunOptions
            {
                StopAt = 30f,
                Probe = p =>
                {
                    if (p.Time < 6f) return;
                    foreach (var role in PerRole.All)
                        if (p.Member(role) is { } member && p.World.Party.IsBotDriven(member))
                            (tracks.TryGetValue(role, out var track) ? track : tracks[role] = []).Add((p.Time, Flat(member.Position)));
                },
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var (sharp, stutters) = (0, 0);
            foreach (var track in tracks.Values)
            {
                var steps = Enumerable.Range(1, (track.Count - 1) / StepFrames).Select(i => (track[i * StepFrames].At - track[(i - 1) * StepFrames].At) / (track[i * StepFrames].T - track[(i - 1) * StepFrames].T)).ToList();
                for (var i = 1; i < steps.Count; i++)
                    if (steps[i - 1].Length() > 2f && steps[i].Length() > 2f
                        && Angle(MathF.Atan2(steps[i - 1].X, steps[i - 1].Y), MathF.Atan2(steps[i].X, steps[i].Y)) > MathF.PI / 4f)
                        sharp++;
                var still = 0;
                foreach (var step in steps)
                {
                    if (step.Length() < 0.8f) still++;
                    else
                    {
                        if (still is > 0 and <= 3) stutters++;
                        still = 0;
                    }
                }
            }
            Assert.Multiple(() =>
            {
                Assert.That(sharp, Is.LessThanOrEqualTo(15), $"seed {seed}: turns past 45 degrees in 0.15s");
                Assert.That(stutters, Is.LessThanOrEqualTo(40), $"seed {seed}: stops under half a second");
            });
        }
    }

    // 0.15s of frames.
    private const int StepFrames = 9;

    private static float Angle(float a, float b) => MathF.Abs(MathF.IEEERemainder(a - b, MathF.Tau));

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
