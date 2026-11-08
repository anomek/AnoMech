using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Ucob.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Exaflares;

namespace AnoMech.Tests;

// Debug mode stands in for a person, so every strat has to play out the same for it: each seat,
// played by the debug-driven local player, stands where that seat's bot stands on the same roll as
// each area resolves. The two go through different motions and different delivery, so an order that
// reaches a bot and not the player, or a move the client makes slower than the strat planned for,
// shows here as a gap, not only when it happens to kill.
public class DebugDrivenSeatTests
{
    // A walk around an obstacle can go either way, and ends on the same spot: up to 5y apart on the
    // way. A move that never happened or came late is the rest of the walk or slide apart.
    private const float SameSpot = 6f;
    // Bots start in a ring and the player at the zone's spawn, until the strat first walks them.
    private const float Settling = 1.5f;
    private const int Seeds = 2;

    // Still played from every seat, but not compared. The exaflare dodges plan each wave from where
    // everyone then stands, so a step's difference early on is another lane by the next wave, and
    // this strat gives the bait to the player, whoever that is.
    private static readonly Type[] PlannedLive = [typeof(UmadP5ExaflaresScenario), typeof(UcobP5ExaflaresScenario)];
    private const string PlayerTakesTheBait = "NA · you take Somber Dance";

    private static bool Compares(Type scenarioType, string strat) => !PlannedLive.Contains(scenarioType) && strat != PlayerTakesTheBait;

    private static IEnumerable<TestCaseData> Strats()
        => from scenario in ScenarioCatalog.Create().Scenarios
           from strat in scenario.AiStrats.Select((ai, index) => (ai, index))
           select new TestCaseData(scenario.GetType(), strat.index, Compares(scenario.GetType(), strat.ai.Name))
               .SetName($"{scenario.FullName()} [{strat.ai.Name}]");

    private sealed record Played(Dictionary<PartyRole, List<(float At, Vector3 Position)>> Tracks, List<float> Resolves, ScenarioRun Run);

    [TestCaseSource(nameof(Strats))]
    public void EverySeatPlaysAsItsBotWouldForTheDebugDrivenPlayer(Type scenarioType, int strat, bool compares)
    {
        var problems = new List<string>();
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var seats = Enum.GetValues<PartyRole>();
            var plays = seats.ToDictionary(s => s, s => Play(scenarioType, strat, seed, s));
            problems.AddRange(plays.Values.Where(p => !p.Run.Passed).Select(p => p.Run.ToString()));
            if (!compares) continue;
            foreach (var seat in seats)
            {
                var other = seats[((int)seat + 1) % seats.Length];
                if (Gap(plays[seat], plays[other], seat, other, seats) is { } gap)
                    problems.Add($"seed {seed}, {seat}: {gap.Apart:F1}y from its bot as an area resolved at {gap.At:F2}");
            }
        }
        Assert.That(problems, Is.Empty, () => string.Join(Environment.NewLine, problems.Take(12)));
    }

    // The worst a seat stands from its bot, at the hits both runs share. A hit that also finds a seat
    // that was a bot both times apart isn't one: that strat planned around where everyone stood, and
    // the runs no longer compare.
    private static (float Apart, float At)? Gap(Played asPlayer, Played asBot, PartyRole seat, PartyRole other, PartyRole[] seats)
    {
        if (!asPlayer.Tracks.TryGetValue(seat, out var mine) || !asBot.Tracks.TryGetValue(seat, out var theirs)) return null;
        (float Apart, float At)? worst = null;
        var from = MathF.Max(FirstWalk(mine), FirstWalk(theirs));
        foreach (var at in asBot.Resolves.Where(t => t >= from))
        {
            if (At(mine, at) is not { } a || At(theirs, at) is not { } b) continue;
            var apart = Apart(a, b);
            if (apart <= SameSpot || apart <= (worst?.Apart ?? 0f)) continue;
            var diverged = seats.Where(s => s != seat && s != other).Any(s =>
                asPlayer.Tracks.TryGetValue(s, out var x) && asBot.Tracks.TryGetValue(s, out var y)
                && At(x, at) is { } p && At(y, at) is { } q && Apart(p, q) > SameSpot);
            if (!diverged) worst = (apart, at);
        }
        return worst;
    }

    private static float FirstWalk(List<(float At, Vector3 Position)> track)
    {
        foreach (var (at, position) in track)
            if (Vector3.Distance(position, track[0].Position) > 1f) return at + Settling;
        return float.MaxValue;
    }

    private static float Apart(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    private static Vector3? At(List<(float At, Vector3 Position)> track, float time)
    {
        var index = track.FindLastIndex(t => t.At <= time);
        return index >= 0 && time - track[index].At <= 0.1f ? track[index].Position : null;
    }

    private static Played Play(Type scenarioType, int strat, int seed, PartyRole seat)
    {
        var tracks = new Dictionary<PartyRole, List<(float At, Vector3 Position)>>();
        var resolves = new SortedSet<float>();
        var now = 0f;
        void Resolved(AoeQuery _) => resolves.Add(now);
        AoeQuery.Evaluated += Resolved;
        try
        {
            var run = ScenarioRun.Execute(scenarioType, strat, seed, new ScenarioRunOptions
            {
                PlayerRole = seat,
                Probe = probe =>
                {
                    now = probe.Time;
                    foreach (var role in Enum.GetValues<PartyRole>())
                        if (probe.Member(role) is { } member && member.IsAlive())
                        {
                            if (!tracks.TryGetValue(role, out var track)) tracks[role] = track = [];
                            track.Add((now, member.Position));
                        }
                },
            });
            return new Played(tracks, resolves.ToList(), run);
        }
        finally
        {
            AoeQuery.Evaluated -= Resolved;
        }
    }
}
