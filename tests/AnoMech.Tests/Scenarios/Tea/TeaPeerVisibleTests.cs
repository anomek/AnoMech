using System.Globalization;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea.I1LimitCut;
using AnoMech.Scenarios.Tea.I2TemporalStasis;
using AnoMech.Scenarios.Tea.P3AlexanderPrime;
using AnoMech.Scenarios.Tea.P4PerfectAlexander;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Tests;

// What a bot peer's own client shows in every TEA phase, on a clean link and on bad Wi-Fi: the rules
// TeaVisibleTests checks on one client, with the slack the link explains, and each phase's turns,
// orbs and verdicts as the peer sees them. Nobody dies on the host, so every run shows the whole
// fight, however the peer's bot fares on its link.
public class TeaPeerVisibleTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA);
    private static readonly PlayerSeat Peer = new(PartyRole.CasterDps);
    private static readonly ConnectionProfile[] Links = [TestRelay.Perfect, BadConnections.BadWifiPeer];

    private static readonly uint[] CruiseChaserHits = [ActionId.AlphaSword, ActionId.SuperBlasstyCharge, ActionId.AlphaSwordP3, ActionId.SuperBlasstyChargeP3];

    private static IEnumerable<TestCaseData> Strats()
        => from scenario in ScenarioCatalog.Create().Scenarios
           where scenario.GetType().Namespace!.StartsWith("AnoMech.Scenarios.Tea.", StringComparison.Ordinal)
           from strat in Enumerable.Range(0, scenario.AiStrats.Count)
           from link in Links
           select new TestCaseData(scenario.GetType(), strat, link).SetArgDisplayNames(scenario.GetType().Name, strat.ToString(CultureInfo.InvariantCulture), link.Name);

    private static IEnumerable<TestCaseData> OnLinks(params Type[] scenarioTypes)
        => from type in scenarioTypes
           from link in Links
           select new TestCaseData(type, link).SetArgDisplayNames(type.Name, link.Name);

    private static IEnumerable<TestCaseData> CruiseChaserPhases() => OnLinks(typeof(TeaI1LimitCutScenario), typeof(TeaI2TemporalStasisScenario), typeof(TeaP3AlexanderPrimeScenario));

    private static IEnumerable<TestCaseData> AlexanderPrime() => OnLinks(typeof(TeaP3AlexanderPrimeScenario));

    private static IEnumerable<TestCaseData> PerfectAlexander() => OnLinks(typeof(TeaP4PerfectAlexanderScenario));

    private static readonly Dictionary<(Type, int, string), MultiplayerRun> Played = [];

    private static MultiplayerRun Play(Type scenarioType, int strat, ConnectionProfile link)
    {
        if (!Played.TryGetValue((scenarioType, strat, link.Name), out var run))
            Played[(scenarioType, strat, link.Name)] = run = MultiplayerRun.Play(scenarioType, strat, 1, Host, [Peer], link, link, immortal: true);
        Assert.That(run.Failure, Is.Null);
        return run;
    }

    private static IReadOnlyList<Dictionary<string, string>> PeerRows(MultiplayerRun run) => run.Peers[0].Fake.Timeline.Rows;

    // The peer's own entity ids of what the host spawned from `bnpcBaseId`.
    private static List<string> Spawned(MultiplayerRun run, uint bnpcBaseId)
        => PeerRows(run).Where(r => r["kind"] == "spawn" && r["id"] == bnpcBaseId.ToString(CultureInfo.InvariantCulture)).Select(r => r["actor"]).Distinct().ToList();

    [TestCaseSource(nameof(Strats))]
    public void APeerSeesEveryCastBarAsItsSheetCastTimeLessTheRelease(Type scenarioType, int strat, ConnectionProfile link)
        => Assert.That(TeaShown.BarsOffTheSheet(PeerRows(Play(scenarioType, strat, link))).ToList(), Is.Empty);

    // Each bar's action reaches the peer as far after it as on the host, give or take what the link
    // explains.
    [TestCaseSource(nameof(Strats))]
    public void APeerSeesEveryCastBarEndInItsOwnAction(Type scenarioType, int strat, ConnectionProfile link)
    {
        var run = Play(scenarioType, strat, link);
        Assert.That(TeaShown.BarsWithoutTheirAction(PeerRows(run), (float)ClientParity.Slack(run)).ToList(), Is.Empty);
    }

    [TestCaseSource(nameof(Strats))]
    public void APeerSeesEveryOmenPointWhereItsCastGoesOff(Type scenarioType, int strat, ConnectionProfile link)
        => Assert.That(TeaShown.OmensPointingElsewhere(PeerRows(Play(scenarioType, strat, link))).ToList(), Is.Empty);

    [TestCaseSource(nameof(Strats))]
    public void EveryTetherAPeerSeesComesDownAgain(Type scenarioType, int strat, ConnectionProfile link)
        => Assert.That(TeaShown.TethersLeftUp(PeerRows(Play(scenarioType, strat, link))).ToList(), Is.Empty);

    // On the peer's client too Cruise Chaser's model faces along every hit of his as it goes out, and
    // his three swords cut three ways onto three players.
    [TestCaseSource(nameof(CruiseChaserPhases))]
    public void APeerSeesCruiseChaserFaceEveryHit(Type scenarioType, ConnectionProfile link)
    {
        var run = Play(scenarioType, 0, link);
        var rows = PeerRows(run);
        var cruiseChasers = Spawned(run, BNpcBaseId.CruiseChaser);
        Assert.That(cruiseChasers, Is.Not.Empty);
        Assert.That(rows.Count(r => r["kind"] == "ability" && cruiseChasers.Contains(r["actor"]) && CruiseChaserHits.Contains(uint.Parse(r["id"], CultureInfo.InvariantCulture))),
            Is.EqualTo(scenarioType == typeof(TeaI1LimitCutScenario) ? 8 : scenarioType == typeof(TeaI2TemporalStasisScenario) ? 3 : 11), "his hits on the peer");
        Assert.Multiple(() =>
        {
            foreach (var cruiseChaser in cruiseChasers)
                TeaShown.FacesEachHitOnScreen(rows, cruiseChaser, CruiseChaserHits, $"{link.Name}, Cruise Chaser {cruiseChaser}");
            if (scenarioType != typeof(TeaI1LimitCutScenario))
                TeaShown.SwordsCutThreeWays(rows, cruiseChasers.Single(c => rows.Any(r => r["actor"] == c && r["id"] == ActionId.AlphaSwordP3.ToString())),
                    ActionId.AlphaSwordP3, link.Name);
        });
    }

    // Each orb goes off once on the peer's client too, on a player, and none bursts.
    [TestCaseSource(nameof(AlexanderPrime))]
    public void APeerSeesEveryOrbGoOff(Type scenarioType, ConnectionProfile link)
    {
        var run = Play(scenarioType, 0, link);
        var rows = PeerRows(run);
        var orbs = Spawned(run, BNpcBaseId.Plasmasphere);
        var peer = run.Peers[0];
        Assert.Multiple(() =>
        {
            Assert.That(orbs, Has.Count.EqualTo(4));
            foreach (var orb in orbs)
            {
                var pops = rows.Where(r => r["kind"] == "ability" && r["actor"] == orb && r["id"] == ActionId.Aetheroplasm.ToString()).ToList();
                Assert.That(pops, Has.Count.EqualTo(1), $"orb {orb}");
                foreach (var pop in pops)
                    Assert.That(ShownRun.Hits(pop).Any(hit => Enum.TryParse<PartyRole>(peer.NameAt(uint.Parse(hit, NumberStyles.HexNumber), ShownRun.Time(pop)), out _)), Is.True,
                        $"orb {orb} went off on nobody");
            }
            Assert.That(rows.Where(r => r["id"] == ActionId.Burst.ToString()), Is.Empty);
        });
    }

    // The four Ordained checks show the peer every player's verdict, and on a clean link every bot passes.
    [TestCaseSource(nameof(PerfectAlexander))]
    public void APeerSeesEveryOrdainedPass(Type scenarioType, ConnectionProfile link)
    {
        var run = Play(scenarioType, 0, link);
        var verdicts = PeerRows(run).Where(r => r["kind"] == "ability" && uint.Parse(r["id"], CultureInfo.InvariantCulture)
            is ActionId.OrdainedMotionSuccess or ActionId.OrdainedStillnessSuccess or ActionId.OrdainedMotionFail or ActionId.OrdainedStillnessFail).ToList();
        var passes = verdicts.Where(r => r["id"] == ActionId.OrdainedMotionSuccess.ToString() || r["id"] == ActionId.OrdainedStillnessSuccess.ToString()).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(verdicts, Has.Count.EqualTo(4 * PerRole.All.Length));
            Assert.That(verdicts.GroupBy(r => r["target"]).Select(g => g.Count()), Is.All.EqualTo(4), "a verdict per player per check");
            if (link == TestRelay.Perfect) Assert.That(passes, Has.Count.EqualTo(verdicts.Count), "fails among them");
        });
    }
}
