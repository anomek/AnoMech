using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea.I2TemporalStasis;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: Brute Justice east, Cruise Chaser west; House Arrest MT + PR, Restraining
// Order OT + M1, Aggravated Assault H2 + C, nothing H1 + M2.
//
//    7.55  Debuffs and tethers; Temporal Stasis cast.
//   16.70  Everyone frozen until 25.60, every other status of theirs holding its time.
//   18.58  Plaints: Surety on each tethered pair, Severity on each Aggravated Assault.
//   20.62  Alpha Swords on Cruise Chaser's three closest at 20.62 / 21.69 / 22.76. Brute Justice turns
//          to his closest with the first sword's pick at 20.49 and flames once facing them (20.87 for
//          this roll's north bait), the second 2.32s later.
public class TeaI2TemporalStasisScenarioTests
{
    private static readonly (PartyRole Role, StasisDebuff Debuff)[] PinnedDebuffs =
    [
        (MainTank, StasisDebuff.HouseArrest), (OffTank, StasisDebuff.RestrainingOrder),
        (RegenHealer, StasisDebuff.None), (ShieldHealer, StasisDebuff.AggravatedAssault),
        (MeleeDpsA, StasisDebuff.RestrainingOrder), (MeleeDpsB, StasisDebuff.None),
        (PhysRangedDps, StasisDebuff.HouseArrest), (CasterDps, StasisDebuff.AggravatedAssault),
    ];

    private static void Pin(TeaI2TemporalStasisStateOverrides o)
    {
        o.BruteJusticeEast = true;
        foreach (var (role, debuff) in PinnedDebuffs) o.Debuff[role] = debuff;
    }

    private static NegativeRun<TeaI2TemporalStasisScenario> Stasis(PartyRole player)
        => Negative<TeaI2TemporalStasisScenario>(player).Overrides<TeaI2TemporalStasisStateOverrides>(Pin);

    [Test]
    public void PinnedRollClears()
    {
        var run = ScenarioRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            Overrides = o => Pin((TeaI2TemporalStasisStateOverrides)o),
        });
        Assert.That(run.Passed, run.ToString);
    }

    // Picks P2's track up where the slowest real kill leaves it, 193.87s after J Kick started it.
    [Test]
    public void TemporalStasisPicksUpTheTrackAtTheSlowestKill()
        => Assert.That(ScenarioCatalog.Create().Scenarios.OfType<TeaI2TemporalStasisScenario>().Single().BgmSecondsAtStart,
            Is.EqualTo(193.87f).Within(1e-3f));

    [Test]
    public void HouseArrestPartnersApartBothDie()
        => Stasis(PhysRangedDps)
            .TeleportAt(15f, to: new(6f, 8f))
            .ShouldKill(ActionId.PlaintOfSurety, MainTank, PhysRangedDps);

    [Test]
    public void RestrainingOrderPartnersTogetherBothDie()
        => Stasis(MeleeDpsA)
            .TeleportAt(15f, to: new(-10f, 0f))
            .ShouldKill(ActionId.PlaintOfSurety, OffTank, MeleeDpsA);

    // Plaint of Severity's vulnerabilities make the first sword lethal to its own bait; M2 behind it
    // is in the cone.
    [Test]
    public void AggravatedAssaultBaitingASwordDies()
        => Stasis(CasterDps)
            .TeleportAt(15f, to: new(-6f, 0.5f))
            .ShouldKill(ActionId.AlphaSwordP3, CasterDps, MeleeDpsB);

    // With time stopped, Plaint of Severity's Magic Vulnerability Up still holds at the second
    // Flarethrower; PR behind it is in the cone. MT and PR stand exactly on their spots, so C is the
    // second closest.
    [Test]
    public void AggravatedAssaultBaitingTheSecondFlarethrowerDies()
        => Stasis(CasterDps)
            .MoveBotAt(15f, MainTank, new(6f, -1.4f))
            .MoveBotAt(15f, PhysRangedDps, new(6f, 1.8f))
            .TeleportAt(15f, to: new(6f, 1.6f))
            .ShouldKill(ActionId.FlarethrowerP3, CasterDps, PhysRangedDps);

    [Test]
    public void StandingInAnotherPlayersSwordDies()
        => Stasis(ShieldHealer)
            .TeleportAt(15f, to: new(-6f, -8f))
            .ShouldKill(ActionId.AlphaSwordP3, ShieldHealer);

    // With every bot on its spot nobody baits both robots, so the burn is set by hand, on M2 as Cruise
    // Chaser's second bait.
    [Test]
    public void ASwordOnAFlarethrowersBaitKills()
        => Stasis(MeleeDpsB)
            .MoveBotAt(15f, RegenHealer, new(-6f, -1.4f))
            .TeleportAt(15f, to: new(-6f, 1.8f))
            .At(21f, p => p.Member(MeleeDpsB)!.AddStatus(StatusId.FireResistanceDownII, 6f))
            .ShouldKill(ActionId.AlphaSwordP3, MeleeDpsB);

    // The client plays a slash along the model's facing: each sword goes out on Cruise Chaser himself,
    // his model on screen already turned along it.
    [Test]
    public void EachSwordFollowsCruiseChasersTurnToItsBait()
    {
        string? cruiseChaser = null;
        var run = ScenarioRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            StopAt = 23.5f,
            Overrides = o => Pin((TeaI2TemporalStasisStateOverrides)o),
            Probe = p => cruiseChaser ??= p.Game.Catalog.Scenarios.OfType<TeaI2TemporalStasisScenario>().Single().LastState?.Objects.CruiseChaser
                ?.EntityId.ToString("X8"),
        });
        Assert.That(run.Passed, run.ToString);
        var rows = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows;
        var swords = rows.Where(r => r["actor"] == cruiseChaser && r["kind"] == "ability" && r["id"] == ActionId.AlphaSwordP3.ToString()).ToList();
        Assert.That(swords, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(swords.Select(s => s["targets"]).Distinct().Count(), Is.EqualTo(3), "each sword on its own player");
            Assert.That(swords.Select(s => s["target"]), Is.All.EqualTo(cruiseChaser));
            TeaShown.FacesEachHitOnScreen(rows, cruiseChaser!, [ActionId.AlphaSwordP3], "Cruise Chaser");
        });
    }

    // The client plays a flame along the model's facing too, but Brute Justice's model turns round to
    // each bait, and he fires on himself once facing them. MT, the closest, stands due north of him: a
    // half turn, 0.375s at a boss's 480 degrees a second. The times land up to two frames late.
    [Test]
    public void EachFlarethrowerFollowsBruteJusticesTurnToItsBait()
    {
        var frozen = new Frozen();
        var run = ScenarioRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            StopAt = 24f,
            Overrides = o => Pin((TeaI2TemporalStasisStateOverrides)o),
            Takeovers = [new Takeover(15f, new(6f, -1.4f))],
            Probe = frozen.Read,
        });
        Assert.That(run.Passed, run.ToString);
        var rows = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows.Where(r => r["actor"] == frozen.BruteJustice).ToList();
        var flames = rows.Where(r => r["kind"] == "ability" && r["id"] == ActionId.FlarethrowerP3.ToString()).ToList();
        Assert.That(flames, Has.Count.EqualTo(2));
        var (first, second) = (flames[0], flames[1]);
        Assert.Multiple(() =>
        {
            Assert.That(flames.Select(f => f["target"]), Is.All.EqualTo(frozen.BruteJustice));
            Assert.That(flames.Select(f => f["targets"]), Is.EqualTo(new[] { frozen.Ids[MainTank], frozen.Ids[PhysRangedDps] }));
            Assert.That(Number(first["t"]), Is.EqualTo(20.865f).Within(2 * ScenarioRun.FrameSeconds));
            Assert.That(Angle(Number(first["rot"]), MathF.PI), Is.LessThan(0.011f));
            Assert.That(Number(second["t"]) - Number(first["t"]), Is.EqualTo(2.32f).Within(0.02f));
            Assert.That(Angle(Number(second["rot"]), frozen.FacingTo(PhysRangedDps)), Is.LessThan(0.011f));
            TeaShown.TurnsRoundOnScreen(rows, frozen.BruteJustice!, 20.49f, Number(first["t"]), 0f, Number(first["rot"]), "the first flame");
            TeaShown.TurnsRoundOnScreen(rows, frozen.BruteJustice!, Number(first["t"]) + 1.9f, Number(second["t"]), Number(first["rot"]), Number(second["rot"]),
                "the second flame");
        });
    }

    // PR in front of Brute Justice and closer than MT: the first flame needs no turn.
    [Test]
    public void AFlarethrowerBaitInFrontFiresWithoutATurn()
    {
        var frozen = new Frozen();
        var run = ScenarioRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = PhysRangedDps,
            Overrides = o => Pin((TeaI2TemporalStasisStateOverrides)o),
            Takeovers = [new Takeover(15f, new(6f, 1f))],
            Probe = frozen.Read,
        });
        Assert.That(run.Passed, run.ToString);
        var flames = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows
            .Where(r => r["actor"] == frozen.BruteJustice && r["kind"] == "ability" && r["id"] == ActionId.FlarethrowerP3.ToString()).ToList();
        Assert.That(flames, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(flames.Select(f => f["targets"]), Is.EqualTo(new[] { frozen.Ids[PhysRangedDps], frozen.Ids[MainTank] }));
            Assert.That(Number(flames[0]["t"]), Is.EqualTo(20.65f).Within(2 * ScenarioRun.FrameSeconds));
            Assert.That(Angle(Number(flames[0]["rot"]), 0f), Is.LessThan(0.011f));
            Assert.That(Number(flames[1]["t"]) - Number(flames[0]["t"]), Is.EqualTo(2.32f).Within(0.02f));
            Assert.That(Angle(Number(flames[1]["rot"]), frozen.FacingTo(MainTank)), Is.LessThan(0.011f));
        });
    }

    // Where everyone stands once time has stopped, and who they are on the timeline.
    private sealed class Frozen
    {
        public string? BruteJustice;
        public readonly Dictionary<PartyRole, string> Ids = [];
        private readonly Dictionary<PartyRole, Vector3> positions = [];
        private Vector3 bruteJusticeAt;

        public void Read(ScenarioProbe p)
        {
            if (!p.Crossed(20f)) return;
            var robot = p.Game.Catalog.Scenarios.OfType<TeaI2TemporalStasisScenario>().Single().LastState!.Objects.BruteJustice!;
            BruteJustice = robot.EntityId.ToString("X8");
            bruteJusticeAt = robot.Position;
            foreach (var role in Enum.GetValues<PartyRole>())
            {
                Ids[role] = p.Member(role)!.EntityId.ToString("X8");
                positions[role] = p.Member(role)!.Position;
            }
        }

        public float FacingTo(PartyRole role) => MathF.Atan2(positions[role].X - bruteJusticeAt.X, positions[role].Z - bruteJusticeAt.Z);
    }

    // Under Temporal Displacement every other status keeps its remaining time; its own runs on.
    [Test]
    public void StatusTimersHoldWhileTimeIsStopped()
    {
        float? casterMagic = null, casterPhysical = null, mainTankFire = null, healerMagic = null, healerPhysical = null, displacement = null, casterMagicAfter = null;
        var run = ScenarioRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            StopAt = 26.6f,
            Overrides = o => Pin((TeaI2TemporalStasisStateOverrides)o),
            Probe = p =>
            {
                if (p.Crossed(25.5f))
                {
                    casterMagic = Remaining(p, CasterDps, StatusId.MagicVulnerabilityUp);
                    casterPhysical = Remaining(p, CasterDps, StatusId.PhysicalVulnerabilityUp);
                    mainTankFire = Remaining(p, MainTank, StatusId.FireResistanceDownII);
                    healerMagic = Remaining(p, RegenHealer, StatusId.MagicVulnerabilityUp);
                    healerPhysical = Remaining(p, RegenHealer, StatusId.PhysicalVulnerabilityUp);
                    displacement = Remaining(p, CasterDps, StatusId.TemporalDisplacement);
                }
                if (p.Crossed(26.5f)) casterMagicAfter = Remaining(p, CasterDps, StatusId.MagicVulnerabilityUp);
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.Multiple(() =>
        {
            Assert.That(casterMagic, Is.EqualTo(4f).Within(0.05f), "C's Magic Vulnerability Up from Plaint of Severity");
            Assert.That(casterPhysical, Is.EqualTo(11f).Within(0.05f), "C's Physical Vulnerability Up from Plaint of Severity");
            Assert.That(mainTankFire, Is.EqualTo(6f).Within(0.05f), "MT's Fire Resistance Down II from its Flarethrower");
            Assert.That(healerMagic, Is.EqualTo(5f).Within(0.05f), "H1's Magic Vulnerability Up from its Alpha Sword");
            Assert.That(healerPhysical, Is.EqualTo(5f).Within(0.05f), "H1's Physical Vulnerability Up from its Alpha Sword");
            Assert.That(displacement, Is.EqualTo(0.2f).Within(0.05f), "Temporal Displacement's own timer");
            Assert.That(casterMagicAfter, Is.EqualTo(3.1f).Within(0.05f), "C's Magic Vulnerability Up once time moves again");
        });
    }

    private static float? Remaining(ScenarioProbe p, PartyRole role, ushort statusId)
        => p.Member(role)?.ActiveStatuses.FirstOrDefault(s => s.StatusId == statusId)?.RemainingTime;

    [Test]
    public void TimeStopsAndResumesWithAOneSecondSkyChange()
    {
        var run = ScenarioRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            StopAt = 26f,
            Overrides = o => Pin((TeaI2TemporalStasisStateOverrides)o),
        });
        Assert.That(run.Passed, run.ToString);
        var weather = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows.Where(r => r["kind"] == "weather" && Number(r["t"]) > 10f).ToList();
        Assert.That(weather.Select(r => (r["id"], r["detail"])), Is.EqualTo(new[]
        {
            (Weather.BeyondTimeStopped.ToString(), "transition=1.0"),
            (Weather.BeyondTime.ToString(), "transition=1.0"),
        }));
        Assert.Multiple(() =>
        {
            Assert.That(Number(weather[0]["t"]), Is.EqualTo(16.70f).Within(0.02f));
            Assert.That(Number(weather[1]["t"]), Is.EqualTo(25.60f).Within(0.02f));
        });
    }

    [Test]
    public void TheArenaPlaysItsSlot7TimelineOnce()
    {
        var run = ScenarioRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsB,
            StopAt = 5f,
            Overrides = o => Pin((TeaI2TemporalStasisStateOverrides)o),
        });
        Assert.That(run.Passed, run.ToString);
        var plays = ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows
            .Where(r => r["kind"] == "map_effect" && r["id"] == "7" && r["detail"].Contains("timeline=2 ")).ToList();
        Assert.That(plays, Has.Count.EqualTo(1));
        Assert.That(Number(plays[0]["t"]), Is.EqualTo(1.19f).Within(0.02f));
    }

    private static float Number(string value) => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static float Angle(float a, float b) => MathF.Abs(MathF.IEEERemainder(a - b, MathF.Tau));

    private static TeaI2TemporalStasisState State(ScenarioProbe p)
        => p.Game.Catalog.Scenarios.OfType<TeaI2TemporalStasisScenario>().Single().LastState!;

    // The swords Cruise Chaser cuts while time stands still, on any roll and from either side: three
    // ways onto three players, his model facing along each.
    [Test]
    public void CruiseChasersThreeSwordsCutThreeWays([Values] bool bruteJusticeEast)
    {
        foreach (var seed in TeaShown.Seeds())
        {
            string? cruiseChaser = null;
            var shown = ShownRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, seed, new ScenarioRunOptions
            {
                Overrides = o => ((TeaI2TemporalStasisStateOverrides)o).BruteJusticeEast = bruteJusticeEast,
                Probe = p => cruiseChaser ??= State(p).Objects.CruiseChaser is { } chaser ? TeaShown.Id(chaser) : null,
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            TeaShown.SwordsCutThreeWays(shown.Rows, cruiseChaser!, ActionId.AlphaSwordP3, $"seed {seed}");
        }
    }

    // Each debuff on the players the roll dealt it to, from 7.55s to 18.45s, each pair tethered for as
    // long, and each Plaint on its own: Surety on the pair, Severity on each Aggravated Assault.
    [Test]
    public void EachDebuffShowsOnItsRolledPlayersAndItsPlaintFindsThem()
    {
        foreach (var seed in TeaShown.Seeds())
        {
            TeaI2TemporalStasisState? state = null;
            var shown = ShownRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, seed, new ScenarioRunOptions { Probe = p => state ??= State(p) });
            var what = $"seed {seed}";
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            string[] With(StasisDebuff debuff) => state!.Debuffs.Where(d => d.Value == debuff).Select(d => shown.IdOf(d.Key)).ToArray();
            string Support(StasisDebuff debuff) => shown.IdOf(TeaI2TemporalStasisState.Supports.Single(r => state!.Debuffs[r] == debuff));
            string Dps(StasisDebuff debuff) => shown.IdOf(TeaI2TemporalStasisState.Dps.Single(r => state!.Debuffs[r] == debuff));
            Assert.Multiple(() =>
            {
                foreach (var (debuff, status) in new[]
                         {
                             (StasisDebuff.AggravatedAssault, StatusId.AggravatedAssault), (StasisDebuff.HouseArrest, StatusId.HouseArrest),
                             (StasisDebuff.RestrainingOrder, StatusId.RestrainingOrder),
                         })
                {
                    Assert.That(shown.Of("status_gain", status).Select(r => r["actor"]), Is.EquivalentTo(With(debuff)), $"{what}: {debuff} on");
                    Assert.That(shown.Of("status_lose", status).Select(r => r["actor"]), Is.EquivalentTo(With(debuff)), $"{what}: {debuff} off");
                    Assert.That(shown.Of("status_gain", status).Select(ShownRun.Time), Is.All.EqualTo(7.55f).Within(0.05f), $"{what}: {debuff} on");
                    Assert.That(shown.Of("status_lose", status).Select(ShownRun.Time), Is.All.EqualTo(18.45f).Within(0.05f), $"{what}: {debuff} off");
                }
                foreach (var (debuff, tetherId) in new[] { (StasisDebuff.HouseArrest, TetherId.HouseArrest), (StasisDebuff.RestrainingOrder, TetherId.RestrainingOrder) })
                {
                    var tether = shown.Rows.FirstOrDefault(r => r["kind"] is "ac" or "ac_self" && r["id"] == "0x23" && r["p"].Split(',')[1] == $"0x{tetherId:X}");
                    Assert.That(tether is null ? Array.Empty<string>() : new[] { tether["actor"], tether["p"].Split(',')[2][2..] }, Is.EquivalentTo(new[] { Support(debuff), Dps(debuff) }),
                        $"{what}: {debuff}'s tether between its pair");
                    Assert.That(tether is null ? (float?)null : ShownRun.Time(tether), Is.EqualTo(7.55f).Within(0.05f), $"{what}: {debuff}'s tether");
                    Assert.That(shown.Rows.Where(r => r["actor"] == tether?["actor"] && r["kind"] is "ac" or "ac_self" && r["id"] == "0x2F").Select(ShownRun.Time).FirstOrDefault(),
                        Is.EqualTo(18.45f).Within(0.05f), $"{what}: {debuff}'s tether down");
                }
                var sureties = shown.Of("ability", ActionId.PlaintOfSurety).Select(r => ShownRun.Hits(r).Order().ToArray()).ToList();
                Assert.That(sureties, Is.EquivalentTo(new[]
                {
                    new[] { Support(StasisDebuff.HouseArrest), Dps(StasisDebuff.HouseArrest) }.Order().ToArray(),
                    new[] { Support(StasisDebuff.RestrainingOrder), Dps(StasisDebuff.RestrainingOrder) }.Order().ToArray(),
                }), $"{what}: Surety on each pair");
                Assert.That(shown.Of("ability", ActionId.PlaintOfSeverity).Select(r => ShownRun.Hits(r).Single()), Is.EquivalentTo(With(StasisDebuff.AggravatedAssault)),
                    $"{what}: Severity on each Aggravated Assault");
            });
        }
    }

    // Brute Justice's two flames, on any roll and from either side, each on its own bait alone.
    [Test]
    public void BruteJusticesTwoFlamesBurnTwoBaits([Values] bool bruteJusticeEast)
    {
        foreach (var seed in TeaShown.Seeds())
        {
            string? bruteJustice = null;
            var shown = ShownRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, seed, new ScenarioRunOptions
            {
                Overrides = o => ((TeaI2TemporalStasisStateOverrides)o).BruteJusticeEast = bruteJusticeEast,
                Probe = p => bruteJustice ??= State(p).Objects.BruteJustice is { } robot ? TeaShown.Id(robot) : null,
            });
            Assert.That(shown.Run.Passed, shown.Run.ToString);
            var flames = shown.Of("ability", ActionId.FlarethrowerP3).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(flames.Select(f => f["actor"]), Is.EqualTo(new[] { bruteJustice, bruteJustice }), $"seed {seed}");
                Assert.That(flames.Select(f => ShownRun.Hits(f).Single()).Distinct().Count(), Is.EqualTo(2), $"seed {seed}: two baits, one each");
                Assert.That(flames.Select(f => f["target"]), Is.All.EqualTo(bruteJustice), $"seed {seed}");
            });
        }
    }

    [Test]
    public void ReplayStateRoundTrips()
    {
        TeaI2TemporalStasisState? host = null;
        TeaI2TemporalStasisState? replayed = null;
        ScenarioRun.Execute(typeof(TeaI2TemporalStasisScenario), 0, Random.Shared.Next(), new ScenarioRunOptions
        {
            StopAt = 1f,
            Probe = probe =>
            {
                if (!probe.Crossed(0.5f)) return;
                var scenario = probe.Game.Catalog.Scenarios.OfType<TeaI2TemporalStasisScenario>().Single();
                host = scenario.LastState;
                replayed = (TeaI2TemporalStasisState?)scenario.StartReplay(scenario.BuildReplayStateMessage()!, 0, MainTank, probe.World);
            },
        });

        Assert.That(host, Is.Not.Null);
        Assert.That(replayed, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(replayed!.Debuffs, Is.EqualTo(host!.Debuffs));
            Assert.That(replayed.BruteJusticeEast, Is.EqualTo(host.BruteJusticeEast));
        });
    }

    [Test]
    public void ReplayStateRejectsBadRolls()
    {
        int[] good = [2, 3, 0, 1, 0, 1, 2, 3];
        Assert.Multiple(() =>
        {
            Assert.That(TeaI2TemporalStasisState.FromNetworkReplay(true, good), Is.Not.Null);
            Assert.That(TeaI2TemporalStasisState.FromNetworkReplay(true, good[..7]), Is.Null);
            Assert.That(TeaI2TemporalStasisState.FromNetworkReplay(true, [2, 2, 0, 1, 0, 1, 2, 3]), Is.Null);
            Assert.That(TeaI2TemporalStasisState.FromNetworkReplay(true, [2, 3, 0, 1, 0, 1, 2, 4]), Is.Null);
        });
    }
}
