using System.Numerics;

namespace AnoMech.Core.Native.Interfaces;

// One EventObject in EventObjectManager's pool. EObjs render through their attached SharedGroup,
// not a DrawObject. World space.
public interface IEventObjectProxy
{
    int Slot { get; }
    uint EntityId { get; }

    Vector3 Position { get; }
    float Rotation { get; }

    // The server's ActorControl packet for this object, through the client's own dispatcher.
    void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0);

    void Despawn();
}
