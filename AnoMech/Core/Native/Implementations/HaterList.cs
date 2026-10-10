using System.Collections.Generic;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Native.Implementations;

internal sealed unsafe class HaterList : IHaterList
{
    public void Receive(IReadOnlyList<Hater> haters)
    {
        var packet = new HaterPacket();
        var count = haters.Count < HaterPacket.Capacity ? haters.Count : HaterPacket.Capacity;
        packet.Count = (byte)count;
        var entries = (HaterPacketEntry*)((byte*)&packet + HaterPacket.EntriesOffset);
        for (var i = 0; i < count; i++)
        {
            entries[i].EntityId = haters[i].EntityId;
            entries[i].Enmity = haters[i].Enmity;
        }
        PacketDispatcherPointers.HandleUpdateHaterPacket(0, &packet);
    }
}
