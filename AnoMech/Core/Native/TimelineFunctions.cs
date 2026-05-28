using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.Native;

// Wrappers around native TimelineContainer functions FFXIVClientStructs doesn't
// bind. Same shape as VfxFunctions: sig-scan at first use, cache the delegate.
internal static unsafe class TimelineFunctions
{
    // SetModelState(TimelineContainer* this, uint value). Parameter is uint
    // (passed in edx); the function reads ExdModule.ModelState rows for the
    // old/new value, plays the transition timeline returned by the row's Start
    // field, writes the new byte at +0x2C0, and fires the per-frame refresh
    // (FUN_1408bce10 / FUN_140907520) the engine gates on a "ModelState
    // changed" flag. Direct field writes skip all of that, which is why the
    // commit was invisible on doppels. Operates on the TimelineContainer
    // directly, so it works regardless of CharacterManager._battleCharas
    // registration.
    private delegate void SetModelStateDelegate(TimelineContainer* self, uint value);
    private static SetModelStateDelegate? setModelState;

    private static SetModelStateDelegate? ResolveSetModelState()
    {
        if (setModelState != null) return setModelState;
        try
        {
            var addr = Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? 8B D5 48 8D 8B");
            setModelState = Marshal.GetDelegateForFunctionPointer<SetModelStateDelegate>(addr);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"TimelineFunctions: failed to resolve SetModelState: {ex.Message}");
        }
        return setModelState;
    }

    public static void SetModelState(TimelineContainer* tc, byte value)
    {
        if (tc == null) return;
        var fn = ResolveSetModelState();
        if (fn == null) return;
        fn(tc, value);
    }
}
