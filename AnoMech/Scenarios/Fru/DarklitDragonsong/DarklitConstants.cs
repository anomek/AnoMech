namespace AnoMech.Scenarios.Fru.DarklitDragonsong;

internal static class DarklitConstants
{
    public const uint OriginalUsurperModel = 4375;
    // m0640 Redress spin: matched to the reference spin_wings_out motion.
    // The show timeline owns the temporary model, VFX, sound and final reveal.
    public const ushort TransformOutTimeline = 4574, TransformInTimeline = 4562;
    public const float TransformTime = 8.8f, TransformCleanupTime = TransformTime + 5.2f;
    public const uint AkhRhaiCast = 40237, AkhRhaiHit = 40238, Raidwide = 40239, OracleRaidwide = 40301;
    public const uint PathCast = 40187, PathHit = 40190, WingLeft = 40227, WingRight = 40228;
    public const uint SomberCast = 40283, SomberFar = 40284, SomberNear = 40285;
    public const float TowerTime = 32.3f, SpiritSnapshot = 35.5f, SpiritTime = 36.7f, WaterTime = 40.5f;
    public const float ChainStart = 29.2f, ChainEnd = 43.5f, FarSnapshot = 44.1f, FarTime = 44.8f, NearTime = 47.6f, EndTime = 63;
    public const float ChainMin = 22 / 2.358f, ChainMax = 61 / 2.358f;
}
