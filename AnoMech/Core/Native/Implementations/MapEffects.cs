using System;
using AnoMech.Core.Native.Implementations;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Native.Implementations;

// Apply() feeds a single MapEffect packet to the game's handler, which runs ProcessMapEffect.
// The hook on ProcessMapEffect (sig from Hyperborea/ECommons) only logs; batch packets set their
// slots without it, so the detour never sees them.
//
// packetFlags is a map-effect packet's own value: low16 = state, high16 = timeline mask. The
// state is what the slot rests in; the director plays it once the SGB is ready. The timeline
// mask plays now if the SGB is ready; each bit selects one of the SGB's timelines, and the ones
// not set stop. A zero mask only stores the state.
internal sealed unsafe class MapEffects : IMapEffects
{
    public bool Loaded { get; set; } = false;

    // void(ContentDirector*, uint, ushort, ushort): Dalamud's hook verification rejects any
    // other shape at load.
    private delegate void ProcessMapEffectDelegate(ContentDirector* module, uint index, ushort state, ushort timeline);
    private readonly Hook<ProcessMapEffectDelegate> hook;

    internal MapEffects()
    {
        var addr = Plugin.SigScanner.ScanText(
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B FA 41 0F B7 E8");
        hook = Plugin.GameInterop.HookFromAddress<ProcessMapEffectDelegate>(addr, Detour);
        hook.Enable();
    }

    private void Detour(ContentDirector* module, uint index, ushort state, ushort timeline)
    {
        Plugin.LogManager.LogMapEffect(index, state, timeline);
        var before = ReadMapEffectItem(module, index);
        hook.Original(module, index, state, timeline);
        var after = ReadMapEffectItem(module, index);
        AnoMech.Core.DiagnosticLog.Info(
            $"[MapEffect] native call: index=0x{index:X} state=0x{state:X} timeline=0x{timeline:X} module=0x{(nint)module:X} "
            + $"item before=({Format(before)}) after=({Format(after)}).");
    }

    public bool Apply(uint packetFlags, byte index)
    {
        if (!TryGetReadyDirector(out var framework, out var module)) return false;
        // ProcessMapEffect writes into MapEffects->Items[index], and index can come off the
        // network.
        if (!IsIndexInRange(module, index)) return false;
        var packet = new MapEffectPacket
        {
            DirectorId = module->GetEventId(),
            State = (ushort)packetFlags,
            Timeline = (ushort)(packetFlags >> 16),
            Index = index,
        };
        PacketDispatcherPointers.HandleMapEffectPacket(framework, &packet);
        // The SharedGroupLayoutInstance behind LayoutId carries its own load state, which
        // ContentDirector's table can't see.
        var layoutId = ReadMapEffectItem(module, index).LayoutId;
        AnoMech.Core.DiagnosticLog.Info($"[MapEffect] SharedGroupLayoutInstance for index=0x{index:X} LayoutId=0x{layoutId:X}: {ReadSharedGroupInstanceState(layoutId)}.");
        return true;
    }

    // Slots past states.Length get state 0, as in retail's zero-padded packet.
    public bool ApplySnapshot(ReadOnlySpan<ushort> states, bool zoneIn)
    {
        if (states.Length > MapEffectSnapshotPacket.Capacity) return false;
        if (!TryGetReadyDirector(out var framework, out var module) || module->MapEffects == null) return false;
        var packet = new MapEffectSnapshotPacket
        {
            DirectorId = module->GetEventId(),
            Header = zoneIn ? MapEffectSnapshotPacket.HeaderZoneIn : MapEffectSnapshotPacket.HeaderReset,
        };
        for (var i = 0; i < states.Length; i++) packet.States[i] = states[i];
        PacketDispatcherPointers.HandleMapEffectSnapshotPacket(framework, &packet);
        return true;
    }

    private bool TryGetReadyDirector(out EventFramework* framework, out ContentDirector* module)
    {
        framework = EventFramework.Instance();
        module = Loaded ? InstanceContentDirectorHelper.ReadyContentDirector(framework) : null;
        return module != null;
    }

    public void LogAllSlots(string label)
    {
        if (!Loaded) return;
        var modulePtr = *(nint*)((nint)EventFramework.Instance() + 344);
        if (modulePtr == 0) return;
        var list = ((ContentDirector*)modulePtr)->MapEffects;
        if (list == null) return;
        AnoMech.Core.DiagnosticLog.Info($"[MapEffect] {label}: {list->ItemCount} slots.");
        for (var i = 0; i < list->ItemCount && i < 128; i++)
        {
            var item = list->Items[i];
            AnoMech.Core.DiagnosticLog.Info($"[MapEffect] {label} slot 0x{i:X}: {Format(item)} -- {LayoutInstanceDiagnostics.DescribeLive(item.LayoutId)}");
        }
    }

    private static bool IsIndexInRange(ContentDirector* director, uint index)
    {
        var list = director->MapEffects;
        return list != null && index < list->ItemCount;
    }

    private static ContentDirector.MapEffectItem ReadMapEffectItem(ContentDirector* director, uint index)
    {
        if (!IsIndexInRange(director, index)) return default;
        return director->MapEffects->Items[(int)index];
    }

    private static string Format(ContentDirector.MapEffectItem item) => $"LayoutId=0x{item.LayoutId:X} State=0x{item.State:X} Flags=0x{item.Flags:X}";

    private static string ReadSharedGroupInstanceState(uint layoutId) => LayoutInstanceDiagnostics.Describe(layoutId);

    public void Dispose()
    {
        hook.Disable();
        hook.Dispose();
    }
}
