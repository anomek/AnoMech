using System.Collections.Generic;
using System.Linq;
using AnoMech.Scenarios.Fru.P2DiamondDust;
using AnoMech.Scenarios.Fru.P2LightRampant;
using AnoMech.Scenarios.Fru.P3Apocalypse;
using AnoMech.Scenarios.Fru.P3UltimateRelativity;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using AnoMech.Scenarios.Fru.P5FulgentBlade;
using AnoMech.Scenarios.Fru.P5ParadiseRegained;
using AnoMech.Scenarios.Top.P2PartySynergy;
using AnoMech.Scenarios.Top.P5Delta;
using AnoMech.Scenarios.Top.P5Omega;
using AnoMech.Scenarios.Top.P5Sigma;
using AnoMech.Scenarios.Tea.P1LivingLiquid;
using AnoMech.Scenarios.Tea.I1LimitCut;
using AnoMech.Scenarios.Tea.I2TemporalStasis;
using AnoMech.Scenarios.Tea.P2BjCc;
using AnoMech.Scenarios.Tea.P3AlexanderPrime;
using AnoMech.Scenarios.Tea.P4PerfectAlexander;
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

// Every runnable scenario, in menu order, plus the zone -> phase -> scenario tree derived from it in
// first-appearance order. Fresh instances per Create: a scenario holds its run state.
public sealed class ScenarioCatalog
{
    public IReadOnlyList<IScenario> Scenarios { get; }
    public IReadOnlyList<IZone> Zones { get; }
    private readonly Dictionary<IZone, IReadOnlyList<IPhase>> phasesByZone;
    private readonly Dictionary<IPhase, IReadOnlyList<IScenario>> scenariosByPhase;

    private ScenarioCatalog(IReadOnlyList<IScenario> scenarios)
    {
        Scenarios = scenarios;
        var zones = new List<IZone>();
        var phases = new Dictionary<IZone, List<IPhase>>();
        var byPhase = new Dictionary<IPhase, List<IScenario>>();
        foreach (var scenario in scenarios)
        {
            var phase = scenario.Phase;
            var zone = phase.Zone;
            if (!phases.TryGetValue(zone, out var zonePhases))
            {
                zonePhases = [];
                phases[zone] = zonePhases;
                zones.Add(zone);
            }
            if (!zonePhases.Contains(phase)) zonePhases.Add(phase);
            if (!byPhase.TryGetValue(phase, out var phaseScenarios))
            {
                phaseScenarios = [];
                byPhase[phase] = phaseScenarios;
            }
            phaseScenarios.Add(scenario);
        }
        Zones = zones;
        phasesByZone = phases.ToDictionary(kv => kv.Key, IReadOnlyList<IPhase> (kv) => kv.Value);
        scenariosByPhase = byPhase.ToDictionary(kv => kv.Key, IReadOnlyList<IScenario> (kv) => kv.Value);
    }

    public IReadOnlyList<IPhase> PhasesOf(IZone zone) => phasesByZone[zone];
    public IReadOnlyList<IScenario> ScenariosOf(IPhase phase) => scenariosByPhase[phase];

    public static ScenarioCatalog Create() => new(
    [
        new UmadP1TeleTrouncingScenario(),
        new UmadP2ForsakenScenario(),
        new UmadP3LimitCutScenario(),
        new UmadP3BlackHoleScenario(),
        new UmadP4KefkaSaysScenario(),
        new UmadP5FloodScenario(),
        new UmadP5CelestriadScenario(),
        new UmadP5ExaflaresScenario(),
        new UmadP5ForsakenNull(),
        new FruP2DiamondDustScenario(),
        new FruP2LightRampantScenario(),
        new FruP3UltimateRelativityScenario(),
        new FruP3ApocalypseScenario(),
        new FruP4DarklitDragonsongScenario(),
        new FruP4CrystallizeTimeScenario(),
        new FruP5FulgentBladeScenario(),
        new FruP5ParadiseRegainedScenario(),
        new TopP2PartySynergyScenario(),
        new TopP5DeltaScenario(),
        new TopP5SigmaScenario(),
        new TopP5OmegaScenario(),
        new TopP6WaveCannon2Scenario(),
        new TeaP1LivingLiquidScenario(),
        new TeaI1LimitCutScenario(),
        new TeaP2BjCcScenario(),
        new TeaI2TemporalStasisScenario(),
        new TeaP3AlexanderPrimeScenario(),
        new TeaP4PerfectAlexanderScenario(),
        new UltimatePredationScenario(),
        new UltimateSuppressionScenario(),
        new UcobP5ExaflaresScenario(),
    ]);
}
