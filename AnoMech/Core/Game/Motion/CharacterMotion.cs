using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game.Motion;

// What the owning character's state allows this tick. The character checks its own gates, so a
// motion never reaches back into it. SpeedFactor scales ordered walks (MovementSpeed).
internal readonly record struct MotionGates(bool Alive, bool MovementLocked, bool AnimationLocked, float SpeedFactor = 1f);

// A character's place in the world: the authoritative position mechanics read, the walking a strat
// or scenario orders (MoveTo, Follow, Intercept, TurnTo), and the forced moves a mechanic makes.
// Each kind of character gets these from a different source:
// - SimulatedMotion (bots, NPCs): the sim computes the position, even under a move whose packet
//   animates the model itself, so a headless run sees what the game does; packets follow it.
// - PlayerMotion (local player): the model is the position. Orders are dropped unless
//   DebugBotControl drives the character; forced moves go out as the packet retail sends.
// - RemoteMotion (a remote player's puppet): the owning peer's reported pose is the position.
//   Orders are dropped; forced moves are queued for the owner.
internal abstract class CharacterMotion
{
    public abstract Vector3 Position { get; }
    public abstract float Rotation { get; }

    // A teleport the scenario or the engine makes, not a mechanic's (see ForcedMove.Teleport).
    public abstract void SetPosition(Vector3 position);
    public abstract void SetPlacement(Placement placement);

    // For an actor the engine hasn't created yet: its spawn packet already carries the transform.
    public virtual void Seed(Placement placement) { }

    // Puts the model back on the transform once an actor moved while its spawn was in flight exists.
    public virtual void ResendTransform() { }

    public abstract void TurnTo(float rotation);
    public abstract void Face(Vector3 target);
    public abstract void MoveTo(Vector3 target, float speed, float? finalRotation);
    public abstract void Follow(SimCharacter? target, float speed);
    public abstract void Intercept(SimTether? tether, float margin);

    // Ends the ordered walk; a forced move in flight carries on.
    public abstract void Stop();

    // Ends everything, forced moves included: despawn, the end of a run.
    public abstract void Clear();

    public abstract void Force(ForcedMove move);

    public abstract bool IsMoving { get; }

    // A knockback, push or carry still moving the character.
    public virtual bool IsForcedMoving => false;
    public virtual Vector3? Destination => null;
    public virtual bool IsIntercepting => false;

    // Yalms (XZ) between the sim's position and the model at the start of the last tick; zero
    // where the model is the position.
    public virtual float ModelDivergence => 0f;
    public virtual float MaxModelDivergence => 0f;

    public abstract void Tick(float deltaSeconds, MotionGates gates);
}
