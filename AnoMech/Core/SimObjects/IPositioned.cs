using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Core.SimObjects;

// Position is scenario-space (relative to SimWorld.ScenarioOrigin). Use
// SimWorld.Coordinates.ToGlobal(...) if you need world coords for native interop.
// Rotation is absolute radians (independent of scenario origin).
public interface IPositioned
{
    Vector3 Position { get; }
    float Rotation { get; }
}

public static class PositionedExtensions
{
    public static Placement Placement(this IPositioned p) => new(p.Position, p.Rotation);
}
