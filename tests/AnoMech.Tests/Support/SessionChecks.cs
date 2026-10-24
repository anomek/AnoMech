using System.Text.Json;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;

namespace AnoMech.Tests;

// What has to hold once a multiplayer session is over, however its links fared: nothing failed or
// was refused; the host's fight ran to its end with each peer still seated and in it; each numbered
// message a client sent was taken by the other end once, in order and unchanged, with nothing either
// end needed gone from the other's log; and each peer's game was shown what the host's was
// (ClientParity), with the same actors left and its run clock on the host's.
internal static class SessionChecks
{
    // A peer's run clock against the host's: ahead by its poses' delay, or behind by no more than
    // a frame past what the peer leaves as jitter rather than catch up.
    private const double ClockAhead = 0.25, ClockBehind = MultiplayerManager.ClockCatchUpSeconds + 1.0 / 60;

    public static string Who(int client) => client == 0 ? "the host" : $"peer {client}";

    public static List<string> Problems(MultiplayerRun run)
    {
        if (run.Failure != null) return [run.Failure];
        var problems = run.Network.Rejected.Select(r => $"refused {r}").ToList();
        // An immortal host's fight runs to its end through the mechanics its players fail, as through
        // their deaths: a player whose link is down can't do their part.
        var ranToItsEnd = run.HostOutcome is SessionStatus.Succeeded or SessionStatus.CompletedWithMistake
                          || run.HostOutcome is SessionStatus.CompletedFailed && run.Host.Game.GodMode;
        if (!ranToItsEnd)
            problems.Add($"the host's fight ended {(run.HostOutcome is { } outcome ? outcome.ToString() : "before its end")}"
                         + string.Concat(run.Host.Log.Warnings.Where(w => w.StartsWith("[ScenarioSession] Mechanic failed", StringComparison.Ordinal)).Select(w => $"; {w}")));
        foreach (var lost in run.Network.Sends.Where(c => c.Message is StreamLostMessage))
            problems.Add($"{Who(lost.From)} no longer had what the other end missed: {lost.Message}");
        foreach (var client in run.Peers.Prepend(run.Host))
        {
            if (client.Multiplayer.RunEndReason is { } reason) problems.Add($"{client.Name}'s run ended: {reason}");
            problems.AddRange(client.Log.Warnings.Where(w => w.Contains("[SimAssets]")).Distinct().Select(w => $"{client.Name}: {w}"));
        }
        for (var i = 0; i < run.Peers.Count; i++)
        {
            var peer = run.Peers[i];
            if (run.Host.Multiplayer.Session.ClaimedBy.GetValueOrDefault(peer.Seat) != peer.PeerId) problems.Add($"{peer.Name} lost its seat");
            problems.AddRange(StreamProblems(run.Host, peer, run.Baseline(peer)));
            problems.AddRange(StreamProblems(peer, run.Host, 0));
            problems.AddRange(ClientParity.Compare(run, peer).Take(10).Select(gap => $"{peer.Name}: {gap}"));
            if (peer.Fake.BattleCharas.Live != run.Host.Fake.BattleCharas.Live)
                problems.Add($"{peer.Name} has {peer.Fake.BattleCharas.Live} characters left, the host {run.Host.Fake.BattleCharas.Live}");
            if (peer.Fake.EventObjects.Live != run.Host.Fake.EventObjects.Live)
                problems.Add($"{peer.Name} has {peer.Fake.EventObjects.Live} event objects left, the host {run.Host.Fake.EventObjects.Live}");
            var lead = run.PeerClocksAtEnd[i] - run.HostClockAtEnd;
            if (lead < -ClockBehind || lead > ClockAhead)
                problems.Add($"{peer.Name}'s run clock is {lead * 1000:+0;-0} ms off the host's, set by: {string.Join(" / ", ClockMoves(peer))}");
        }
        return problems;
    }

    // What moved a peer's run clock: its sync to the host's at the run's start, and each catch-up.
    private static IEnumerable<string> ClockMoves(TestClient peer)
    {
        lock (peer.Log.Lines) return peer.Log.Lines.Where(l => l.Contains("[Multiplayer] Peer: run clock", StringComparison.Ordinal)).ToList();
    }

    // What the host's packets left on a peer's client.
    public static List<string> Leftovers(TestClient peer)
    {
        var left = peer.Fake.BattleCharas.LiveEntityIds.Concat(peer.Fake.EventObjects.LiveEntityIds)
            .Select(id => peer.NameAt(id, double.MaxValue))
            .Where(name => name.StartsWith("actor ", StringComparison.Ordinal))
            .ToList();
        left.AddRange(peer.Game.World.Children.OfType<SimTether>().Select(t => "a tether"));
        if (peer.Fake.PlayerConditions.StatusAffliction) left.Add("a status lock on its player");
        if (peer.Fake.PlayerConditions.ControlAffliction) left.Add("a control lock on its player");
        left.AddRange(peer.Fake.BattleCharas.Player.Statuses.Keys.Select(id => $"status {id} on its player"));
        return left;
    }

    // All of `from`'s stream past where `to` took it up, each message once, in order, as it was
    // sent.
    private static IEnumerable<string> StreamProblems(TestClient from, TestClient to, long baseline)
    {
        var sent = from.StreamSent.Where(s => s.Message.Seq > baseline).ToList();
        var taken = MultiplayerRun.Taken(to, from);
        for (var i = 0; i < Math.Max(sent.Count, taken.Count); i++)
        {
            if (i >= taken.Count)
            {
                yield return $"{to.Name} never took {sent.Count - i} of {from.Name}'s messages, from #{sent[i].Message.Seq} ({sent[i].Message.GetType().Name})";
                yield break;
            }
            if (i >= sent.Count || taken[i].Seq != sent[i].Message.Seq)
            {
                yield return $"{to.Name} took {from.Name}'s #{taken[i].Seq} where {(i < sent.Count ? $"#{sent[i].Message.Seq}" : "nothing")} was next";
                yield break;
            }
            if (JsonSerializer.Serialize(taken[i]) != sent[i].Json)
            {
                yield return $"{from.Name}'s #{sent[i].Message.Seq} ({sent[i].Message.GetType().Name}) reached {to.Name} changed";
                yield break;
            }
        }
    }
}
