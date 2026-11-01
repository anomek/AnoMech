using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P2LightRampant;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

// However a peer's run ends, nothing the host's packets put on its client stays: no copy of an
// actor or event object the host spawned, no tether shown for one, and its own player without the
// host's statuses, control locks or tether. Fights ended mid-way each way a run can end for a peer.
public class PeerCleanupTests
{
    private const double EndAt = 20;
    // Down for longer than any run lasts.
    private const double Forever = 1e6;
    // Long enough for the host to end the run over a peer that stopped answering.
    private const double GiveUpAfter = 15;

    public enum Ending
    {
        HostResets,
        HostLeavesTheFight,
        HostLeavesTheSession,
        HostVanishes,
        PeerAsksToLeaveTheFight,
        PeerLeavesTheSession,
        PeerIsKicked,
        PeerLosesItsLinkThenLeaves,
    }

    [TestCase(Ending.HostResets)]
    [TestCase(Ending.HostLeavesTheFight)]
    [TestCase(Ending.HostLeavesTheSession)]
    [TestCase(Ending.HostVanishes)]
    [TestCase(Ending.PeerAsksToLeaveTheFight)]
    [TestCase(Ending.PeerLeavesTheSession)]
    [TestCase(Ending.PeerIsKicked)]
    [TestCase(Ending.PeerLosesItsLinkThenLeaves)]
    public void APeerKeepsNothingOfTheHostsOnceItsRunEnds(Ending ending)
    {
        TestRelay? relay = null;
        List<string> before = [];
        var inputs = Inputs(ending, () => relay!).Prepend((EndAt - 0.5, 1, peer => before = SessionChecks.Leftovers(peer))).ToList();
        var run = MultiplayerRun.Play(typeof(UmadP3BlackHoleScenario), 0, 1, new PlayerSeat(PartyRole.MeleeDpsA), [new PlayerSeat(PartyRole.CasterDps)],
            immortal: true, interrupt: r => relay = r, inputs: inputs);
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.InputsLeft, Is.Zero, "the run ended before the ending");
        Assert.That(before, Is.Not.Empty, "the peer had nothing of the host's to keep");
        Assert.That(SessionChecks.Leftovers(run.Peers[0]), Is.Empty);
    }

    // The peer's player outlives the stay, and the next run's doppels come back with the same
    // entity ids: a chain left on it reappears, tied to whoever now holds the id.
    [Test]
    public void APeersPlayerKeepsNoChainOnceItsRunEnds([Values] Ending ending)
    {
        const PartyRole chained = PartyRole.ShieldHealer;
        TestRelay? relay = null;
        ushort held = 0;
        var inputs = Inputs(ending, () => relay!)
            .Prepend((EndAt - 0.5, 1, peer => held = peer.Fake.BattleCharas.Player.Tethers.GetValueOrDefault((byte)0))).ToList();
        var run = MultiplayerRun.Play(typeof(FruP2LightRampantScenario), 0, 1, new PlayerSeat(PartyRole.RegenHealer), [new PlayerSeat(chained)],
            immortal: true, interrupt: r => relay = r, inputs: inputs,
            configure: client =>
            {
                if (client.IsHost)
                    ((FruP2LightRampantStateOverrides)client.Game.Catalog.Scenarios.OfType<FruP2LightRampantScenario>().Single().SettingsOverrides).Duty[chained] = LightRampantDuty.Tether;
            });
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.InputsLeft, Is.Zero, "the run ended before the ending");
        Assert.That(held, Is.Not.Zero, "the peer's player held no chain");
        Assert.That(run.Peers[0].Fake.BattleCharas.Player.Tethers.Values.Where(id => id != 0), Is.Empty);
    }

    private static IEnumerable<(double At, int Client, Action<TestClient> Input)> Inputs(Ending ending, Func<TestRelay> relay)
    {
        switch (ending)
        {
            case Ending.HostResets:
                yield return (EndAt, 0, host => host.Game.Reset());
                break;
            case Ending.HostLeavesTheFight:
                yield return (EndAt, 0, host => host.Game.Leave());
                break;
            case Ending.HostLeavesTheSession:
                yield return (EndAt, 0, LeaveSession);
                break;
            case Ending.HostVanishes:
                yield return (EndAt, 0, _ => relay().Schedule(0, relay().Now, new LinkDrops(Forever)));
                break;
            case Ending.PeerAsksToLeaveTheFight:
                yield return (EndAt, 1, peer => peer.Multiplayer.RequestLeaveInstance());
                break;
            case Ending.PeerLeavesTheSession:
                yield return (EndAt, 1, LeaveSession);
                break;
            case Ending.PeerIsKicked:
                yield return (EndAt, 0, host => host.Multiplayer.KickPeer(host.Multiplayer.Session.Names.Keys.First(id => id != host.PeerId)));
                break;
            case Ending.PeerLosesItsLinkThenLeaves:
                yield return (EndAt, 1, _ => relay().Schedule(1, relay().Now, new LinkDrops(Forever)));
                yield return (EndAt + GiveUpAfter, 1, LeaveSession);
                break;
        }
    }

    // As the window's Disconnect button.
    private static void LeaveSession(TestClient client)
    {
        client.Multiplayer.LeaveSession();
        if (client.Game.World.Map.IsInInstance) client.Game.Leave();
    }
}
