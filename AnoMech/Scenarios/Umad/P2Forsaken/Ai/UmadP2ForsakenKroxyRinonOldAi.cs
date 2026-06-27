using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

public sealed class UmadP2ForsakenKroxyRinonOldAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "[old positions] Kroxy-Rinon 341 (melee flex)";
    public string? Group => "NA";

    public void Run(UmadP2ForsakenState state, SimWorld world)
        => new UmadP2ForsakenRinonAiHelper(
               UmadP2ForsakenRinonAiHelper.StandardOdd,
               UmadP2ForsakenRinonAiHelper.OldEven,
               UmadP2ForsakenRinonAiHelper.KroxyReorder).Run(state, world);
}
