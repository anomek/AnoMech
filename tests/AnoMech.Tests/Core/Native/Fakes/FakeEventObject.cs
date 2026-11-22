using System.Numerics;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

internal sealed class FakeEventObject(int slot, uint entityId, Vector3 position, float rotation, SimTimeline timeline, string name)
    : IEventObjectProxy
{
    public bool Exists { get; private set; } = true;
    public int Slot => slot;
    public uint EntityId => entityId;

    public Vector3 Position => position;
    public float Rotation => rotation;

    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
        => timeline.Add("ac", entityId, name, $"0x{category:X}", p: SimTimeline.Params(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));

    public void Despawn()
    {
        if (Exists) timeline.Add("despawn", entityId, name);
        Exists = false;
    }
}
