using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// Every seat but the host's played by a bot-controlled peer, each client rolling its own seed: a
// peer's Ai runs on the rolls the host broadcast, never on its own RNG, and on its own copy of the
// world, built from what its client was shown. Each has to head for the spot the host's Ai
// planned for that seat, and like any strat clear the fight. Every multiplayer scenario and strat,
// so a roll a replay message leaves out or garbles, or an actor a peer was never shown, shows up
// in whichever seat reads it. Every client plays with the plugin's defaults, user actions and tank
// mitigation on, so a hit that judges a player differently from a bot judges these seats too: a
// bot is a bot on whichever client it runs. Over a long line too, where a peer is shown each change
// a round trip after the host's own bots would act on it: a strat that reads one sooner than a
// person could react plays its seat on what that peer hasn't been shown yet.
public partial class DebugBotReplayTests
{
    private const PartyRole HostSeat = PartyRole.MeleeDpsA;
    // The spot before AiManager's jitter, which each client rolls itself; logged to 0.01y.
    private const float SpotTolerance = 0.05f;

    private static readonly ConnectionProfile LongLine = TestRelay.Perfect with
    {
        Name = "250 ms ping", UploadLatency = 0.125, DownloadLatency = 0.125, DownloadJitter = 0.03,
    };

    private static IEnumerable<TestCaseData> Strats() =>
        from scenario in ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer)
        from strat in scenario.AiStrats.Select((ai, index) => (ai, index))
        select new TestCaseData(scenario.GetType(), strat.index).SetArgDisplayNames(scenario.FullName(), strat.ai.Name);

    [TestCaseSource(nameof(Strats))]
    public void EveryBotPeerWalksWhereTheHostsAiSendsItsSeat(Type scenarioType, int strat) => Verify(scenarioType, strat, runs: 1);

    private static IEnumerable<TestCaseData> FirstStrats() =>
        from scenario in ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer)
        select new TestCaseData(scenario.GetType()).SetArgDisplayNames(scenario.FullName());

    // The stay's next run: each peer's bot starts over on that run's rolls and clock.
    [TestCaseSource(nameof(FirstStrats))]
    public void EveryBotPeerWalksWhereTheHostsAiSendsItsSeatAfterAReset(Type scenarioType) => Verify(scenarioType, 0, runs: 2);

    [TestCaseSource(nameof(FirstStrats))]
    public void EveryBotPeerWalksWhereTheHostsAiSendsItsSeatOverALongLine(Type scenarioType) => Verify(scenarioType, 0, runs: 1, LongLine);

    private static void Verify(Type scenarioType, int strat, int runs, ConnectionProfile? peerLink = null)
    {
        var peers = Enum.GetValues<PartyRole>().Where(r => r != HostSeat).Select(r => new PlayerSeat(r)).ToList();
        var run = MultiplayerRun.Play(scenarioType, strat, 1, new PlayerSeat(HostSeat), peers, peerLink: peerLink, runs: runs,
            configure: client => client.Fake.UserActions.Enabled = true);
        Assert.That(run.Failure, Is.Null);

        var wrong = new List<string>();
        foreach (var peer in run.Peers)
        {
            var planned = Moves(run.Host, peer.Seat);
            var walked = Moves(peer, peer.Seat);
            if (planned.Count != walked.Count)
                wrong.Add($"{peer.Seat}: the host's Ai moved it {planned.Count} times, its own peer {walked.Count}");
            for (var i = 0; i < Math.Min(planned.Count, walked.Count); i++)
                if (planned[i].At != walked[i].At || Vector2.Distance(planned[i].To, walked[i].To) > SpotTolerance)
                    wrong.Add($"{peer.Seat}: the host's Ai sent it to {planned[i]}, its peer went to {walked[i]}");
        }
        foreach (var line in wrong.Take(20)) TestContext.Out.WriteLine(line);
        Assert.That(wrong, Is.Empty, "seats a peer's Ai played differently from the host's");
        Assert.That(run.Deaths, Is.Empty, () => string.Join(", ", run.Deaths.Select(d => $"{d.Role}: {d.Cause} at {d.Time:F1}s")));
    }

    private sealed record Move(string At, Vector2 To)
    {
        public override string ToString() => $"({To.X:F2},{To.Y:F2}) at {At}s";
    }

    // The scheduled time as AiManager logs it, so both clients' lines for one move name it alike.
    private static List<Move> Moves(TestClient client, PartyRole seat)
    {
        var moves = new List<Move>();
        foreach (var line in client.Log.Lines)
            if (AiMove().Match(line) is { Success: true } move && move.Groups["role"].Value == seat.ToString())
                moves.Add(new Move(move.Groups["at"].Value,
                    new Vector2(float.Parse(move.Groups["x"].Value, CultureInfo.InvariantCulture), float.Parse(move.Groups["z"].Value, CultureInfo.InvariantCulture))));
        return moves;
    }

    [GeneratedRegex(@"\[AiManager\] Move@(?<at>[\d.]+): (?<role>\w+) \([-\d.]+,[-\d.]+\) -> \([-\d.]+,[-\d.]+\) for \((?<x>-?[\d.]+),(?<z>-?[\d.]+)\)")]
    private static partial Regex AiMove();
}
