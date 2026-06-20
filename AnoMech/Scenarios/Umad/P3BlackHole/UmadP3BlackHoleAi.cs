using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// Party-movement AI ("strat") for UMAD P3 "Black Hole". State arrives via Run, not
// the ctor, so the strat is a stateless choreography fed the per-run truth at start.
// See UmadP4KefkaSaysAi for the canonical shape.
public sealed class UmadP3BlackHoleAi : IScenarioAi<UmadP3BlackHoleState>
{
    public string Name => "Black Hole (WIP)";

    public void Run(UmadP3BlackHoleState state, SimWorld world)
    {
        var ai = new AiManager(world);
        // TODO: ai.Move(<absolute t>, <positions>) beats, in ascending time order.
    }
}
