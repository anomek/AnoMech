using System;
using System.Collections.Generic;
using AnoMech.Core.Native;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace AnoMech.Core.Map;

// Hook on ProcessMapEffect (sig from Hyperborea/ECommons).
// Apply() replays a known effect by calling the native function directly.
//
// The "module" arg (EventFramework+0x158) resolves to DirectorModule.ActiveContentDirector.
// Single map-effect packets reach the game through this function; batch packets set their slots
// without it, so the detour never sees them.
//
// packetFlags is a map-effect packet's own value: low16 = state, high16 = timeline mask. The
// state is what the slot rests in; the director plays it once the SGB is ready. The timeline
// mask plays now if the SGB is ready; each bit selects one of the SGB's timelines, and the ones
// not set stop. A zero mask only stores the state.
internal sealed unsafe class MapEffects : IDisposable
{
    public bool Loaded { get; set; } = false;

    // void(ContentDirector*, uint, ushort, ushort): Dalamud's hook verification rejects any
    // other shape at load.
    private delegate void ProcessMapEffectDelegate(ContentDirector* module, uint index, ushort state, ushort timeline);
    private readonly Hook<ProcessMapEffectDelegate> hook;
    private readonly nint address;
    private bool applyingForSim;

    internal MapEffects()
    {
        address = Plugin.SigScanner.ScanText(
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B FA 41 0F B7 E8");
        hook = Plugin.GameInterop.HookFromAddress<ProcessMapEffectDelegate>(address, Detour);
        hook.Enable();
    }

    private void Detour(ContentDirector* module, uint index, ushort state, ushort timeline)
    {
        if (applyingForSim)
        {
            hook.Original(module, index, state, timeline);
            return;
        }
        Plugin.LogManager.LogMapEffect(index, state, timeline);
        // Only the real native/packet path gets past the flag above; a host's SGB has been seen
        // carrying flag bits nothing here sends.
        var before = ReadMapEffectItem(module, index);
        hook.Original(module, index, state, timeline);
        var after = ReadMapEffectItem(module, index);
        AnoMech.Core.DiagnosticLog.Info(
            $"[MapEffect] REAL native call: index=0x{index:X} state=0x{state:X} timeline=0x{timeline:X} module=0x{(nint)module:X} "
            + $"item before=({Format(before)}) after=({Format(after)}).");
    }

    // Returns false when the zone/director isn't ready yet (async load still in
    // flight) so MapController can retry instead of silently losing the call.
    internal bool Apply(uint packetFlags, byte index)
    {
        if (!Loaded) return false;
        var modulePtr = *(nint*)((nint)EventFramework.Instance() + 344);
        if (modulePtr == 0) return false;
        var module = (ContentDirector*)modulePtr;
        // ProcessMapEffect writes into MapEffects->Items[index], and index can come off the
        // network. Not ready reads like any other not-ready case, so the caller retries.
        if (!IsIndexInRange(module, index)) return false;
        var state = (ushort)packetFlags;
        var timeline = (ushort)(packetFlags >> 16);
        // Before/after per index: whether LayoutId is populated at all and whether State/Flags took.
        var before = ReadMapEffectItem(module, index);
        CallForSim(module, index, state, timeline);
        var after = ReadMapEffectItem(module, index);
        AnoMech.Core.DiagnosticLog.Info(
            $"[MapEffect] native call: index=0x{index:X} state=0x{state:X} timeline=0x{timeline:X} module=0x{modulePtr:X} "
            + $"item before=({Format(before)}) after=({Format(after)}).");
        // The SharedGroupLayoutInstance behind LayoutId carries its own load state, which
        // ContentDirector's table can't see.
        var sgState = ReadSharedGroupInstanceState(after.LayoutId);
        AnoMech.Core.DiagnosticLog.Info($"[MapEffect] SharedGroupLayoutInstance for index=0x{index:X} LayoutId=0x{after.LayoutId:X}: {sgState}.");
        return true;
    }

    // hook.Original skips every hook installed after ours, so whether Splatoon or BossMod saw the
    // sim's map effects would depend on plugin load order. Entering through the patched function
    // runs the whole chain; the flag keeps our own detour from logging it as a server effect.
    private void CallForSim(ContentDirector* module, uint index, ushort state, ushort timeline)
    {
        if (!Plugin.Config.SplatoonCompat)
        {
            hook.Original(module, index, state, timeline);
            return;
        }
        applyingForSim = true;
        try
        {
            ((delegate* unmanaged<ContentDirector*, uint, ushort, ushort, void>)address)(module, index, state, timeline);
        }
        finally
        {
            applyingForSim = false;
        }
    }

    // Hard-deactivate one arena scenery slot's SharedGroup and all its children (geometry, VFX
    // AND sound), bypassing ProcessMapEffect's flag state machine: flag 0x04 ("hide") blanks the
    // BgParts but leaves the SGB's Sound children playing. False until the slot's SGB resolves.
    internal bool SuppressSlot(byte index)
    {
        if (!Loaded) return false;
        var modulePtr = *(nint*)((nint)EventFramework.Instance() + 344);
        if (modulePtr == 0) return false;
        if (!IsIndexInRange((ContentDirector*)modulePtr, index)) return false;
        var item = ReadMapEffectItem((ContentDirector*)modulePtr, index);
        if (item.LayoutId == 0) return false;
        if (!preSuppressionStates.ContainsKey(index)
            && LayoutInstanceDiagnostics.CaptureActiveStates(item.LayoutId) is { } states)
            preSuppressionStates[index] = states;
        var ok = LayoutInstanceDiagnostics.SetSharedGroupActive(item.LayoutId, active: false, recurseChildren: true);
        if (ok)
            AnoMech.Core.DiagnosticLog.Info($"[MapEffect] SuppressSlot index=0x{index:X} LayoutId=0x{item.LayoutId:X} -- SG + children set inactive.");
        return ok;
    }

    // Each suppressed slot's tree as it was before its first SuppressSlot, for RestoreSlot.
    private readonly Dictionary<byte, bool[]> preSuppressionStates = new();

    internal IReadOnlyCollection<byte> SuppressedSlots => preSuppressionStates.Keys;

    // Undoes SuppressSlot. Nothing to do for a slot never suppressed since the territory loaded.
    internal void RestoreSlot(byte index)
    {
        resuppressedSlots.Remove(index);
        if (!preSuppressionStates.Remove(index, out var states) || !Loaded) return;
        var modulePtr = *(nint*)((nint)EventFramework.Instance() + 344);
        if (modulePtr == 0 || !IsIndexInRange((ContentDirector*)modulePtr, index)) return;
        var item = ReadMapEffectItem((ContentDirector*)modulePtr, index);
        if (item.LayoutId == 0) return;
        if (LayoutInstanceDiagnostics.RestoreActiveStates(item.LayoutId, states))
            AnoMech.Core.DiagnosticLog.Info($"[MapEffect] RestoreSlot index=0x{index:X} LayoutId=0x{item.LayoutId:X} -- SG + children back as before suppression.");
        else
            AnoMech.Core.DiagnosticLog.Warn($"[MapEffect] RestoreSlot index=0x{index:X} LayoutId=0x{item.LayoutId:X} -- tree changed shape; only the SG re-enabled.");
    }

    // The territory reverted: its SharedGroups are gone.
    internal void ForgetSuppressions()
    {
        preSuppressionStates.Clear();
        resuppressedSlots.Clear();
    }

    // The engine re-activates a SharedGroup as it finishes streaming and the SGB re-arms its Sound
    // children, so a suppressed slot is re-suppressed every frame.
    private readonly HashSet<byte> resuppressedSlots = new();

    internal void KeepSlotSuppressed(byte index)
    {
        if (!Loaded) return;
        var modulePtr = *(nint*)((nint)EventFramework.Instance() + 344);
        if (modulePtr == 0) return;
        var item = ReadMapEffectItem((ContentDirector*)modulePtr, index);
        if (item.LayoutId == 0) return;
        if (LayoutInstanceDiagnostics.KeepSuppressed(item.LayoutId) && resuppressedSlots.Add(index))
            AnoMech.Core.DiagnosticLog.Info($"[MapEffect] SuppressSlot index=0x{index:X} LayoutId=0x{item.LayoutId:X} -- the engine re-activated it; kept inactive per frame from here on.");
    }

    internal void LogAllSlots(string label)
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
