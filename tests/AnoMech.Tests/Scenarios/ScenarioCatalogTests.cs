using AnoMech.Core.Game;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

public class ScenarioCatalogTests
{
    private static IEnumerable<TestCaseData> ScenarioStrats()
        => from scenario in ScenarioCatalog.Create()
           from strat in scenario.AiStrats.Select((ai, index) => (ai, index))
           select new TestCaseData(scenario.GetType(), strat.index)
               .SetName($"{Game.FullName(scenario)} [{strat.ai.Name}]");

    [TestCaseSource(nameof(ScenarioStrats))]
    public void SunnyDay(Type scenarioType, int strat)
    {
        var seeds = TestContext.Parameters.Get("SunnySeeds", 1);
        var failures = Enumerable.Range(0, seeds)
            .Select(_ => ScenarioRun.Execute(scenarioType, strat, Random.Shared.Next()))
            .Where(run => !run.Passed)
            .ToList();

        Assert.That(failures, Is.Empty, () => $"{failures.Count}/{seeds} seeds failed:{Environment.NewLine}" + string.Join(Environment.NewLine,
            failures.Select(run => $"{run}{Environment.NewLine}  replay: {run.ReplayTestCase}")));
    }

    // Paste the replay line of a failing seed here to debug it.
    [Explicit]
    [TestCase(typeof(global::AnoMech.Scenarios.Umad.P3BlackHole.UmadP3BlackHoleScenario), 0, 285776436)]
    public void Replay(Type scenarioType, int strat, int seed)
    {
        var run = ScenarioRun.Execute(scenarioType, strat, seed, new ScenarioRunOptions { AlwaysWriteArtifacts = true });
        Assert.That(run.Passed, run.ToString);
    }

    // Replay driven by run parameters (Scenario = type full name, Strat, Seed, optional StopAt);
    // ScenarioRun.ReplayCommand prints the invocation.
    [Explicit]
    [Test]
    public void ReplayFromParameters()
    {
        var name = TestContext.Parameters.Get("Scenario");
        var scenarioType = name is null ? null : typeof(Game).Assembly.GetType(name);
        Assert.That(scenarioType, Is.Not.Null, $"Scenario parameter '{name}' is not a scenario type's full name.");
        var stopAt = TestContext.Parameters.Get("StopAt") is { } text ? float.Parse(text, System.Globalization.CultureInfo.InvariantCulture) : (float?)null;
        var run = ScenarioRun.Execute(scenarioType!, TestContext.Parameters.Get("Strat", 0), TestContext.Parameters.Get("Seed", 0),
            new ScenarioRunOptions { AlwaysWriteArtifacts = true, StopAt = stopAt });
        Assert.That(run.Passed, run.ToString);
    }
}
