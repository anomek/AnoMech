using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// A peer whose download can't keep up with what the host sends falls further behind every second
// while still hearing from the host, so nothing else notices. Once it's more than 10 s behind, the
// host ends the run for everyone, saying who. In every multiplayer scenario, with the peer's
// download well under what the host sends: the run ends this way only once the peer really is
// that far behind, and if it doesn't, the peer never got a ping cycle past it.
public class FallingBehindTests
{
    // Two thirds or less of what a fight's host sends each second.
    private static readonly ConnectionProfile Starved = TestRelay.Perfect with { Name = "12 KB/s download", DownloadRate = 12_000 };
    private const double LimitSeconds = 10, PingSeconds = 2;

    private static IEnumerable<TestCaseData> Scenarios() =>
        from scenario in ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer)
        select new TestCaseData(scenario.GetType()).SetArgDisplayNames(scenario.FullName());

    [TestCaseSource(nameof(Scenarios))]
    public void TheHostEndsTheRunOnceAPeerFallsMoreThanTenSecondsBehind(Type scenarioType)
    {
        var run = MultiplayerRun.Play(scenarioType, 0, 1, new PlayerSeat(PartyRole.MeleeDpsA), [new PlayerSeat(PartyRole.CasterDps)],
            peerLink: Starved, immortal: true);
        Assert.That(run.Failure, Is.Null);
        var behind = run.Relay.MaxHostDelay;
        var reason = run.Host.Multiplayer.RunEndReason;
        TestContext.Out.WriteLine($"Fell {behind:F1}s behind; {reason ?? "the run went on"}.");
        if (reason == null)
        {
            Assert.That(behind, Is.LessThan(LimitSeconds + PingSeconds), "a peer this far behind kept playing");
            return;
        }
        Assert.That(reason, Is.EqualTo($"{run.Peers[0].Name} fell more than {LimitSeconds} seconds behind"));
        // A frame's slack: a ping can reach the peer a frame before its answer reaches the host.
        Assert.That(behind, Is.GreaterThan(LimitSeconds - 0.1), "the run ended before the peer was that far behind");
        Assert.That(run.Peers[0].Multiplayer.RunEndReason, Is.EqualTo(reason), "the peer never learned why its run ended");
    }
}
