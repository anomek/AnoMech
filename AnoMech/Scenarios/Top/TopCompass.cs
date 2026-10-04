using System;
using System.Numerics;

namespace AnoMech.Scenarios.Top;

// Arena-local XZ positions by compass bearing in degrees: 0 = north (-Z), clockwise.
public static class TopCompass
{
    public static Vector2 Point(float radius, float bearing)
    {
        var radians = bearing * MathF.PI / 180f;
        return new Vector2(radius * MathF.Sin(radians), -radius * MathF.Cos(radians));
    }

    public static float Bearing(Vector2 point) => MathF.Atan2(point.X, -point.Y) * 180f / MathF.PI;

    // Signed shortest turn from one bearing to another, in [-180, 180).
    public static float Turn(float from, float to) => ((to - from) % 360f + 540f) % 360f - 180f;
}
