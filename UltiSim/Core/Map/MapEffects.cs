using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace UltiSim.Core.Map;

// Hook on ProcessMapEffect (sig from Hyperborea/ECommons).
// Logs every call at Debug level: [MapEffect] index=0x?? state=0x???? flags=0x??
// Apply() replays a known effect by calling the native function directly.
//
// The "module" arg (EventFramework+0x158) resolves to DirectorModule.ActiveContentDirector.
// ProcessMapEffectEx (the network-packet batch variant) routes through this same function
// internally, so there is no separate commit step — this is the correct and only endpoint.
//
// Encoding: packetFlags high16 = State, low8 = Flags
// State: selects the SGB animation mode on the FIRST call to a slot; ignored on subsequent calls.
// Flags: triggers a specific animation action (0x01 show, 0x02 spawn, 0x04 hide, 0x08 despawn,
//        0x10 eyelid-close-instant, 0x20 eyelid-close-anim, 0x40/0x80 charge anim).
internal sealed unsafe class MapEffects : IDisposable
{
    public bool Loaded { get; set; } = false;

    private delegate long ProcessMapEffectDelegate(long module, uint index, ushort state, ushort flags);
    private readonly Hook<ProcessMapEffectDelegate> hook;

    internal MapEffects()
    {
        var addr = Plugin.SigScanner.ScanText(
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B FA 41 0F B7 E8");
        hook = Plugin.GameInterop.HookFromAddress<ProcessMapEffectDelegate>(addr, Detour);
        hook.Enable();
    }

    private long Detour(long module, uint index, ushort state, ushort flags)
    {
        // Info logging so i can gather logs of events from instance
        Plugin.Log.Info($"[MapEffect] index=0x{index:X} state=0x{state:X} flags=0x{flags:X}");
        Plugin.LogManager.LogMapEffect(index, state, flags);
        return hook.Original(module, index, state, flags);
    }

    // packetFlags: high16=State, low8=Flags (ACT type-257 raw value).
    internal void Apply(uint packetFlags, byte index)
    {
        if (!Loaded) return;
        var module = *(nint*)((nint)EventFramework.Instance() + 344);
        if (module == 0) return;
        hook.Original(module, index, (ushort)(packetFlags >> 16), (ushort)(packetFlags & 0xFF));
    }

    public void Dispose()
    {
        hook.Disable();
        hook.Dispose();
    }
}
