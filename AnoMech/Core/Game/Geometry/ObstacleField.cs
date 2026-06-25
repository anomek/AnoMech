using System;
using System.Collections.Generic;
using System.Numerics;

namespace AnoMech.Core.Game.Geometry;

// A scenario's "geometry": the set of obstacles party bots steer around while
// moving. The special container that fully owns both the obstacle list and the
// steering algorithm — SimWorld just holds one instance and hands it to each
// party doppel; it has no obstacle logic of its own.
//
// Empty by default, so with no obstacles every bot moves in a straight line
// (Steer returns the desired direction unchanged). Scenarios mutate the field
// over their event timeline via Add / Remove / Clear; it is wiped on world
// teardown. Non-bot characters (bosses, the player) keep ObstacleField.Empty and
// are never affected.
//
// Steering is reactive and per-frame, not a planned path: it naturally copes
// with obstacles that appear, drift, or vanish mid-move, and is intentionally
// simple (single dominant obstacle per frame) rather than globally optimal —
// good enough for sparse convex shapes, which is all this needs to be.
public sealed class ObstacleField
{
    // Shared empty field for every non-bot character. Never mutated: only
    // world.Obstacles is exposed to scenarios, so this stays pristine.
    public static readonly ObstacleField Empty = new();

    // How far ahead (local yalms) a bot looks to begin arcing around an obstacle
    // before reaching it. Larger -> earlier, wider arcs.
    private const float LookAhead = 2.5f;

    // Depth (local yalms) over which a bot found *inside* an obstacle is pushed
    // back out. Only relevant when a bot ends up inside one (e.g. a circle that
    // spawns on top of it); normal grazing stays at the boundary with no push.
    private const float RecoveryBand = 1.0f;

    private readonly List<IObstacle> obstacles = new();

    public IReadOnlyList<IObstacle> All => obstacles;

    // Adds an obstacle and returns it (typed) so a scenario can keep the
    // reference for a later Remove, or to mutate it (e.g. a CircleObstacle's
    // Center/Radius) over time.
    public T Add<T>(T obstacle) where T : IObstacle
    {
        obstacles.Add(obstacle);
        return obstacle;
    }

    public void Remove(IObstacle obstacle) => obstacles.Remove(obstacle);

    public void Clear() => obstacles.Clear();

    // Projects `p` out onto the boundary of the obstacle it is most deeply inside
    // so a destination authored (or fallen) inside an obstacle becomes a
    // reachable edge point instead of an unreachable center the bot orbits
    // forever. Single-pass / single-obstacle: with overlapping obstacles the
    // result may still sit inside another (acceptable; best-effort by design).
    public Vector2 ClampOutside(Vector2 p)
    {
        if (Deepest(p, out var dist, out var normal) == null || dist >= 0f) return p;
        return p - normal * dist;   // dist < 0, normal outward -> move out by |dist|
    }

    // Returns the unit steering direction for a bot at `pos` that wants to head
    // in unit `desired`. With no obstacle in the way this is just `desired`
    // (=> straight line). Otherwise the bot glides tangent to the nearest
    // blocking obstacle, on the side that still makes progress toward `desired`,
    // plus an outward push if it is currently inside.
    internal Vector2 Steer(Vector2 pos, Vector2 desired)
    {
        if (obstacles.Count == 0) return desired;

        // The obstacle "in the way": the nearest one we are inside, or near and
        // closing on along the straight path within LookAhead.
        IObstacle? blocking = null;
        var blockingDist = float.MaxValue;
        var ahead = pos + desired * LookAhead;
        foreach (var o in obstacles)
        {
            var d = o.SignedDistance(pos);
            var closing = d < LookAhead && o.SignedDistance(ahead) < d;
            if (d >= 0f && !closing) continue;          // outside and not approaching
            if (d < blockingDist) { blockingDist = d; blocking = o; }
        }
        if (blocking == null) return desired;

        var normal = blocking.Normal(pos);

        // Already heading outward / around it — don't fight the desired direction.
        if (Vector2.Dot(desired, normal) >= 0f) return desired;

        // Glide along the boundary, choosing the tangent that progresses toward
        // the target; add an outward component only while actually inside.
        var tangent = new Vector2(-normal.Y, normal.X);
        if (Vector2.Dot(tangent, desired) < 0f) tangent = -tangent;

        var push = blockingDist < 0f ? Math.Clamp(-blockingDist / RecoveryBand, 0f, 1f) : 0f;
        var heading = tangent + normal * push;
        var len = heading.Length();
        return len > 1e-4f ? heading / len : desired;
    }

    // The obstacle with the smallest signed distance from `p` (most inside, or
    // nearest if all are outside), with its outward normal at `p`.
    private IObstacle? Deepest(Vector2 p, out float dist, out Vector2 normal)
    {
        IObstacle? worst = null;
        dist = float.MaxValue;
        normal = default;
        foreach (var o in obstacles)
        {
            var d = o.SignedDistance(p);
            if (d < dist) { dist = d; worst = o; normal = o.Normal(p); }
        }
        return worst;
    }
}
