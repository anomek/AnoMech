using System;
using System.Collections.Generic;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.Apocalypse;
using AnoMech.Scenarios.Fru.CrystallizeTime;
using AnoMech.Scenarios.Fru.DarklitDragonsong;
using AnoMech.Scenarios.Fru.DiamondDust;
using AnoMech.Scenarios.Fru.FulgentBlade;
using AnoMech.Scenarios.Fru.LightRampant;
using AnoMech.Scenarios.Fru.ParadiseRegained;
using AnoMech.Scenarios.Fru.UltimateRelativity;

namespace AnoMech.Scenarios.Fru;

public sealed class FruAllScenario(IReadOnlyList<IScenario> scenarios) : IScenarioSequence
{
    public static IReadOnlyList<IScenario> CreateCatalog()
    {
        // Shared instances retain each scenario's settings when selected via All.
        IScenario[] ordered = [new FruDiamondDustScenario(), new FruLightRampantScenario(),
            new FruUltimateRelativityScenario(), new FruApocalypseScenario(),
            new FruDarklitDragonsongScenario(), new FruCrystallizeTimeScenario(),
            new FruFulgentBladeScenario(), new FruParadiseRegainedScenario()];
        return [new FruAllScenario(ordered), .. ordered];
    }
    public string Name => "All";
    public IPhase Phase => FruZone.P2;
    public IReadOnlyList<IScenario> Scenarios { get; } = scenarios;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = [new AllAi()];
    // Game expands this menu entry into its individual scenarios before Run.
    public void Run(SimWorld world, int? selectedAi) => throw new InvalidOperationException("Start scenario sequences through Game.");
    private sealed class AllAi : IScenarioAi
    {
        public string Name => "NA";
        public string Group => "NA";
    }
}
