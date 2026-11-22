using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Fru;

// The JP (Mana) community preset: A-D on the cardinals and 1-4 on the intercardinals, both 10y out,
// with 1 at NE going clockwise. Inner Circle numbers from NW instead.
public static class FruJpWaymarks
{
    public static WaymarkLayout Layout => new("JP · Mana",
    [
        new(WaymarkSlot.A,     new Vector3(      0f, 0f,     -10f)),
        new(WaymarkSlot.B,     new Vector3(     10f, 0f,       0f)),
        new(WaymarkSlot.C,     new Vector3(      0f, 0f,      10f)),
        new(WaymarkSlot.D,     new Vector3(    -10f, 0f,       0f)),
        new(WaymarkSlot.One,   new Vector3(  7.071f, 0f,  -7.071f)),
        new(WaymarkSlot.Two,   new Vector3(  7.071f, 0f,   7.071f)),
        new(WaymarkSlot.Three, new Vector3( -7.071f, 0f,   7.071f)),
        new(WaymarkSlot.Four,  new Vector3( -7.071f, 0f,  -7.071f)),
    ]);
}
