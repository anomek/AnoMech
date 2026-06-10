using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using System.Numerics;
using System.Runtime.InteropServices;

namespace AnoMech.Core.Map;

// Replays native DirectorUpdate events via the ProcessDirectorUpdate function
// (sig from ECommons/Hyperborea), and disables BG SharedGroup colliders that
// would otherwise lock the player into the spawn ring. Only works when an
// InstanceContentDirector exists — i.e. after ZoneSession.SetupInstanceContent
// has run for the zone. No disposal needed: we hold a delegate, not a hook.
internal static unsafe class DirectorFunctions
{
    private delegate void DirectorUnknownUpdateDelegate(EventFramework* eventFramework, uint eventId, byte sequence, byte unk, byte* unionData, ulong size);
    private static readonly DirectorUnknownUpdateDelegate DirectorUnknownUpdateFunction;

    static DirectorFunctions()
    {
        var unknownUpdateAddr = Plugin.SigScanner.ScanText(
            "89 54 24 10 48 89 4C 24 ?? 53 56 57 41 55 41 57 48 83 EC 30 48 8B 99");
        DirectorUnknownUpdateFunction = Marshal.GetDelegateForFunctionPointer<DirectorUnknownUpdateDelegate>(unknownUpdateAddr);

        Plugin.Log.Information("[DirectorFunctions] Initialized.");
    }

    // The duty pre-pull spawn barrier is a set of SharedGroup ILayoutInstances
    // (the engine's "prefab" container type, see SharedGroupLayoutInstance.cs).
    // In a real duty the server-driven Commence flow clears their collision; in
    // our fake instance we have to do it ourselves. Both steps are necessary:
    //   PrefabFlags2 & 0x8 is a marker bit ("colliders active"), and vfunc 37
    //   SetColliderActive(false) is the actual physics-world off-switch.
    //
    // Returns the count of SharedGroups deactivated. Zero is the normal early
    // return during async zone-load streaming; the caller retries each frame.
    internal static int DisableSpawnAreaColliders(Vector3 center, float radius)
    {
        var lw = LayoutWorld.Instance();
        if (lw == null || lw->ActiveLayout == null) return 0;

        var r2 = radius * radius;
        int disabled = 0;
        foreach (var layerKv in lw->ActiveLayout->Layers)
        {
            var layer = layerKv.Item2.Value;
            if (layer == null) continue;
            foreach (var instKv in layer->Instances)
            {
                var inst = instKv.Item2.Value;
                if (inst == null) continue;
                if (inst->Id.Type != InstanceType.SharedGroup) continue;

                var sg = (SharedGroupLayoutInstance*)inst;
                var pos = sg->Transform.Translation;
                var dx = pos.X - center.X;
                var dz = pos.Z - center.Z;
                if (dx * dx + dz * dz > r2) continue;

                sg->PrefabFlags2 &= ~0x8u;
                inst->SetColliderActive(false);
                disabled++;
            }
        }

        if (disabled > 0)
            Plugin.Log.Information($"[BarrierDrop] disabled {disabled} SharedGroups within r{radius} of ({center.X:F2},{center.Y:F2},{center.Z:F2})");
        return disabled;
    }

    internal static void DirectorUnknownUpdate(byte sequence, byte unk, byte* unionData)
    {
        var eventFramework = EventFramework.Instance();
        var instanceDirector = eventFramework->GetInstanceContentDirector();

        if (instanceDirector == null)
        {
            Plugin.Log.Debug("[DirectorFunctions] DirectorUnknownUpdate: no InstanceContentDirector, skipping.");
            return;
        }

        DirectorUnknownUpdateFunction(eventFramework, instanceDirector->Info.EventId.Id, sequence, unk, unionData, 12); // Size 12 is hard-coded in the game .exe
    }

    internal static void DirectorUnknownUpdate(byte sequence, byte unk, byte[] unionData)
    {
        fixed (byte* unionDataPtr = unionData)
        {
            DirectorUnknownUpdate(0, 1, unionDataPtr);
        }
    }
}
