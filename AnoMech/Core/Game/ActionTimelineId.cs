namespace AnoMech.Core.Game;

// Generic ActionTimeline rows every monster shares. Which of the two variants a boss uses is
// per model (TOP's left arm unit takes the 2s, Garuda warps out with WarpStart2).
public static class ActionTimelineId
{
    public const ushort WarpStart = 7737;   // warp/warp_start
    public const ushort WarpStart2 = 7738;  // warp/warp_start2
    public const ushort WarpEnd = 7747;     // warp/warp_end
    public const ushort WarpEnd2 = 7748;    // warp/warp_end2
}
