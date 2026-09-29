using System;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace AnoMech.Core.Native;

internal static unsafe class StaticVfxTrigger
{
    // AVFX scheduler trigger slots are zero-based (0..11). The resource API
    // reserves bit 0, queues slot + 1, and the scheduler subtracts one on dispatch.
    public static bool TryQueue(VfxObject* vfx, uint triggerIndex)
    {
        if (triggerIndex >= 12) throw new ArgumentOutOfRangeException(nameof(triggerIndex));
        if (vfx == null || vfx->VfxResourceInstance == null) return false;

        // The resource API stores the request even while its AVFX is loading;
        // it checks load state and runtime handles itself. Never pass the scene
        // object to the low-level Apricot timeline constructor (crashes at +0x230).
        VfxDataPointers.QueueResourceTrigger(vfx->VfxResourceInstance, triggerIndex + 1);
        return true; // queued, not a claim that particles have rendered yet
    }
}
