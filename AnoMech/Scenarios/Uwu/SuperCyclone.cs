using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Uwu.UwuConstants;

namespace AnoMech.Scenarios.Uwu;

// For OnStatusLost in every scenario where Mesohigh takes Thermal Low off its target.
public static class SuperCyclone
{
    private const float AfterThermalLowLost = 0.07f;

    // Thermal Low's stacks pick the Super Cyclone that follows, from where its holder stands.
    public static void FollowMesohigh(SimWorld world, StatusLost lost, SimEnemy? caster)
    {
        if (lost is not { StatusId: StatusId.ThermalLow, Reason: StatusLossReason.Removed, Stacks: > 0 } || caster is null) return;
        var from = new Placement(lost.Holder.Position, 0f);
        world.Events.Add(AfterThermalLowLost, () => caster.CastFromHelper(UwuActions.SuperCyclone(lost.Stacks), at: from));
    }
}
