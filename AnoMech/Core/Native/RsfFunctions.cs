using AnoMech.Pointers;

namespace AnoMech.Core.Native;

// Feeds the game's RSF (file-level Resolved String) table — the RSV twin that unlocks
// obfuscated model / VFX / sound *file paths* for duty content the shipped game files
// hide. AnoMech runs inn-only and never receives these live, so we replay the exact
// 0x48-byte records captured from a duty replay (0xF002 pseudo-packets). Twin of
// RsvFunctions; the record table lives in the scenario layer (e.g.
// Scenarios/Umad/UmadReplayData.cs). See memory reference_rsv_action_names and
// tools/replay_export_rsv_rsf.py.
internal static unsafe class RsfFunctions
{
    // record = the exact 0x48-byte buffer the native RsfReceive consumes (one 0xF002
    // payload). No-op if the signature didn't resolve on this build or the record is
    // short — RSF is best-effort and must never break a scenario run.
    public static void Add(byte[] record)
    {
        var fn = RsfPointers.RsfReceive;
        if (fn == null || record == null || record.Length < 0x48) return;
        fixed (byte* p = record)
            fn(p);
    }
}
