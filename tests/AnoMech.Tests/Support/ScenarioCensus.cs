using AnoMech.Scenarios;

namespace AnoMech.Tests;

// Every strat of every scenario played solo, each once on the same seed, for a test that has to
// cover all the sim gives a client.
internal static class ScenarioCensus
{
    // played: the run's name, and its fake game once it's over.
    public static void EveryStrat(Action<string, FakeGame> played)
    {
        foreach (var scenario in ScenarioCatalog.Create().Scenarios)
        for (var strat = 0; strat < scenario.AiStrats.Count; strat++)
        {
            var run = $"{scenario.FullName()} [{scenario.AiStrats[strat].Name}]";
            ScenarioRun.Execute(scenario.GetType(), strat, 1, new ScenarioRunOptions
            {
                WriteArtifactsOnFailure = false,
                Finished = fake => played(run, fake),
            });
        }
    }
}
