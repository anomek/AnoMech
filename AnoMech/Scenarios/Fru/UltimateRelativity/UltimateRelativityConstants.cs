namespace AnoMech.Scenarios.Fru.UltimateRelativity;

internal static class UltimateRelativityConstants
{
    public const uint Oracle = 17831, OracleName = 9832, Hourglass = 17832, HourglassName = 9823;
    public const uint Raidwide = 40266, Speed = 40293, Quicken = 40294, Slow = 40295;
    public const uint Fire = 40276, Darkness = 40277, Ice = 40279, MeltdownCast = 40291, MeltdownFirst = 40235, MeltdownRest = 40292;
    public const uint Return = 40267, ReturnIV = 40268, Shadoweye = 40278, Eruption = 40274, Water = 40271, ShellCast = 40286, ShellHit = 40287;
    public const ushort FireStatus = 2455, DarknessStatus = 2454, IceStatus = 2462, EyeStatus = 2456,
        EruptionStatus = 2460, WaterStatus = 2461, WaitingStatus = 2464, ReturnStatus = 2452, StunStatus = 4163, RotationStatus = 2970;
    // Rotation Param selects StatusLoopVFX 269 (left) / 348 (right); OnGainStatus owns the native arrows.
    public const ushort FastTether = 134, SlowTether = 133;
    public const string WaitingClock = "vfx/common/eff/d1049_stlp_b0k1.avfx";
    public const string ReturnMarker = "vfx/common/eff/d1049_returnmark_c0k1.avfx";
    public static readonly ushort[] Statuses = [FireStatus, DarknessStatus, IceStatus, EyeStatus, EruptionStatus, WaterStatus, WaitingStatus, ReturnStatus, StunStatus];
    public static float FireTime(int wave) => wave switch { 0 => 22.9f, 1 => 32.8f, _ => 42.8f };
    public static float LaserStart(int wave) => wave switch { 0 => 23.9f, 1 => 34, _ => 45 };
    public static float LaserHit(int wave, int shot) => (wave switch { 0 => 27.7f, 1 => 37.7f, _ => 48.7f }) + (shot == 0 ? 0 : shot + 1.1f);
    public const float StunTime = 51.9f, RewindTime = 53.3f, FinalTime = 54.8f, ShellTime = 58.6f, EndTime = 61;
}
