using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru;

// For OnStatusAdded in every scenario that stacks Lightsteeped.
public static class Lightsteeped
{
    private const float ExplodesAfterFifthStack = 0.09f;

    // The action that lands a fifth stack has its caster follow up with Lightsteep.
    public static void ExplodeFifthStack(SimWorld world, StatusAdded added)
    {
        if (added is { StatusId: StatusId.Lightsteeped, Stacks: >= 5, Caster: SimEnemy caster })
            world.Events.Add(ExplodesAfterFifthStack, () => caster.Cast(FruActions.Lightsteep));
    }
}
