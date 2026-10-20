using System;
using System.Numerics;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game.Motion;

// Bots and NPCs, which the server moves in retail: the sim owns the position, and the model
// follows it through SetPos/Move packets, trailing by however long the client takes to apply them.
internal sealed class SimulatedMotion : CharacterMotion, INavigated
{
    private readonly Func<IBattleCharaProxy?> proxy;
    private readonly Coordinates coordinates;
    private readonly ActorControl actorControl;
    private readonly ModelSync model;
    private readonly Navigator navigator;

    private Vector3 position;
    private float rotation;

    // False until the sim gives the character a transform; the first tick with a model adopts
    // the model's instead, so a turn can't drag it to the origin.
    private bool placed;

    // A step went out since the last Halt, so the model may be aimed ahead of the sim.
    private bool stepping;
    private float stepSpeed;

    private Trajectory? forcedMove;

    // The forced move's packet animates the model itself (a server carry, an NPC knockback), so no
    // ActorMove may cut across it.
    private bool forcedByActorControl;
    private ActorMoveFlags forcedFlags;
    private float forcedSpeed;

    // An NPC, which the server moves through a knockback with its own packet; a bot's knockback is
    // what its client reports, ActorMoves.
    private readonly bool npc;

    // Retail's Super Cyclone push of a Razor Plume (3-4y, row speed 40). UNVERIFIED whether the
    // server sends another duration for other pushes.
    private const float NpcKnockbackSeconds = 0.5f;

    public SimulatedMotion(Func<IBattleCharaProxy?> proxy, Coordinates coordinates, ActorControl actorControl, ObstacleField obstacles, float? turnSpeed, bool npc)
    {
        this.proxy = proxy;
        this.coordinates = coordinates;
        this.actorControl = actorControl;
        this.npc = npc;
        Obstacles = obstacles;
        model = new ModelSync(proxy, coordinates, turnSpeed);
        navigator = new Navigator(this);
    }

    public override Vector3 Position => position;
    public override float Rotation => rotation;
    public ObstacleField Obstacles { get; }
    public float HitboxRadius => proxy()?.HitboxRadius ?? 0f;
    public bool TurnsAlongPath => true;

    public override bool IsMoving => navigator.IsMoving || forcedMove != null;
    public override bool IsForcedMoving => forcedMove != null;
    public override Vector3? Destination => navigator.Destination ?? forcedMove?.End;
    public override bool IsIntercepting => navigator.IsIntercepting;

    public override float ModelDivergence => modelDivergence;
    public override float MaxModelDivergence => maxModelDivergence;
    private float modelDivergence;
    private float maxModelDivergence;

    public override void SetPosition(Vector3 newPosition)
    {
        placed = true;
        position = newPosition;
        model.SetPos(newPosition);
    }

    public override void SetPlacement(Placement placement)
    {
        placed = true;
        position = placement.Position;
        rotation = placement.Rotation;
        model.SetPos(placement.Position, placement.Rotation);
    }

    public override void Seed(Placement placement)
    {
        placed = true;
        position = placement.Position;
        rotation = placement.Rotation;
        model.Assume(placement.Position, placement.Rotation);
    }

    public override void ResendTransform() => model.SetPos(position);

    // The sim's turn takes effect at once, so a scenario that faces and fires in the same tick
    // still aims where it faced; only the model turns at the turn speed.
    public override void TurnTo(float newRotation)
    {
        rotation = newRotation;
        model.Turn(newRotation);
    }

    void INavigated.Turn(float newRotation) => TurnTo(newRotation);

    public override void Face(Vector3 target) => navigator.Face(target);

    public override void MoveTo(Vector3 target, float speed, float? finalRotation) => navigator.MoveTo(target, speed, finalRotation);

    public override void Follow(SimCharacter? target, float speed) => navigator.Follow(target, speed, forced: false);

    public void Follow(SimCharacter? target, float speed, float reach) => navigator.Follow(target, speed, forced: false, reach);

    public void HomeIn(SimCharacter? target, float speed) => navigator.Follow(target, speed, forced: false, toCentre: true);

    public void KeepFacing(SimCharacter? target) => navigator.KeepFacing(target);

    public override void Intercept(SimTether? tether, float margin) => navigator.Intercept(tether, margin);

    public override void Stop() => navigator.Stop();

    public override void Clear()
    {
        navigator.Clear();
        forcedMove = null;
    }

    public override void Force(ForcedMove move)
    {
        switch (move)
        {
            case ForcedMove.Knockback kb:
                SlideAway(kb.From, kb.Row.Distance, kb.Row.Speed > 0f ? kb.Row.Distance / kb.Row.Speed : 0f);
                break;
            case ForcedMove.PushAway away:
                SlideAway(away.From, away.Distance, away.DurationSeconds);
                break;
            case ForcedMove.Push push:
            {
                var to = position + new Vector3(MathF.Sin(push.Heading), 0f, MathF.Cos(push.Heading)) * push.Distance;
                Slide(to, push.DurationSeconds);
                break;
            }
            case ForcedMove.Carry carry:
                navigator.Clear();
                actorControl.CarryTo(coordinates.ToGlobal(carry.To), rotation, carry.Timeline);
                forcedMove = Trajectory.Carry(position, carry.To);
                forcedByActorControl = true;
                model.Assume(carry.To);
                break;
            case ForcedMove.IceSlide slide:
            {
                navigator.Clear();
                var direction = slide.To - position;
                var heading = direction.LengthSquared() > 1e-6f ? MathF.Atan2(direction.X, direction.Z) : rotation;
                actorControl.Slide(coordinates.ToGlobal(slide.To), heading);
                forcedMove = Trajectory.Slide(position, slide.To, slide.DurationSeconds);
                forcedByActorControl = true;
                rotation = heading;
                model.Assume(slide.To, heading);
                break;
            }
            case ForcedMove.Teleport teleport:
                SetPlacement(teleport.To);
                break;
            case ForcedMove.Follow follow:
                navigator.Follow(follow.Target, follow.Speed, forced: true);
                break;
            case ForcedMove.March march:
            {
                navigator.Clear();
                var direction = march.To - position;
                var distance = new Vector2(direction.X, direction.Z).Length();
                if (distance < 1e-3f || march.Speed <= 0f) break;
                rotation = MathF.Atan2(direction.X, direction.Z);
                model.Face(rotation);
                forcedMove = Trajectory.Walk(position, march.To, distance / march.Speed);
                forcedByActorControl = false;
                forcedFlags = ActorMoveFlags.None;
                forcedSpeed = march.Speed;
                break;
            }
        }
    }

    // Placement.Face keeps the facing of a character standing on `from`, which is flung along it.
    private void SlideAway(Vector3 from, float distance, float duration)
        => Slide(new Placement(position, rotation).Face(from).MoveForward(-distance).Position, duration);

    // A knockback or push: an NPC's is one server packet, which the client slides out; a bot's
    // model follows it in ActorMove steps.
    private void Slide(Vector3 to, float duration)
    {
        navigator.Clear();
        if (npc)
        {
            forcedMove = Trajectory.Slide(position, to, NpcKnockbackSeconds);
            forcedByActorControl = true;
            model.NpcKnockback(to, NpcKnockbackSeconds);
            return;
        }
        forcedMove = Trajectory.Slide(position, to, duration);
        forcedByActorControl = false;
        forcedFlags = ActorMoveFlags.Knockback;
        forcedSpeed = duration > 0f ? Vector3.Distance(position, to) / duration : 0f;
    }

    public override void Tick(float deltaSeconds, MotionGates gates)
    {
        if (proxy() is { Exists: true } native)
        {
            var modelPosition = coordinates.ToLocal(native.Position);
            if (!placed)
            {
                position = modelPosition;
                rotation = native.Rotation;
                model.Assume(modelPosition, native.Rotation);
                placed = true;
            }
            modelDivergence = Vector2.Distance(new Vector2(position.X, position.Z), new Vector2(modelPosition.X, modelPosition.Z));
            maxModelDivergence = MathF.Max(maxModelDivergence, modelDivergence);
        }

        if (forcedMove is { } trajectory)
        {
            trajectory.Advance(deltaSeconds);
            position = trajectory.Position;
            if (!forcedByActorControl)
            {
                stepping = true;
                model.Glide(trajectory.Ahead(ModelSync.LeadSeconds), forcedFlags, forcedSpeed, now: trajectory.Done);
            }
            if (trajectory.Done) forcedMove = null;
            else if (forcedByActorControl) return;
        }
        else if (!gates.Alive)
        {
            if (navigator.HasOrders) navigator.Clear();
        }
        else
        {
            if (gates.MovementLocked) navigator.DropUnforced();
            navigator.Tick(deltaSeconds, held: gates.AnimationLocked, gates.SpeedFactor);
        }

        model.Flush(deltaSeconds);
    }

    private void Step(Vector3 next, Vector3 glide, float speed, bool arrived)
    {
        stepping = true;
        stepSpeed = speed;
        position = next;
        model.Glide(glide, ActorMoveFlags.None, speed);
    }

    void INavigated.Step(Vector3 next, Vector3 glide, float speed, bool arrived) => Step(next, glide, speed, arrived);

    // The last throttled move left the model short of where the sim stopped; it walks the rest at
    // the walk's speed, since the ground controller moves at the speed byte.
    void INavigated.Halt()
    {
        if (!stepping) return;
        stepping = false;
        model.Glide(position, ActorMoveFlags.None, stepSpeed, now: true);
    }
}
