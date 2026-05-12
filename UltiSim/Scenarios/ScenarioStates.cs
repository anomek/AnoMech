using System;
using UltiSim.Core;

namespace UltiSim.Scenarios;

public sealed record EightWayDirection(string Name, float RadiansFromNorth, int Index)
{
    public static readonly EightWayDirection N = new("N", 0f, 0);
    public static readonly EightWayDirection NE = new("NE", MathF.PI * 1f / 4f, 1);
    public static readonly EightWayDirection E = new("E", MathF.PI * 2f / 4f, 2);
    public static readonly EightWayDirection SE = new("SE", MathF.PI * 3f / 4f, 3);
    public static readonly EightWayDirection S = new("S", MathF.PI * 4f / 4f, 4);
    public static readonly EightWayDirection SW = new("SW", MathF.PI * 5f / 4f, 5);
    public static readonly EightWayDirection W = new("W", MathF.PI * 6f / 4f, 6);
    public static readonly EightWayDirection NW = new("NW", MathF.PI * 7f / 4f, 7);
    public static readonly EightWayDirection[] All = [N, NE, E, SE, S, SW, W, NW];

    public Placement Apply(Placement placement)
    {
        return placement.RotateAroundOrigin(RadiansFromNorth);
    }
}
