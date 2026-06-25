using System.Numerics;

namespace AnoMech.Core.Game.Geometry;

// A circular keep-out area. Radius is the whole keep-out: a bot's center is kept
// at/outside `Radius`, so a scenario sizes the circle to the hazard plus however
// much body clearance it wants baked in. Center/Radius are public and mutable so
// a scenario can drift or grow the circle over its event timeline.
public sealed class CircleObstacle(Vector2 center, float radius) : IObstacle
{
    public Vector2 Center = center;
    public float Radius = radius;

    public float SignedDistance(Vector2 p) => Vector2.Distance(p, Center) - Radius;

    public Vector2 Normal(Vector2 p)
    {
        var d = p - Center;
        var len = d.Length();
        // Exactly at the center there is no defined outward direction; pick an
        // arbitrary stable one so callers always get a unit vector.
        return len > 1e-4f ? d / len : new Vector2(1f, 0f);
    }
}
