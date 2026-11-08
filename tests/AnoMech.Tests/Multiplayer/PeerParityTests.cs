using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// What a peer's game client is told against what the host's was, in every multiplayer scenario:
// each spawn, cast, action with all its hits and their results, status with its timer and source,
// head marker, tether, sign, prop state, map effect, director update, weather, death, fade and
// despawn has to reach the peer's game as well, within what the link explains (see ClientParity).
// No case names a mechanic, so a new scenario, or a new kind of packet a scenario starts sending,
// is covered the moment it exists.
public class PeerParityTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA);
    private const PartyRole PeerSeat = PartyRole.CasterDps;

    private static IEnumerable<IScenario> Scenarios => ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer);

    // A bot peer plays its seat through the whole fight on its own client. A player who never
    // touches the controls leaves the host immortal, so nobody dies and the fight runs to its end.
    private static IEnumerable<TestCaseData> CleanLink() =>
        from scenario in Scenarios
        from bot in new[] { true, false }
        select new TestCaseData(scenario.GetType(), bot).SetArgDisplayNames(scenario.FullName(), bot ? "bot peer" : "player peer");

    [TestCaseSource(nameof(CleanLink))]
    public void APeerSeesWhatTheHostSees(Type scenarioType, bool bot)
        => Verify(MultiplayerRun.Play(scenarioType, 0, 1, Host, [new PlayerSeat(PeerSeat, bot)], immortal: !bot));

    private static IEnumerable<TestCaseData> BadLinks() =>
        from scenario in Scenarios
        from link in BadConnections.All
        select new TestCaseData(scenario.GetType(), link).SetArgDisplayNames(scenario.FullName(), link.Name);

    [TestCaseSource(nameof(BadLinks))]
    public void APeerSeesWhatTheHostSeesOverABadConnection(Type scenarioType, ConnectionProfile link)
        => Verify(MultiplayerRun.Play(scenarioType, 0, 1, Host, [new PlayerSeat(PeerSeat)], link, link));

    private static IEnumerable<TestCaseData> EachScenario() =>
        from scenario in Scenarios
        select new TestCaseData(scenario.GetType()).SetArgDisplayNames(scenario.FullName());

    // The stay's next run: its stage comes from the host as well, onto an arena already the
    // host's, with every actor of the run before gone.
    [TestCaseSource(nameof(EachScenario))]
    public void APeerSeesWhatTheHostSeesAfterAReset(Type scenarioType)
        => Verify(MultiplayerRun.Play(scenarioType, 0, 1, Host, [new PlayerSeat(PeerSeat)], runs: 2));

    private static void Verify(MultiplayerRun run)
    {
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.Network.Rejected, Is.Empty, "messages the relay or a peer refused");
        var gaps = ClientParity.Compare(run, run.Peers[0]);
        TestContext.Out.WriteLine($"Within {ClientParity.Slack(run):F2}s (worst delivery {run.Network.MaxHostDelay:F2}s):");
        foreach (var gap in gaps.Take(40)) TestContext.Out.WriteLine($"  {gap}");
        foreach (var client in run.Peers.Prepend(run.Host))
        foreach (var warning in client.Log.Warnings.Where(w => w.Contains("[Multiplayer]") || w.Contains("[SimAssets]")).Distinct().Take(15))
            TestContext.Out.WriteLine($"  {client.Name} warned: {warning}");
        var refused = run.Peers.Prepend(run.Host).SelectMany(c => c.Log.Warnings).Where(w => w.Contains("[SimAssets]")).Distinct().ToList();
        Assert.That(refused, Is.Empty, "ids this build's allowlist doesn't have");
        Assert.That(gaps, Is.Empty, $"{gaps.Count} differences between what the host's and the peer's games were told");
        foreach (var peer in run.Peers)
        {
            Assert.That(peer.Fake.BattleCharas.Live, Is.EqualTo(run.Host.Fake.BattleCharas.Live), $"characters {peer.Name}'s client still has against the host's");
            Assert.That(peer.Fake.EventObjects.Live, Is.EqualTo(run.Host.Fake.EventObjects.Live), $"event objects {peer.Name}'s client still has against the host's");
        }
    }
}
