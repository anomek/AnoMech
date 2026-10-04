using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Tests;

internal sealed class FakeEventObject(int slot, uint entityId, Vector3 position, float rotation, ushort timelineState) : IEventObjectProxy
{
    private const float SharedGroupAttachSeconds = 1f;

    private float age;

    public bool Exists { get; private set; } = true;
    public int Slot => slot;
    public uint EntityId => entityId;
    public GameObjectId GameObjectId => new() { ObjectId = entityId, Type = 0 };

    public Vector3 Position { get; private set; } = position;
    public float Rotation { get; private set; } = rotation;
    public void SetPosition(Vector3 newPosition) => Position = newPosition;
    public void SetRotation(float newRotation) => Rotation = newRotation;

    // ── SharedGroup state ────────────────────────────────────────────────────

    public void SetState(ushort state) { }

    public ushort SharedTimelineState { get; private set; } = timelineState;
    public void SetSharedTimelineState(ushort state) => SharedTimelineState = state;
    public void UpdateSharedTimelineState(ushort from, ushort to) => SharedTimelineState = to;
    public void PlayAnimation(uint state, uint bitmask) { }

    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0) { }

    public bool IsSharedGroupAttached => Exists && age >= SharedGroupAttachSeconds;
    public bool IsSharedGroupTimelinePlaying => false;
    public void SilenceSharedGroupSounds() { }
    public bool DeactivateSharedGroup() => IsSharedGroupAttached;
    public bool ForceSharedGroupActive() => IsSharedGroupAttached;

    public void Despawn() => Exists = false;

    // ── Diagnostics ──────────────────────────────────────────────────────────

    public string DescribeSharedGroup() => "fake";
    public string DescribeLoadState(uint layoutId) => "fake";

    internal void Tick(float deltaSeconds) => age += deltaSeconds;
}
