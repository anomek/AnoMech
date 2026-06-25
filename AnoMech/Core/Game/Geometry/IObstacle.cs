using System.Numerics;

namespace AnoMech.Core.Game.Geometry;

// A convex keep-out area in scenario-local XZ coordinates. The whole avoidance
// system is built on just these two primitives, so a new shape (rectangle,
// donut, cone, half-plane, ...) only has to implement them — the steering in
// ObstacleField never changes.
//
// The pair is a signed distance field plus its outward normal: SignedDistance
// gives "how far in/out am I" (the union of obstacles is the per-point minimum),
// and Normal gives "which way is out". Keep them consistent — Normal(p) should
// point in the direction SignedDistance increases at p.
public interface IObstacle
{
    // Signed distance from `p` to the boundary, in local yalms: < 0 inside,
    // 0 on the boundary, > 0 outside.
    float SignedDistance(Vector2 p);

    // Outward-pointing unit direction at `p` (obstacle -> away). Used both to
    // push a bot out and to pick which way to round the shape.
    Vector2 Normal(Vector2 p);
}
