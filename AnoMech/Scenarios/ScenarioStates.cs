using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;

namespace AnoMech.Scenarios;

public record Direction
{
    public static readonly Direction N = new(0f);
    public static readonly Direction NE = new(MathF.PI * 1f / 4f);
    public static readonly Direction E = new(MathF.PI * 2f / 4f);
    public static readonly Direction SE = new(MathF.PI * 3f / 4f);
    public static readonly Direction S = new(MathF.PI * 4f / 4f);
    public static readonly Direction SW = new(MathF.PI * 5f / 4f);
    public static readonly Direction W = new(MathF.PI * 6f / 4f);
    public static readonly Direction NW = new(MathF.PI * 7f / 4f);
    public static readonly IReadOnlyList<Direction> All = [N, NE, E, SE, S, SW, W, NW];
    public static readonly IReadOnlyList<Direction> Intercardinal = [NE, SE, SW, NW];
    public static readonly IReadOnlyList<Direction> Cardinal = [N, E, S, W];

    private static readonly string[] Names = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
    
    public float RadiansFromNorth { get; private init; }

    public Direction(float radians)
    {
        var tau = 2f * MathF.PI;
        radians %= tau;
        RadiansFromNorth = radians < 0 ? radians + tau : radians;
    }

    public Placement Apply(Placement placement)
    {
        return placement.RotateAroundOrigin(RadiansFromNorth);
    }

    public Vector3 Apply(Vector3 placement)
    {
        return new Placement(placement, 0).RotateAroundOrigin(RadiansFromNorth).Position;
    }

    public Vector3? Apply(Vector3? placement)
    {
        return placement == null ? null : Apply(placement.Value);
    }

    public void Apply(IAiPositions positions)
    {
        positions.Rotate(RadiansFromNorth);
    }

    // Rotates by `count` eighths (45°) around the compass wheel.
    public Direction Rotate(int count)
    {
        return new Direction(RadiansFromNorth + count * MathF.PI / 4f);
    }
    
    public Direction RotateRad(float radians)
    {
        return new Direction(RadiansFromNorth + radians);
    }

    public Direction Flip()
    {
        return new Direction(RadiansFromNorth + MathF.PI);
    }

    // Octant index 0..7 (N=0, NE=1, …, NW=7), derived from the angle.
    public int Index()
    {
        return ((int)MathF.Round(RadiansFromNorth / (MathF.PI / 4f)) % 8 + 8) % 8;
    }

    public string Name() => Names[Index()];
}

public record Tower(Vector3 Position, int MinPlayers);
