using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

// Stub: South-Flex reorder over the second coordinate set. TODO: pick a real
// display name and fill in UmadP2ForsakenRinonAiHelper.SecondOdd/SecondEven.
public sealed class UmadP2ForsakenSouthFlexAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "South Flex 341";
    public string? Group => "NA";

    public void Run(UmadP2ForsakenState state, SimWorld world)
        => new UmadP2ForsakenRinonAiHelper(
               UmadP2ForsakenRinonAiHelper.StandardOdd,
               UmadP2ForsakenRinonAiHelper.DiamonMarkersEven,
               UmadP2ForsakenRinonAiHelper.SouthFlexReorder).Run(state, world);
}
