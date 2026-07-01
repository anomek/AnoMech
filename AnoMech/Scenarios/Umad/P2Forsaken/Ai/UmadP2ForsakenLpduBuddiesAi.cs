using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

// Copy of the EU "p3Z Buddy Meow" strat. Runs over its own helper
// (UmadP2ForsakenLpduBuddiesAiHelper) so it can diverge without affecting the original.
public sealed class UmadP2ForsakenLpduBuddiesAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "[LPDU] Buddies";
    public string? Group => "EU";

    public void Run(UmadP2ForsakenState state, SimWorld world)
        => new UmadP2ForsakenLpduBuddiesAiHelper(
               UmadP2ForsakenLpduBuddiesAiHelper.StandardOdd,
               UmadP2ForsakenLpduBuddiesAiHelper.DiamonMarkersEven,
               UmadP2ForsakenLpduBuddiesAiHelper.KroxyReorder).Run(state, world);
}
