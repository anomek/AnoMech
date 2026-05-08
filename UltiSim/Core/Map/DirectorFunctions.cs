using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace UltiSim.Core.Map;

// Replays native DirectorUpdate events via the ProcessDirectorUpdate function
// (sig from ECommons/Hyperborea). Only works when an InstanceContentDirector
// exists — i.e. after ZoneSession.SetupInstanceContent has run for the zone.
// No disposal needed: we hold a delegate, not a hook.
internal static unsafe class DirectorFunctions
{
    // Windows x64: rcx=a1(director), rdx=a2, r8=a3(category), r9=a4, stack=a5..a9
    private delegate nint ProcessDirectorUpdateDelegate(
        nint a1, uint a2, uint a3, uint a4, uint a5, int a6, int a7, int a8, int a9);

    private static readonly ProcessDirectorUpdateDelegate processDirectorUpdate;

    static DirectorFunctions()
    {
        var addr = Plugin.SigScanner.ScanText(
            "40 53 57 48 83 EC 58 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 41 8B F9");
        processDirectorUpdate =
            Marshal.GetDelegateForFunctionPointer<ProcessDirectorUpdateDelegate>(addr);
        Plugin.Log.Information("[DirectorFunctions] Initialized.");
    }

    // Fires the three DirectorUpdate events the server sends at instance Commence:
    //   4000000C | 00       ← pre-Commence signal
    //   40000001 | 1C20     ← Commence (drops spawn-area barrier + wall visual)
    //   80000004 | 1C1F     ← post-Commence signal
    // Values from real TOP_pull_01_wipe.log (t=12.5 s, simultaneous with "has begun").
    // a2 = director's own EventId (same pattern as the ACT log sourceId 0x800375AC).
    // No-ops silently if no InstanceContentDirector is present.
    internal static void Commence()
    {
        var instanceDirector = EventFramework.Instance()->GetInstanceContentDirector();
        if (instanceDirector == null)
        {
            Plugin.Log.Debug("[DirectorFunctions] Commence: no InstanceContentDirector, skipping.");
            return;
        }
        var director = (nint)instanceDirector;
        // Log the director's EventId and ContentId to confirm it's the TOP director.
        // EventHandler.Info (offset 0x20) → EventId.Id = 0x8003xxxx for InstanceContentDirector.
        // Director.ContentId (offset 0x2E0) = InstanceContent row ID (TOP ≈ 1142).
        var eventId  = ((EventHandler*)director)->Info.EventId.Id;
        var contentId = *(uint*)(director + 0x2E0);
        Plugin.Log.Information($"[DirectorFunctions] Commence: director=0x{director:X} eventId=0x{eventId:X} contentId={contentId}");
        // a1 = director (InstanceContentDirector*), a2 = 0.
        // Passing a2 = eventId crashed: ProcessDirectorUpdate uses a2 as a GetEventHandlerById
        // lookup key, and our freshly-created client-side director isn't in the handler map.
        var r0 = processDirectorUpdate(director, 0, 0x4000000C, 0,      0, 0, 0, 0, 0);
        var r1 = processDirectorUpdate(director, 0, 0x40000001, 0x1C20, 0, 0, 0, 0, 0);
        var r2 = processDirectorUpdate(director, 0, 0x80000004, 0x1C1F, 0, 0, 0, 0, 0);
        Plugin.Log.Information($"[DirectorFunctions] Commence returns: 0x{r0:X} 0x{r1:X} 0x{r2:X}");
    }
}
