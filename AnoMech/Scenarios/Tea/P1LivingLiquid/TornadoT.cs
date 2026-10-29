using System;
using System.Numerics;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P1LivingLiquid;

// Three Liquid Rages 15y out on a cross, its fourth arm left open. The party reads the T from its
// open side, so positions are written in that frame: north is the open side, the tornadoes stand
// relative west, east and south.
public readonly record struct TornadoT(int OpenSide)
{
    public const float Radius = 15f;
    public const int West = 0;
    public const int East = 1;
    public const int South = 2;

    private static readonly float[] RelativeBearings = [270f, 90f, 180f];

    public bool Cardinal => OpenSide % 90 == 0;

    public float Bearing(float relative) => OpenSide + relative;

    public Vector2 Tornado(int index) => Abs(Polar(RelativeBearings[index], Radius));

    public Vector2 Abs(Vector2 relative)
    {
        var radians = float.DegreesToRadians(OpenSide);
        var (cos, sin) = (MathF.Cos(radians), MathF.Sin(radians));
        return new Vector2(relative.X * cos - relative.Y * sin, relative.X * sin + relative.Y * cos);
    }

    public static Vector2 Polar(float bearing, float radius)
    {
        var p = Geometry.OnCompass(bearing, radius);
        return new Vector2(p.X, p.Z);
    }
}
