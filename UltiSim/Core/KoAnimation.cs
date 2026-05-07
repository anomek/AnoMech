using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace UltiSim.Core;

// KO pose helper shared by every SimCharacter that wants to lay on the floor
// (party doppels, the local player). Two-step shape: Play() kicks off the
// fall-then-prone chain, and PinLoop() is called from Tick after the intro
// would have finished to keep the engine from sitting them back up.
//
// Pinning BaseOverride on the same frame as Play() snaps straight to the
// prone loop and skips the fall — the IntroDurationSeconds delay is what makes
// the animation read.
//
// ActionTimeline rows from en/ActionTimeline.csv: 72 = battle/dead (the
// falling-over animation), 73 = battle/dead_pose (the looping prone pose).
internal static unsafe class KoAnimation
{
    public const ushort IntroTimelineId = 72;
    public const ushort LoopTimelineId = 73;
    public const float IntroDurationSeconds = 2.5f;
    // ActionTimeline row 77 = normal/revive (the stand-up-from-prone animation).
    // Used to undo the KO pose on persistent characters (the local player) when
    // a scenario resets — clearing BaseOverride alone leaves the loop playing.
    public const ushort ReviveTimelineId = 77;

    public static void Play(BattleChara* bc)
    {
        if (bc == null) return;
        // _schedulerTimelines slots are null until the engine initialises the
        // animation system; PlayActionTimeline dereferences them and crashes.
        // Parent is set as part of that same init, so it's a reliable gate.
        if (bc->Timeline.TimelineSequencer.Parent == null) return;
        bc->Timeline.PlayActionTimeline(IntroTimelineId, LoopTimelineId);
    }

    public static void PinLoop(BattleChara* bc)
    {
        if (bc == null) return;
        if (bc->Timeline.BaseOverride != LoopTimelineId)
            bc->Timeline.BaseOverride = LoopTimelineId;
    }

    // Reverse of Play+PinLoop. The critical step is clearing ModelState —
    // that's the byte the engine reads each frame to decide the base pose
    // (drawn weapon, downed, etc.). With ModelState non-zero the engine keeps
    // reasserting the prone pose no matter what we write to BaseOverride or
    // the timeline slot. Clearing AnimationState (the two internal bytes
    // immediately after) goes with it since the spawn packet pairs them.
    // After the pose state is cleared, evict the leftover loop id from slot 0
    // and play the revive animation so the get-up reads visibly.
    public static void Revive(BattleChara* bc)
    {
        if (bc == null) return;
        bc->Timeline.BaseOverride = 0;
        bc->Timeline.ModelState = 0;
        // _animationState (FixedSizeArray2<byte> at 0x2C1) is internal — reach
        // it via pointer arithmetic off the public ModelState (0x2C0).
        byte* animState = &bc->Timeline.ModelState + 1;
        animState[0] = 0;
        animState[1] = 0;
        bc->Timeline.TimelineSequencer.SetSlotTimeline(0, 0);
        if (bc->Timeline.TimelineSequencer.Parent == null) return;
        bc->Timeline.PlayActionTimeline(ReviveTimelineId);
    }
}
