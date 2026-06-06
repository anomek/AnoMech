using System.Numerics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using EventHandler = FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandler;

namespace AnoMech.Core.Map;

// Replays native DirectorUpdate events via the ProcessDirectorUpdate function
// (sig from ECommons/Hyperborea), and disables BG SharedGroup colliders that
// would otherwise lock the player into the spawn ring. Only works when an
// InstanceContentDirector exists — i.e. after ZoneSession.SetupInstanceContent
// has run for the zone. No disposal needed: we hold a delegate, not a hook.
internal static unsafe class DirectorFunctions
{
    // Windows x64: rcx=a1(director), rdx=a2, r8=a3(category), r9=a4, stack=a5..a9
    private delegate nint ProcessDirectorUpdateDelegate(
        nint a1, uint a2, uint a3, uint a4, uint a5, int a6, int a7, int a8, int a9);

    private static readonly ProcessDirectorUpdateDelegate processDirectorUpdate;

    private delegate void DirectorUnknownUpdateDelegate(EventFramework* eventFramework, uint eventId, byte sequence, byte unk, byte* unionData, ulong size);
    private static readonly DirectorUnknownUpdateDelegate DirectorUnknownUpdateFunction;

    static DirectorFunctions()
    {
        var addr = Plugin.SigScanner.ScanText(
            "40 53 57 48 83 EC 58 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 41 8B F9");
        processDirectorUpdate =
            Marshal.GetDelegateForFunctionPointer<ProcessDirectorUpdateDelegate>(addr);

        var unknownUpdateAddr = Plugin.SigScanner.ScanText(
            "89 54 24 10 48 89 4C 24 ?? 53 56 57 41 55 41 57 48 83 EC 30 48 8B 99");
        DirectorUnknownUpdateFunction = Marshal.GetDelegateForFunctionPointer<DirectorUnknownUpdateDelegate>(unknownUpdateAddr);

        Plugin.Log.Information("[DirectorFunctions] Initialized.");
    }

    // Fires the three DirectorUpdate events the server sends at instance Commence:
    //   4000000C | 00       ← pre-Commence signal
    //   40000001 | 1C20     ← Commence (drops spawn-area wall visual)
    //   80000004 | 1C1F     ← post-Commence signal
    // Values from real TOP_pull_01_wipe.log (t=12.5 s, simultaneous with "has begun").
    // a2 = 0 (passing the director's own EventId crashed: ProcessDirectorUpdate
    // uses a2 as a GetEventHandlerById lookup key, and our freshly-created
    // client-side director isn't in the handler map). Note: these IDs/params are
    // TOP-specific — other content fires different values (e.g. T=1045 uses
    // 40000007 / 40000001-E10 / 80000004-E0F). Future work to parameterize.
    internal static void Commence()
    {
        var instanceDirector = EventFramework.Instance()->GetInstanceContentDirector();
        if (instanceDirector == null)
        {
            Plugin.Log.Debug("[DirectorFunctions] Commence: no InstanceContentDirector, skipping.");
            return;
        }
        var director = (nint)instanceDirector;
        var eventId  = ((EventHandler*)director)->Info.EventId.Id;
        var contentId = *(uint*)(director + 0x2E0);
        Plugin.Log.Information($"[DirectorFunctions] Commence: director=0x{director:X} eventId=0x{eventId:X} contentId={contentId}");
        var r0 = processDirectorUpdate(director, 0, 0x4000000C, 0,      0, 0, 0, 0, 0);
        var r1 = processDirectorUpdate(director, 0, 0x40000001, 0x1C20, 0, 0, 0, 0, 0);
        var r2 = processDirectorUpdate(director, 0, 0x80000004, 0x1C1F, 0, 0, 0, 0, 0);
        Plugin.Log.Information($"[DirectorFunctions] Commence returns: 0x{r0:X} 0x{r1:X} 0x{r2:X}");
    }

    // Replays a server-sent director ActorControl (`33|` log opcode) by calling
    // ProcessDirectorUpdate directly with the recorded category/arg pair. a2=0
    // for the same reason as Commence — client-side directors aren't in the
    // EventHandler map, so the director's own EventId as a2 crashes.
    // Drives only the director's internal state — scripted side-effects the
    // server fires after this packet (BNpc spawns, model changes) are not
    // replayed and must be driven by the scenario timeline.
    internal static void FireDirectorUpdate(uint category, uint arg1, uint arg2 = 0,
                                            int a6 = 0, int a7 = 0, int a8 = 0, int a9 = 0)
    {
        var instanceDirector = EventFramework.Instance()->GetInstanceContentDirector();
        if (instanceDirector == null)
        {
            Plugin.Log.Debug($"[DirectorFunctions] FireDirectorUpdate(0x{category:X}, 0x{arg1:X}): no InstanceContentDirector, skipping.");
            return;
        }
        var director = (nint)instanceDirector;
        var eventId  = ((EventHandler*)director)->Info.EventId.Id;
        var contentId = *(uint*)(director + 0x2E0);
        Plugin.Log.Information(
            $"[DirectorFunctions] FireDirectorUpdate cat=0x{category:X} arg1=0x{arg1:X} arg2=0x{arg2:X} " +
            $"director=0x{director:X} eventId=0x{eventId:X} contentId={contentId}");
        var r = processDirectorUpdate(director, 0, category, arg1, arg2, a6, a7, a8, a9);
        Plugin.Log.Information($"[DirectorFunctions] FireDirectorUpdate returns: 0x{r:X}");
    }

    // Replays the P5 Sigma transition trigger observed in TOP_pull_05_clear.log
    // at 01:21:13.0890 (~135 ms before Omega-M's 7B85 / Omega-F's 7B86 cast):
    //   33 | 800375AC | 8000001E | 2AC
    internal static void FireP5SigmaTransition() => FireDirectorUpdate(0x8000001E, 0x2AC);

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
}
