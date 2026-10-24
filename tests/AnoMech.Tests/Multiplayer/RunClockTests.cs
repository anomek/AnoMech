using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Scenarios.Umad.P3LimitCut;

namespace AnoMech.Tests;

// A peer's run clock against the host's (SessionChecks): set at the run's start, it stays where it
// was put, ahead of the host's by the peer's poses' own delay, whatever one ping went through.
public class RunClockTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA);
    private static readonly PlayerSeat[] Peers = [new(PartyRole.MainTank)];

    // A lost packet holding the peer's upload up, mid-fight.
    private const double StalledAfter = 10, Stall = 0.3;

    [Test]
    public void OneSlowPingLeavesAPeersRunClockOnTheHosts()
    {
        var run = MultiplayerRun.Play(typeof(UmadP3LimitCutScenario), 0, 1, Host, Peers, immortal: true,
            interrupt: relay => relay.StallAfterSending(1, carried => carried.Message is PongMessage && carried.At > StalledAfter, Stall));
        var reported = run.Relay.Sends.Select(c => c.Message).OfType<PeerStatusMessage>()
            .Select(s => s.Statuses.GetValueOrDefault(run.Peers[0].PeerId)?.LatencyMs ?? 0f);
        Assert.That(reported, Has.Some.GreaterThanOrEqualTo(Stall * 1000), "the host told the peer of the slow round trip");
        Assert.That(SessionChecks.Problems(run), Is.Empty);
    }
}
