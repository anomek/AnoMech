using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace UltiSim.Core;

// Hook on the native ProcessMapEffect function (ported from Hyperborea/ECommons).
// When enabled logs every map-effect call so values can be validated in-game:
//   [MapEffect] a2=0x?? a3=0x???? a4=0x???? (debug level — enable "Debug" in Dalamud)
//
// Apply() replays known effect values via direct struct write to ContentDirector.MapEffects.
// Only has visible effect when the player is inside a duty whose ContentDirector has
// loaded the territory's MapEffect layout rows.
// Encoding: packetFlags high16 = MapEffectItem.State, low8 = MapEffectItem.Flags
internal sealed unsafe class MapEffectFunctions : IDisposable
{
    private delegate long ProcessMapEffectDelegate(long module, uint a2, ushort a3, ushort a4);
    private readonly Hook<ProcessMapEffectDelegate> hook;

    private const int ItemStride  = 0xC;
    private const int StateOffset = 0x08;
    private const int FlagsOffset = 0x0A;
    private const int DirtyOffset = 0x604;

    internal MapEffectFunctions()
    {
        var addr = Plugin.SigScanner.ScanText(
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B FA 41 0F B7 E8");
        hook = Plugin.GameInterop.HookFromAddress<ProcessMapEffectDelegate>(addr, Detour);
        hook.Enable();
        Plugin.Log.Information("[MapEffect] Hook initialized.");
    }

    private long Detour(long module, uint a2, ushort a3, ushort a4)
    {
        Plugin.Log.Debug($"[MapEffect] a2=0x{a2:X} a3=0x{a3:X} a4=0x{a4:X}");
        return hook.Original(module, a2, a3, a4);
    }

    // Set by Game when a scenario starts; cleared by SimWorld.Reset().
    // Apply() is a no-op unless the current territory matches this value.
    internal uint? ExpectedTerritoryId { get; set; }

    // Applies a single MapEffect state change by writing directly to ContentDirector.MapEffects.
    // packetFlags: raw 32-bit value from ACT type-257 (high16=State, low8=Flags). index: slot.
    // No-op when the current territory doesn't match ExpectedTerritoryId.
    internal void Apply(uint packetFlags, byte index)
    {
        if (ExpectedTerritoryId is { } tid && Plugin.ClientState.TerritoryType != tid) return;

        var director = EventFramework.Instance()->GetContentDirector();
        Plugin.Log.Debug($"[MapEffect] Apply 0x{packetFlags:X8}|{index}: director=0x{((nint)director):X} mapEffects=0x{(director == null ? 0 : (nint)director->MapEffects):X}");
        if (director == null) return;
        var mapEffects = director->MapEffects;
        if (mapEffects == null || index >= 128) return;

        var base_ = (byte*)mapEffects;
        *(ushort*)(base_ + (nint)index * ItemStride + StateOffset) = (ushort)(packetFlags >> 16);
        *(byte*)(base_ + (nint)index * ItemStride + FlagsOffset) = (byte)(packetFlags & 0xFF);
        *(byte*)(base_ + DirtyOffset) = 1;
    }

    public void Dispose()
    {
        hook.Disable();
        hook.Dispose();
    }
}
