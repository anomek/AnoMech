using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Fru;

public static class FruUtils
{
    public static IReadOnlyList<Waymark> FruWaymarks { get; } =
    [
        new(WaymarkSlot.A,     new Vector3(     0f, 0f,   -10f)),
        new(WaymarkSlot.B,     new Vector3(    10f, 0f,      0f)),
        new(WaymarkSlot.C,     new Vector3(     0f, 0f,    10f)),
        new(WaymarkSlot.D,     new Vector3(   -10f, 0f,      0f)),
        new(WaymarkSlot.One,   new Vector3( -7.07f, 0f, -7.07f)),
        new(WaymarkSlot.Two,   new Vector3(  7.07f, 0f, -7.07f)),
        new(WaymarkSlot.Three, new Vector3(  7.07f, 0f,  7.07f)),
        new(WaymarkSlot.Four,  new Vector3( -7.07f, 0f,  7.07f)),
    ];
}
