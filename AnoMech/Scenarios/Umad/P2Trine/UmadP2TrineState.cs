using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AnoMech.Scenarios.Umad.P2Trine;

public sealed class UmadP2TrineStateOverrides
{
    public int? Pattern { get; set; }
    public bool? LeftWing { get; set; }
}

public sealed record TrineTriangle(Vector2 Center, bool PointsRight)
{
    public const float Circumradius = 5.7735027f;
    public IReadOnlyList<Vector2> Vertices
    {
        get
        {
            var x = PointsRight ? Circumradius : -Circumradius;
            return [Center + new Vector2(x, 0), Center + new Vector2(-x / 2f, 5), Center + new Vector2(-x / 2f, -5)];
        }
    }
    public uint ObjectId => PointsRight ? 0x1EBFB2u : 0x1EBFB3u;
}

public sealed class UmadP2TrineState
{
    public int Pattern { get; }
    public bool LeftWing { get; }
    public static readonly IReadOnlyList<TrineTriangle> Triangles =
    [
        new(new(-11.547005f, -10), false),
        new(new(2.8867513f, -15), true),
        new(new(14.433757f, -5), false),
        new(new(11.547005f, 10), true),
        new(new(-2.8867513f, 15), false),
        new(new(-14.433757f, 5), true),
        new(Vector2.Zero, false),
    ];
    private static readonly int[][] First = [[0, 3, 4], [0, 1, 2], [2, 3, 4]];
    private static readonly int[] Second = [2, 4, 5];

    public UmadP2TrineState(Rng rng, UmadP2TrineStateOverrides settings)
    {
        Pattern = settings.Pattern ?? rng.NextInt(3);
        LeftWing = settings.LeftWing ?? rng.NextBool();
        if (Pattern is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(settings.Pattern));
    }

    public int WaveOf(int triangle)
        => First[Pattern].Contains(triangle) ? 0 : Second[Pattern] == triangle ? 1 : 2;

    public IReadOnlyList<Vector2> WaveVertices(int wave)
        => Triangles.Where((_, i) => WaveOf(i) == wave).SelectMany(t => t.Vertices).ToArray();
}
