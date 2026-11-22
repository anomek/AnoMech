using System.Numerics;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace AnoMech.Core.Native.Implementations;

internal sealed unsafe class EventObjectProxy(int slot, GameObject* obj) : IEventObjectProxy
{
    private const uint NoTarget = 0xE0000000;

    public int Slot => slot;
    public uint EntityId => obj->EntityId;

    public Vector3 Position => obj->Position;
    public float Rotation => obj->Rotation;

    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
        => PacketDispatcher.HandleActorControlPacket(obj->EntityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, NoTarget, false);

    public void Despawn()
    {
        byte[] packet = [(byte)slot];
        fixed (byte* packetPtr = packet)
            PacketDispatcherPointers.HandleDespawnObjectPacket(0, packetPtr);
    }
}
