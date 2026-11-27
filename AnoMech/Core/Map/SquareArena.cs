using System;
using System.Numerics;

namespace AnoMech.Core.Map;

// Axis-aligned square, `HalfWidth` from the center along X and Z.
public sealed record SquareArena(float HalfWidth) : IArenaShape
{
    public bool IsOutside(Vector3 local) => MathF.Abs(local.X) > HalfWidth || MathF.Abs(local.Z) > HalfWidth;
}
