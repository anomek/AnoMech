using System;
using System.Linq;
using AnoMech.Core.EnemyActions;
using static AnoMech.Core.EnemyActions.Distribution;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

public static class TopActions
{
    public static readonly DamageSpec Magic = DamageType.Magic
        .VulnerableTo(StatusId.MagicVulnerabilityUp)
        .VulnerableTo(StatusId.VulnerabilityUp)
        .VulnerableTo(StatusId.MagicVulnerabilityUpMini, minStacks: 2);

    private static readonly DamageSpec Physical = DamageType.Physical;

    // UNVERIFIED for Thrice-come Ruin: no pull ever took a third stack.
    public static readonly RuinSpec ComeRuin = new((2, StatusId.TwiceComeRuin), (3, StatusId.TriceComeRuin))
    {
        Doom = (StatusId.Doom, Duration.Doom),
    };

    // Retail resolves an NPC action this long after its bar ends.
    private static TimingSpec Lands(float fireDelay) => new() { VfxOffset = fireDelay, ResolveOffset = fireDelay };

    // -- Sword/shield/leg/staff body forms --
    private static readonly CastSpec OmegaAttackCast = new() { CastTime = 1.2f, OmenDelay = Duration.OmegaAttackOmenDelay, AnimationLock = 3.1f };

    public static readonly EnemyAction EfficientBladework = new(ActionId.EfficientBladework)
    {
        Cast = OmegaAttackCast,
        Effects = [Damage(Magic, Lethal)],
        Timing = Lands(0.27f),
    };

    public static readonly EnemyAction BeyondStrength = new(ActionId.BeyondStrength)
    {
        Cast = OmegaAttackCast,
        Area = new() { Size = Geometry.BeyondStrengthSafeRadius },
        Effects = [Damage(Magic, Lethal)],
        Timing = Lands(0.27f),
    };

    public static readonly EnemyAction OptimizedBlizzardIII = new(ActionId.OptimizedBlizzardIII)
    {
        Cast = OmegaAttackCast,
        Effects = [Damage(Magic, Lethal)],
        Timing = Lands(0.27f),
    };

    // Visual only: the two helpers' SuperliminalSteelL/R deal the damage.
    public static readonly EnemyAction SuperliminalSteel = new(ActionId.SuperliminalSteel)
    {
        Cast = OmegaAttackCast with { OmenDelay = 0f },
        Timing = Lands(0.27f),
    };

    // Cast at LegsSideTargetL/R from LegsSideHelperPlacement: the rect starts at the target and runs
    // along the helper's facing, back across the arena.
    public static readonly EnemyAction SuperliminalSteelL = SuperliminalSteelSide(ActionId.SuperliminalSteelOmenL);
    public static readonly EnemyAction SuperliminalSteelR = SuperliminalSteelSide(ActionId.SuperliminalSteelOmenR);

    private static EnemyAction SuperliminalSteelSide(uint actionId) => new(actionId)
    {
        Cast = OmegaAttackCast with { AnimationLock = 1.1f },
        Effects = [Damage(Magic, Lethal)],
        Timing = Lands(0.27f),
    };

    public static readonly EnemyAction BeyondDefense = new(ActionId.BeyondDefenseAOE)
    {
        Effects =
        [
            OnOthers(Damage(Magic, Lethal)),
            OnTarget(Damage(Physical)),
            OnTarget(ApplyRuin(ComeRuin, 2, 6.96f)),
        ],
        Timing = new() { ResolveOffset = 0.4f },
    };

    public static readonly EnemyAction PilePitch = new(ActionId.PilePitch)
    {
        Effects = [Damage(Magic, split: Stack(3)), ApplyRuin(ComeRuin, 2, 6.96f)],
    };

    public static readonly EnemyAction Discharger = new(ActionId.Discharger)
    {
        Cast = new() { AnimationLock = 3.1f },
        Effects = [Knockback(KnockbackId.Discharger)],
        Timing = new() { ResolveOffset = 0.68f },
    };

    public static readonly EnemyAction OptimizedFireIII = new(ActionId.OptimizedFireIII)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Magic), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
        Timing = new() { DeathDelay = 0.62f },
    };

    // -- Wave Cannon / Oversampled / Diffuse --
    public static readonly EnemyAction OversampledWaveCannon = new(ActionId.OversampledWaveCannonAoe)
    {
        Effects =
        [
            Damage(Magic),
            ApplyRuin(ComeRuin, 2, 6.96f),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 4.96f),
        ],
    };

    public static readonly EnemyAction WaveCannon = new(ActionId.WaveCannonAoe)
    {
        Effects = [Damage(Magic), ApplyStatus(StatusId.MagicVulnerabilityUp, 4.96f)],
    };

    public static readonly EnemyAction DiffuseWaveCannon = new(ActionId.OmegaDiffuseWaveCannonAOE)
    {
        Area = new() { Size = MathF.PI / 3f },
        Effects = [Damage(Magic, Lethal)],
    };

    // -- Omega-specific --
    public static readonly EnemyAction RunMiOmegaVersion = new(ActionId.RunMiOmegaVersion)
    {
        Effects = [Damage(Magic)],
    };

    public static readonly EnemyAction Blaster = new(ActionId.OmegaBlasterAoe)
    {
        Effects =
        [
            Damage(Magic),
            ApplyRuin(ComeRuin, 2, 10.96f),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 4.96f),
            ApplyStatus(StatusId.HPPenalty, 3f),
        ],
    };

    // -- Towers --
    public static readonly EnemyAction StorageViolationSolo = StorageViolation(ActionId.StorageViolationSolo);
    public static readonly EnemyAction StorageViolationPair = StorageViolation(ActionId.StorageViolationPair);

    private static EnemyAction StorageViolation(uint actionId) => new(actionId)
    {
        Effects =
        [
            Damage(Magic),
            ApplyRuin(ComeRuin, 2, 10.96f),
            RemoveStatus(StatusId.Looper),
        ],
    };

    public static readonly EnemyAction StorageViolationObliteration = new(ActionId.StorageViolationObliteration)
    {
        Effects = [Damage(Magic, Lethal)],
        DeathExplanation = "tower unfilled",
    };

    // -- Hyper Pulse --
    public static readonly EnemyAction HyperPulseSigma = new(ActionId.HyperPulseSigma)
    {
        Effects = [Damage(Magic), ApplyStatus(StatusId.MagicVulnerabilityUp, 4.96f)],
    };

    public static readonly EnemyAction HyperPulseCharging = new(ActionId.HyperPulseDeltaCharging)
    {
        Effects = [Damage(Magic, Lethal)],
    };

    public static readonly EnemyAction HyperPulseShoot = new(ActionId.HyperPulseDeltaShoot)
    {
        Effects = [Damage(Magic, Lethal)],
    };

    // -- Delta-specific --
    public static readonly EnemyAction RunMiDeltaVersion = new(ActionId.RunMiDeltaVersion)
    {
        Effects = [Damage(Magic)],
    };

    public static readonly EnemyAction DeltaExplosion = new(ActionId.DeltaExplosion)
    {
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { ResolveOffset = 0.4f },
    };

    public static readonly EnemyAction DeltaUnmitigatedExplosion = new(ActionId.DeltaUnmitigatedExplosion)
    {
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { ResolveOffset = 0.4f },
    };

    public static readonly EnemyAction SwivelCannonLeft = SwivelCannon(ActionId.SwivelCannonL, MathF.PI / 2);
    public static readonly EnemyAction SwivelCannonRight = SwivelCannon(ActionId.SwivelCannonR, -MathF.PI / 2);

    private static EnemyAction SwivelCannon(uint actionId, float rotation) => new(actionId)
    {
        Cast = new() { OmenDelay = 8.5f },
        Area = new() { Size = Geometry.SwivelCannonHalfAngle, Rotation = rotation },
        Effects = [Damage(Magic, Lethal)],
    };

    public static readonly EnemyAction HwTetherBreak = new(ActionId.HwTetherBreak)
    {
        Effects =
        [
            Damage(Magic),
            ApplyRuin(ComeRuin, 3, Duration.HwTetherBreakStack),
            ApplyStatus(StatusId.MagicVulnerabilityUpMini, Duration.HwTetherBreakStack),
        ],
    };

    public static readonly EnemyAction HwTetherFail = new(ActionId.HwTetherFail)
    {
        Effects = [Damage(Magic, Lethal)],
    };

    // -- Sigma-specific --
    public static readonly EnemyAction RunMiSigmaVersion = new(ActionId.RunMiSigmaVersion)
    {
        Effects = [Damage(Magic)],
    };

    public static readonly EnemyAction RearLasersCharging = new(ActionId.RearLasersCharging)
    {
        Effects = [Damage(Magic, Lethal)],
    };

    public static readonly EnemyAction RearLasersShoot = new(ActionId.RearLasersShoot)
    {
        Effects = [Damage(Magic, Lethal)],
    };

    // -- Hello World --
    // Above the soaks, whose FollowUp captures it at initialization.
    public static readonly EnemyAction HelloWorldFail = new(ActionId.HelloWorldFail)
    {
        Effects = [Damage(Magic, Lethal)],
        DeathExplanation = "Failed Hello World mechanic",
    };

    public static readonly EnemyAction HelloNearWorld = HelloWorldSoak(ActionId.HelloNearWorld);
    public static readonly EnemyAction HelloNearWorldJump = HelloWorldSoak(ActionId.HelloNearWorldJump);
    public static readonly EnemyAction HelloDistantWorld = HelloWorldSoak(ActionId.HelloDistantWorld);
    public static readonly EnemyAction HelloDistantWorldJump = HelloWorldSoak(ActionId.HelloDistantWorldJump);

    private static EnemyAction HelloWorldSoak(uint actionId) => new(actionId)
    {
        Effects =
        [
            Damage(Magic),
            ApplyStatusOrOverload(StatusId.QuickeningDynamis, maxStacks: 3),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 4.96f),
            FollowUp(HelloWorldFail, when: ctx => ctx.Hits.Count != 1 || ctx.Hits.Any(ctx.IsKilled)),
        ],
    };

    // -- Optical Unit --
    public static readonly EnemyAction OpticalLaser = new(ActionId.OpticalLaser)
    {
        Cast = new() { CastTime = 1f, AnimationLock = 1.1f },
        Effects = [Damage(Magic, Lethal)],
        Timing = Lands(0.29f),
    };

    // -- P2 Party Synergy --
    public static readonly EnemyAction Spotlight = new(ActionId.Spotlight)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Magic, split: Stack(4)), ApplyStatus(StatusId.MagicVulnerabilityUp, 1.96f)],
        Timing = new() { DeathDelay = 0.71f },
    };

    // -- P6 Wave Cannon 2 --
    public static readonly EnemyAction CosmoArrowOmen = new(ActionId.CosmoArrowOmen)
    {
        Effects = [Damage(Magic, Lethal)],
    };

    public static readonly EnemyAction CosmoArrowLine = new(ActionId.CosmoArrowDamage)
    {
        Effects = [Damage(Magic, Lethal)],
    };

    public static readonly EnemyAction WaveCannonProtean = new(ActionId.WaveCannonProtean)
    {
        Effects = [Damage(Magic), ApplyStatus(StatusId.MagicVulnerabilityUp, 2.5f)],
    };

    public static readonly EnemyAction WaveCannonWildCharge = new(ActionId.WaveCannonWildCharge)
    {
        Effects = [Damage(Magic, split: WildCharge(front: 2, min: 8, frontHit: TankBuster))],
    };
}
