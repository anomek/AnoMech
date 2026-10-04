using System.Collections.Generic;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

internal sealed class FakePartyHud : IPartyHud
{
    public IReadOnlyList<PartyRole>? DisplayOrder { get; private set; }

    public void Refresh(SimParty party) { }
    public void Clear() => DisplayOrder = null;
    public void SetDisplayOrder(IReadOnlyList<PartyRole>? order) => DisplayOrder = order;
}
