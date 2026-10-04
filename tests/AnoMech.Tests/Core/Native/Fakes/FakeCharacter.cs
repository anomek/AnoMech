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
    public byte TargetableStatus { get; set; }
    public byte ModelState { get; set; }
    public bool HasDrawObject { get; set; }
    public bool IsDrawObjectVisible { get; set; }
    public Dictionary<byte, ushort> Tethers { get; } = new();

    public bool IsCasting { get; set; }
    public uint CastActionId { get; set; }
    public float CurrentCastTime { get; set; }
    public byte ModeAttributeFlags { get; set; }
    public float TotalCastTime { get; set; }

    private Vector3 carryStart;
    private Vector3? carryDestination;
    private float carryElapsed;

    public void StartCarry(Vector3 destination, float rotation)
    {
        carryStart = Position;
        carryDestination = destination;
        carryElapsed = 0f;
        Rotation = rotation;
    }

    // The engine's own per-frame update, which runs after the plugin's.
    public void Tick(float deltaSeconds)
    {
        if (IsCasting)
        {
            CurrentCastTime = MathF.Min(CurrentCastTime + deltaSeconds, TotalCastTime);
            // UNVERIFIED: whether the engine drops IsCasting at a full bar or only on the
            // ActionEffect; SimCast reads the full CurrentCastTime either way.
            if (CurrentCastTime >= TotalCastTime) IsCasting = false;
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
