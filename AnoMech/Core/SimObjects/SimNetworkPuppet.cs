using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.SimObjects;

// A party slot occupied by a real remote player. Same doppel spawn path as SimPartyNpc, but
// Movement is a no-op (NetworkPuppetMovement), so code that addresses every slot uniformly
// skips it the way it skips SimPlayer; only ApplyNetworkPose moves it.
//
// Not a SimPartyNpc subclass: that class is sealed and the two share only a dozen lines.
public sealed class SimNetworkPuppet : SimNpc, ISimPartyMember
{
    // Same smoothing model as SimEnemy's Network* fields: the catch-up speed is a floor once
    // the real pose interval is known, anything beyond SnapThreshold (spawn, lag spike,
    // teleport) snaps instead of gliding, and extrapolation only feeds the visual glide.
    private const float CatchUpSpeed = 20f;
    private const float SnapThreshold = 15f;
    private const float AngularCatchUpSpeed = MathF.PI * 20f;
    private const float IntervalSmoothingFactor = 0.3f;
    private const float MinPacingWindowSeconds = 0.05f;
    private float timeSinceLastPose;
    private float estimatedPoseInterval = 0.05f;
    private const float MaxPoseExtrapolationSeconds = 1f;
    private Vector3 poseVelocity;
    // Game.Movement.RunTimelineId; the Movement property below shadows that type name here.
    private const ushort RunTimelineId = 22;

    private Vector3? targetPosition;
    private float targetRotation;
    private bool interpAnimActive;
    private bool poseMoving;

    // Below this, two consecutive poses read as "same spot" rather than motion.
    private const float PoseMovementEpsilon = 0.01f;

    // Mechanic resolution must see where the peer's real character is, not where the model
    // has interpolated to; base.Position stays what Tick drives and what is rendered.
    public override Vector3 Position => targetPosition ?? base.Position;

    public PartyRole Role { get; set; }
    public bool Dead { get; private set; }
    public byte ClassJob { get; }
    public string DisplayName { get; }

    internal SimNetworkPuppet(IBattleCharaProxy proxy, Coordinates coordinates, PartyRole role, byte classJob, string name) : base(proxy, coordinates)
    {
        Role = role;
        ClassJob = classJob;
        DisplayName = name;
    }

    private protected override Movement Movement => field ??= new NetworkPuppetMovement(this);

    // The position write happens in Tick so the model steps toward the pose with the run
    // animation playing. poseMoving comes from whether the reported position is advancing,
    // not from interpolation state (see SimEnemy.TickNetworkPosition).
    public void ApplyNetworkPose(Vector3 position, float rotation)
    {
        if (targetPosition is { } previous)
        {
            poseMoving = Vector3.DistanceSquared(previous, position) > PoseMovementEpsilon * PoseMovementEpsilon;
            estimatedPoseInterval += (timeSinceLastPose - estimatedPoseInterval) * IntervalSmoothingFactor;
            poseVelocity = timeSinceLastPose > MinPacingWindowSeconds ? (position - previous) / timeSinceLastPose : Vector3.Zero;
        }
        targetPosition = position;
        targetRotation = rotation;
        timeSinceLastPose = 0f;
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        if (Dead || targetPosition is not { } rawTarget) return;
        timeSinceLastPose += deltaSeconds;

        // Interpolates the native transform; the overridden Position reports targetPosition
        // untouched by the extrapolation.
        var target = rawTarget + poseVelocity * MathF.Min(timeSinceLastPose, MaxPoseExtrapolationSeconds);
        var basePos = base.Position;
        var delta = target - basePos;
        var dist = delta.Length();
        var remainingWindow = MathF.Max(estimatedPoseInterval - timeSinceLastPose, MinPacingWindowSeconds);
        var step = MathF.Max(dist / remainingWindow, CatchUpSpeed) * deltaSeconds;
        var nextRotation = MathUtil.StepRotation(Rotation, targetRotation, AngularCatchUpSpeed * deltaSeconds);
        if (dist > SnapThreshold || dist <= step)
            SetPosition(new Placement(target, nextRotation));
        else
            SetPosition(new Placement(basePos + delta / dist * step, nextRotation));

        // Native entry points: interpolation is not a scenario cue to broadcast.
        if (AnimationLock) return;
        if (poseMoving && !interpAnimActive)
        {
            PlayActionTimelineNative(RunTimelineId, baseOverride: RunTimelineId);
            interpAnimActive = true;
        }
        else if (!poseMoving && interpAnimActive)
        {
            ResetActionTimelineNative();
            interpAnimActive = false;
        }
    }

    private protected override void PauseMoveAnimationForAction()
    {
        base.PauseMoveAnimationForAction();
        if (!interpAnimActive) return;
        ResetActionTimelineNative();
        interpAnimActive = false;
    }

    // A forced move here only moves the host's cosmetic copy; MultiplayerManager polls these
    // to tell the owning peer to apply it to their real character.
    public (Vector3 Source, float Distance, float Speed)? PendingNetworkKnockback { get; private set; }
    public (float Heading, float Distance, float Speed, float DurationSeconds, bool Walk)? PendingNetworkPush { get; private set; }
    public Placement? PendingNetworkTeleport { get; private set; }
    // Edge-triggered on the (target, speed) pair: Umad P1's confused chase re-issues Follow
    // every tick with the same target.
    public (SimCharacter? Target, float Speed)? PendingNetworkFollow { get; private set; }
    private (SimCharacter? Target, float Speed) lastNetworkFollow;

    public void Knockback(Vector3 source, float distance, float speed)
    {
        Movement.Knockback(source, distance, speed);
        PendingNetworkKnockback = (source, distance, speed);
    }

    public void ClearPendingNetworkKnockback() => PendingNetworkKnockback = null;

    public void PushInDirection(float heading, float distance, float speed)
    {
        Movement.PushInDirection(heading, distance, speed);
        PendingNetworkPush = (heading, distance, speed, 0f, false);
    }

    public void PushInDirectionEased(float heading, float distance, float durationSeconds)
    {
        Movement.PushInDirectionEased(heading, distance, durationSeconds);
        PendingNetworkPush = (heading, distance, 0f, durationSeconds, false);
    }

    public void WalkInDirection(float heading, float distance, float speed)
    {
        Movement.WalkInDirection(heading, distance, speed);
        PendingNetworkPush = (heading, distance, speed, 0f, true);
    }

    public void ClearPendingNetworkPush() => PendingNetworkPush = null;

    // The cosmetic copy snaps too, so mechanics read the new spot until the peer's next pose.
    public void TeleportTo(Placement placement)
    {
        SetPosition(placement);
        if (targetPosition != null) targetPosition = placement.Position;
        PendingNetworkTeleport = placement;
    }

    public void ClearPendingNetworkTeleport() => PendingNetworkTeleport = null;

    // The owner's client performs the carry; this copy follows their reported poses.
    public (Vector3 Destination, CarryMode Mode)? PendingNetworkCarry { get; private set; }

    public override void CarryTo(Vector3 destination, CarryMode mode = CarryMode.Native) => PendingNetworkCarry = (destination, mode);

    public void ClearPendingNetworkCarry() => PendingNetworkCarry = null;

    // Only a forced follow reaches the owner: an unforced one is a strat walking its bots, and
    // the person in this seat is playing, not being driven. A release always propagates -- it
    // only ever hands control back. Never applied locally either: this copy's position is the
    // owner's reported pose, which TickFollow would fight every frame.
    public override void Follow(SimCharacter? target = null, float speed = 6f, bool forced = false)
    {
        var liveTarget = target.IsAlive() ? target : null;
        if (!forced && liveTarget != null) return;
        if (ReferenceEquals(liveTarget, lastNetworkFollow.Target) && (liveTarget == null || speed == lastNetworkFollow.Speed)) return;
        lastNetworkFollow = (liveTarget, speed);
        PendingNetworkFollow = lastNetworkFollow;
    }

    public void ClearPendingNetworkFollow() => PendingNetworkFollow = null;

    public void OnKilled()
    {
        Dead = true;
        StopMoving();
        AbortPlayedAction();
        interpAnimActive = false;
        if (Proxy is not { Exists: true } chara) return;
        chara.ApplyDeadState();
        this.PlayKoActionTimeline();
    }
}
