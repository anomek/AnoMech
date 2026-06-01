using System;
using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game;

internal class Movement(SimCharacter parent)
{
    protected const ushort RunTimelineId = 22;
    private const ushort KnockbackTimelineId = 156;

    private Vector3? destination;
    private float speed;
    private float? finalRotation;
    private bool faceTravel = true;
    private ushort timelineId;
    private bool timelineBaseOverride;
    private bool animActive;

    private SimCharacter? followTarget;

    public bool IsMoving => destination != null;

    public virtual void MoveTo(Vector3 t, float sp = 6f, float? finalRot = null, ushort tl = RunTimelineId, bool baseOverride = true)
        => InternalMoveTo(t, sp, finalRot, tl, baseOverride);

    public void Follow(SimCharacter? target, float speed = 6f)
    {
        if (!target.IsAlive())
        {
            followTarget = null;
            Stop();
            return;
        }
        followTarget = target;
        this.speed = MathF.Max(0f, speed);
    }

    public void Knockback(Vector3 source, float distance, float kbSpeed)
    {
        var kbDestination = parent.Placement().Face(source).MoveForward(-distance).Position;
        InternalMoveTo(kbDestination, kbSpeed, tl: KnockbackTimelineId, baseOverride: false, faceTravel: false);
        
    }

    // Shared move entry for MoveTo (locomotion) and Knockback (one-shot action).
    // `baseOverride` selects the animation mechanism in StartAnim: true for a
    // looping locomotion clip (run/walk), false for a one-shot action timeline
    // (knockback). See StartAnim for why the two need different handling.
    private void InternalMoveTo(
        Vector3 moveDestination, float sp = 6f, float? finalRot = null, ushort tl = RunTimelineId, bool baseOverride = true,
        bool faceTravel = true)
    {
        destination = moveDestination;
        speed = MathF.Max(0f, sp);
        finalRotation = finalRot;
        this.faceTravel = faceTravel;
        var sameAnim = animActive && timelineId == tl;
        timelineId = tl;
        timelineBaseOverride = baseOverride;
        if (!sameAnim) StartAnim();
    }

    public void Tick(float deltaSeconds)
    {
        if (parent.AnimationLock)
        {
            StopAnim();
            return;
        }

        TickFollow();

        if (destination is not { } dest) return;

        // Re-assert the movement animation if a move is still pending but its
        // animation was stopped while we were animation-locked. Follow re-issues
        // via InternalMoveTo every tick (which restarts the anim itself), but a
        // one-shot MoveTo/Knockback won't, so re-assert here or it would slide the
        // rest of the way unanimated.
        if (!animActive) StartAnim();

        var cur = parent.Position;
        var dx = dest.X - cur.X;
        var dz = dest.Z - cur.Z;
        var distSq = dx * dx + dz * dz;
        var step = speed * deltaSeconds;

        if (distSq <= step * step || step <= 0f)
        {
            var rot = finalRotation ?? (faceTravel && distSq > 1e-6f ? MathF.Atan2(dx, dz) : parent.Rotation);
            parent.SetPosition(new Placement(dest, rot));
            finalRotation = null;
            Stop();
        }
        else
        {
            var dist = MathF.Sqrt(distSq);
            var next = new Vector3(cur.X + dx / dist * step, cur.Y, cur.Z + dz / dist * step);
            parent.SetPosition(new Placement(next, faceTravel ? MathF.Atan2(dx, dz) : parent.Rotation));
        }
    }

    private void TickFollow()
    {
        if (followTarget == null) return;
        if (!followTarget.IsAlive())
        {
            followTarget = null;
            Stop();
            return;
        }
        var stopDist = parent.HitboxRadius + followTarget.HitboxRadius;
        if (parent.Placement().DistanceSq(followTarget) <= stopDist * stopDist)
        {
            Stop();
            Face(followTarget.Position);
        }
        else
        {
            InternalMoveTo(followTarget.Position, speed);
        }
    }

    public void Stop()
    {
        destination = null;
        StopAnim();
    }


    // Two distinct animation mechanisms, selected by timelineBaseOverride:
    //   * Locomotion (run/walk, 22): the engine recomputes the base slot from the
    //     character's movement state every frame, and a SetPosition-driven doppel
    //     reads as velocity 0 -> idle, which clobbers a plain PlayActionTimeline
    //     (the model just slides). BaseOverride is the one field the engine forces
    //     instead, so we pass timelineId there to keep the run loop applied.
    //   * One-shot action (knockback, 156): an action timeline isn't derived from
    //     movement state, so it plays through with baseOverride 0; ResetActionTimeline
    //     (StopAnim) returns it to idle on arrival. Passing it as BaseOverride would
    //     loop the pose forever (the original "knockback stuck" bug).
    protected void StartAnim()
    {
        parent.PlayActionTimeline(timelineId, baseOverride: timelineBaseOverride ? timelineId : (ushort)0);
        animActive = true;
    }

    protected void StopAnim()
    {
        if (!animActive) return;
        parent.ResetActionTimeline();
        animActive = false;
    }

    public void Face(Vector3? t)
    {
        if(t is not {} target) return;
        var dx = target.X - parent.Position.X;
        var dz = target.Z - parent.Position.Z;
        if (dx * dx + dz * dz < 1e-6f) return;
        parent.SetRotation(MathF.Atan2(dx, dz));
    }
}

internal sealed class PlayerMovement(SimCharacter parent) : Movement(parent)
{
    public override void MoveTo(Vector3 t, float sp = 6f, float? finalRot = null, ushort tl = RunTimelineId, bool baseOverride = true)
    {
        // NO-OP - player cannot be moved like this
    }
}
