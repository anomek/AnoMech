using System;
using System.Numerics;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game.Motion;

// The local player, whom the client moves, as in the real game: the model is the position.
// Orders reach the character only under DebugBotControl, as movement input; a strat walking its
// bots must never take the wheel from someone practising. Forced moves, Confusion's follow
// included, go out as the packets retail sends the player.
internal sealed class PlayerMotion : CharacterMotion, INavigated
{
    // Retail's carry has settled ~0.9s after the ActorControl.
    private const float CarryHoldSeconds = 1f;

    // Retail re-sends the forced follow this often, retargeting on each.
    private const float ForcedFollowResendSeconds = 0.1f;

    // Arrival as a bot's follow counts it, against the target's sim position: the client's own
    // approach checks the target's model, which trails a walking bot and lets two chasers meet on
    // top of each other. Stopped, the chase resumes once the gap opens past the margin.
    private const float ForcedFollowArrivalSlack = 0.05f;
    private const float ForcedFollowResumeMargin = 0.5f;

    private readonly Coordinates coordinates;
    private readonly ActorControl actorControl;
    private readonly ObstacleField obstacles;
    private readonly Navigator navigator;

    // The last transform read off the model, for while there is none (a zone change).
    private Vector3 lastPosition;
    private float lastRotation;

    // The client is sliding the character; a debug driver's walk waits it out.
    private float holdRemaining;

    private SimCharacter? forcedFollowTarget;
    private float forcedFollowResend;
    private bool forcedFollowArrived;

    public PlayerMotion(Coordinates coordinates, ActorControl actorControl, ObstacleField obstacles)
    {
        this.coordinates = coordinates;
        this.actorControl = actorControl;
        this.obstacles = obstacles;
        navigator = new Navigator(this);
    }

    private static IBattleCharaProxy Proxy => Natives.BattleCharas.LocalPlayer;

    public override Vector3 Position
        => Proxy is { Exists: true } native ? lastPosition = coordinates.ToLocal(native.Position) : lastPosition;

    public override float Rotation
        => Proxy is { Exists: true } native ? lastRotation = native.Rotation : lastRotation;

    // Only a bot steers around geometry; a human walks a forced follow (Confusion) straight.
    public ObstacleField Obstacles => DebugBotControl.Enabled ? obstacles : ObstacleField.Empty;
    public float HitboxRadius => Proxy.HitboxRadius;
    public bool TurnsAlongPath => false;

    public override bool IsMoving => navigator.IsMoving || forcedFollowTarget != null;
    public override Vector3? Destination => navigator.Destination;
    public override bool IsIntercepting => navigator.IsIntercepting;
    public bool IsHeld => holdRemaining > 0f;
    public override bool IsForcedMoving => IsHeld;

    public override void SetPosition(Vector3 position)
        => Proxy.SetPos(coordinates.ToGlobal(position), Rotation);

    public override void SetPlacement(Placement placement)
        => Proxy.SetPos(coordinates.ToGlobal(placement.Position), placement.Rotation);

    // The client drops an ActorMove for its own player, so the turn goes as a SetPos on the spot.
    public override void TurnTo(float rotation)
    {
        if (DebugBotControl.Enabled) Proxy.SetPos(Proxy.Position, rotation);
    }

    void INavigated.Turn(float rotation) => TurnTo(rotation);

    public override void Face(Vector3 target) => navigator.Face(target);

    public override void MoveTo(Vector3 target, float speed, float? finalRotation)
    {
        if (DebugBotControl.Enabled) navigator.MoveTo(target, speed, finalRotation);
    }

    // Releasing always applies: it only ever hands control back.
    public override void Follow(SimCharacter? target, float speed)
    {
        if (target is null || DebugBotControl.Enabled) navigator.Follow(target, speed, forced: false);
    }

    // The client walks the follow itself at its own speed; the next resend carries a new target.
    private void ForceFollow(SimCharacter? target)
    {
        if (!target.IsAlive()) target = null;
        if (ReferenceEquals(target, forcedFollowTarget)) return;
        var released = target is null;
        forcedFollowTarget = target;
        forcedFollowResend = 0f;
        forcedFollowArrived = false;
        Plugin.Log.Info(released
            ? "[ForcedFollow] sent: release"
            : $"[ForcedFollow] sent: follow {target!.GameObjectId.ObjectId:X8} at {Vector3.Distance(Position, target.Position):F1}y");
        if (released) Proxy.ForcedFollow(null);
    }

    private void TickForcedFollow(float deltaSeconds)
    {
        if (forcedFollowTarget is null) return;
        if (!forcedFollowTarget.IsAlive())
        {
            ForceFollow(null);
            return;
        }

        var target = forcedFollowTarget.Position;
        var position = Position;
        var gap = new Vector2(target.X - position.X, target.Z - position.Z).Length();
        var reach = HitboxRadius + forcedFollowTarget.HitboxRadius + ForcedFollowArrivalSlack;
        if (gap <= reach)
        {
            if (!forcedFollowArrived) Proxy.StopForcedMove();
            forcedFollowArrived = true;
            return;
        }
        if (forcedFollowArrived)
        {
            if (gap <= reach + ForcedFollowResumeMargin) return;
            forcedFollowArrived = false;
            forcedFollowResend = 0f;
        }

        forcedFollowResend -= deltaSeconds;
        if (forcedFollowResend > 0f) return;
        forcedFollowResend = ForcedFollowResendSeconds;
        Proxy.ForcedFollow(forcedFollowTarget.GameObjectId.ObjectId);
    }

    public override void Intercept(SimTether? tether, float margin)
    {
        if (DebugBotControl.Enabled) navigator.Intercept(tether, margin);
    }

    public override void Stop() => navigator.Stop();

    public override void Clear()
    {
        navigator.Clear();
        ForceFollow(null);
        holdRemaining = 0f;
        march = null;
    }

    public override void Force(ForcedMove move)
    {
        switch (move)
        {
            case ForcedMove.Knockback kb:
            {
                if (kb.Row.Speed <= 0f) return;
                var duration = kb.Row.Distance / kb.Row.Speed;
                // The ActionEffect that carried the row already made the client slide its player.
                if (kb.Row.ClientApplies) Hold(duration);
                else PushAway(kb.From, kb.Row.Distance, duration);
                break;
            }
            case ForcedMove.PushAway away:
                PushAway(away.From, away.Distance, away.DurationSeconds);
                break;
            case ForcedMove.Push push:
                actorControl.Push(push.Heading, push.Distance, push.DurationSeconds);
                Hold(push.DurationSeconds);
                break;
            case ForcedMove.Carry carry:
                actorControl.CarryTo(coordinates.ToGlobal(carry.To), Rotation, carry.Timeline);
                Hold(CarryHoldSeconds);
                break;
            case ForcedMove.IceSlide slide:
            {
                var direction = slide.To - Position;
                actorControl.Slide(coordinates.ToGlobal(slide.To), MathF.Atan2(direction.X, direction.Z));
                Hold(slide.DurationSeconds);
                break;
            }
            case ForcedMove.Teleport teleport:
                SetPlacement(teleport.To);
                break;
            case ForcedMove.Follow follow:
                ForceFollow(follow.Target);
                break;
            case ForcedMove.March march:
                StartMarch(march.To, march.Speed);
                break;
        }
    }

    // Retail streams the next point of the march every ~0.25s, ~0.37s ahead of where the player is,
    // then the end point with speed 0. UNVERIFIED how the client walks its player on them.
    private const float MarchStepSeconds = 0.25f;
    private const float MarchLeadSeconds = 0.37f;

    private Trajectory? march;
    private float marchHeading;
    private float marchSpeed;
    private float marchStepIn;

    private void StartMarch(Vector3 to, float speed)
    {
        var start = Position;
        var direction = to - start;
        var distance = new Vector2(direction.X, direction.Z).Length();
        if (distance < 1e-3f || speed <= 0f) return;
        var duration = distance / speed;
        march = Trajectory.Walk(start, to, duration);
        marchHeading = MathF.Atan2(direction.X, direction.Z);
        marchSpeed = speed;
        marchStepIn = 0f;
        Hold(duration);
    }

    private void TickMarch(float deltaSeconds)
    {
        if (march is not { } walk) return;
        walk.Advance(deltaSeconds);
        if (walk.Done)
        {
            march = null;
            actorControl.ForcedMarchEnd(coordinates.ToGlobal(walk.End), marchHeading);
            return;
        }
        marchStepIn -= deltaSeconds;
        if (marchStepIn > 0f) return;
        marchStepIn = MarchStepSeconds;
        actorControl.ForcedMarchStep(coordinates.ToGlobal(walk.Ahead(MarchLeadSeconds)), marchHeading, marchSpeed);
    }

    // A player standing on `from` has no direction to be pushed in.
    private void PushAway(Vector3 from, float distance, float duration)
    {
        var position = Position;
        var dx = position.X - from.X;
        var dz = position.Z - from.Z;
        if (dx * dx + dz * dz < 1e-6f) return;
        actorControl.Push(MathF.Atan2(dx, dz), distance, duration);
        Hold(duration);
    }

    private void Hold(float seconds) => holdRemaining = MathF.Max(holdRemaining, seconds);

    public override void Tick(float deltaSeconds, MotionGates gates)
    {
        holdRemaining = MathF.Max(0f, holdRemaining - deltaSeconds);
        if (!gates.Alive)
        {
            if (navigator.HasOrders) navigator.Clear();
            ForceFollow(null);
            march = null;
            return;
        }
        TickMarch(deltaSeconds);
        TickForcedFollow(deltaSeconds);
        if (!DebugBotControl.Enabled || gates.MovementLocked) navigator.DropUnforced();
        navigator.Tick(deltaSeconds, held: IsHeld || gates.AnimationLocked, gates.SpeedFactor);
    }

    // The client owns its player's position, so the walk goes in as movement input and the game
    // moves the character at its own run (or sprint) speed.
    void INavigated.Step(Vector3 next, Vector3 glide, float speed, bool arrived)
    {
        if (arrived)
        {
            Natives.PlayerInput.Steer(null, 0f);
            return;
        }
        var d = next - Position;
        Natives.PlayerInput.Steer(new Vector2(d.X, d.Z), speed);
    }

    void INavigated.Halt() => Natives.PlayerInput.Steer(null, 0f);
}
