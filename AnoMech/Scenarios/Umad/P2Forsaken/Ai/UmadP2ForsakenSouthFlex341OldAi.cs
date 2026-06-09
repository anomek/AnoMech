using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

public sealed class UmadP2ForsakenSouthFlex341OldAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "[old positions] South Flex 341";

    public void Run(UmadP2ForsakenState state, SimWorld world)
        => new UmadP2ForsakenRinonAiHelper(
               UmadP2ForsakenRinonAiHelper.StandardOdd,
               UmadP2ForsakenRinonAiHelper.OldEven,
               UmadP2ForsakenRinonAiHelper.SouthFlexReorder).Run(state, world);
}
