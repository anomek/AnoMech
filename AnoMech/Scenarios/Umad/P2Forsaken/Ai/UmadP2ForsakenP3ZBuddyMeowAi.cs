using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

// EU strat: forked from the NA "Kroxy-Rinon 341 (melee flex)" strat. Runs over its own
// helper (UmadP2ForsakenP3ZBuddyMeowAiHelper) with hand-placed positions and a first-tower
// stack-spot rule, diverging from the NA version without affecting it.
public sealed class UmadP2ForsakenP3ZBuddyMeowAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "p3Z Buddy Meow";
    public string? Group => "EU";

    public void Run(UmadP2ForsakenState state, SimWorld world)
        => new UmadP2ForsakenP3ZBuddyMeowAiHelper(
               UmadP2ForsakenP3ZBuddyMeowAiHelper.StandardOdd,
               UmadP2ForsakenP3ZBuddyMeowAiHelper.DiamonMarkersEven,
               UmadP2ForsakenP3ZBuddyMeowAiHelper.KroxyReorder).Run(state, world);
}
