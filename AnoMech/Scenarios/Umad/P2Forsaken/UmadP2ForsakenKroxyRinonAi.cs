using System.Collections.Generic;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Forsaken;

public sealed class UmadP2ForsakenKroxyRinonAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "Kroxy-Rinon 341 (melee flex)";

    public void Run(UmadP2ForsakenState state, SimWorld world)
        => new UmadP2ForsakenRinonAiHelper(ReorderActive).Run(state, world);

    private void ReorderActive(int index, IList<PartyRole> active)
    {
    }
}
