using System.Globalization;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// What every TEA phase shows its client, on several seeds of each strat: the rules real pulls keep,
// checked on the rows the client got, so no phase drifts from them unnoticed.
public class TeaVisibleTests
{
    private const float ReleaseSlack = 0.04f;
    // A status shows as its hit's EffectResult lands, up to about 0.8s after the sim applies it.
    private const float StatusShownWithin = 1f;
    // The engine builds a spawned NPC 2-12 frames after its packet; anything sent to it before is lost.
    private static readonly (int, int) SpawnFrames = (2, 12);

    private static IEnumerable<TestCaseData> Strats()
        => from scenario in ScenarioCatalog.Create().Scenarios
           where scenario.GetType().Namespace!.StartsWith("AnoMech.Scenarios.Tea.", StringComparison.Ordinal)
           from strat in Enumerable.Range(0, scenario.AiStrats.Count)
           select new TestCaseData(scenario.GetType(), strat).SetArgDisplayNames(scenario.GetType().Name, strat.ToString(CultureInfo.InvariantCulture));

    // Judged: each AoE the sim checked, by the time of the frame before.
    private sealed record Seen(ShownRun Shown, List<(float Time, uint ActionId)> Judged, List<string> StatusGaps);

    private static readonly Dictionary<(Type, int), List<Seen>> Watched = [];

    private static List<Seen> RunsOf(Type scenarioType, int strat)
    {
        if (!Watched.TryGetValue((scenarioType, strat), out var runs))
            Watched[(scenarioType, strat)] = runs = TeaShown.Seeds().Select(seed => Watch(scenarioType, strat, seed)).ToList();
        return runs;
    }

    private static Seen Watch(Type scenarioType, int strat, int seed)
    {
        var judged = new List<(float, uint)>();
        var gaps = new List<string>();
        var missing = new Dictionary<(uint Entity, ushort Status, bool Stale), float>();
        var reported = new HashSet<(uint, ushort, bool)>();
        var now = 0f;
        void OnJudged(AoeQuery query) => judged.Add((now, query.ActionId));
        AoeQuery.Evaluated += OnJudged;
        try
        {
            var shown = ShownRun.Execute(scenarioType, strat, seed, new ScenarioRunOptions
            {
                PacketSpawnFrames = SpawnFrames,
                Probe = p =>
                {
                    now = p.Time;
                    var client = (FakeBattleCharas)Natives.BattleCharas;
                    var gone = missing.Keys.ToHashSet();
                    foreach (var character in PerRole.All.Select(p.Member).Concat(p.World.Children.OfType<SimEnemy>()).OfType<SimCharacter>())
                    {
                        if (client.Find(character.EntityId) is not { } actor) continue;
                        var simIds = character.ActiveStatusSnapshot.Select(s => s.StatusId).Where(id => !SimOnlyStatus.Is(id)).ToHashSet();
                        var clientIds = actor.Statuses.Keys.Select(k => k.StatusId).ToHashSet();
                        foreach (var (id, stale) in simIds.Except(clientIds).Select(id => (id, false)).Concat(clientIds.Except(simIds).Select(id => (id, true))))
                        {
                            var key = (character.EntityId, id, stale);
                            gone.Remove(key);
                            if (!missing.TryGetValue(key, out var since)) missing[key] = since = p.Time;
                            if (p.Time - since > StatusShownWithin && reported.Add(key))
                                gaps.Add($"seed {seed}: {actor.Name}'s status {id} {(stale ? "still shown" : "not shown")} {StatusShownWithin}s after t={since:F2}");
                        }
                    }
                    foreach (var key in gone) missing.Remove(key);
                },
            });
            return new Seen(shown, judged, gaps);
        }
        finally
        {
            AoeQuery.Evaluated -= OnJudged;
        }
    }

    private static List<string> Broken(Type scenarioType, int strat, Func<IReadOnlyList<Dictionary<string, string>>, IEnumerable<string>> rule)
        => RunsOf(scenarioType, strat).SelectMany(seen => rule(seen.Shown.Rows).Select(problem => $"seed {seen.Shown.Run.Seed} {problem}")).ToList();

    [TestCaseSource(nameof(Strats))]
    public void ACleanRunWarnsOfNothing(Type scenarioType, int strat)
    {
        var runs = RunsOf(scenarioType, strat);
        Assert.Multiple(() =>
        {
            foreach (var run in runs.Select(s => s.Shown.Run))
            {
                Assert.That(run.Passed, run.ToString);
                Assert.That(run.Warnings, Is.Empty, run.ToString);
            }
        });
    }

    [TestCaseSource(nameof(Strats))]
    public void EveryCastBarIsItsSheetCastTimeLessTheRelease(Type scenarioType, int strat)
        => Assert.That(Broken(scenarioType, strat, TeaShown.BarsOffTheSheet), Is.Empty);

    [TestCaseSource(nameof(Strats))]
    public void EveryCastBarEndsInItsOwnAction(Type scenarioType, int strat)
        => Assert.That(Broken(scenarioType, strat, rows => TeaShown.BarsWithoutTheirAction(rows, ReleaseSlack)), Is.Empty);

    [TestCaseSource(nameof(Strats))]
    public void ACastOnItsCasterLetsGoWhereItsOmenPointed(Type scenarioType, int strat)
        => Assert.That(Broken(scenarioType, strat, TeaShown.OmensPointingElsewhere), Is.Empty);

    // Whatever a hit lands on, the client sees the action that dealt it: an AoE judged with no
    // ActionEffect after it is damage out of nowhere.
    [TestCaseSource(nameof(Strats))]
    public void EveryHitTheSimJudgesIsShown(Type scenarioType, int strat)
    {
        var problems = new List<string>();
        foreach (var seen in RunsOf(scenarioType, strat))
        {
            var shownAt = seen.Shown.Of("ability").GroupBy(r => uint.Parse(r["id"], CultureInfo.InvariantCulture))
                .ToDictionary(g => g.Key, g => g.Select(ShownRun.Time).ToList());
            foreach (var (time, actionId) in seen.Judged)
                if (!shownAt.TryGetValue(actionId, out var times) || !times.Any(t => t >= time && t <= time + TeaShown.CastRelease + 2 * ReleaseSlack))
                    problems.Add($"seed {seen.Shown.Run.Seed} t={time:F2}: {Natives.Data.ActionName(actionId)} ({actionId}) judged, never shown");
        }
        Assert.That(problems.Distinct().ToList(), Is.Empty);
    }

    [TestCaseSource(nameof(Strats))]
    public void EveryStatusIsShownAsTheSimHasIt(Type scenarioType, int strat)
        => Assert.That(RunsOf(scenarioType, strat).SelectMany(s => s.StatusGaps).ToList(), Is.Empty);

    [TestCaseSource(nameof(Strats))]
    public void EveryTetherComesDownAgain(Type scenarioType, int strat)
        => Assert.That(Broken(scenarioType, strat, TeaShown.TethersLeftUp), Is.Empty);

    // Bosses keep their HP and every run plays to the slowest real kill, so every seed lasts as long.
    [TestCaseSource(nameof(Strats))]
    public void EverySeedPlaysTheWholeTimeline(Type scenarioType, int strat)
    {
        var ends = RunsOf(scenarioType, strat).Select(s => s.Shown.Run.Elapsed).ToList();
        Assert.That(ends.Max() - ends.Min(), Is.LessThanOrEqualTo(2 * ScenarioRun.FrameSeconds), string.Join(", ", ends.Select(e => e.ToString("F3", CultureInfo.InvariantCulture))));
    }
}
