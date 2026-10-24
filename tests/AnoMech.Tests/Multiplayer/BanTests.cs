using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

// A ban takes the banned player's whole network out of the session, as the relay sees it: anyone
// else connected from that address goes with them and can't come back, and the host, which named
// only the one, drops them too. Mid-fight, with another peer on a network of its own.
public class BanTests
{
    private const double BanAt = 20;

    [Test]
    public void ABanTakesEveryoneOnTheBannedPlayersNetworkOutOfTheSession()
    {
        var run = MultiplayerRun.Play(typeof(UmadP3BlackHoleScenario), 0, 1, new PlayerSeat(PartyRole.MeleeDpsA),
            [new PlayerSeat(PartyRole.CasterDps), new PlayerSeat(PartyRole.RegenHealer), new PlayerSeat(PartyRole.MainTank)],
            immortal: true, interrupt: relay => relay.SameNetwork(1, 2),
            inputs: [(BanAt, 0, host => host.Multiplayer.BanPeer(host.Multiplayer.Session.ClaimedBy[PartyRole.CasterDps]))]);
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.InputsLeft, Is.Zero, "the fight ended before the ban");

        var (banned, housemate, elsewhere) = (run.Peers[0], run.Peers[1], run.Peers[2]);
        var lobby = run.Host.Multiplayer.Session;
        Assert.That(lobby.Names.Keys, Is.EquivalentTo(new[] { run.Host.PeerId, elsewhere.PeerId }), "the players the host's lobby lists");
        Assert.That(lobby.ClaimedBy.Keys, Is.EquivalentTo(new[] { run.Host.Seat, elsewhere.Seat }), "the seats taken in the host's lobby");
        foreach (var gone in new[] { banned, housemate })
        {
            Assert.That(gone.Multiplayer.SessionEndReason, Is.EqualTo("You were banned from the session by the host."), $"what {gone.Name}'s client says");
            Assert.That(gone.Game.World.Map.IsInInstance, Is.False, $"{gone.Name}'s client still in the fight");
        }
        Assert.That(elsewhere.Multiplayer.SessionCode, Is.Not.Null, "the peer on another network left the session");
    }
}
