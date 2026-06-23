using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

// EU strat: isolated copy of the NA "South Flex 341" strat. Runs over its own forked
// helper (UmadP2ForsakenZP6SouthAdjustAiHelper) so it can diverge from the NA version
// without affecting it.
public sealed class UmadP2ForsakenZP6SouthAdjustAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "zP6 South adjust";
    public string? Group => "EU";

    public void Run(UmadP2ForsakenState state, SimWorld world)
        => new UmadP2ForsakenZP6SouthAdjustAiHelper(
               UmadP2ForsakenZP6SouthAdjustAiHelper.StandardOdd,
               UmadP2ForsakenZP6SouthAdjustAiHelper.DiamonMarkersEven,
               UmadP2ForsakenZP6SouthAdjustAiHelper.SouthFlexReorder).Run(state, world);
}
