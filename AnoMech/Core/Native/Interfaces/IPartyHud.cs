using System.Collections.Generic;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Native.Interfaces;

// Mirrors the sim party into the _PartyList addon.
public interface IPartyHud
{
    void Refresh(SimParty party);
    void Clear();
    // The local player's list top to bottom; null keeps the game's own order. Clear resets it.
    void SetDisplayOrder(IReadOnlyList<PartyRole>? order);
}
