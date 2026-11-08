using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// However a peer's run ends, its client is left as the run found it: no copy of an actor or event
// object the host spawned, no tether shown for one, and its own player and the characters it keeps
// as they were; the host's too, once it has left. Every multiplayer fight, ended each way a run can
// end for a peer, at the moment it has the most on a peer's player: a leftover only shows where the
// fight put something there.
public class PeerCleanupTests
{
    private const PartyRole HostSeat = PartyRole.MeleeDpsA;
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
        PeerAsksToReset,
        PeerAsksToLeaveTheFight,
        PeerLeavesTheSession,
        PeerIsKicked,
        PeerLosesItsLinkThenLeaves,
    }

    private static IEnumerable<TestCaseData> Cases() =>
        from scenario in ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer)
        from ending in Enum.GetValues<Ending>()
        select new TestCaseData(scenario.GetType(), ending).SetArgDisplayNames(scenario.FullName(), ending.ToString());

    [TestCaseSource(nameof(Cases))]
    public void AnEndedRunLeavesAPeersClientAsItFoundIt(Type scenarioType, Ending ending)
    {
        var (seat, at) = Busiest(scenarioType);
        TestRelay? relay = null;
        var asFound = new Dictionary<string, Dictionary<string, string>>();
        List<string> before = [];
        var inputs = Inputs(ending, at, () => relay!).Prepend((at - 0.5, 1, peer => before = SessionChecks.Leftovers(peer, asFound[peer.Name]))).ToList();
        var run = MultiplayerRun.Play(scenarioType, 0, 1, new PlayerSeat(HostSeat), [new PlayerSeat(seat)], immortal: true,
            interrupt: r => relay = r, inputs: inputs,
            beforeFrame: client =>
            {
                if (!asFound.ContainsKey(client.Name)) asFound[client.Name] = SessionChecks.OwnPlayer(client);
            });
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.InputsLeft, Is.Zero, "the run ended before the ending");
        Assert.That(before, Is.Not.Empty, "the peer had nothing of the host's to keep");
        Assert.That(SessionChecks.Leftovers(run.Peers[0], asFound[run.Peers[0].Name]), Is.Empty, () => $"{seat}, ended at {at:F1}s");
        if (!run.Host.Game.World.Map.IsInInstance)
            Assert.That(SessionChecks.Leftovers(run.Host, asFound[run.Host.Name]), Is.Empty, () => $"the host, ended at {at:F1}s");
    }

    private static readonly Dictionary<Type, (PartyRole Seat, double At)> Scanned = new();

    // The seat and moment a fight puts the most on a peer's player, a tether above all: every seat
    // a peer's, played through once, and the middle of the longest stretch that holds the most. One
    // that never touches a player comes out as the middle of the fight, with its actors to clear.
    private static (PartyRole Seat, double At) Busiest(Type scenarioType)
    {
        if (Scanned.TryGetValue(scenarioType, out var known)) return known;
        var held = new Dictionary<PartyRole, List<(double At, int Weight)>>();
        var run = MultiplayerRun.Play(scenarioType, 0, 1, new PlayerSeat(HostSeat),
            Enum.GetValues<PartyRole>().Where(r => r != HostSeat).Select(r => new PlayerSeat(r)).ToList(), immortal: true,
            beforeFrame: client =>
            {
                if (client.IsHost || client.Game.ActiveScenario is null) return;
                var player = client.Fake.BattleCharas.Player;
                if (!held.TryGetValue(client.Seat, out var track)) held[client.Seat] = track = [];
                track.Add((client.Game.Events.Elapsed, 100 * player.Tethers.Count + player.Statuses.Count));
            });
        Assert.That(run.Failure, Is.Null);
        var most = held.Select(h => Longest(h.Key, h.Value)).MaxBy(s => (s.Weight, s.Seconds));
        return Scanned[scenarioType] = (most.Seat, most.At);
    }

    private static (int Weight, double Seconds, PartyRole Seat, double At) Longest(PartyRole seat, List<(double At, int Weight)> track)
    {
        var most = track.Max(t => t.Weight);
        var (longest, middle, from) = (0.0, 0.0, -1);
        for (var i = 0; i <= track.Count; i++)
        {
            if (i < track.Count && track[i].Weight == most)
            {
                if (from < 0) from = i;
                continue;
            }
            if (from < 0) continue;
            var (start, end) = (track[from].At, track[i - 1].At);
            if (end - start > longest) (longest, middle) = (end - start, (start + end) / 2);
            from = -1;
        }
        return (most, longest, seat, middle);
    }

    private static IEnumerable<(double At, int Client, Action<TestClient> Input)> Inputs(Ending ending, double at, Func<TestRelay> relay)
    {
        switch (ending)
        {
            case Ending.HostResets:
                yield return (at, 0, host => host.Game.Reset());
                break;
            case Ending.HostLeavesTheFight:
                yield return (at, 0, host => host.Game.Leave());
                break;
            case Ending.HostLeavesTheSession:
                yield return (at, 0, LeaveSession);
                break;
            case Ending.HostVanishes:
                yield return (at, 0, _ => relay().Schedule(0, relay().Now, new LinkDrops(Forever)));
                break;
            case Ending.PeerAsksToReset:
                yield return (at, 1, peer => peer.Multiplayer.RequestReset());
                break;
            case Ending.PeerAsksToLeaveTheFight:
                yield return (at, 1, peer => peer.Multiplayer.RequestLeaveInstance());
                break;
            case Ending.PeerLeavesTheSession:
                yield return (at, 1, LeaveSession);
                break;
            case Ending.PeerIsKicked:
                yield return (at, 0, host => host.Multiplayer.KickPeer(host.Multiplayer.Session.Names.Keys.First(id => id != host.PeerId)));
                break;
            case Ending.PeerLosesItsLinkThenLeaves:
                yield return (at, 1, _ => relay().Schedule(1, relay().Now, new LinkDrops(Forever)));
                yield return (at + GiveUpAfter, 1, LeaveSession);
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
