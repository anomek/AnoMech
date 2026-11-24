using System.Collections.Generic;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

// Retail chains the six to their nearest neighbours, so the loop follows wherever the strat
// pre-positions its ring. A strat whose ring isn't the NA conga declares it here (clockwise, from
// the seat it counts towers from) and the state chains the loop in that order.
public interface IFruP2LightRampantChainRing
{
    IReadOnlyList<PartyRole> ClockwiseRing { get; }
}
