using System;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game.Motion;

// A move the mechanic makes, whatever the character wants. Says what happens, never which packet
// shows it: each CharacterMotion picks the delivery retail uses for its kind of character.
internal abstract record ForcedMove
{
    // Away from `From`, by a Knockback sheet row an ActionEffect carried. The client applies it to
    // its own player itself, except for a row the server sends as the push packet.
    public sealed record Knockback(Vector3 From, KnockbackRow Row) : ForcedMove;

    // A scripted push, which no Knockback row describes: the server sends it as the push packet.
    // Along a world heading (Placement.Rotation convention)...
    public sealed record Push(float Heading, float Distance, float DurationSeconds) : ForcedMove;

    // ...or away from a point.
    public sealed record PushAway(Vector3 From, float Distance, float DurationSeconds) : ForcedMove;

    // The server carry (ActorControl 0xF1), UMAD P1's arrows.
    public sealed record Carry(Vector3 To, ushort? Timeline = null) : ForcedMove;

    // The icefloor slide (ActorControl 0xDF), FRU P2 Frost Armor.
    public sealed record IceSlide(Vector3 To, float DurationSeconds) : ForcedMove;

    public sealed record Teleport(Placement To) : ForcedMove;

    // Confusion's chase; a null target hands control back. Re-issued as the target changes, so a
    // motion keeps it as a standing order rather than a one-shot move.
    public sealed record Follow(SimCharacter? Target, float Speed) : ForcedMove;
}

// Where a forced move has the character at each moment, for motions that simulate it.
internal sealed class Trajectory(Vector3 start, Vector3 end, float delay, float duration, Func<float, float> ease)
{
    // The retail knockback and the scripted push both decelerate from about twice their average
    // speed.
    public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    // The carry stands still for a moment, then settles in sharply.
    public static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    private const float CarryStartDelay = 0.2f;
    private const float CarrySettledSeconds = 0.9f;

    public static Trajectory Slide(Vector3 start, Vector3 end, float duration)
        => new(start, end, 0f, duration, EaseOutQuad);

    public static Trajectory Carry(Vector3 start, Vector3 end)
        => new(start, end, CarryStartDelay, CarrySettledSeconds - CarryStartDelay, EaseOutCubic);

    private float elapsed;

    public Vector3 End => end;
    public bool Done => elapsed >= delay + duration;

    public void Advance(float deltaSeconds) => elapsed += deltaSeconds;

    public Vector3 Position => At(elapsed);

    // `seconds` past the current moment.
    public Vector3 Ahead(float seconds) => At(elapsed + seconds);

    private Vector3 At(float time)
    {
        var t = duration > 0f ? Math.Clamp((time - delay) / duration, 0f, 1f) : time >= delay ? 1f : 0f;
        return Vector3.Lerp(start, end, ease(t));
    }
}
