using System.Numerics;

namespace AnoMech.Scenarios.Fru.CrystallizeTime;

// Verified against the installed 2026.09.15 game sheets and native timelines.
internal static class CrystallizeTimeConstants
{
    // Authored actor-bound clock and original player Return trace (VFX 1013).
    // The m0640 variant is the enlarged boss marker (authored root scale 2).
    public const string ReturnClockVfx = "vfx/common/eff/d1049_stlp_b0k1.avfx";
    public const string ReturnMarkerVfx = "vfx/common/eff/d1049_returnmark_c0k1.avfx";
    public const float ReturnSnapshotTime = 39.6f, ReturnMarkerEndTime = 42.7f;
    public const uint Usurper = 17833, Oracle = 17835, Dragon = 17836, Hourglass = 17837, Fragment = 17841;
    // BNpc 17833 defaults to body 2 (4375). CT uses the dragon-armored body 3.
    public const uint UsurperDragonModel = 4376; // m0640/b0003, variant 1.
    public const uint VisionOfRyne = 17844, VisionOfGaia = 17845, RyneName = 9829, GaiaName = 9830;
    // Training health, consistent with other simulated actors; not retail HP.
    public const uint FragmentMaxHealth = 1_000_000;
    // Native draw offset above the grounded actors, not actor Position.Y.
    public const float FragmentVisionHeight = 2f;
    // Action 40229 releases hide/mon_sp010; edge arrivals use the native
    // show/mon_sp_a_start + loop on the dragon-armored m0640 body.
    public const ushort WingsWindupTimeline = 136, WingsWindupLoop = 137;
    public const ushort WingsHideTimeline = 4582, WingsShowTimeline = 7780, WingsShowLoop = 7781;
    public const ushort WingsSwingTimeline = 4568; // show/mon_sp008: body swing, VFX and sound together.
    // Give the native appearance a one-second lead-in before circular travel;
    // spawning with the tethers at 10s left visible heads idle for 5.3 seconds.
    public const float DragonSpawnTime = 14.3f;
    // mon_sp/[SKL_ID]/show/mon_sp002: native appearance, finite burst and sound.
    // Request once at spawn, without a persistent status-loop effect.
    public const ushort DragonSpawnTimeline = 4562;
    public const float DragonInitialScale = 2f, DragonAfterFirstHitScale = 1f;
    public const float DragonInitialContactRadius = 2f, DragonAfterFirstHitContactRadius = 1f;
    // Initial activation and retrigger protection use the scenario clock.
    public const float DragonMovementStart = 15.3f, DragonHitCooldown = 2f;
    public const uint UsurperName = 12809, OracleName = 9832, DragonName = 9323, HourglassName = 9823, FragmentName = 13559;
    public const uint Puddle = 0x1EBD41; // ExportedSG 38841 -> sgvf_w_btl_b3566.sgb
    // ContentDirectorManagedSG 181/46 -> layout 10885836 -> sgvf_n4gw_b3557.sgb.
    // The visible crystal is scenery; BNpc 17841 is its invisible combat anchor.
    public const byte ArenaSlot = 40, FragmentSlot = 46, HourglassSlot = 34;
    public static readonly Vector3 FragmentPosition = new(0, 0, -16); // native world (100, 0, 84)
    public const uint CrystallizeUsurper = 40240, CrystallizeOracle = 40298, Speed = 40293;
    public const uint Quicken = 40294, Slow = 40295, Maelstrom = 40299;
    public const uint Water = 40271, Ice = 40279, Aero = 40280, Eruption = 40274, Darkness = 40277;
    public const uint Tidal = 40251, TidalFirst = 40252, TidalRest = 40253;
    public const uint Quietus = 40281, DragonHit = 40241, DragonDisappear = 40244, CleanseFailure = 40242;
    public const uint Return = 40267, ReturnIV = 40268, SpiritTaker = 40288, SpiritHit = 40289;
    public const uint WingsFirst = 40229, WingsSecond = 40230, WingsHit = 40332;
    public const uint AkhMornUsurper = 40247, AkhMornOracle = 40302, AkhHitUsurper = 40248, AkhHitOracle = 40303;
    public const ushort ClawStatus = 3263, FangStatus = 3264, AeroStatus = 2463, IceStatus = 2462;
    public const ushort WaterStatus = 2461, EruptionStatus = 2460, DarknessStatus = 2454;
    public const ushort QuietusStatus = 4174, ReturnWaitingStatus = 2464, ReturnStatus = 2452, StunStatus = 4163;
    public const ushort MagicVulnerability = 2941, SoloTankInvulnerability = 409;
    public const ushort SlowTether = 133, FastTether = 134;
    public static readonly ushort[] Statuses = [ClawStatus, FangStatus, AeroStatus, IceStatus, WaterStatus,
        EruptionStatus, DarknessStatus, QuietusStatus, ReturnWaitingStatus, ReturnStatus, StunStatus,
        MagicVulnerability, SoloTankInvulnerability];
}
