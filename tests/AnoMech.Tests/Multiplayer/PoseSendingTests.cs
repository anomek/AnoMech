using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

// A client sends its own pose as it changes, and an unchanged one again only once a second, for
// whoever missed it; the host sends the players' seats the same way. Everyone here is a player who
// only moves when the test moves them, with an immortal host so the fight runs to its end.
public class PoseSendingTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA, BotControlled: false);
    private static readonly PlayerSeat[] Peers = [new(PartyRole.MainTank, BotControlled: false), new(PartyRole.CasterDps, BotControlled: false)];
    private static readonly PartyRole[] Seats = [Host.Role, .. Peers.Select(p => p.Role)];

    // The start's own placement.
    private const double SettleSeconds = 2;
    // PoseRefreshMs, less a frame.
    private const double RefreshSeconds = 0.98;

    [Test]
    public void APoseThatHasNotChangedGoesOutOnceASecond()
    {
        var run = MultiplayerRun.Play(typeof(UmadP3BlackHoleScenario), 0, 1, Host, Peers, immortal: true);
        Assert.That(run.Failure, Is.Null);

        var from = run.StartedAt + SettleSeconds;
        var seconds = run.EndedAt - from;
        var problems = new List<string>();
        for (var client = 0; client <= Peers.Length; client++)
        {
            var poses = run.Relay.Sends
                .Where(s => s.From == client && s.At >= from && s.At <= run.EndedAt && s.Message is SelfPoseMessage or RolesSnapshotMessage)
                .ToList();
            for (var i = 1; i < poses.Count; i++)
                if (Same(poses[i - 1].Message, poses[i].Message) && poses[i].At - poses[i - 1].At < RefreshSeconds)
                    problems.Add($"client {client} sent the same pose again {poses[i].At - poses[i - 1].At:F2}s later, at {poses[i].At - run.StartedAt:F2}s");
            if (poses.Count < seconds * RefreshSeconds - 1)
                problems.Add($"client {client} sent {poses.Count} poses in {seconds:F0}s standing, short of one a second");
            TestContext.Out.WriteLine($"client {client}: {poses.Count} poses in {seconds:F1}s");
        }
        Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems.Take(20)));
    }

    // Peer1's link drops for a moment while it and Peer2 each walk a few steps: the host never
    // heard Peer1's, nor Peer1 Peer2's. Once it's back, every client has each player where they
    // stand.
    [Test]
    public void APeerBackFromALinkDropSeesTheOthersWhereTheyStandAndIsSeenWhereItStands()
    {
        TestRelay? relay = null;
        var real = new Dictionary<PartyRole, List<(double At, Vector3 Position)>>();
        var copies = new List<(double At, string Viewer, PartyRole Seat, Vector3 Position)>();
        var run = MultiplayerRun.Play(typeof(UmadP3BlackHoleScenario), 0, 1, Host, Peers, immortal: true,
            interrupt: r => relay = r,
            inputs:
            [
                (5.0, 1, _ => relay!.Schedule(1, relay.Now, new LinkDrops(0))),
                (5.5, 1, c => c.Fake.PlayerInput.Steer(new Vector2(0f, 1f), 6f)),
                (5.5, 2, c => c.Fake.PlayerInput.Steer(new Vector2(1f, 0f), 6f)),
                (6.5, 1, c => c.Fake.PlayerInput.Steer(null, 0f)),
                (6.5, 2, c => c.Fake.PlayerInput.Steer(null, 0f)),
            ],
            beforeFrame: client =>
            {
                if (client.Game.ActiveScenario is null) return;
                var at = (double)client.Fake.Timeline.Clock();
                foreach (var seat in Seats)
                {
                    if (client.Game.World.Party.Get(seat) is not { } member) continue;
                    if (seat == client.Seat) Track(real, seat).Add((at, member.Position));
                    else copies.Add((at, client.Name, seat, member.Position));
                }
            });
        Assert.That(run.Failure, Is.Null);

        var outage = run.Relay.Outages.Single(o => o.Client == 1);
        Assert.That(outage.Up, Is.LessThan(run.EndedAt - 5), "Peer1 was back well before the fight ended");
        foreach (var seat in new[] { Peers[0].Role, Peers[1].Role })
            Assert.That(Vector3.Distance(At(real[seat], outage.Down), At(real[seat], outage.Up)), Is.GreaterThan(3f), $"{seat} walked while Peer1's link was down");

        // The refresh's second and the trip, after Peer1 is back.
        var wrong = copies
            .Where(c => c.At >= outage.Up + 1.5 && StoodFor(real[c.Seat], c.At, 1.5)
                        && Vector3.Distance(c.Position, At(real[c.Seat], c.At)) > 0.1f)
            .ToList();
        Assert.That(wrong.Select(c => $"{c.Viewer} has {c.Seat} {Vector3.Distance(c.Position, At(real[c.Seat], c.At)):F1} y off at {c.At - run.StartedAt:F1}s").Distinct().Take(20),
            Is.Empty);
    }

    private static bool Same(MpMessage a, MpMessage b) => (a, b) switch
    {
        (SelfPoseMessage x, SelfPoseMessage y) => x == y,
        (RolesSnapshotMessage x, RolesSnapshotMessage y) => x.Roles.SequenceEqual(y.Roles),
        _ => false,
    };

    private static List<T> Track<TKey, T>(Dictionary<TKey, List<T>> into, TKey key) where TKey : notnull
    {
        if (!into.TryGetValue(key, out var list)) into[key] = list = [];
        return list;
    }

    // Where the player was at `at`, by their own last frame then.
    private static Vector3 At(List<(double At, Vector3 Position)> path, double at)
    {
        var i = path.FindLastIndex(p => p.At <= at);
        return path[Math.Max(i, 0)].Position;
    }

    private static bool StoodFor(List<(double At, Vector3 Position)> path, double at, double seconds)
    {
        var now = At(path, at);
        return path[0].At <= at - seconds && path.All(p => p.At < at - seconds || p.At > at || Vector3.Distance(p.Position, now) <= 0.01f);
    }
}
