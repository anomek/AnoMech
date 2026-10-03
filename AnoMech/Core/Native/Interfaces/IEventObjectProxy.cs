using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Native.Interfaces;

// One EventObject in EventObjectManager's pool. EObjs render through their attached SharedGroup,
// not a DrawObject. World space.
public interface IEventObjectProxy
{
    bool Exists { get; }
    int Slot { get; }
    uint EntityId { get; }
    GameObjectId GameObjectId { get; }

    Vector3 Position { get; }
    float Rotation { get; }
    void SetPosition(Vector3 position);
    void SetRotation(float rotation);

    // ── SharedGroup state ────────────────────────────────────────────────────

    // The state field at actor+0x1B2, plus the SharedGroup notify once it is attached.
    void SetState(ushort state);

    ushort SharedTimelineState { get; }
    void SetSharedTimelineState(ushort state);
    void UpdateSharedTimelineState(ushort from, ushort to);
    void PlayAnimation(uint state, uint bitmask);

    // The server's ActorControl packet for this object, through the client's own dispatcher.
    void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0);

    // The SharedGroup attaches about a second after the spawn.
    bool IsSharedGroupAttached { get; }
    bool IsSharedGroupTimelinePlaying { get; }
    void SilenceSharedGroupSounds();

    // False when there is no SharedGroup to change.
    bool DeactivateSharedGroup();
    bool ForceSharedGroupActive();

    void Despawn();

    // ── Diagnostics ──────────────────────────────────────────────────────────

    string DescribeSharedGroup();
    string DescribeLoadState(uint layoutId);
}
