using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea;

public static class TeaActions
{
    // Water Resistance Down II makes the next water hit lethal; a tank lives through it on an invuln, which only
    // tank mitigation checks see.
    private static readonly VulnSpec TankWater = VulnerableTo(StatusId.WaterResistanceDownII, 1f);
    private static readonly VulnSpec Water = VulnerableTo(StatusId.WaterResistanceDownII);
    private static readonly IEnemyActionEffect WaterResistanceDown = ApplyStatus(StatusId.WaterResistanceDownII, 4f);

    private static readonly VulnSpec MagicallyVulnerable = VulnerableTo(StatusId.MagicVulnerabilityUp);
    private static readonly VulnSpec PhysicallyVulnerable = VulnerableTo(StatusId.PhysicalVulnerabilityUp);

    private static readonly AreaSpec ProteanCone = new() { CastTypeOverride = CastType.Cone, Size = MathF.PI / 12f };

    // Lethal, though a tank lives through it on an invuln.
    private static IEnemyActionEffect LethalShortOfAnInvuln(VulnSpec spec)
        => new TankOrOtherEffect(new ShortOfTheirInvulnEffect(spec), Damage(spec, Lethal));

    private static IEnemyActionEffect WaterDamage(Severity? severity = null)
        => new TankOrOtherEffect(Damage(TankWater, severity), Damage(Water, severity));

    // -- P1 --
    public static readonly EnemyAction LivingLiquidAttack = AutoAttack(ActionId.LivingLiquidAttack, 0.52f);
    public static readonly EnemyAction LiquidHandAttack = AutoAttack(ActionId.LiquidHandAttack, 0.53f);
    public static readonly EnemyAction JagdDollAttack = AutoAttack(ActionId.JagdDollAttack, 0.52f);

    // Both cleaves are 90° wide: their tank takes them, anyone else in one dies.
    public static readonly EnemyAction FluidSwing = new(ActionId.FluidSwing)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects = [OnTarget(Damage(TankBuster), WaterResistanceDown), OnOthers(Damage(Lethal))],
        Timing = new() { DamageDelay = 0.80f },
    };

    public static readonly EnemyAction FluidStrike = new(ActionId.FluidStrike)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects = [OnTarget(Damage(TankBuster), WaterResistanceDown), OnOthers(Damage(Lethal))],
        Timing = new() { DamageDelay = 0.78f },
    };

    public static readonly EnemyAction Cascade = Raidwide(ActionId.Cascade, 4.1f, 1.60f) with { Effects = [WaterDamage()] };

    // The real damage grows with the gap between Living Liquid's and Liquid Hand's HP; both stay at 100% here.
    public static readonly EnemyAction HandOfPain = Raidwide(ActionId.HandOfPain, 2.1f, 1.27f);

    public static readonly EnemyAction HandOfParting = Raidwide(ActionId.HandOfParting, 2.1f, 0.99f);
    public static readonly EnemyAction HandOfPrayer = Raidwide(ActionId.HandOfPrayer, 2.1f, 0.94f);

    // Wrong spacing hits everyone for about 80k, past anyone's HP.
    public static readonly EnemyAction HandOfPartingApart = HandOfParting with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "Living Liquid and Liquid Hand were apart",
    };

    public static readonly EnemyAction HandOfPrayerTogether = HandOfPrayer with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "Living Liquid and Liquid Hand were together",
    };

    // Lethal short of an invuln, under which the MT may take the first one. UNVERIFIED: its knockback, never seen
    // land, is left out.
    public static readonly EnemyAction ProteanWaveTornadoVisible = new(ActionId.ProteanWaveTornadoVisible)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = ProteanCone,
        Effects = [LethalShortOfAnInvuln(Water), WaterResistanceDown],
        Timing = new() { DamageDelay = 1.10f },
    };

    // Kills everyone it hits, so the real cast's knockback is left out.
    public static readonly EnemyAction ProteanWaveBossVisible = ProteanWaveTornadoVisible with
    {
        ActionId = ActionId.ProteanWaveBossVisible,
        Effects = [Damage(Water, Lethal)],
    };

    public static readonly EnemyAction ProteanWaveHelperVisible = ProteanWaveBossVisible with
    {
        ActionId = ActionId.ProteanWaveHelperVisible,
        Cast = new() { AnimationLock = 1.9f },
    };

    public static readonly EnemyAction ProteanWaveTornadoInvisible = Protean(ActionId.ProteanWaveTornadoInvisible, 2.1f, 0.78f);
    // Living Liquid's shadow of its own cast cone kills like the cast one, without the knockback.
    public static readonly EnemyAction ProteanWaveBossInvisible = Protean(ActionId.ProteanWaveBossInvisible, 3.1f, 0.82f) with
    {
        Effects = [Damage(Water, Lethal), WaterResistanceDown],
    };

    // Each goes at one of the four closest: they take it, anyone else in it dies.
    public static readonly EnemyAction ProteanWaveHelperInvisible = Protean(ActionId.ProteanWaveHelperInvisible, 1.9f, 0.80f) with
    {
        Effects = [OnTarget(WaterDamage(), WaterResistanceDown), OnOthers(Damage(Lethal))],
    };

    // The holder's own 9,999,999 to everyone within 8y of them, holder first, going off with the Exhaust that
    // overloads it.
    public static readonly EnemyAction ExhaustOverload = new(ActionId.ExhaustOverload)
    {
        Cast = new() { AnimationLock = 0.6f },
        Area = new() { AdjustTargets = (ctx, hits) => [ctx.Caster, .. hits] },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.49f },
        DeathExplanation = "a second Luminous Aetheroplasm went off",
    };

    // A second Luminous Aetheroplasm sets its holder off.
    public static readonly EnemyAction Exhaust = new(ActionId.Exhaust)
    {
        Cast = new() { AnimationLock = 0.6f },
        Effects = [Damage(), new LuminousOverloadEffect(ExhaustOverload)],
        Timing = new() { DamageDelay = 0.62f },
    };

    // The fed doll goes down with it.
    public static readonly EnemyAction ReducibleComplexity = Raidwide(ActionId.ReducibleComplexity, 2.1f, 0.93f) with
    {
        Effects = [Damage(), DefeatCaster()],
    };

    // A doll fed at 25% HP or more hits everyone past anyone's HP, harder the more HP it has.
    public static readonly EnemyAction ReducibleComplexityTooHigh = ReducibleComplexity with
    {
        Effects = [Damage(Lethal), DefeatCaster()],
        DeathExplanation = "A Jagd Doll was fed at 25% HP or more",
    };

    public static readonly EnemyAction Pressurize = Visual(ActionId.Pressurize, 2.1f);

    // Touching an Embolus wipes the raid.
    public static readonly EnemyAction Outburst = new(ActionId.Outburst)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Water, Lethal)],
        Timing = new() { DamageDelay = 0.58f },
    };

    // Lethal: a hit nobody should take.
    public static readonly EnemyAction Sluice = new(ActionId.Sluice)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Water, Lethal)],
        Timing = new() { DamageDelay = 0.84f }, // TODO: verify with replay
    };

    public static readonly EnemyAction Splash = Raidwide(ActionId.Splash, 1.1f, 0.54f) with { Effects = [WaterDamage()] };

    // Its holder takes it; anyone else within 6y dies short of an invuln.
    public static readonly EnemyAction Drainage = new(ActionId.Drainage)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [OnTarget(Damage(TankWater, TankBuster)), OnOthers(LethalShortOfAnInvuln(VulnSpec.None))],
        Timing = new() { DamageDelay = 0.51f },
    };

    // The second on one holder of both tethers: the pair comes to about 1.7x a tank's HP, lethal to
    // everyone it hits short of an invuln.
    public static readonly EnemyAction DrainageOnBothTethersHolder = Drainage with
    {
        Effects = [LethalShortOfAnInvuln(VulnSpec.None)],
        DeathExplanation = "held both Drainage tethers",
    };

    public static readonly EnemyAction LiquidGaol = Visual(ActionId.LiquidGaol, 2.1f);

    // -- Limit Cut --
    public static readonly EnemyAction HawkBlaster = new(ActionId.HawkBlaster)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.63f },
    };

    // A second Limit Cut hit lands on the first's Physical Vulnerability Up.
    private static readonly VulnSpec LimitCutHit = VulnerableTo(StatusId.PhysicalVulnerabilityUp);

    // 90° wide. The bait takes it, and anyone else in it dies.
    public static EnemyAction AlphaSword(SimCharacter bait) => new(ActionId.AlphaSword)
    {
        Cast = new() { AnimationLock = 1.5f },
        Area = new() { Size = MathF.PI / 4f },
        Effects =
        [
            OnOnly(bait, Damage(LimitCutHit), ApplyStatus(StatusId.MagicVulnerabilityUp, 10f), ApplyStatus(StatusId.PhysicalVulnerabilityUp, 17f),
                Knockback(KnockbackId.AlphaSword, pushes: NotImmune)),
            OnAllBut(bait, Damage(Lethal)),
        ],
        Timing = new() { DamageDelay = 0.31f },
    };

    // The bait takes it, and anyone else in it dies. Facing Cruise Chaser spares the bait the
    // knockback. UNVERIFIED: the arc's edge, between 43° and 68° off; the gaze's 45° stands in.
    public static EnemyAction SuperBlasstyCharge(SimCharacter bait) => new(ActionId.SuperBlasstyCharge)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects =
        [
            OnOnly(bait, Damage(LimitCutHit), ApplyStatus(StatusId.MagicVulnerabilityUp, 10f), ApplyStatus(StatusId.PhysicalVulnerabilityUp, 17f),
                Knockback(KnockbackId.SuperBlasstyCharge, pushes: NeitherImmuneNorFacingCaster)),
            OnAllBut(bait, Damage(Lethal)),
        ],
        Timing = new() { DamageDelay = 0.53f },
    };

    public static readonly EnemyAction JKick = new(ActionId.JKick)
    {
        Cast = new() { AnimationLock = 3.1f },
        Effects = [Damage()],
        Timing = new() { DamageDelay = 1.28f },
    };

    // -- P2 --
    public static readonly EnemyAction CruiseChaserAttack = AutoAttack(ActionId.CruiseChaserAttack, 0.53f);
    public static readonly EnemyAction BruteJusticeAttack = AutoAttack(ActionId.BruteJusticeAttack, 0.80f);

    public static readonly EnemyAction Whirlwind = Raidwide(ActionId.Whirlwind, 3.1f, 1.24f);

    // With the Plasma Shield still standing Cruise Chaser hits with Damage Up, past anyone's HP.
    public static readonly EnemyAction WhirlwindEmpowered = Whirlwind with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "the Plasma Shield was still up",
    };

    public static readonly EnemyAction JudgmentNisi = Visual(ActionId.JudgmentNisi, 2.1f);
    public static readonly EnemyAction LinkUp = Visual(ActionId.LinkUp, 2.1f);
    public static readonly EnemyAction OpticalSight = Visual(ActionId.OpticalSight, 4.1f);
    public static readonly EnemyAction Photon = Visual(ActionId.Photon, 3.1f);
    public static readonly EnemyAction MissileCommand = Visual(ActionId.MissileCommand, 3.1f);
    public static readonly EnemyAction Verdict = Visual(ActionId.Verdict, 2.1f);
    public static readonly EnemyAction LimitCutP2 = Visual(ActionId.LimitCutP2, 3.1f);
    public static readonly EnemyAction Flarethrower = Visual(ActionId.Flarethrower, 0.2f);

    // UNVERIFIED in P2: the animation locks.
    public static readonly EnemyAction SuperJump = Visual(ActionId.SuperJump, 0.2f);
    public static readonly EnemyAction ApocalypticRay = Visual(ActionId.ApocalypticRay, 7.1f);

    // Sets everyone's HP to 1, which the sim has no HP to show.
    public static readonly EnemyAction PhotonHit = new(ActionId.PhotonHit)
    {
        Cast = new() { AnimationLock = 0.6f },
        Effects = [Damage(Harmless)],
        Timing = new() { DamageDelay = 0.62f },
    };

    public static readonly EnemyAction HawkBlasterP2 = LethalCircle(ActionId.HawkBlasterP2);

    public static readonly EnemyAction EyeOfTheChakram = new(ActionId.EyeOfTheChakram)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 1.15f },
    };

    // 90 degrees wide. Nobody stands in it in real clears, its bait included.
    public static readonly EnemyAction SpinCrusher = new(ActionId.SpinCrusher)
    {
        Cast = new() { AnimationLock = 3.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.5f },
    };

    private static readonly VulnSpec WaterTwice = VulnerableTo(StatusId.WaterResistanceDownIIP2);
    private static readonly VulnSpec LightningTwice = VulnerableTo(StatusId.LightningResistanceDownII);

    public static readonly EnemyAction CrashingWave = new(ActionId.CrashingWave)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(WaterTwice, split: Distribution.Stack(3))],
        Timing = new() { DamageDelay = 0.6f },
    };

    public static readonly EnemyAction CrashingThunder = new(ActionId.CrashingThunder)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(LightningTwice, split: Distribution.Exactly(2))],
        Timing = new() { DamageDelay = 0.6f },
    };

    // Its target and anyone within 6y of them. UNVERIFIED: the animation lock.
    public static readonly EnemyAction DrainageP2 = new(ActionId.DrainageP2)
    {
        Cast = new() { AnimationLock = 0.6f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.49f },
        DeathExplanation = "stood too near a Liquid Rage as it woke",
    };

    // A holder gone before the stack wipes the party.
    public static readonly EnemyAction PunishingWave = Raidwide(ActionId.PunishingWave, 1.1f, 0.6f) with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "Compressed Water's holder died before its stack",
    };

    public static readonly EnemyAction PunishingThunder = PunishingWave with
    {
        ActionId = ActionId.PunishingThunder,
        DeathExplanation = "Compressed Lightning's holder died before its stack",
    };

    public static readonly EnemyAction EarthMissile = LethalCircle(ActionId.EarthMissile);
    public static readonly EnemyAction HiddenMinefield = LethalCircle(ActionId.HiddenMinefield);

    // The marked tank alone.
    public static readonly EnemyAction EarthMissileIce = new(ActionId.EarthMissileIce)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [OnTarget(Damage()), OnOthers(Damage(Lethal))],
        Timing = new() { DamageDelay = 0.62f },
    };

    // Exactly three in it and it goes off on nobody; otherwise everyone in it dies.
    public static readonly EnemyAction Enumeration = new(ActionId.Enumeration)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { AdjustTargets = (ctx, hits) => hits.Count == 3 ? [] : hits },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.5f },
        DeathExplanation = "not exactly three in an Enumeration",
    };

    // A tank walking onto a mine takes it on mitigation: the pair comes to about 1.55x a tank's HP
    // before it. Anyone else in reach dies.
    public static readonly EnemyAction HiddenMine = new(ActionId.HiddenMine)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(TankBuster.MinMit(0.36f))],
        Timing = new() { DamageDelay = 0.31f },
    };

    // A real party mostly dies to a mine left alone, tanks aside; the sim takes everyone.
    public static readonly EnemyAction HiddenMineShrapnel = Raidwide(ActionId.HiddenMineShrapnel, 1.1f, 0.6f) with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "a Hidden Mine nobody popped",
    };

    // UNVERIFIED: 90 degrees wide. Its bait takes Fire Resistance Down II, and anyone else in it dies.
    public static readonly EnemyAction FlarethrowerCone = new(ActionId.FlarethrowerCone)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects =
        [
            OnTarget(Damage(VulnerableTo(StatusId.FireResistanceDownII)), ApplyStatus(StatusId.FireResistanceDownII, 6f)),
            OnOthers(Damage(Lethal)),
        ],
        Timing = new() { DamageDelay = 0.85f },
    };

    public static readonly EnemyAction PropellerWind = new(ActionId.PropellerWind)
    {
        Cast = new() { AnimationLock = 3.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.6f },
        DeathExplanation = "Cruise Chaser could see them past the Gelid Gaol",
    };

    public static readonly EnemyAction Gavel = Raidwide(ActionId.Gavel, 2.1f, 1.32f);

    // A player's own: on touching a different nisi, or for everyone once Gavel finds one wrong.
    public static readonly EnemyAction FinalSentence = new(ActionId.FinalSentence)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.8f },
    };

    // Shared by the two tanks.
    public static readonly EnemyAction DoubleRocketPunch = new(ActionId.DoubleRocketPunch)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(TankBuster, split: Distribution.Stack(2))],
        Timing = new() { DamageDelay = 1.11f },
    };

    // Its farthest player takes it; anyone it lands on beside them dies. UNVERIFIED in P2: the animation lock.
    public static readonly EnemyAction SuperJumpLanding = new(ActionId.SuperJumpLanding)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [OnTarget(Damage()), OnOthers(Damage(Lethal))],
        Timing = new() { DamageDelay = 0.98f },
    };

    // UNVERIFIED: 90 degrees wide, and P2's animation lock.
    public static readonly EnemyAction ApocalypticRayHit = new(ActionId.ApocalypticRayHit)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.18f },
    };

    // UNVERIFIED: the animation lock.
    public static readonly EnemyAction SevereContamination = Raidwide(ActionId.SevereContamination, 2.1f, 0.49f) with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "a Liquid Rage was left standing",
    };

    // Neither lands: the slowest real kill beats them.
    public static readonly EnemyAction FinalSentenceEnrage = Visual(ActionId.FinalSentenceEnrage, 2.1f);
    public static readonly EnemyAction EternalDarknessP2 = Visual(ActionId.EternalDarknessP2, 2.1f);

    // -- Temporal Stasis --
    public static readonly EnemyAction TemporalStasis = Visual(ActionId.TemporalStasis, 5.1f);

    // UNVERIFIED: 90 degrees wide. Cruise Chaser fires it on himself along his facing, having turned to
    // `bait`: the bait takes it unless a Flarethrower already burnt them, and anyone else in it dies.
    public static EnemyAction AlphaSwordP3(SimCharacter bait) => new(ActionId.AlphaSwordP3)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects =
        [
            OnOnly(bait, Damage(PhysicallyVulnerable.VulnerableTo(StatusId.FireResistanceDownII)), ApplyStatus(StatusId.MagicVulnerabilityUp, 5f), ApplyStatus(StatusId.PhysicalVulnerabilityUp, 5f)),
            OnAllBut(bait, Damage(Lethal)),
        ],
        Timing = new() { DamageDelay = 0.3f },
    };

    // About 90 degrees wide: everyone in it takes it, and a second one on its Fire Resistance Down II kills.
    public static readonly EnemyAction FlarethrowerP3 = new(ActionId.FlarethrowerP3)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects = [Damage(MagicallyVulnerable.VulnerableTo(StatusId.FireResistanceDownII)), ApplyStatus(StatusId.FireResistanceDownII, 6f)],
        Timing = new() { DamageDelay = 0.8f },
    };

    // Brute Justice fires it on himself along his facing, having turned to `bait`: the bait takes
    // it, and anyone else in it dies.
    public static EnemyAction FlarethrowerStasis(SimCharacter bait) => FlarethrowerP3 with
    {
        Effects =
        [
            OnOnly(bait, Damage(MagicallyVulnerable.VulnerableTo(StatusId.FireResistanceDownII)), ApplyStatus(StatusId.FireResistanceDownII, 6f)),
            OnAllBut(bait, Damage(Lethal)),
        ],
    };

    public static readonly EnemyAction PlaintOfSeverity = new(ActionId.PlaintOfSeverity)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicallyVulnerable), ApplyStatus(StatusId.MagicVulnerabilityUp, 4f), ApplyStatus(StatusId.PhysicalVulnerabilityUp, 11f)],
        Timing = new() { DamageDelay = 0.66f },
    };

    // On a tethered pair alone: `shown` damage each, or both die when `broken` says how the pair broke
    // its tether's rule.
    public static EnemyAction PlaintOfSurety(SimCharacter first, SimCharacter second, string? broken, uint shown = 1) => new(ActionId.PlaintOfSurety)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { AdjustTargets = (_, hits) => hits.Where(h => h == first || h == second).ToList() },
        Effects = [Damage(broken is null ? Harmless.Showing(shown) : Lethal)],
        Timing = new() { DamageDelay = 0.66f },
        DeathExplanation = broken,
    };

    // -- P3 --
    public static readonly EnemyAction AlexanderPrimeAttack = AutoAttack(ActionId.AlexanderPrimeAttack, 0.5f);

    public static readonly EnemyAction ChasteningHeat = new(ActionId.ChasteningHeat)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(TankBuster), ApplyStatus(StatusId.MagicVulnerabilityUp, 10f)],
        Timing = new() { DamageDelay = 1.10f },
    };

    // UNVERIFIED: 90 degrees wide. On Chastening Heat's Magic Vulnerability Up it takes an invuln.
    public static readonly EnemyAction DivineSpear = new(ActionId.DivineSpear)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects = [Damage(VulnerableTo(StatusId.MagicVulnerabilityUp, 1f), TankBuster)],
        Timing = new() { DamageDelay = 0.80f },
    };

    public static readonly EnemyAction InceptionFormation = Visual(ActionId.InceptionFormation, 3.1f);
    public static readonly EnemyAction InceptionVisual = Visual(ActionId.InceptionVisual, 3.1f);
    public static readonly EnemyAction JudgmentCrystal = Visual(ActionId.JudgmentCrystal, 3.1f);
    public static readonly EnemyAction UndyingAffection = Visual(ActionId.UndyingAffection, 3.1f);
    public static readonly EnemyAction TetrashatterVisual = Visual(ActionId.TetrashatterVisual, 1.1f);
    // Its animation hides Alexander Prime until he reappears for Sacrament.
    public static readonly EnemyAction Inception = Visual(ActionId.Inception, 4.1f);
    public static readonly EnemyAction WormholeFormation = Visual(ActionId.WormholeFormation, 3.1f);
    public static readonly EnemyAction LimitCutP3 = Visual(ActionId.LimitCutP3, 3.1f);
    public static readonly EnemyAction VoidOfRepentance = Visual(ActionId.VoidOfRepentance, 2.1f);
    public static readonly EnemyAction SummonAlexander = Visual(ActionId.SummonAlexander, 4.1f);
    public static readonly EnemyAction DivineJudgment = Visual(ActionId.DivineJudgment, 2.1f);
    public static readonly EnemyAction DivineJudgmentSuccess = Visual(ActionId.DivineJudgmentSuccess, 2.1f);
    public static readonly EnemyAction DivineJudgmentCountdown = Visual(ActionId.DivineJudgmentCountdown, 13.4f);

    // On Physical Vulnerability Up the landing is a tank's to take: 22-29k on a tank, 43k on anyone else.
    public static readonly EnemyAction SuperJumpLandingVulnerable = SuperJumpLanding with
    {
        Effects = [OnTarget(Damage(TankBuster)), OnOthers(Damage(Lethal))],
    };

    // Its own player takes it. UNVERIFIED: anyone else in it dies.
    public static readonly EnemyAction JudgmentCrystalAoe = new(ActionId.JudgmentCrystalAoe)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [OnTarget(Damage()), OnOthers(Damage(Lethal))],
        Timing = new() { DamageDelay = 0.6f },
        DeathExplanation = "stood in another player's Judgment Crystal",
    };

    // Its own player takes it, weakened by the orb's Damage Down. UNVERIFIED: anyone else in it dies.
    public static readonly EnemyAction Aetheroplasm = new(ActionId.Aetheroplasm)
    {
        Cast = new() { AnimationLock = 1.3f },
        Effects = [OnTarget(Damage()), OnOthers(Damage(Lethal))],
        Timing = new() { DamageDelay = 0.31f },
        DeathExplanation = "caught in another player's Plasmasphere",
    };

    // Before its first shrink it hits its own player for about 40k: past any non-tank's HP.
    public static readonly EnemyAction AetheroplasmUnshrunk = Aetheroplasm with
    {
        Effects = [OnTarget(Damage(TankBuster)), OnOthers(Damage(Lethal))],
        DeathExplanation = "caught in a Plasmasphere that hadn't shrunk",
    };

    // Two orbs' blasts overlapping: 5-18k on everyone, who live through it, and death for the two who set them off.
    public static EnemyAction ElectromagneticBurst(SimCharacter first, SimCharacter second) => Raidwide(ActionId.ElectromagneticBurst, 1.1f, 0.6f) with
    {
        Effects = [OnOnly(first, Damage(Lethal)), OnOnly(second, Damage(Lethal)), Damage()],
        DeathExplanation = "their Plasmasphere went off over another",
    };

    // An orb nobody set off goes off over everyone: 11-35k in real pulls. Its own player, who let it,
    // dies here.
    public static readonly EnemyAction Burst = Raidwide(ActionId.Burst, 1.1f, 0.4f) with
    {
        Effects = [OnTarget(Damage(Lethal)), OnOthers(Damage(MagicallyVulnerable))],
        DeathExplanation = "an orb nobody set off burst",
    };

    // About 150k on everyone: a tank's LB3 takes enough off, nothing short of it does.
    public static readonly EnemyAction DivineJudgmentRaidwide = new(ActionId.DivineJudgmentRaidwide)
    {
        Cast = new() { AnimationLock = 3.1f },
        Effects = [Damage(Normal.MinMit(0.8f)), ApplyStatus(StatusId.DownForTheCount, 57f, delayToEffectResult: true)],
        Timing = new() { DamageDelay = 0.18f },
        DeathExplanation = "no tank LB3 for Divine Judgment",
    };

    // Every shatter hits everyone, harder the closer: about 52.9k less 2.37k a yalm, never under 3.3k.
    // What a player takes from the four adds up and kills past their HP; a tank lives through it on the
    // mitigation that keeps the sum under it. UNVERIFIED inside 10y, where one crystal may hit harder still.
    public static EnemyAction Tetrashatter(Dictionary<SimCharacter, float> taken) => new(ActionId.Tetrashatter)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [new TetrashatterEffect(taken, 52900f, 2370f)],
        Timing = new() { DamageDelay = 0.80f },
        DeathExplanation = "the Judgment Crystals' shatters added up past their HP",
    };

    // A crystal set off on its own blows up telegraphed, out of the True Heart's reach: everyone takes
    // about 86.7k less 3.96k a yalm, never under 3.3k, added to what `taken` holds. `cause` names what
    // set it off; `spent` runs at its snapshot. UNVERIFIED inside 10y, where real hits at 6-8y ran
    // anywhere from 41k to 61k.
    public static EnemyAction TetrashatterFail(Dictionary<SimCharacter, float> taken, string cause, Action spent) => new(ActionId.TetrashatterFail)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [new TetrashatterEffect(taken, 86700f, 3960f, spent)],
        Timing = new() { DamageDelay = 1.21f },
        DeathExplanation = cause,
    };

    // Each hit adds to what its target took from the shatters before it.
    private sealed class TetrashatterEffect(Dictionary<SimCharacter, float> taken, float atCentre, float perYalm, Action? spent = null) : IEnemyActionEffect
    {
        public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
        {
            spent?.Invoke();
            foreach (var target in hits)
            {
                var dx = target.Position.X - ctx.Origin.Position.X;
                var dz = target.Position.Z - ctx.Origin.Position.Z;
                var sum = taken[target] = taken.GetValueOrDefault(target) + MathF.Max(3300f, atCentre - perYalm * MathF.Sqrt(dx * dx + dz * dz));
                var hp = HitPoints(target);
                var severity = sum < hp ? Normal : DamageCheck.IsTank(target) ? TankBuster.MinMit(1f - hp / sum) : Lethal;
                Damage(severity).Apply(ctx, [target]);
            }
        }

        // Real max HP at the fight's item level.
        private static float HitPoints(SimCharacter target) => (target as ISimPartyMember)?.Role switch
        {
            PartyRole.MainTank or PartyRole.OffTank => 46500f,
            PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => 32300f,
            PartyRole.PhysRangedDps => 32200f,
            _ => 29300f,
        };
    }

    public static readonly EnemyAction TrueHeartSuccess = new(ActionId.TrueHeartSuccess)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [ApplyStatus(StatusId.EnigmaCodex, 0f)],
    };

    public static readonly EnemyAction SacramentInception = new(ActionId.SacramentInception)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.6f },
    };

    // Without the gold heart's flight nothing shows where it comes from, so the party can but guess
    // where to stand clear. Whoever `mistakes` names, whose crystal kept the heart from turning gold,
    // dies to it wherever they stand, which the real one doesn't do. `why` says how the heart failed.
    public static EnemyAction SacramentUnforeseen(string? why, IReadOnlyDictionary<SimCharacter, string> mistakes) => SacramentInception with
    {
        Area = new() { AdjustTargets = (_, hits) => hits.Union(mistakes.Keys.Where(m => m.IsAlive())).ToList() },
        Effects = [new MistakesKill(mistakes), Damage(Lethal)],
        DeathExplanation = Because("the True Heart never turned gold to show where Sacrament would come from", why),
    };

    private sealed class MistakesKill(IReadOnlyDictionary<SimCharacter, string> mistakes) : IEnemyActionEffect
    {
        public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
        {
            foreach (var target in hits)
                if (mistakes.TryGetValue(target, out var mistake)) ctx.Kill(target, mistake);
        }
    }

    private static string Because(string what, string? why) => why is null ? what : $"{what} ({why})";

    // Alexander Prime powered up by the True Heart: 130-145k, past anyone's HP.
    public static EnemyAction PlaintOfSeverityPoweredUp(string? why) => PlaintOfSeverity with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = Because("the True Heart reached Alexander Prime", why),
    };

    public static readonly EnemyAction SacramentWormhole = new(ActionId.SacramentWormhole)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.6f },
    };

    // UNVERIFIED: that fewer than three in it die.
    public static readonly EnemyAction PlaintOfSolidarity = new(ActionId.PlaintOfSolidarity)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(MagicallyVulnerable, split: Distribution.Stack(3)),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 4f),
            ApplyStatus(StatusId.PhysicalVulnerabilityUp, 11f),
        ],
        Timing = new() { DamageDelay = 0.66f },
    };

    // 93-168k each on Alexander Prime powered up by the True Heart, however many share it.
    public static EnemyAction PlaintOfSolidarityPoweredUp(string? why) => PlaintOfSolidarity with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = Because("the True Heart reached Alexander Prime", why),
    };

    // Fired on himself, having turned to `bait`: the bait takes it, and anyone else in it dies.
    // UNVERIFIED: the knockback Limit Cut's charge has; every even number takes it knockback-immune.
    public static EnemyAction SuperBlasstyChargeP3(SimCharacter bait) => new(ActionId.SuperBlasstyChargeP3)
    {
        Cast = new() { AnimationLock = 1.7f },
        Effects =
        [
            OnOnly(bait, Damage(PhysicallyVulnerable), ApplyStatus(StatusId.MagicVulnerabilityUp, 10f), ApplyStatus(StatusId.PhysicalVulnerabilityUp, 17f),
                Knockback(KnockbackId.SuperBlasstyCharge, pushes: NeitherImmuneNorFacingCaster)),
            OnAllBut(bait, Damage(Lethal)),
        ],
        Timing = new() { DamageDelay = 0.53f },
    };

    public static readonly EnemyAction Confession = Raidwide(ActionId.Confession, 1.1f, 0.6f) with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "a wormhole nobody soaked",
    };

    public static readonly EnemyAction Repentance1 = Repentance(ActionId.Repentance1);
    public static readonly EnemyAction Repentance2 = Repentance(ActionId.Repentance2);
    public static readonly EnemyAction Repentance3 = Repentance(ActionId.Repentance3);

    // UNVERIFIED: anything short of all eight in it kills those who are.
    public static readonly EnemyAction IncineratingHeat = new(ActionId.IncineratingHeat)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(split: Distribution.Stack(8))],
        Timing = new() { DamageDelay = 1.10f },
    };

    public static readonly EnemyAction MegaHoly = Raidwide(ActionId.MegaHoly, 2.1f, 1.19f);
    public static readonly EnemyAction JStorm = Raidwide(ActionId.JStorm, 2.1f, 1.16f);
    public static readonly EnemyAction JWave = Raidwide(ActionId.JWave, 1.1f, 0.6f);

    public static readonly EnemyAction EternalDarknessP3 = Raidwide(ActionId.EternalDarknessP3, 3.1f, 0.6f) with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "Cruise Chaser wasn't down in time",
    };

    // -- P4 --
    public static readonly EnemyAction PerfectAlexanderAttack = AutoAttack(ActionId.PerfectAlexanderAttack, 0.53f);

    // On a tank still carrying Ordained Punishment's vulnerability.
    public static readonly EnemyAction PerfectAlexanderAttackUnswapped = PerfectAlexanderAttack with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "the tanks didn't swap after Ordained Punishment",
    };

    public static readonly EnemyAction TheFinalWord = Visual(ActionId.TheFinalWord, 3.1f);
    public static readonly EnemyAction OrdainedMotion = Visual(ActionId.OrdainedMotion, 2.1f);
    public static readonly EnemyAction OrdainedStillness = Visual(ActionId.OrdainedStillness, 2.1f);
    public static readonly EnemyAction OrdainedMotionSuccess = Visual(ActionId.OrdainedMotionSuccess, 0.6f);
    public static readonly EnemyAction OrdainedStillnessSuccess = Visual(ActionId.OrdainedStillnessSuccess, 0.6f);

    public static readonly EnemyAction OrdainedMotionFail = new(ActionId.OrdainedMotionFail)
    {
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.6f },
        DeathExplanation = "stood still through Ordained Motion",
    };

    public static readonly EnemyAction OrdainedStillnessFail = new(ActionId.OrdainedStillnessFail)
    {
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.6f },
        DeathExplanation = "moved or turned during Ordained Stillness",
    };

    public static readonly EnemyAction DivineRetributionLight = new(ActionId.DivineRetributionLight)
    {
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.5f },
    };

    public static readonly EnemyAction DivineRetributionDark = DivineRetributionLight with { ActionId = ActionId.DivineRetributionDark };

    public static readonly EnemyAction OpticalSightSpread = Visual(ActionId.OpticalSightSpread, 2.1f);
    public static readonly EnemyAction OpticalSightStack = Visual(ActionId.OpticalSightStack, 2.1f);

    public static readonly EnemyAction IndividualReprobation = new(ActionId.IndividualReprobation)
    {
        Effects = [Damage(MagicallyVulnerable), ApplyStatus(StatusId.MagicVulnerabilityUp, 4f)],
        Timing = new() { DamageDelay = 0.5f },
    };

    // A heavy raidwide and a long Damage Down that kill most, so it wipes.
    public static readonly EnemyAction Faithlessness = Raidwide(ActionId.Faithlessness, 0.6f, 0.6f) with
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "an Optical Sight stack was taken alone",
    };

    // Taken alone it sets off Faithlessness; two in it die, though real pairs often survive the hit.
    public static readonly EnemyAction CollectiveReprobation = new(ActionId.CollectiveReprobation)
    {
        Effects =
        [
            Damage(MagicallyVulnerable, split: Distribution.Stack(3)),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 4f),
        ],
        Timing = new() { DamageDelay = 0.5f },
    };

    public static readonly EnemyAction FateProjectionLeap = Visual(ActionId.FateProjectionLeap, 2.5f);
    public static readonly EnemyAction FateProjectionAlpha = Visual(ActionId.FateProjectionAlpha, 2.1f);
    public static readonly EnemyAction FateCalibrationAlpha = Visual(ActionId.FateCalibrationAlpha, 2.1f);
    public static readonly EnemyAction FateProjectionBeta = Visual(ActionId.FateProjectionBeta, 2.1f);
    public static readonly EnemyAction FateCalibrationBeta = Visual(ActionId.FateCalibrationBeta, 2.1f);
    public static readonly EnemyAction AlphaFirstOrdainedTell = Visual(ActionId.AlphaFirstOrdainedTell, 0.6f);
    public static readonly EnemyAction AlphaSecondOrdainedTell = Visual(ActionId.AlphaSecondOrdainedTell, 0.6f);

    // Sacrament's cross from its clone; the future's own fx only show it.
    public static readonly EnemyAction Sacrament = new(ActionId.Sacrament)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.5f },
    };

    // Only the defamation player may be in it.
    public static readonly EnemyAction PlaintOfObloquy = new(ActionId.PlaintOfObloquy)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            OnTarget(Damage(MagicallyVulnerable), ApplyStatus(StatusId.MagicVulnerabilityUp, 4f), ApplyStatus(StatusId.PhysicalVulnerabilityUp, 11f)),
            OnOthers(Damage(Lethal)),
        ],
        Timing = new() { DamageDelay = 0.66f },
        DeathExplanation = "stood in the defamation",
    };

    public static readonly EnemyAction OrdainedCapitalPunishment = Visual(ActionId.OrdainedCapitalPunishment, 2.1f);

    // Shared by whoever stands in it; alone, a human tank needs an invuln.
    public static readonly EnemyAction OrdainedCapitalPunishmentHit = new(ActionId.OrdainedCapitalPunishmentHit)
    {
        Effects = [Damage(MagicallyVulnerable, TankBuster.MinMit(0.3f), split: Distribution.Stack(2, understacked: TankBuster.MinMit(1f)))],
        Timing = new() { DamageDelay = 0.5f },
    };

    // Its target alone; the Physical Vulnerability Up it leaves makes the tanks swap. A target dead by
    // the time it lands takes it into the void.
    public static readonly EnemyAction OrdainedPunishment = new(ActionId.OrdainedPunishment)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = new() { AdjustTargets = (ctx, hits) => ctx.Target?.IsAlive() == true ? hits : [] },
        Effects =
        [
            OnTarget(Damage(MagicallyVulnerable, TankBuster.MinMit(0.3f)), ApplyStatus(StatusId.PhysicalVulnerabilityUp, 30f)),
            OnOthers(Damage(Lethal)),
        ],
        Timing = new() { DamageDelay = 0.5f },
    };

    // Its bait alone.
    public static readonly EnemyAction JJump = new(ActionId.JJump)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            OnTarget(Damage(PhysicallyVulnerable), ApplyStatus(StatusId.PhysicalVulnerabilityUp, 5f)),
            OnOthers(Damage(Lethal)),
        ],
        Timing = new() { DamageDelay = 0.5f },
    };

    // Safe within 8y of its clone.
    public static readonly EnemyAction RadiantSacrament = new(ActionId.RadiantSacrament)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = 8f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.5f },
    };

    public static readonly EnemyAction AlmightyJudgment = Visual(ActionId.AlmightyJudgment, 2.1f);
    public static readonly EnemyAction AlmightyJudgmentTile = Visual(ActionId.AlmightyJudgmentTile, 0.6f);
    public static readonly EnemyAction AlmightyJudgmentBlast = LethalCircle(ActionId.AlmightyJudgmentBlast);

    // The whole party's stack: short of all eight, everyone in it dies, though real parties of seven survive it.
    public static readonly EnemyAction IrresistibleGrace = new(ActionId.IrresistibleGrace)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(split: Distribution.Stack(8))],
        Timing = new() { DamageDelay = 0.5f },
    };

    public static readonly EnemyAction TemporalInterference = Visual(ActionId.TemporalInterference, 2.1f);

    public static readonly EnemyAction TemporalPrison = new(ActionId.TemporalPrison)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.6f },
        DeathExplanation = "Perfect Alexander still stood when Temporal Prison ended",
    };

    private static EnemyAction LethalCircle(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.5f },
    };

    private static bool NotImmune(EnemyActionContext ctx, SimCharacter target) => !KnockbackImmunity.Has(target);

    private static bool NeitherImmuneNorFacingCaster(EnemyActionContext ctx, SimCharacter target)
        => NotImmune(ctx, target) && !target.Placement().IsLookingAt(ctx.Caster.Position);

    private static EnemyAction AutoAttack(uint actionId, float damageDelay) => new(actionId)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage()],
        Timing = new() { DamageDelay = damageDelay },
    };

    private static EnemyAction Protean(uint actionId, float animationLock, float damageDelay) => new(actionId)
    {
        Cast = new() { AnimationLock = animationLock },
        Area = ProteanCone,
        Effects = [WaterDamage(), WaterResistanceDown],
        Timing = new() { DamageDelay = damageDelay },
    };

    // A soak on a Magic Vulnerability Up, or a second one within 3s, kills; nobody in it and the
    // wormhole goes off on everyone.
    private static EnemyAction Repentance(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(MagicallyVulnerable.VulnerableTo(StatusId.VulnerabilityUp)),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 10f),
            ApplyStatus(StatusId.VulnerabilityUp, 3f),
        ],
        Timing = new() { DamageDelay = 0.15f },
    };

    private static EnemyAction Visual(uint actionId, float animationLock) => new(actionId)
    {
        Cast = new() { AnimationLock = animationLock },
    };

    // Survivable by everyone.
    private static EnemyAction Raidwide(uint actionId, float animationLock, float damageDelay, VulnSpec? vulns = null) => new(actionId)
    {
        Cast = new() { AnimationLock = animationLock },
        Effects = [Damage(vulns ?? VulnSpec.None)],
        Timing = new() { DamageDelay = damageDelay },
    };
}

// A holder already carrying Luminous Aetheroplasm goes off with the hit; anyone else takes it.
internal sealed class LuminousOverloadEffect(EnemyAction explosion) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        var party = ctx.Party;
        foreach (var target in hits)
        {
            if (ctx.IsKilled(target) || !target.IsAlive()) continue;
            // Once this resolve is done, so the explosion can't claim its kills.
            if (target.HasStatus(StatusId.LuminousAetheroplasm))
                ctx.Events.Add(0f, () =>
                {
                    if (target.IsAlive()) party.Act(target, explosion);
                });
            else
                ctx.AddStatus(target, StatusId.LuminousAetheroplasm, 5f);
        }
    }
}

// `tank` on the tanks it reaches, `other` on everyone else.
internal sealed class TankOrOtherEffect(IEnemyActionEffect tank, IEnemyActionEffect other) : IEnemyActionEffect
{
    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        tank.Apply(ctx, hits.Where(DamageCheck.IsTank).ToList());
        other.Apply(ctx, hits.Where(h => !DamageCheck.IsTank(h)).ToList());
    }
}

// Lethal to the tanks it reaches short of an invuln; a death names their own job's.
internal sealed class ShortOfTheirInvulnEffect(VulnSpec spec) : IEnemyActionEffect
{
    private static readonly ushort[] Invulns = [.. Mitigation.ByStatusId.Where(m => m.Value.Damage >= 1f).Select(m => m.Key)];

    public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
    {
        foreach (var tank in hits)
        {
            var own = tank is ISimPartyMember member ? OwnInvuln((JobId)member.ClassJob) : null;
            Damage(Invulns.OrderBy(id => id != own).Aggregate(spec, (s, id) => s.ProtectedBy(id)), Lethal).Apply(ctx, [tank]);
        }
    }

    private static ushort? OwnInvuln(JobId job) => job switch
    {
        JobId.Paladin => 82,
        JobId.Warrior => 409,
        JobId.DarkKnight => 810,
        JobId.Gunbreaker => 1836,
        _ => null,
    };
}
