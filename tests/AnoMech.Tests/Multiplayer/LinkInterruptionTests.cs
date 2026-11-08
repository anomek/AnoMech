using System.Collections.Concurrent;
using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// The session's links breaking under whole multiplayer sessions, two bot peers with an immortal
// host so every fight runs to its end: a client's link drops, or its uploads or downloads die
// first, and it reconnects on the real backoff. Wherever that happens, each numbered message a
// client sent has to be taken by the other end once, in order and unchanged, with nothing either
// end needed gone from the other's log; every fight has to run to its end with each peer still
// seated and in it; and each peer's game has to end up shown what the host's was (ClientParity,
// its slack stretched by the resends), with the same actors left and its run clock on the host's.
// Where the links break comes from the sessions themselves: at every kind of message any client
// sends in any scenario, as it leaves and as it arrives, the recovery's own traffic included, and
// at random moments of every fight. A new message, or a new scenario sending one, is covered as
// it is.
[TestFixture]
public class LinkInterruptionTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA);
    private static readonly PlayerSeat[] Peers = [new(PartyRole.MainTank), new(PartyRole.CasterDps)];

    // Back at the first reconnect attempt.
    private static readonly LinkFault Drop = new LinkDrops(0);

    // The longest anyone may go unheard, a break and the ones overlapping it together: the host
    // ends the run for a client silent past its 8 s stale timeout, and a peer leaves a host that
    // is.
    private const double MaxUnheard = 6.5;
    // ReconnectBackoff: a link is back 2 s after it went if its outage is over by then, else 4 s
    // after that.
    private const double FirstReconnect = 2.1, SecondReconnect = 6.1;

    private static List<Type> Scenarios() =>
        ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer).Select(s => s.GetType()).ToList();

    // ── Every kind of message ───────────────────────────────────────────────

    [Test]
    public void ASessionGoesOnWhenALinkDropsRightAfterAnyMessageLeaves() => DropAt(Census.Value.Sent, arriving: false);

    // The message is lost with the link.
    [Test]
    public void ASessionGoesOnWhenALinkDropsAsAnyMessageArrives() => DropAt(Census.Value.Arrived, arriving: true);

    private static void DropAt(IReadOnlyList<Occasion> occasions, bool arriving)
    {
        var failures = new List<string>();
        foreach (var occasion in occasions)
        {
            var run = Play(occasion.Scenario, runs: 2, relay =>
            {
                var armedAt = 0;
                if (occasion.After is { } first)
                {
                    relay.Schedule(first.Client, first.At, first.Fault);
                    armedAt = 1;
                }
                bool Hit(Carried carried) => relay.FaultsApplied >= armedAt && Side(carried) == occasion.Side;
                if (arriving) relay.DropBeforeReceiving(occasion.Client, Hit, Drop);
                else relay.DropAfterSending(occasion.Client, Hit, Drop);
            });
            var problems = SessionChecks.Problems(run);
            if (run.Relay.TriggersPending > 0) problems.Insert(0, "the link never carried it");
            TestContext.Out.WriteLine($"{(problems.Count == 0 ? "ok" : "FAILED")}: {occasion}");
            failures.AddRange(problems.Take(8).Select(p => $"{occasion}: {p}"));
        }
        foreach (var failure in failures.Take(60)) TestContext.Out.WriteLine(failure);
        Assert.That(failures, Is.Empty, $"{failures.Count} problems across {occasions.Count} breaks");
    }

    // A kind of message, from the host or from a peer.
    private static string Side(Carried carried) => $"{carried.Kind} from {(carried.From == 0 ? "the host" : "a peer")}";

    private sealed record Moment(string Side, int Client, double At);

    // A session's first break, which some of what a session sends only follows.
    private sealed record Breakage(int Client, double At, LinkFault Fault)
    {
        public override string ToString() => $"{Who(Client)}'s link {Fault} at {At:F2}s";
    }

    // Where a link breaks: as a kind of message (Side) first leaves, or first reaches, Client.
    private sealed record Occasion(Type Scenario, string Side, int Client, double At, bool Arriving, Breakage? After)
    {
        public override string ToString() =>
            $"{Side} {(Arriving ? "reaching" : "leaving")} {Who(Client)} at {At:F2}s in {Scenario.Name}{(After is { } first ? $", after {first}" : "")}";
    }

    private static string Who(int client) => SessionChecks.Who(client);

    private sealed record Occasions(IReadOnlyList<Occasion> Sent, IReadOnlyList<Occasion> Arrived);

    // Each kind of message where a session first sends it, and where one first reaches a client,
    // in the first scenario having it. A resend, a reconnecting peer's Hello or a gap request
    // only follows a break: those come from sessions whose link broke once, an end's uploads or
    // its downloads dying, as what they send after it that the same session unbroken doesn't.
    private static readonly Lazy<Occasions> Census = new(() =>
    {
        var sent = new List<Occasion>();
        var arrived = new List<Occasion>();
        var seenSent = new HashSet<string>();
        var seenArrived = new HashSet<string>();
        var scenarios = Scenarios();
        foreach (var scenario in scenarios)
        {
            var clean = Clean(scenario);
            sent.AddRange(clean.Sent.Where(m => seenSent.Add(m.Side)).Select(m => new Occasion(scenario, m.Side, m.Client, m.At, false, null)));
            arrived.AddRange(clean.Arrived.Where(m => seenArrived.Add(m.Side)).Select(m => new Occasion(scenario, m.Side, m.Client, m.At, true, null)));
        }
        var first = scenarios[0];
        var unbroken = Clean(first);
        var at = unbroken.StartedAt + Math.Min(5, (unbroken.EndedAt - unbroken.StartedAt) / 2);
        foreach (var client in new[] { 0, 1 })
        foreach (var fault in new LinkFault[] { new UploadsDie(1, 0), new DownloadsDie(1, 0) })
        {
            var breakage = new Breakage(client, at, fault);
            var broken = Summarize(first, Play(first, runs: 2, relay => relay.Schedule(client, at, fault)));
            var usualSent = unbroken.Sent.Where(m => m.At >= at).Select(m => m.Side).ToHashSet();
            var usualArrived = unbroken.Arrived.Where(m => m.At >= at).Select(m => m.Side).ToHashSet();
            sent.AddRange(broken.Sent.Where(m => m.At >= at && usualSent.Add(m.Side)).Select(m => new Occasion(first, m.Side, m.Client, m.At, false, breakage)));
            arrived.AddRange(broken.Arrived.Where(m => m.At >= at && usualArrived.Add(m.Side)).Select(m => new Occasion(first, m.Side, m.Client, m.At, true, breakage)));
        }
        return new Occasions(sent, arrived);
    });

    // ── Random breaks ───────────────────────────────────────────────────────

    private static IEnumerable<TestCaseData> RandomBreaks() =>
        from scenario in ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer)
        from seed in Enumerable.Range(0, 4)
        select new TestCaseData(scenario.GetType(), seed).SetArgDisplayNames(scenario.FullName(), seed == 0 ? "no breaks" : $"breaks {seed}");

    // Seed 0 breaks nothing: the checks hold for the session as it is.
    [TestCaseSource(nameof(RandomBreaks))]
    public void ASessionGoesOnThroughLinksBreakingAtRandom(Type scenarioType, int seed)
    {
        var breaks = new List<Breakage>();
        if (seed != 0)
        {
            var clean = Clean(scenarioType);
            breaks = RandomBreakages(new Random(seed), clean.StartedAt, clean.EndedAt);
        }
        foreach (var breakage in breaks) TestContext.Out.WriteLine(breakage);
        var run = Play(scenarioType, runs: 1, relay =>
        {
            foreach (var breakage in breaks) relay.Schedule(breakage.Client, breakage.At, breakage.Fault);
        });
        foreach (var (client, down, up) in run.Relay.Outages) TestContext.Out.WriteLine($"{Who(client)} down {down:F2}s to {up:F2}s");
        TestContext.Out.WriteLine($"Worst resend {run.MaxStreamDelay():F2}s, parity within {ClientParity.Slack(run):F2}s");
        var problems = SessionChecks.Problems(run);
        foreach (var problem in problems.Take(40)) TestContext.Out.WriteLine(problem);
        Assert.That(problems, Is.Empty);
    }

    // Two to five breaks spread over the fight, on any client and sometimes overlapping, none
    // leaving anyone unheard past MaxUnheard.
    private static List<Breakage> RandomBreakages(Random rng, double from, double to)
    {
        var breaks = new List<Breakage>();
        var count = rng.Next(2, 6);
        var spacing = (to - from) / count;
        double windowStart = from, windowEnd = from - 1, at = from;
        for (var i = 0; i < count; i++)
        {
            var (fault, unheard) = RandomFault(rng);
            var overlap = windowStart + rng.NextDouble() * (windowEnd - windowStart);
            if (windowEnd > windowStart && rng.NextDouble() < 0.3 && Math.Max(windowEnd, overlap + unheard) - windowStart <= MaxUnheard)
            {
                at = overlap;
                windowEnd = Math.Max(windowEnd, at + unheard);
            }
            else
            {
                at = Math.Max(at, windowEnd + 1) + rng.NextDouble() * spacing;
                (windowStart, windowEnd) = (at, at + unheard);
            }
            breaks.Add(new Breakage(rng.Next(Peers.Length + 1), at, fault));
        }
        return breaks;
    }

    private static (LinkFault Fault, double Unheard) RandomFault(Random rng)
    {
        var dead = 0.05 + rng.NextDouble() * 3.3;
        return rng.Next(4) switch
        {
            0 => (new LinkDrops(rng.NextDouble() * 1.8), FirstReconnect),
            1 => (new LinkDrops(2.2 + rng.NextDouble() * 3), SecondReconnect),
            2 => (new UploadsDie(dead, rng.NextDouble() * 1.8), dead + FirstReconnect),
            _ => (new DownloadsDie(dead, rng.NextDouble() * 1.8), dead + FirstReconnect),
        };
    }

    // ── Sessions ────────────────────────────────────────────────────────────

    private static MultiplayerRun Play(Type scenario, int runs, Action<TestRelay> interrupt)
        => MultiplayerRun.Play(scenario, 0, 1, Host, Peers, immortal: true, runs: runs, interrupt: interrupt);

    // A session as the census reads it: its first fight's window, and what its links carried.
    private sealed record Summary(double StartedAt, double EndedAt, List<Moment> Sent, List<Moment> Arrived);

    private static readonly ConcurrentDictionary<Type, Summary> CleanRuns = new();

    private static Summary Clean(Type scenario) => CleanRuns.GetOrAdd(scenario, s => Summarize(s, Play(s, runs: 2, _ => { })));

    private static Summary Summarize(Type scenario, MultiplayerRun run)
    {
        if (run.Failure != null) throw new InvalidOperationException($"{scenario.Name} doesn't get through a session: {run.Failure}");
        return new Summary(run.Fights[0].StartedAt, run.Fights[0].EndedAt,
            run.Relay.Sends.Select(c => new Moment(Side(c), c.From, c.At)).ToList(),
            run.Relay.Delivered.Select(d => new Moment(Side(d.Carried), d.To, d.Carried.At)).ToList());
    }
}
