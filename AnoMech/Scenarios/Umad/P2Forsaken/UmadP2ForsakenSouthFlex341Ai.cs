using System.Collections.Generic;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Forsaken;

public sealed class UmadP2ForsakenSouthFlex341Ai : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "South Flex 341";
    private UmadP2ForsakenRinonAiHelper helper = null!;

    public void Run(UmadP2ForsakenState state, SimWorld world)
    {
        helper = new UmadP2ForsakenRinonAiHelper(ReorderActive);
        helper.Run(state, world);
    }

    private void ReorderActive(int index, IList<PartyRole> active)
    {
        if (index % 2 == 0) // odd tower
        {
           List<PartyRole> reordered = [
                helper.ActiveRole(LockonId.ForsakenStack, index, 0),
                helper.ActiveRole(LockonId.ForsakenCone, index, 0),
                helper.ActiveRole(LockonId.ForsakenChariot, index, 0),
                helper.ActiveRole(LockonId.ForsakenStack, index, 1)
           ];
           active.Clear();
           reordered.ForEach(active.Add);
        }
        else // even tower
        {
            List<PartyRole> reordered = [
                helper.ActiveRole(LockonId.ForsakenCone, index, 0),
                helper.ActiveRole(LockonId.ForsakenChariot, index, 0),
                helper.ActiveRole(LockonId.ForsakenChariot, index, 1),
                helper.ActiveRole(LockonId.ForsakenCone, index, 1)
            ];
            active.Clear();
            reordered.ForEach(active.Add);
        }
    }
}
