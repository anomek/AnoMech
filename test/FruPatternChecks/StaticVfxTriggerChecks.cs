using AnoMech.Core.Native;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using static AnoMech.Scenarios.Fru.FruConstants;

internal static unsafe class StaticVfxTriggerChecks
{
    public static void Run()
    {
        var calls = 0;
        nint actualResource = 0;
        uint actualNumber = 0;
        VfxDataPointers.QueueResourceTrigger = (resource, number) =>
        {
            calls++;
            actualResource = (nint)resource;
            actualNumber = number;
        };

        VfxObject scene = default;
        Check(!StaticVfxTrigger.TryQueue(null, 1), "Null scene must not call native code");
        Check(!StaticVfxTrigger.TryQueue(&scene, 1), "Missing resource must defer activation");
        Check(calls == 0, "Missing objects must not enqueue anything");

        VfxResourceInstance resourceInstance = default;
        scene.VfxResourceInstance = &resourceInstance;
        for (uint index = 0; index < 12; index++)
        {
            Check(StaticVfxTrigger.TryQueue(&scene, index), "An allocated resource can queue while loading");
            Check(actualResource == (nint)(&resourceInstance) && actualResource != (nint)(&scene),
                "Native queue must receive the resource instance, NEVER the scene object");
            Check(actualNumber == index + 1, "Queue numbers must be one-based");
        }
        foreach (var invalid in new[] { 12u, uint.MaxValue })
        {
            var rejected = false;
            try { StaticVfxTrigger.TryQueue(&scene, invalid); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid indices must be rejected before the native boundary");
        }
        Check(calls == 12, "Invalid indices must not call native code");
        Check(StaticVfxTrigger.TryQueue(&scene, Vfx.InitialSeamTrigger) && actualNumber == 2,
            "Initial seam must use native timeline 1 via queue number 2");
        Check(StaticVfxTrigger.TryQueue(&scene, Vfx.SeamChargeTrigger) && actualNumber == 4,
            "Intermediate charge must use native timeline 3 via queue number 4");
        Console.WriteLine("PASS: VFX trigger bridge; null/loading resources; resource-vs-scene pointer; all 12 one-based queue mappings; invalid indices.");
        Console.WriteLine("PASS: FRU initial seam and intermediate charge use distinct native stages (queue 2/4).");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

// Test doubles for the native boundary, not a simulation of the game renderer.
namespace FFXIVClientStructs.FFXIV.Client.Graphics.Vfx
{
    public struct VfxResourceInstance { public nint VfxResourceObject; }
}

namespace FFXIVClientStructs.FFXIV.Client.Graphics.Scene
{
    public unsafe struct VfxObject { public VfxResourceInstance* VfxResourceInstance; }
}

namespace AnoMech.Pointers
{
    internal static unsafe class VfxDataPointers
    {
        public delegate void QueueResourceTriggerDelegate(VfxResourceInstance* resource, uint triggerNumber);
        public static QueueResourceTriggerDelegate QueueResourceTrigger = null!;
    }
}
