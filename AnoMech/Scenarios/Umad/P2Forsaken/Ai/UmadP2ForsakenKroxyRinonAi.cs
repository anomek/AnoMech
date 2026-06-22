using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

// Stub: Kroxy-Rinon (no reorder) over the second coordinate set. TODO: pick a real
// display name and fill in UmadP2ForsakenRinonAiHelper.SecondOdd/SecondEven.
public sealed class UmadP2ForsakenKroxyRinonAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "Kroxy-Rinon 341 (melee flex)";
    public string? Group => "NA";

    public void Run(UmadP2ForsakenState state, SimWorld world)
        => new UmadP2ForsakenRinonAiHelper(
               UmadP2ForsakenRinonAiHelper.StandardOdd,
               UmadP2ForsakenRinonAiHelper.DiamonMarkersEven,
               UmadP2ForsakenRinonAiHelper.KroxyReorder).Run(state, world);
}
