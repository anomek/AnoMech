using System.Collections.Generic;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Tea.P1LivingLiquid;

public sealed class TeaP1LivingLiquidStateOverrides
{
    // Compass bearing of the open side of each tornado T, a multiple of 45; null = random.
    public int? FirstTornadoesOpenSide { get; set; } = null;
    public int? SecondTornadoesOpenSide { get; set; } = null;

    // The tornado (0..2 = the T's relative W, E, S) that sits out each Pressurize; null = random.
    internal int? FirstCalmTornado { get; set; }
    internal int? SecondCalmTornado { get; set; }

    // Whether the first Embolus of each pair, the one aimed at Liquid Hand, comes from the T's
    // earlier tornado (W before E before S); null = random.
    internal bool? FirstHandOrbFromEarlier { get; set; }
    internal bool? SecondHandOrbFromEarlier { get; set; }

    // The set-A tornado (0..2) left without a Drainage tether, and whether MT holds the earlier one.
    internal int? UntetheredTornado { get; set; }
    internal bool? MainTankHoldsEarlierTether { get; set; }

    // Who holds a dead tank's Drainage tether; null = a random living player, rolled at the tether.
    internal PartyRole? DeadTanksDrainageGoesTo { get; set; }

    // The six Throttle targets.
    internal IReadOnlyList<PartyRole>? Throttled { get; set; }

    // Every doll's roll.
    internal DollRoll? DollRoll { get; set; }
}
