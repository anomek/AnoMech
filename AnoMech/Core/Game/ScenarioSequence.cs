using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Scenarios;

namespace AnoMech.Core.Game;

// Only decides when to advance. Game owns teardown, zone/party setup and cancellation.
internal sealed class ScenarioSequence
{
    public const float BetweenScenarios = 2;
    public IReadOnlyList<IScenario> Scenarios { get; }
    public int Index { get; private set; }
    public IScenario Current => Scenarios[Index];
    public bool Finished { get; private set; }
    public bool Waiting { get; private set; }
    private float remaining;

    public ScenarioSequence(IReadOnlyList<IScenario> scenarios)
    {
        if (scenarios.Count == 0 || scenarios.Any(s => s is IScenarioSequence || s.Duration is not > 0 || !float.IsFinite(s.Duration)))
            throw new ArgumentException("A sequence requires scenarios with finite positive durations.");
        Scenarios = scenarios.ToArray();
    }

    public IScenario? Tick(float timelineElapsed, float deltaSeconds, bool failed)
    {
        if (Finished) return null;
        if (failed) { Finished = true; Waiting = false; return null; }
        if (!Waiting)
        {
            if (timelineElapsed < Current.Duration) return null;
            if (Index == Scenarios.Count - 1) { Finished = true; return null; }
            Waiting = true;
            remaining = BetweenScenarios;
            return null;
        }
        // Use real frame time, independent of the scenario event-speed setting.
        remaining = MathF.Max(0, remaining - MathF.Max(0, deltaSeconds));
        if (remaining > 0.0001f) return null;
        Waiting = false;
        return Scenarios[++Index];
    }

    public static int AiIndex(IScenario scenario)
    {
        for (var i = 0; i < scenario.AiStrats.Count; i++) if (scenario.AiStrats[i].Group == "NA") return i;
        return 0;
    }
}
