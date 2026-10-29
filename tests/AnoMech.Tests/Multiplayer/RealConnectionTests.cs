using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Umad.P3LimitCut;

namespace AnoMech.Tests;

// Sessions over real connections (LiveRelay): every client on a real RelayClient over TCP to the
// real relay on this machine, in real time. In every multiplayer scenario a full party, each player
// a debug bot on their own client, has to clear the fight with everything a session needs holding
// (SessionChecks) and every client showing the other players moving as RemotePlayerMotionTests
// requires. A session has to get through a player's link failing each way TestRelay plays it, on a
// real socket, with the real client's own drop, backoff and redial doing the work. And a player
// leaving mid-fight has to be gone for everyone at once, no client keeping anything of the host's.
//
// The test run parameter RealConnectionShard, "i/n", runs every n-th of these cases from the i-th,
// so CI can run them side by side as processes of their own.
public class RealConnectionTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA);
    private static readonly PlayerSeat[] FewPeers = [new(PartyRole.MainTank), new(PartyRole.CasterDps)];
    private static readonly Type MidFight = typeof(UmadP3LimitCutScenario);
    // Into the fight, with most of it still to come.
    private const double BreakAt = 15;
    // How soon everyone learns a player left.
    private const double GoneWithin = 2;

    private static List<IScenario> Scenarios() => ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer).ToList();

    // ── Every scenario, a full party ────────────────────────────────────────

    private static IEnumerable<TestCaseData> EachScenario() =>
        Shard(Scenarios().Select(s => new TestCaseData(s.GetType()).SetArgDisplayNames(s.FullName())), before: 0);

    [Category("RealConnection")]
    [TestCaseSource(nameof(EachScenario))]
    public void AFullPartyClearsTheFightWithEverythingHolding(Type scenarioType)
    {
        var track = new MotionTrack();
        var peers = Enum.GetValues<PartyRole>().Where(r => r != Host.Role).Select(r => new PlayerSeat(r)).ToList();
        var run = MultiplayerRun.Play(scenarioType, 0, 1, Host, peers, liveConnection: true,
            configure: client => client.Fake.UserActions.Enabled = true, beforeFrame: track.BeforeFrame);
        var problems = SessionChecks.Problems(run);
        problems.AddRange(run.Deaths.Select(d => $"{d.Role} died to \"{d.Cause}\" at t={d.Time:F2}"));
        if (run.Failure == null)
        {
            var motion = MotionCheck.Measure(run, (_, _) => 0, track, realTime: true);
            foreach (var line in motion.Lines) TestContext.Out.WriteLine(line);
            problems.AddRange(motion.Problems);
        }
        Verify(run, problems);
    }

    // ── A link failing ──────────────────────────────────────────────────────

    // A player's link failing at BreakAt, on the client (the host 0, then the peers).
    private sealed record Failure(string Name, int Client, LinkFault Fault);

    // Back at the first reconnect, at the second, or once the drop after a dying direction comes.
    private static readonly Failure[] Failures =
    [
        new("a peer's link drops", 1, new LinkDrops(0)),
        new("a peer's link drops for 3 s", 1, new LinkDrops(3)),
        new("the host's link drops", 0, new LinkDrops(0)),
        new("a peer's uploads die for 2 s, then its link drops", 1, new UploadsDie(2, 0)),
        new("a peer's downloads die for 2 s, then its link drops", 1, new DownloadsDie(2, 0)),
        new("the host's uploads die for 2 s, then its link drops", 0, new UploadsDie(2, 0)),
        new("the host's downloads die for 2 s, then its link drops", 0, new DownloadsDie(2, 0)),
    ];

    private static IEnumerable<TestCaseData> EachFailure() =>
        Shard(Failures.Select((f, i) => new TestCaseData(i).SetArgDisplayNames(f.Name)), before: Scenarios().Count);

    [Category("RealConnection")]
    [TestCaseSource(nameof(EachFailure))]
    public void ASessionGetsThroughALinkFailing(int which)
    {
        var failure = Failures[which];
        ISessionNetwork network = null!;
        var run = MultiplayerRun.Play(MidFight, 0, 1, Host, FewPeers, immortal: true, liveConnection: true, network: n => network = n,
            inputs: [(BreakAt, 0, _ => network.Schedule(failure.Client, network.Now, failure.Fault))]);
        var problems = SessionChecks.Problems(run);
        var outages = ((LiveRelay)run.Network).Outages.Where(o => o.Client == failure.Client).ToList();
        foreach (var (_, down, up) in outages) TestContext.Out.WriteLine($"{SessionChecks.Who(failure.Client)} down {down - run.StartedAt:F2}s to {up - run.StartedAt:F2}s into the fight");
        if (outages.Count == 0) problems.Insert(0, "the link never went down");
        else if (outages.Any(o => double.IsNaN(o.Up))) problems.Insert(0, "the link never came back");
        Verify(run, problems);
    }

    // ── A player leaving ────────────────────────────────────────────────────

    public enum Leaver
    {
        APeer,
        TheHost,
    }

    private static IEnumerable<TestCaseData> EachLeaver() =>
        Shard(Enum.GetValues<Leaver>().Select(l => new TestCaseData(l)), before: Scenarios().Count + Failures.Length);

    // Mid-fight, through the real client's own leave: the others learn at once, the run is over
    // for everyone (a seat gone usually dooms the mechanic, so the host calls it off), and no
    // peer's client keeps anything of the host's.
    [Category("RealConnection")]
    [TestCaseSource(nameof(EachLeaver))]
    public void APlayerWhoLeavesIsGoneForEveryoneAtOnce(Leaver leaver)
    {
        ISessionNetwork network = null!;
        var leaving = leaver == Leaver.TheHost ? 0 : 1;
        var (leftAt, leftId) = (double.NaN, Guid.Empty);
        List<string> before = [];
        var learned = new Dictionary<string, double>();
        var run = MultiplayerRun.Play(MidFight, 0, 1, Host, FewPeers, immortal: true, liveConnection: true, network: n => network = n,
            inputs:
            [
                (BreakAt - 0.5, 1, peer => before = SessionChecks.Leftovers(peer)),
                (BreakAt, leaving, client =>
                {
                    (leftAt, leftId) = (network.Now, client.PeerId);
                    client.Multiplayer.LeaveSession();
                    if (client.Game.World.Map.IsInInstance) client.Game.Leave();
                }),
            ],
            beforeFrame: client =>
            {
                if (double.IsNaN(leftAt) || client.PeerId == leftId || learned.ContainsKey(client.Name)) return;
                var told = leaver == Leaver.TheHost ? client.Multiplayer.SessionCode == null
                    : client.IsHost ? !client.Multiplayer.Session.Names.ContainsKey(leftId) : client.Game.ActiveScenario == null;
                if (told) learned[client.Name] = network.Now - leftAt;
            });
        List<string> problems = run.Failure is { } failure ? [failure] : [];
        if (run.InputsLeft > 0) problems.Add("the fight ended before anyone left");
        if (before.Count == 0) problems.Add("the peer had nothing of the host's to keep");
        var left = leaver == Leaver.TheHost ? run.Host : run.Peers[0];
        foreach (var client in run.Peers.Prepend(run.Host).Where(c => c != left))
        {
            if (!learned.TryGetValue(client.Name, out var after)) problems.Add($"{client.Name} never learned {left.Name} left");
            else if (after > GoneWithin) problems.Add($"{client.Name} learned {left.Name} left {after:F1}s after");
            else TestContext.Out.WriteLine($"{client.Name} learned {left.Name} left {after:F2}s after");
        }
        foreach (var peer in run.Peers)
            problems.AddRange(SessionChecks.Leftovers(peer).Select(thing => $"{peer.Name} kept {thing}"));
        Verify(run, problems);
    }

    // ── ──────────────────────────────────────────────────────────────────────

    private static void Verify(MultiplayerRun run, List<string> problems)
    {
        TestContext.Out.WriteLine($"Parity within {ClientParity.Slack(run):F2}s (worst delivery {run.Network.MaxHostDelay:F2}s, worst stream delay {run.MaxStreamDelay():F2}s)");
        foreach (var problem in problems.Take(60)) TestContext.Out.WriteLine(problem);
        Assert.That(problems, Is.Empty, $"{problems.Count} problems");
    }

    // RealConnectionShard "i/n": the i-th of every n of this fixture's cases, counting the `before`
    // cases of the sources ahead of this one.
    private static IEnumerable<TestCaseData> Shard(IEnumerable<TestCaseData> cases, int before)
    {
        if (TestContext.Parameters.Get("RealConnectionShard")?.Split('/') is not [var i, var n]
            || !int.TryParse(i, out var index) || !int.TryParse(n, out var count) || count < 1)
            return cases;
        return cases.Where((_, k) => (before + k) % count == index);
    }
}
