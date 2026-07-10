using System.Text;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;

namespace AnoMech.Core.Native;

// Seeds the game's RSV (Resolved String Value) table. Newer content (ultimates,
// recent savage — e.g. UMAD/Kefka) stores action/status names in the Excel sheets
// as "_rsv_<id>_..." placeholder tokens; the real localized text is delivered at
// runtime by the server via LayoutWorld::AddRsvString, but ONLY while you are
// inside the duty. AnoMech runs scenarios inn-only, so it never receives those
// packets and the cast bar / tooltip name resolution falls back to the raw
// placeholder (blank name). Writing the mappings directly via AddRsvString
// populates both the game's native RSV map (native cast bars resolve) and — because
// Dalamud hooks the same function (RsvResolver) — Dalamud's Lumina-side lookup.
//
// This is generic plumbing; the content-specific name table lives in the scenario
// layer (e.g. Scenarios/Umad/UmadReplayData.cs). See memory reference_rsv_action_names.
internal static unsafe class RsvFunctions
{
    // rsvKey is the placeholder token stored in the sheet (e.g.
    // "_rsv_49884_-1_1_0_0_SE2DC5B04_EE2DC5B04"); resolved is the display string
    // (e.g. "Kefka Says"). Safe to call repeatedly — the native map does an
    // insert-or-assign. No-op if the layout world isn't up yet.
    public static void Add(string rsvKey, string resolved)
    {
        var lw = LayoutWorld.Instance();
        if (lw == null) return;

        var keyBytes = Encoding.UTF8.GetBytes(rsvKey + "\0");
        var valBytes = Encoding.UTF8.GetBytes(resolved + "\0"); // null-terminated buffer...
        fixed (byte* keyPtr = keyBytes)
        fixed (byte* valPtr = valBytes)
        {
            // ...but the size the engine copies is the content length, excluding the null.
            lw->AddRsvString(keyPtr, valPtr, (nuint)(valBytes.Length - 1));
        }
    }

    // Same as Add, but the resolved value is raw bytes rather than a UTF-8 string —
    // for RSV values that carry embedded SeString payloads (e.g. status descriptions
    // that link an action name) and so aren't valid UTF-8. Written to the native map
    // verbatim; the game re-parses it as a SeString on display.
    public static void AddRaw(string rsvKey, byte[] valueBytes)
    {
        var lw = LayoutWorld.Instance();
        if (lw == null || valueBytes == null) return;

        var keyBytes = Encoding.UTF8.GetBytes(rsvKey + "\0");
        var valBuf = new byte[valueBytes.Length + 1]; // extra byte stays 0 = null terminator
        valueBytes.CopyTo(valBuf, 0);
        fixed (byte* keyPtr = keyBytes)
        fixed (byte* valPtr = valBuf)
        {
            lw->AddRsvString(keyPtr, valPtr, (nuint)valueBytes.Length);
        }
    }
}
