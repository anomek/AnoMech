using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Tests;

// The native BattleChara behind a FakeBattleChara proxy: plain fields, plus the parts the engine
// advances on its own every frame (cast bar, server carry).
internal sealed class FakeCharacter(uint entityId, string name)
{
    // The real carry stands still this long, then has arrived by CarrySettledSeconds.
    private const float CarryStartDelay = 0.2f;
    private const float CarrySettledSeconds = 0.9f;

    public uint EntityId { get; } = entityId;
    public GameObjectId GameObjectId => new() { ObjectId = EntityId, Type = 0 };
    public string Name { get; set; } = name;
    public byte ClassJob { get; set; }

    public Vector3 Position { get; set; }
    public float Rotation { get; set; }
    public float HitboxRadius { get; set; }

    public uint Health { get; set; }
    public uint MaxHealth { get; set; }
    public uint Mana { get; set; }
    public uint MaxMana { get; set; }
    public byte TargetableStatus { get; set; }
    public byte ModelState { get; set; }
    public bool HasDrawObject { get; set; }
    public bool IsDrawObjectVisible { get; set; }
    public Dictionary<byte, ushort> Tethers { get; } = new();
    // By id and source: one id applied by two sources is two entries, as in the status list. The spawn
    // packet names no source as 0xE0000000, the status list as 0.
    public static uint StatusSource(uint entityId) => entityId == 0xE0000000 ? 0 : entityId;
    public Dictionary<(ushort StatusId, uint Source), AnoMech.Core.Native.Interfaces.StatusSlot> Statuses { get; set; } = new();

    public bool IsCasting { get; set; }
    public uint CastActionId { get; set; }
    public float CurrentCastTime { get; set; }
    public float TotalCastTime { get; set; }

    private Vector3 carryStart;
    private Vector3? carryDestination;
    private float carryElapsed;

    public void StartCarry(Vector3 destination, float rotation)
    {
        glideRemaining = 0f;
        carryStart = Position;
        carryDestination = destination;
        carryElapsed = 0f;
        Rotation = rotation;
    }

    // The client's ActorMove handling: a straight walk from where the model is to the packet's
    // position over 1s, or a snap when every axis is within 1/32y.
    private const float GlideSeconds = 1f;
    private const float GlideSnapDistance = 0.03125f;
    private Vector3 glideStep;
    private float glideRemaining;

    public void StartGlide(Vector3 target, float rotation)
    {
        Rotation = rotation;
        var delta = target - Position;
        if (MathF.Abs(delta.X) < GlideSnapDistance && MathF.Abs(delta.Y) < GlideSnapDistance && MathF.Abs(delta.Z) < GlideSnapDistance)
        {
            Position = target;
            glideRemaining = 0f;
            return;
        }
        glideStep = delta / GlideSeconds;
        glideRemaining = GlideSeconds;
    }

    // UNVERIFIED: how long the client slides; the packet carries no duration.
    public void StartSlide(Vector3 destination, float rotation) => StartGlide(destination, rotation);

    public void Teleport(Vector3 position, float rotation)
    {
        Position = position;
        Rotation = rotation;
        glideRemaining = 0f;
    }

    private Vector3 pushStart;
    private Vector3 pushOffset;
    private float pushDuration;
    private float pushElapsed;

    public void StartPush(float heading, float distance, float durationSeconds)
    {
        glideRemaining = 0f;
        pushStart = Position;
        pushOffset = new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading)) * distance;
        pushDuration = MathF.Max(0.01f, durationSeconds);
        pushElapsed = 0f;
    }

    // The client walks a forced follow at run speed and stops at the target's hitbox edge.
    private const float RunSpeed = 6f;
    private FakeCharacter? forcedFollowTarget;

    public void StartForcedFollow(FakeCharacter? target) => forcedFollowTarget = target;

    private void WalkForcedFollow(float deltaSeconds)
    {
        if (forcedFollowTarget is not { } target) return;
        var away = new Vector2(target.Position.X - Position.X, target.Position.Z - Position.Z);
        var gap = away.Length() - HitboxRadius - target.HitboxRadius;
        if (gap <= 0f) return;
        var dir = away / away.Length();
        var step = dir * MathF.Min(gap, RunSpeed * deltaSeconds);
        Position += new Vector3(step.X, 0f, step.Y);
        Rotation = MathF.Atan2(dir.X, dir.Y);
    }

    // The engine's own per-frame update, which runs after the plugin's.
    public void Tick(float deltaSeconds)
    {
        WalkForcedFollow(deltaSeconds);
        if (glideRemaining > 0f)
        {
            var dt = MathF.Min(deltaSeconds, glideRemaining);
            Position += glideStep * dt;
            glideRemaining -= dt;
        }

        if (IsCasting)
        {
            CurrentCastTime = MathF.Min(CurrentCastTime + deltaSeconds, TotalCastTime);
            // UNVERIFIED: whether the engine drops IsCasting at a full bar or only on the
            // ActionEffect; SimCast reads the full CurrentCastTime either way.
            if (CurrentCastTime >= TotalCastTime) IsCasting = false;
        }

        if (pushDuration > 0f)
        {
            pushElapsed += deltaSeconds;
            var t = Math.Clamp(pushElapsed / pushDuration, 0f, 1f);
            Position = pushStart + pushOffset * (1f - (1f - t) * (1f - t));
            if (t >= 1f) pushDuration = 0f;
        }

        if (carryDestination is { } destination)
        {
            carryElapsed += deltaSeconds;
            var t = Math.Clamp((carryElapsed - CarryStartDelay) / (CarrySettledSeconds - CarryStartDelay), 0f, 1f);
            var eased = 1f - (1f - t) * (1f - t) * (1f - t);
            Position = Vector3.Lerp(carryStart, destination, eased);
            if (t >= 1f) carryDestination = null;
        }
    }
}
