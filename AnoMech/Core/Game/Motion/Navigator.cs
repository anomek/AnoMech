using System;
using System.Numerics;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game.Motion;

// What a Navigator walks and how its steps reach the game.
internal interface INavigated
{
    Vector3 Position { get; }
    float Rotation { get; }
    float HitboxRadius { get; }
    ObstacleField Obstacles { get; }

    // False where the game walks the character itself and faces it the way it goes.
    bool TurnsAlongPath { get; }

    // One step toward `next` at `speed` y/s; `glide` is where the walker will be
    // ModelSync.LeadSeconds from now. `arrived` on the step that lands on the destination.
    void Step(Vector3 next, Vector3 glide, float speed, bool arrived);

    // Walking stopped, or is held by an animation lock.
    void Halt();

    void Turn(float rotation);
}

// Commanded walking: MoveTo, Follow, Intercept, with obstacle steering. Holds the order and works
// out each step; the walker decides what a step does.
internal sealed class Navigator(INavigated walker)
{
    // After a follower reaches its target it waits this long before chasing again,
    // so it doesn't twitch back into motion the instant the target drifts.
    private const float FollowArrivalCooldown = 0.5f;

    // A follow lands exactly on the hitbox edge; float error mustn't keep it walking.
    private const float FollowArrivalSlack = 0.05f;

    private Vector3? destination;
    private float speed;
    private float? finalRotation;
    private int steerSide;
    private bool moveForced;

    private SimCharacter? followTarget;
    private bool followForced;
    private bool followWalking;
    private float followCooldown;
    private float followReach;
    private bool followToCentre;

    private SimTether? interceptTether;
    private float interceptMargin = 3f;   // park this many yards short of either tether endpoint

    private SimCharacter? faceTarget;

    // Set around RetargetIntercept/TickFollow's own re-issued move so SetDestination doesn't
    // cancel the tracking driving them; any external move cancels a stale Intercept/Follow.
    private bool internalReissue;

    public bool IsMoving => destination != null;
    public Vector3? Destination => destination;
    public bool IsIntercepting => interceptTether != null;
    public bool HasOrders => destination != null || followTarget != null || interceptTether != null || faceTarget != null;

    public void MoveTo(Vector3 to, float sp, float? finalRot) => SetDestination(to, sp, finalRot, forced: false);

    // forced: the mechanic itself is taking control (UMAD P1's Confusion), as opposed to a
    // strat walking a bot to its spot. A null or dead target releases the follow. reach: how far
    // past touching hitboxes to stop, or past the target's centre with toCentre, whatever either
    // hitbox is.
    public void Follow(SimCharacter? target, float sp, bool forced, float reach = 0f, bool toCentre = false)
    {
        if (!target.IsAlive())
        {
            followTarget = null;
            followForced = false;
            Stop();
            return;
        }
        followTarget = target;
        followForced = forced;
        followCooldown = 0f;
        followReach = reach;
        followToCentre = toCentre;
        interceptTether = null;
        speed = MathF.Max(0f, sp);
    }

    // Turns to the target whenever the walker is free: not held by an animation lock, walking or
    // following. Outlasts a one-off Face (a cast release faces its own target); null releases it.
    public void KeepFacing(SimCharacter? target) => faceTarget = target;

    // Walk to the nearest point on the tether line and keep tracking it: TickIntercept
    // re-projects every frame so a tether whose endpoints drift is still met. `margin`
    // is how many yards short of either endpoint to park.
    public void Intercept(SimTether? tether, float margin)
    {
        followTarget = null;
        interceptTether = tether;
        interceptMargin = margin;
        RetargetIntercept(logDetail: true);
    }

    // Re-project the walker onto the tether segment and re-issue the move; self-cancels if the
    // tether or an endpoint is gone. logDetail only on the initial Intercept() call.
    private void RetargetIntercept(bool logDetail = false)
    {
        var margin = interceptMargin;
        if (interceptTether is not { A: { } a, B: { } b } || !a.IsAlive() || !b.IsAlive())
        {
            DiagnosticLog.Warn(
                $"[Navigator] RetargetIntercept self-cancelled: tether={(interceptTether == null ? "null" : "present")} "
                + $"A={(interceptTether?.A == null ? "null" : interceptTether.A.IsAlive() ? "alive" : "dead")} "
                + $"B={(interceptTether?.B == null ? "null" : interceptTether.B.IsAlive() ? "alive" : "dead")} -- no move issued.");
            interceptTether = null;
            return;
        }
        var position = walker.Position;
        var src = new Vector2(a.Position.X, a.Position.Z);
        var seg = new Vector2(b.Position.X, b.Position.Z) - src;
        var len = seg.Length();
        internalReissue = true;
        if (len < 1e-6f)
        {
            MoveTo(new Vector3(src.X, position.Y, src.Y), 6f, null);
            internalReissue = false;
            return;
        }
        var rel = new Vector2(position.X, position.Z) - src;
        // Park `margin` yards short of either endpoint instead of standing right on it.
        // On a segment under 2*margin long the two insets cross, so settle on the midpoint.
        var inset = margin / len;
        var (tMin, tMax) = 2f * inset >= 1f ? (0.5f, 0.5f) : (inset, 1f - inset);
        var t = Math.Clamp(Vector2.Dot(rel, seg) / (len * len), tMin, tMax);
        // Slide the parked point along the tether line to the nearest spot clear of
        // obstacles, so the bot lands on the grab corridor instead of being parked
        // perpendicular off it when a black hole sits on the line.
        var target = walker.Obstacles.NearestClearOnSegment(src, src + seg, t, tMin, tMax);
        if (logDetail)
            DiagnosticLog.Info(
                $"[Navigator] RetargetIntercept: from ({position.X:F1},{position.Z:F1}) toward "
                + $"target ({target.X:F1},{target.Y:F1}) on segment A({src.X:F1},{src.Y:F1})-B({(src + seg).X:F1},{(src + seg).Y:F1}), t={t:F2}.");
        MoveTo(new Vector3(target.X, position.Y, target.Y), 6f, null);
        internalReissue = false;
    }

    // Keep the intercept aimed at the moving tether each frame. Exception: once the
    // tether is attached to this walker (it has become an endpoint) we stop
    // re-projecting — that would collapse the target onto ourselves and freeze us
    // mid-approach — and let the in-flight move finish on its own. Also stops once the
    // move completes or the tether goes inactive.
    private void TickIntercept()
    {
        if (interceptTether is not { } tether) return;
        if (!IsMoving || !tether.IsActive || IsWalker(tether.A) || IsWalker(tether.B))
        {
            interceptTether = null;
            return;
        }
        RetargetIntercept();
    }

    // Tether endpoints are characters; the walker is that character's motion.
    private bool IsWalker(SimCharacter? endpoint) => endpoint is not null && endpoint.WalksWith(walker);

    private void SetDestination(Vector3 to, float sp, float? finalRot, bool forced)
    {
        // An external move supersedes any Intercept/Follow.
        if (!internalReissue)
        {
            interceptTether = null;
            followTarget = null;
            steerSide = 0;
        }
        destination = to;
        moveForced = forced;
        speed = MathF.Max(0f, sp);
        finalRotation = finalRot;
    }

    // held: an animation lock roots the walker in place for now; the order resumes after.
    // speedFactor scales every order's speed: the walker's statuses' (MovementSpeed).
    public void Tick(float deltaSeconds, bool held, float speedFactor = 1f)
    {
        if (held)
        {
            walker.Halt();
            return;
        }

        TickFollow(deltaSeconds);
        TickIntercept();

        if (destination is not { } dest)
        {
            if (followTarget == null && faceTarget.IsAlive()) Face(faceTarget!.Position);
            return;
        }

        var cur = walker.Position;

        // Park-at-edge: if the destination lies inside (or within steering clearance of) an
        // obstacle, retarget just outside it so the walker stops at the edge instead of
        // orbiting an unreachable point.
        var dest2 = walker.Obstacles.ClampForSteering(new Vector2(dest.X, dest.Z));

        var dx = dest2.X - cur.X;
        var dz = dest2.Y - cur.Z;
        var distSq = dx * dx + dz * dz;
        var walkSpeed = speed * speedFactor;
        var step = walkSpeed * deltaSeconds;

        if (distSq <= step * step || step <= 0f)
        {
            var rot = finalRotation ?? (distSq > 1e-6f ? MathF.Atan2(dx, dz) : walker.Rotation);
            var arrival = new Vector3(dest2.X, dest.Y, dest2.Y);
            walker.Step(arrival, arrival, walkSpeed, arrived: true);
            walker.Turn(rot);
            finalRotation = null;
            Stop();
        }
        else
        {
            var dist = MathF.Sqrt(distSq);
            var desired = new Vector2(dx / dist, dz / dist);
            // A no-op (returns `desired`) when the field is empty or nothing blocks.
            var heading = walker.Obstacles.Steer(new Vector2(cur.X, cur.Z), desired, dist, ref steerSide);
            var next = new Vector3(cur.X + heading.X * step, cur.Y, cur.Z + heading.Y * step);
            var lead = walkSpeed * ModelSync.LeadSeconds;
            var glide = lead >= dist
                ? new Vector3(dest2.X, dest.Y, dest2.Y)
                : new Vector3(cur.X + heading.X * lead, cur.Y, cur.Z + heading.Y * lead);
            walker.Step(next, glide, walkSpeed, arrived: false);
            if (walker.TurnsAlongPath) walker.Turn(MathF.Atan2(heading.X, heading.Y));
        }
    }

    private void TickFollow(float deltaSeconds)
    {
        if (followTarget == null) return;
        if (!followTarget.IsAlive())
        {
            followTarget = null;
            followForced = false;
            followCooldown = 0f;
            Stop();
            return;
        }

        // Arrived last frame: sit out the cooldown facing the target, don't chase yet.
        if (followCooldown > 0f)
        {
            followCooldown -= deltaSeconds;
            Face(followTarget.Position);
            return;
        }

        var position = walker.Position;
        var target = followTarget.Position;
        var stopDist = followToCentre ? followReach : walker.HitboxRadius + followTarget.HitboxRadius + followReach;
        var reach = stopDist + FollowArrivalSlack;
        var away = new Vector2(position.X - target.X, position.Z - target.Z);
        if (away.LengthSquared() <= reach * reach)
        {
            // Start the cooldown only on the frame we actually arrive (were walking the follow;
            // the walk may already have stopped itself on the edge point).
            if (IsMoving || followWalking) followCooldown = FollowArrivalCooldown;
            followWalking = false;
            Stop();
            Face(target);
        }
        else
        {
            // Walk to the hitbox edge, not the centre: the model is aimed ahead along the path
            // (ModelSync.LeadSeconds), so a centre destination would carry it inside.
            var edge = away.LengthSquared() > 1e-6f ? Vector2.Normalize(away) * stopDist : Vector2.Zero;
            followWalking = true;
            internalReissue = true;
            SetDestination(new Vector3(target.X + edge.X, target.Y, target.Z + edge.Y), speed, null, followForced);
            internalReissue = false;
        }
    }

    // A lock landing mid-move stops what the AI ordered; a forced follow carries on.
    public void DropUnforced()
    {
        if (followTarget != null && !followForced) followTarget = null;
        interceptTether = null;
        if (destination != null && !moveForced) Stop();
    }

    // Ends the walk in hand; a follow re-issues it next tick unless released too.
    public void Stop()
    {
        destination = null;
        interceptTether = null;
        walker.Halt();
    }

    public void Clear()
    {
        followTarget = null;
        followForced = false;
        followCooldown = 0f;
        faceTarget = null;
        Stop();
    }

    public void Face(Vector3 target)
    {
        var position = walker.Position;
        var dx = target.X - position.X;
        var dz = target.Z - position.Z;
        if (dx * dx + dz * dz < 1e-6f) return;
        walker.Turn(MathF.Atan2(dx, dz));
    }
}
