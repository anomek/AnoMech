namespace AnoMech.Scenarios.Top.P6AlphaOmega;

public static class TopP6AlphaOmegaConstants
{
    public const uint BossMaxHealth = 20_530_948;
    public const uint CometMaxHealth = 228_360;
    public const uint MeteorMaxHealth = 482_324;

    // What a human tank's own mitigation must cover of each tank hit, once the real party's
    // mitigation and barriers on it are taken off: 1 - max HP / hit, at the real tank's 116,580 HP.
    // Flash Gale needs none.
    public static readonly float[] CosmoDiveRequiredMitigation = [0.36f, 0.43f];
    public static readonly float[] WildChargeRequiredMitigation = [0.51f, 0.43f];

    // How long after an effect the server's result lands; a hit on several players reaches them
    // one step apart, in its target order. A limit-break raidwide's order is no fixed one.
    public const float HitResultDelay = 0.62f;
    public const float RaidwideFirstResult = 1.96f;
    public const float ResultStep = 0.0445f;
    public const float MagicVulnerabilitySeconds = 1.96f;
    public const float MagicNumberSeconds = 6f;

    // A Cosmo Meteor flare reaches the whole arena, falling off with distance; this close it kills.
    // UNVERIFIED: no real player ever stood inside it.
    public const float FlareLethalRange = 20f;

    // A healer's LB3 lands before its bar fills, like a DPS one. A Brilliant Dynamis holder's spent
    // bars come back after RefundDelay.
    public const float HealerLimitBreakBar = 2f;
    public const float HealerLimitBreakLands = 1.47f;

    // The gauge's third bar before Cosmo Dive 2 completes anywhere from 4s before the stack lands
    // to 4s after it; until then it waits a tick short of full, where a press is an LB2.
    public const float ThirdBarEarliest = 154.57f;
    public const float ThirdBarLatest = 162.57f;
    // A bot waiting on an LB3 checks this often, and gives up after the limit.
    public const float LimitBreakWaitStep = 0.25f;
    public const float LimitBreakWaitLimit = 12f;
    // What these mechanics add to the gauge on top of its own fill.
    public static readonly (float T, float Units)[] LimitBreakGaugeFills =
    [
        (13.19f, 2400f), (15.47f, 9000f), (44.74f, 900f), (81.90f, 900f), (83.91f, 900f), (90.23f, 2100f),
        (115.71f, 900f), (117.71f, 900f), (124.04f, 2100f), (188.84f, 600f), (213.02f, 2400f),
    ];

    // Caster and physical ranged LB3s, whose damage reaches the Cosmo Meteor adds this long after
    // they land: a caster's kills the comets one by one and cuts the meteors to these HP; a
    // physical ranged one's line finishes the meteors. Each reaches only the adds its area
    // touches: a caster's circle around where it was placed, a physical ranged one's line from its
    // user toward the target.
    public static readonly uint[] CasterLimitBreakActionIds = [205, 4246, 7862, 34867];
    public static readonly uint[] PhysRangedLimitBreakActionIds = [4244, 4245, 17106];
    public const float CasterLimitBreakRadius = 15f;
    public const float PhysRangedLimitBreakLength = 30f;
    public const float PhysRangedLimitBreakHalfWidth = 4f;
    public const float CometHitboxRadius = 1f;
    public const float MeteorHitboxRadius = 3f;

    public const float CometsFallAfter = 4.89f;
    public const float CometFallStep = 0.134f;
    public static readonly (float After, uint Hp)[] MeteorsAfterCasterLimitBreak = [(4.62f, 207_406), (4.76f, 208_223)];
    public static readonly float[] MeteorsFallAfter = [3.16f, 3.43f];

    public static readonly (ushort Id, string Key)[] GimmickTimelines =
    [
        (10729, "mon_sp/gimmick/z3oz_boss_gimmick17"),
        (10730, "mon_sp/gimmick/z3oz_boss_gimmick18"),
        (10731, "mon_sp/gimmick/z3oz_boss_gimmick19"),
        (10732, "mon_sp/gimmick/z3oz_boss_gimmick20"),
        (10733, "mon_sp/gimmick/z3oz_boss_gimmick21"),
        (10734, "mon_sp/gimmick/z3oz_boss_gimmick22"),
        (10735, "mon_sp/gimmick/z3oz_boss_gimmick23"),
        (10764, "mon_sp/gimmick/z3oz_boss_gimmick26"),
        (4848, "mon_sp/gimmick/z2r2_boss3_gimmick02"),
        (6111, "mon_sp/gimmick/z3oa_boss_gimmick14"),
        (6109, "mon_sp/gimmick/z3oa_boss_gimmick13"),
        (6734, "mon_sp/gimmick/z3of_boss_gimmick04"),
    ];
}
