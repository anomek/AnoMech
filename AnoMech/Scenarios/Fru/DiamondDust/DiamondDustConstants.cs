namespace AnoMech.Scenarios.Fru.DiamondDust;

internal static class DiamondDustConstants
{
    public const uint Usurper = 17823, Reflection = 17824, UsurperName = 12809, ReflectionName = 13554;
    public const uint IceModel = 4374, LightModel = 4375;
    public const uint MirrorImage = 40180, Raidwide = 40197, Icicle = 40198, Stone = 40199;
    public const uint NeedleCircle = 40200, NeedleCross = 40201, Axe = 40202, Scythe = 40203;
    public const uint Protean = 40206, HeavenlyStrike = 40207, HolyCast = 40208, Holy = 40209;
    public const uint Gaze = 40185, Frost = 40184;
    public const uint StillnessFirst = 40193, StillnessSecond = 40196, SilenceFirst = 40194, SilenceSecond = 40195;
    public const uint StillnessVoice = 8205521, SilenceVoice = 8205522;
    public const ushort ThinIce = 911;
    // Native Thin Ice Param encodes slide distance in tenths of a yalm.
    public const ushort ThinIceDistanceParam = 320;
    public const uint FreezeFloor = 0x00010020, ThawFloor = 0x00010040;
    public const float RunSpeed = 6, SlideDistance = 32, SlideSpeed = 32;
    public const float KickTime = 20.9f, StoneTime = 23.3f, KnockbackTime = 27.6f, StarTime = 30.4f;
    public const float GazeTime = 40.1f, ComboTime = 43.1f, FirstHit = 46.6f, SecondHit = 48.7f, IceEnd = 49.1f;
    public const uint HolyPuddle = 0x1EBC4F; // ExportedSG 38437: sgvf_n4gw_b3555.sgb.
    public const string StoneMarker = "vfx/lockon/eff/tag_ae5m_8s_0v.avfx";
    public static float HolyTime(int wave) => 31.7f + wave * (4.7f / 3);
}
