using System;
using System.Runtime.InteropServices;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Native.Implementations;

// A change to the local player's job gauge, delivered as the server's ActorGauge packet. Writes go
// to a copy of JobGaugeManager; disposing sends the packet if the copy differs. One edit at a time.
internal unsafe ref struct JobGaugeEdit
{
    private static readonly JobGaugeManager* Scratch =
        (JobGaugeManager*)NativeMemory.AllocZeroed((nuint)sizeof(JobGaugeManager));
    private static bool open;

    // Null when the player has no gauge; still dispose it.
    public JobGaugeManager* Gauge { get; private init; }

    public static JobGaugeEdit Begin()
    {
        if (open) throw new InvalidOperationException("A job gauge edit is already open.");
        if (!Natives.PlayerActionState.HasJobGauge) return default;
        var live = JobGaugeManager.Instance();
        open = true;
        *Scratch = *live;
        return new JobGaugeEdit { Gauge = Scratch };
    }

    public void Dispose()
    {
        if (Gauge == null) return;
        open = false;
        var live = JobGaugeManager.Instance();
        if (live == null || live->CurrentGauge == null || live->ClassJobId != Scratch->ClassJobId) return;
        var values = new ReadOnlySpan<byte>((byte*)Scratch + ActorGaugePacket.ManagerValuesOffset, ActorGaugePacket.ValuesLength);
        if (values.SequenceEqual(new ReadOnlySpan<byte>((byte*)live + ActorGaugePacket.ManagerValuesOffset, ActorGaugePacket.ValuesLength)))
            return;
        Send(Scratch);
    }

    // Every byte the packet carries to 0.
    public void Clear()
    {
        if (Gauge == null) return;
        new Span<byte>((byte*)Gauge + ActorGaugePacket.ManagerValuesOffset, ActorGaugePacket.ValuesLength).Clear();
    }

    // The gauge as it is now, to Restore later.
    public static ActorGaugePacket? Capture()
    {
        if (!Natives.PlayerActionState.HasJobGauge) return null;
        return PacketOf(JobGaugeManager.Instance());
    }

    // Skipped once the player has changed job: the values would decode as another job's gauge.
    public static void Restore(ActorGaugePacket packet)
    {
        if (!Natives.PlayerActionState.HasJobGauge) return;
        var live = JobGaugeManager.Instance();
        if (live->ClassJobId != packet.ClassJobId) return;
        PacketDispatcherPointers.HandleActorGaugePacket(live, &packet);
    }

    private static void Send(JobGaugeManager* source)
    {
        var packet = PacketOf(source);
        PacketDispatcherPointers.HandleActorGaugePacket(JobGaugeManager.Instance(), &packet);
    }

    private static ActorGaugePacket PacketOf(JobGaugeManager* source)
    {
        var packet = new ActorGaugePacket { ClassJobId = source->ClassJobId };
        Buffer.MemoryCopy((byte*)source + ActorGaugePacket.ManagerValuesOffset, packet.Values,
            ActorGaugePacket.ValuesLength, ActorGaugePacket.ValuesLength);
        return packet;
    }
}
