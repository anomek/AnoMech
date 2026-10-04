using System.Collections.Generic;
using AnoMech.Scenarios.Top.P1ProgramLoop;
using AnoMech.Scenarios.Top.P2PartySynergy;
using AnoMech.Scenarios.Top.P3HelloWorld;
using AnoMech.Scenarios.Top.P3Intermission;
using AnoMech.Scenarios.Top.P3Monitors;
using AnoMech.Scenarios.Top.P5Delta;
using AnoMech.Scenarios.Top.P5Omega;
using AnoMech.Scenarios.Top.P5Sigma;
using AnoMech.Scenarios.Top.P6WaveCannon2;
using AnoMech.Scenarios.Ucob.P5Exaflares;
using AnoMech.Scenarios.Umad;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;
using AnoMech.Scenarios.Umad.P2Forsaken;
using AnoMech.Scenarios.Umad.P3BlackHole;
using AnoMech.Scenarios.Umad.P3LimitCut;
using AnoMech.Scenarios.Umad.P4KefkaSays;
using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;
using AnoMech.Scenarios.Uwu.UltimatePredation;
using AnoMech.Scenarios.Uwu.UltimateSuppression;

namespace AnoMech.Scenarios;

// Every runnable scenario, in menu order. Fresh instances per call: a scenario holds its run state.
public static class ScenarioCatalog
{
    public static IReadOnlyList<IScenario> Create() =>
    [
        new UmadP1TeleTrouncingScenario(),
        new UmadP2ForsakenScenario(),
        new UmadP3LimitCutScenario(),
        new UmadP3BlackHoleScenario(),
        new UmadP4KefkaSaysScenario(),
        new UmadP5FloodScenario(),
        new UmadP5ExaflaresScenario(),
        new UmadP5CelestriadScenario(),
        new UmadP5ForsakenNull(),
        new TopP1ProgramLoopScenario(),
        new TopP2PartySynergyScenario(),
        new TopP3IntermissionScenario(),
        new TopP3HelloWorldScenario(),
        new TopP3MonitorsScenario(),
        new TopP5DeltaScenario(),
        new TopP5SigmaScenario(),
        new TopP5OmegaScenario(),
        new TopP6WaveCannon2Scenario(),
        new UltimatePredationScenario(),
        new UltimateSuppressionScenario(),
        new UcobP5ExaflaresScenario(),
    ];
}
