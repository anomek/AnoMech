using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

// How every client shows another player's jump: the stages retail marks with packets of their own
// (ActorMoveFlags), in order, each sent the moment it reaches that client and at its own height,
// and the speed byte over the ground only. A peer's standing jump and running jump, and the
// host's own. Everyone is a player who only moves when the test moves them, with an immortal host
// so the fight runs on.
public class RemotePlayerJumpTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA, BotControlled: false);
    private static readonly PlayerSeat[] Peers = [new(PartyRole.MainTank, BotControlled: false), new(PartyRole.CasterDps, BotControlled: false)];

    private static readonly ConnectionProfile LongLine = TestRelay.Perfect with
    {
        Name = "250 ms ping", UploadLatency = 0.125, DownloadLatency = 0.125, DownloadJitter = 0.03,
    };

    // Bad Wi-Fi's stalls bunch a jump's poses up, so only its shape is checked there.
    private static IEnumerable<TestCaseData> Links() =>
    [
        new TestCaseData(TestRelay.Perfect, true).SetArgDisplayNames(TestRelay.Perfect.Name),
        new TestCaseData(LongLine, true).SetArgDisplayNames(LongLine.Name),
        new TestCaseData(BadConnections.BadWifiPeer, false).SetArgDisplayNames(BadConnections.BadWifiPeer.Name),
    ];

    private const float RunSpeed = 6f;
    // A stage reaches a client its link's delay after the jump, plus the frames the pose waits
    // for: its own client's send, the host's, and the copy's tick.
    private const double Late = 0.15;
    private const double Early = 0.02;
    private const float Height = 0.01f;
    private const float PeakHeight = 0.02f;
    // Standing: nothing to walk over the ground. Running: the player's own pace, give or take.
    private const float StandingSpeed = 0.1f;
    private const float RunningSpeedRatio = 1.2f;

    private sealed record Jump(PartyRole Seat, double At, float Ground, bool Running);

    private sealed record Move(double At, ActorMoveFlags Flags, float Y, float Speed);

    [TestCaseSource(nameof(Links))]
    public void EveryClientShowsAnotherPlayersJumpStageByStage(ConnectionProfile link, bool timed)
    {
        var jumps = new List<Jump>();
        Action<TestClient> Jumps(bool running) => c =>
        {
            jumps.Add(new Jump(c.Seat, c.Fake.Timeline.Clock(), c.Fake.BattleCharas.Player.Position.Y, running));
            c.Fake.PlayerInput.Jump();
        };
        var run = MultiplayerRun.Play(typeof(UmadP3BlackHoleScenario), 0, 1, Host, Peers, peerLink: link, immortal: true,
            inputs:
            [
                (5.0, 1, Jumps(running: false)),
                (7.0, 1, c => c.Fake.PlayerInput.Steer(new Vector2(0f, 1f), RunSpeed)),
                (7.6, 1, Jumps(running: true)),
                (8.8, 1, c => c.Fake.PlayerInput.Steer(null, 0f)),
                (10.0, 0, Jumps(running: false)),
            ]);
        Assert.That(run.Failure, Is.Null);
        Assert.That(jumps, Has.Count.EqualTo(3), "every jump was made before the fight ended");

        var problems = new List<string>();
        foreach (var viewer in run.Peers.Prepend(run.Host))
        foreach (var seat in jumps.Select(j => j.Seat).Distinct().Where(s => s != viewer.Seat))
        {
            var who = $"{viewer.Name}'s copy of {seat}";
            var moves = Moves(viewer, seat);
            var staged = 0;
            foreach (var j in jumps.Where(j => j.Seat == seat))
            {
                var delay = Delay(link, viewer.Seat, seat);
                var stages = moves.SkipWhile(m => m.At < j.At || m.Flags == ActorMoveFlags.None).TakeWhile(m => m.Flags != ActorMoveFlags.None).ToList();
                staged += stages.Count;
                var shape = string.Concat(stages.Select(m => Letter(m.Flags)));
                TestContext.Out.WriteLine($"{who}, jump at {j.At - run.StartedAt:F2}s: {shape} "
                                          + string.Join(" ", stages.Select(m => $"{m.At - j.At - delay:+0.000;-0.000}s/y{m.Y - j.Ground:F2}/v{m.Speed:F1}")));
                if (!Regex.IsMatch(shape, "^SR*AF*L$"))
                {
                    problems.Add($"{who} showed the jump at {j.At - run.StartedAt:F2}s as {(shape.Length > 0 ? shape : "nothing")}, not SR*AF*L");
                    continue;
                }
                var (start, apex, landed) = (stages[0], stages.Single(m => Letter(m.Flags) == 'A'), stages[^1]);
                if (MathF.Abs(start.Y - j.Ground) > Height) problems.Add($"{who} took off {start.Y - j.Ground:F2} y off the ground");
                if (MathF.Abs(landed.Y - j.Ground) > Height) problems.Add($"{who} landed {landed.Y - j.Ground:F2} y off the ground");
                if (timed && MathF.Abs(apex.Y - j.Ground - FakeLocalPlayerInput.JumpHeight) > PeakHeight)
                    problems.Add($"{who} peaked {apex.Y - j.Ground:F2} y up, not {FakeLocalPlayerInput.JumpHeight:F2}");
                if (!j.Running && stages.Any(m => m.Speed > StandingSpeed))
                    problems.Add($"{who} walked a standing jump at {stages.Max(m => m.Speed):F1} y/s");
                if (timed && j.Running && stages.Any(m => m.Speed > RunSpeed * RunningSpeedRatio))
                    problems.Add($"{who} walked a running jump at {stages.Max(m => m.Speed):F1} y/s");
                if (!timed) continue;
                foreach (var (stage, at) in new[] { (start, 0.0), (apex, FakeLocalPlayerInput.JumpSeconds / 2.0), (landed, (double)FakeLocalPlayerInput.JumpSeconds) })
                {
                    var off = stage.At - (j.At + at + delay);
                    if (off < -Early || off > Late) problems.Add($"{who} sent {stage.Flags} {off:+0.000;-0.000}s off the jump's own time, with the link's {delay:F3}s");
                }
            }
            var stray = moves.Count(m => m.Flags != ActorMoveFlags.None) - staged;
            if (stray > 0) problems.Add($"{who} sent {stray} jump packets outside its jumps");
        }
        Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems.Take(20)));
    }

    // S takes off, R rises, A tops out, F falls, L lands.
    private static char Letter(ActorMoveFlags flags) => flags switch
    {
        ActorMoveFlags.JumpStart | ActorMoveFlags.InAir => 'S',
        ActorMoveFlags.InAir => 'R',
        ActorMoveFlags.Apex | ActorMoveFlags.InAir | ActorMoveFlags.Falling => 'A',
        ActorMoveFlags.InAir | ActorMoveFlags.Falling => 'F',
        ActorMoveFlags.Landed => 'L',
        _ => '?',
    };

    // The jumper's pose up to the host, and the host's down to the viewer.
    private static double Delay(ConnectionProfile link, PartyRole viewer, PartyRole jumper)
        => (jumper == Host.Role ? 0 : link.UploadLatency) + (viewer == Host.Role ? 0 : link.DownloadLatency);

    // The ActorMoves the viewer's game was sent for the seat's copy, the speed byte as it went.
    private static List<Move> Moves(TestClient viewer, PartyRole seat)
    {
        var moves = new List<Move>();
        foreach (var row in viewer.Fake.Timeline.Rows)
        {
            if (row["kind"] != "move" || row.GetValueOrDefault("actor") is not { Length: > 0 } actor) continue;
            var at = double.Parse(row["t"], CultureInfo.InvariantCulture);
            if (viewer.NameAt(uint.Parse(actor, NumberStyles.HexNumber), at) != seat.ToString()) continue;
            var detail = row["detail"];
            var speedAt = detail.IndexOf(" speed=", StringComparison.Ordinal);
            var flags = Enum.Parse<ActorMoveFlags>(detail["flags=".Length..speedAt]);
            var speed = float.Parse(detail[(speedAt + " speed=".Length)..], CultureInfo.InvariantCulture);
            moves.Add(new Move(at, flags, float.Parse(row["y"], CultureInfo.InvariantCulture), ((int)MathF.Round(speed * 10f) & 0xFF) / 10f));
        }
        return moves;
    }
}
