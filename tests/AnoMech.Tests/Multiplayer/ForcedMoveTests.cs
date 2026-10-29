using System.Globalization;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// What a forced move shows the player it moves doesn't depend on whose client they play on. In
// every multiplayer scenario one seat is played in two sessions otherwise the same: once by the
// host's own player and once by a peer's, the other player in the other's seat. Every move the
// seat's own client is sent for its player (pushes, slides, carries and the animation they play,
// gimmick jumps, forced follows) has to come in both, at the same moment and to the same spot; and
// someone who never touches the controls, on an immortal host so the whole fight plays, has to be
// put in the same spots at the same moments, knockbacks the client takes from an ActionEffect
// included. A debug bot steers its seat on spots its own client rolls, so its moves are compared by
// kind and moment. No case names a mechanic, so a new forced move, or a scenario that starts using
// one, is covered as it lands.
public class ForcedMoveTests
{
    private const PartyRole Seat = PartyRole.CasterDps;
    private const PartyRole Other = PartyRole.MeleeDpsA;
    // Still for this long, the player has come to rest.
    private const double RestSeconds = 0.25;
    // A chase a peer's player starts a frame later ends a little apart; a move made twice, or to
    // the wrong spot, by yalms.
    private const float Moved = 0.01f, SameSpot = 0.5f;

    // Each a player's own: what their client makes of it is the move, and nobody else is sent it.
    private static readonly HashSet<uint> OwnMoves =
        PacketRules.Controls.Where(r => r.Value.Delivery == Delivery.Movement).Select(r => r.Key).ToHashSet();

    private static IEnumerable<TestCaseData> Cases() =>
        from scenario in ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer)
        from bot in new[] { false, true }
        select new TestCaseData(scenario.GetType(), bot).SetArgDisplayNames(scenario.FullName(), bot ? "debug bot" : "player");

    [TestCaseSource(nameof(Cases))]
    public void AForcedMoveIsTheSameWhicheverClientItsPlayerIsOn(Type scenarioType, bool bot)
    {
        var onHost = Play(scenarioType, bot, seatOnHost: true);
        var onPeer = Play(scenarioType, bot, seatOnHost: false);
        var slack = ClientParity.Slack(onPeer.Run);
        var problems = new List<string>();
        Compare(onHost.Moves, onPeer.Moves, slack, bot, problems);
        if (!bot) CompareRests(onHost.Rests, onPeer.Rests, slack, problems);
        TestContext.Out.WriteLine($"{onHost.Moves.Count} forced moves, {onHost.Rests.Count} rests on the host's own player; "
                                  + $"{onPeer.Moves.Count} and {onPeer.Rests.Count} on a peer's (within {slack:F2}s)");
        foreach (var problem in problems.Take(30)) TestContext.Out.WriteLine(problem);
        if (problems.Count > 0)
            foreach (var (session, who) in new[] { (onHost, "host's"), (onPeer, "peer's") })
            {
                foreach (var move in session.Moves) TestContext.Out.WriteLine($"  {who}: {move}");
                foreach (var rest in session.Rests) TestContext.Out.WriteLine($"  {who}: {rest}");
            }
        Assert.That(problems, Is.Empty, $"{problems.Count} differences");
    }

    // At: seconds into the host's fight. What: the control and its arguments, or the follow.
    private sealed record OwnMove(double At, string Kind, string What)
    {
        public override string ToString() => $"{What} at {At:F2}s";
    }

    // Where the player came to rest after a move that started At seconds into the host's fight.
    private sealed record Rest(double At, Vector3 Position)
    {
        public override string ToString() => $"({Position.X:F2},{Position.Z:F2}) after a move at {At:F2}s";
    }

    private sealed record Session(MultiplayerRun Run, List<OwnMove> Moves, List<Rest> Rests);

    private static Session Play(Type scenarioType, bool bot, bool seatOnHost)
    {
        var seat = new PlayerSeat(Seat, bot);
        var other = new PlayerSeat(Other, BotControlled: false);
        var trail = new List<(double At, Vector3 Position)>();
        var run = MultiplayerRun.Play(scenarioType, 0, 1, seatOnHost ? seat : other, [seatOnHost ? other : seat], immortal: true,
            configure: client => client.Fake.UserActions.Enabled = true,
            beforeFrame: client =>
            {
                if (client.Seat == Seat && client.Game.ActiveScenario is not null)
                    trail.Add((client.Fake.Timeline.Clock(), client.Fake.BattleCharas.Player.Position));
            });
        Assert.That(run.Failure, Is.Null, $"the session with the seat on {(seatOnHost ? "the host" : "a peer")}");
        var own = seatOnHost ? run.Host : run.Peers[0];
        return new Session(run, Moves(run, own), Rests(run, trail));
    }

    private static List<OwnMove> Moves(MultiplayerRun run, TestClient own)
    {
        var player = own.Fake.BattleCharas.Player.EntityId.ToString("X8");
        var moves = new List<OwnMove>();
        foreach (var row in own.Fake.Timeline.RunRows)
        {
            if (row["actor"] != player) continue;
            var at = double.Parse(row["t"], CultureInfo.InvariantCulture) - run.StartedAt;
            if (at < 0 || at > run.EndedAt - run.StartedAt) continue;
            if (row["kind"] is "ac_self" && OwnMoves.Contains(Convert.ToUInt32(row["id"], 16)))
                moves.Add(new OwnMove(at, row["id"], $"{row["id"]} {Arguments(row)}"));
            else if (row["kind"] is "forced_follow" or "forced_stop")
            {
                var follow = new OwnMove(at, row["kind"], $"{row["kind"]} {own.NameAt(row["target"] is { Length: > 0 } t ? uint.Parse(t, NumberStyles.HexNumber) : 0, at + run.StartedAt)}");
                // A chase is sent again as it goes, for as long as it lasts.
                if (moves.LastOrDefault()?.What != follow.What) moves.Add(follow);
            }
        }
        return moves;
    }

    private const uint WarpCategory = 0xF1;

    // A warp's last argument numbers the client's own warps, whoever they moved.
    private static string Arguments(Dictionary<string, string> row)
        => Convert.ToUInt32(row["id"], 16) == WarpCategory ? string.Join(",", row["p"].Split(',').Take(3)) : row["p"];

    // The spots the player was put in, each with when the move there started.
    private static List<Rest> Rests(MultiplayerRun run, List<(double At, Vector3 Position)> trail)
    {
        var rests = new List<Rest>();
        double? movingSince = null;
        double stillSince = 0;
        for (var i = 1; i < trail.Count; i++)
        {
            var (at, position) = trail[i];
            if (Vector3.Distance(position, trail[i - 1].Position) > Moved)
            {
                movingSince ??= trail[i - 1].At;
                stillSince = at;
                continue;
            }
            if (movingSince is not { } started || at - stillSince < RestSeconds) continue;
            rests.Add(new Rest(started - run.StartedAt, position));
            movingSince = null;
        }
        if (movingSince is { } last) rests.Add(new Rest(last - run.StartedAt, trail[^1].Position));
        return rests;
    }

    private static void Compare(List<OwnMove> onHost, List<OwnMove> onPeer, double slack, bool bot, List<string> problems)
    {
        var unmatched = onPeer.ToList();
        foreach (var move in onHost)
        {
            var match = unmatched.FindIndex(m => (bot ? m.Kind == move.Kind : m.What == move.What) && Math.Abs(m.At - move.At) <= slack);
            if (match >= 0) unmatched.RemoveAt(match);
            else problems.Add($"the host's own player was sent {move}; a peer's in the seat wasn't");
        }
        foreach (var move in unmatched) problems.Add($"a peer's own player was sent {move}; the host's in the seat wasn't");
    }

    private static void CompareRests(List<Rest> onHost, List<Rest> onPeer, double slack, List<string> problems)
    {
        for (var i = 0; i < Math.Max(onHost.Count, onPeer.Count); i++)
        {
            if (i >= onHost.Count || i >= onPeer.Count)
            {
                problems.Add(i >= onHost.Count
                    ? $"a peer's own player was moved {onPeer.Count - i} more times, first to {onPeer[i]}"
                    : $"the host's own player was moved {onHost.Count - i} more times, first to {onHost[i]}");
                return;
            }
            if (Math.Abs(onHost[i].At - onPeer[i].At) > slack || Vector3.Distance(onHost[i].Position, onPeer[i].Position) > SameSpot)
                problems.Add($"the host's own player was put at {onHost[i]}, a peer's at {onPeer[i]}");
        }
    }
}
