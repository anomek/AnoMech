using System.Numerics;

namespace AnoMech.Core.Map;

public sealed record CircleArena(float Radius) : IArenaShape
{
    public bool IsOutside(Vector3 local) => local.X * local.X + local.Z * local.Z > Radius * Radius;
}
