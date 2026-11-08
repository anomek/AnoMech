using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

internal sealed class FakeWaymarks(SimTimeline timeline) : IWaymarks
{
    public bool Place(IReadOnlyList<(WaymarkSlot Slot, Vector3 Position)> markers)
    {
        timeline.Add("waymarks", detail: string.Join(" ", markers.Select(m => $"{m.Slot}=({SimTimeline.F(m.Position.X)},{SimTimeline.F(m.Position.Z)})")));
        return true;
    }

    public void ClearAll() { }
}
