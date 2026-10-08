using System;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.EnemyActions.Distribution;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;

namespace AnoMech.Scenarios.Umad;

public static class UmadActions
{
    internal static readonly VulnSpec MagicVulns = VulnerableTo(UmadConstants.StatusId.MagicVulnerabilityUp);

    private static readonly IEnemyActionEffect MagicVulnerabilityUp =
        ApplyStatus(UmadConstants.StatusId.MagicVulnerabilityUp, 0.96f);

    internal static readonly IEnemyActionEffect LongMagicVulnerabilityUp =
        ApplyStatus(UmadConstants.StatusId.MagicVulnerabilityUp, 1.96f);

    public static readonly EnemyAction AutoAttack = new(UmadConstants.ActionId.AutoAttack1)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage()],
        Timing = new() { DamageDelay = 0.72f },
    };

    // -- P1 Tele-trouncing --

    public static readonly EnemyAction DoubleTroubleTrapStack = new(UmadConstants.ActionId.DoubleTroubleTrapStack)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(MagicVulns, split: Stack(4)),
            OnOthers(MagicVulnerabilityUp),
            OnOthers(Knockback(UmadConstants.KnockbackId.DoubleTroubleTrapStack, knockbackDelay: 0f)),
        ],
        Timing = new() { DamageDelay = 0.67f },
    };

    public static readonly EnemyAction IndulgentWill = new(UmadConstants.ActionId.IndulgentWill)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns)],
        Timing = new() { DamageDelay = 0.63f },
    };

    public static readonly EnemyAction IdyllicWill = new(UmadConstants.ActionId.IdyllicWill)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns), MagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.63f },
    };

    public static readonly EnemyAction ThrummingThunderReal1 = ThrummingThunder(UmadConstants.ActionId.ThrummingThunderIII_Real);
    public static readonly EnemyAction ThrummingThunderReal2 = ThrummingThunder(UmadConstants.ActionId.ThrummingThunderIII_FakeAnim);

    private static EnemyAction ThrummingThunder(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
        Timing = new() { DamageDelay = 0.65f },
    };

    public static readonly EnemyAction IndolentWill = StatueGaze(UmadConstants.ActionId.IndolentWill, lookAway: true);
    public static readonly EnemyAction AveMaria = StatueGaze(UmadConstants.ActionId.AveMaria, lookAway: false);

    private static EnemyAction StatueGaze(uint actionId, bool lookAway) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Gaze(lookAway)],
        Timing = new() { DamageDelay = 0.71f },
    };

    public static readonly EnemyAction FlagrantFireSpread = new(UmadConstants.ActionId.FlagrantFireSpread)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns), MagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.63f },
    };

    public static readonly EnemyAction FlagrantFireStack = new(UmadConstants.ActionId.FlagrantFireStack)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, split: Stack(4)), MagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.71f },
    };

    // -- P2 Forsaken --

    public static readonly EnemyAction Forsaken = new(UmadConstants.ActionId.Forsaken)
    {
        Cast = new() { AnimationLock = 3.1f },
        Effects = [Damage(MagicVulns)],
        Timing = new() { DamageDelay = 1.61f },
    };

    public static readonly EnemyAction LightOfJudgment = new(UmadConstants.ActionId.LightOfJudgment)
    {
        Cast = new() { AnimationLock = 3.1f },
        Effects = [Damage(MagicVulns.VulnerableTo(UmadConstants.StatusId.SpellsTrouble))],
        Timing = new() { DamageDelay = 0.8f },
    };

    private static readonly EnemyAction TheRiverOfLight = new(UmadConstants.ActionId.TheRiverOfLight)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
        DeathExplanation = "a tower not taken by exactly two",
    };

    public static readonly EnemyAction ThePathOfLight = new(UmadConstants.ActionId.ThePathOfLight)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [FollowUp(TheRiverOfLight, ctx => ctx.Hits.Count != 2)],
        Timing = new() { DamageDelay = 0.64f },
    };

    public static readonly EnemyAction Spelldriver = new(UmadConstants.ActionId.Spelldriver)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, split: Stack(3)), LongMagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.62f },
    };

    public static readonly EnemyAction Spellscatter = new(UmadConstants.ActionId.Spellscatter)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns), LongMagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.62f },
    };

    public static readonly EnemyAction Spellwave = new(UmadConstants.ActionId.Spellwave)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = MathF.PI / 4, ExcludeCaster = true },
        Effects = [Damage(MagicVulns), LongMagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.62f },
    };

    // -- P3 Black Hole --

    public static readonly EnemyAction ExdeathAutoAttack = new(UmadConstants.ActionId.AutoAttack2)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage()],
        Timing = new() { DamageDelay = 0.8f },
    };

    public static readonly EnemyAction SlapHappySlap = new(UmadConstants.ActionId.SlapHappy_Slap)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction SlapHappyFinalSlap = new(UmadConstants.ActionId.SlapHappy_FinalSlap)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Lethal)],
    };

    // Half-angle estimated from the animation.
    private const float SlapConeHalfAngle = MathF.PI / 6;

    public static readonly EnemyAction ShockwaveCone = new(UmadConstants.ActionId.ShockwaveCone)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = SlapConeHalfAngle },
        Effects = [Damage(MagicVulns), LongMagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.65f },
    };

    public static readonly EnemyAction ShockingImpact = new(UmadConstants.ActionId.ShockingImpact)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = SlapConeHalfAngle },
        Effects = [Damage(MagicVulns, split: Stack(8)), LongMagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.62f },
    };

    public static readonly EnemyAction DamningEdict = new(UmadConstants.ActionId.DamningEdict)
    {
        Cast = new() { AnimationLock = 3.1f, OmenDelay = 4f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction LookUponMeAndDespair = new(UmadConstants.ActionId.LookUponMeAndDespair_Omen)
    {
        Cast = new() { AnimationLock = 1.1f, OmenDelay = 4f },
        Effects = [Damage(MagicVulns, Lethal)],
        Timing = new() { DamageDelay = 0.1f },
    };

    public static readonly EnemyAction ThunderIII = new(UmadConstants.ActionId.ThunderIII_Resolve)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(MagicVulns.VulnerableTo(UmadConstants.StatusId.LightningResistanceDownII, 1f), TankBuster.MinMit(0.60f)),
            ApplyStatus(UmadConstants.StatusId.LightningResistanceDownII, 3.96f),
        ],
        Timing = new() { DamageDelay = 0.22f },
    };

    public static readonly EnemyAction ImplosionShockwave = new(UmadConstants.ActionId.Shockwave)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = MathF.PI / 4 },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction BlizzardIII = new(UmadConstants.ActionId.BlizzardIII)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction KnockDown = new(UmadConstants.ActionId.KnockDown)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, split: Stack(4)), LongMagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.48f },
    };

    private static readonly EnemyAction UnmitigatedImpact = new(UmadConstants.ActionId.UnmitigatedImpact)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
        DeathExplanation = "a tower nobody took",
    };

    public static readonly EnemyAction StompAMole = new(UmadConstants.ActionId.StompAMole)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(MagicVulns, split: Stack(2)),
            ApplyStatus(UmadConstants.StatusId.MagicVulnerabilityUp, 3f),
            FollowUp(UnmitigatedImpact, ctx => ctx.Hits.Count == 0),
        ],
        Timing = new() { DamageDelay = 0.17f },
    };

    public static readonly EnemyAction EarthquakeCleanse = new(UmadConstants.ActionId.Earthquake_Cleanse)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            OnOthers(Damage(VulnerableTo(UmadConstants.StatusId.EarthResistanceDownII))),
            OnOthers(ApplyStatus(UmadConstants.StatusId.EarthResistanceDownII, 1.96f)),
            OnTarget(RemoveStatus(UmadConstants.StatusId.Accretion)),
        ],
        Timing = new() { DamageDelay = 0.64f },
    };

    public static readonly EnemyAction Nothingness = new(UmadConstants.ActionId.Nothingness)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [new NothingnessEffect()],
        Timing = new() { DamageDelay = 0.5f },
    };

    // -- P3 Limit Cut --

    // Kefka's second trance beat: instant, although the sheet gives it a 3s cast.
    public static readonly EnemyAction RingOfFire = new(UmadConstants.ActionId.RingOfFire)
    {
        Cast = new() { CastSecondsOverride = 0f },
    };

    public static readonly EnemyAction UmbraSmash = new(UmadConstants.ActionId.UmbraSmash)
    {
        Cast = new() { AnimationLock = 4.1f },
        Effects = [Damage(split: Falloff(20f))],
        Timing = new() { DamageDelay = 1.78f },
    };

    public static readonly EnemyAction VacuumWave = new(UmadConstants.ActionId.VacuumWave)
    {
        Cast = new() { AnimationLock = 3.1f },
        Effects =
        [
            Knockback(VacuumWavePush, 40f, knockbackDelay: 0.78f),
            RemoveStatus(UmadConstants.StatusId.Headwind),
            RemoveStatus(UmadConstants.StatusId.Tailwind),
        ],
        Timing = new() { DamageDelay = 0.84f },
    };

    private static float? VacuumWavePush(EnemyActionContext ctx, SimCharacter target)
    {
        var headwind = target.HasStatus(UmadConstants.StatusId.Headwind);
        if (!headwind && !target.HasStatus(UmadConstants.StatusId.Tailwind)) return 20f;
        var facing = target.Placement();
        var from = ctx.Caster.Position;
        return (headwind ? facing.IsLookingAwayFrom(from) : facing.IsLookingAt(from)) ? 10f : 40f;
    }

    // Shorter than the clone's 1.8s materialise leaves it frozen white and translucent.
    public static readonly EnemyAction UltimaBlaster = new(UmadConstants.ActionId.UltimaBlaster)
    {
        Cast = new() { AnimationLock = 1.8f },
        Effects = [Damage(MagicVulns)],
        Timing = new() { DamageDelay = 0.38f },
    };

    public static readonly EnemyAction UltimaBlasterCharge = new(UmadConstants.ActionId.UltimaBlasterCharge)
    {
        Cast = new() { AnimationLock = 1.8f },
        Effects = [Damage(MagicVulns, split: Falloff(35f)), ApplyStatus(UmadConstants.StatusId.MagicVulnerabilityUp, 2.96f)],
        Timing = new() { DamageDelay = 0.36f },
    };

    public static readonly EnemyAction Cyclone = new(UmadConstants.ActionId.Cyclone)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(VulnerableTo(UmadConstants.StatusId.WindResistanceDownII, 0.80f), split: Stack(2, understacked: TankBuster.MinMit(0.80f))),
            ApplyStatus(UmadConstants.StatusId.WindResistanceDownII, 0.96f),
        ],
        Timing = new() { DamageDelay = 0.63f },
    };

    // -- P4 Kefka Says --

    public static readonly EnemyAction BlizzardIIIBlowout = MysteryBlizzard(UmadConstants.ActionId.BlizzardIIIBlowout_Real);
    public static readonly EnemyAction BlizzardIIIBlowoutLie = MysteryBlizzard(UmadConstants.ActionId.BlizzardIIIBlowout_FakeAnim);

    private static EnemyAction MysteryBlizzard(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = MathF.PI / 4 },
        Effects = [Damage(MagicVulns, Lethal)],
        Timing = new() { DamageDelay = 0.6f },
    };

    public static readonly EnemyAction ThrummingThunderIII = MysteryThunder(UmadConstants.ActionId.ThrummingThunderIII_Real);
    public static readonly EnemyAction ThrummingThunderIIILie = MysteryThunder(UmadConstants.ActionId.ThrummingThunderIII_FakeAnim);

    private static EnemyAction MysteryThunder(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
        Timing = new() { DamageDelay = 0.6f },
    };

    public static readonly EnemyAction EdgeOfDeath = new(UmadConstants.ActionId.EdgeOfDeath)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction WhiteAntilight = Antilight(UmadConstants.ActionId.WhiteAntilight, debuffsTrue: true, UmadConstants.StatusId.WhiteWound);
    public static readonly EnemyAction WhiteAntilightLie = Antilight(UmadConstants.ActionId.WhiteAntilight, debuffsTrue: false, UmadConstants.StatusId.WhiteWound);
    public static readonly EnemyAction BlackAntilight = Antilight(UmadConstants.ActionId.BlackAntilight, debuffsTrue: true, UmadConstants.StatusId.BlackWound);
    public static readonly EnemyAction BlackAntilightLie = Antilight(UmadConstants.ActionId.BlackAntilight, debuffsTrue: false, UmadConstants.StatusId.BlackWound);

    private static EnemyAction Antilight(uint actionId, bool debuffsTrue, ushort wound)
    {
        var lethalWound = debuffsTrue ? wound : OtherWound(wound);
        var cleansedBy = debuffsTrue ? UmadConstants.StatusId.BeyondDeath : UmadConstants.StatusId.AllaganField;
        return new(actionId)
        {
            Cast = new() { AnimationLock = 2.1f },
            Effects =
            [
                Damage(VulnerableTo(lethalWound).ProtectedBy(cleansedBy)),
                OnHavingStatus(lethalWound, RemoveStatus(cleansedBy)),
                RemoveStatus(UmadConstants.StatusId.WhiteWound),
                RemoveStatus(UmadConstants.StatusId.BlackWound),
                ApplyStatus(wound, 0f),
            ],
            Timing = new() { DamageDelay = 0.13f },
        };
    }

    private static ushort OtherWound(ushort wound)
        => wound == UmadConstants.StatusId.WhiteWound ? UmadConstants.StatusId.BlackWound : UmadConstants.StatusId.WhiteWound;

    public static readonly EnemyAction DeathBoltStack = DeathElement(UmadConstants.ActionId.DeathBolt, stack: true);
    public static readonly EnemyAction DeathBoltSpread = DeathElement(UmadConstants.ActionId.DeathBolt, stack: false);
    public static readonly EnemyAction DeathWaveStack = DeathElement(UmadConstants.ActionId.DeathWave, stack: true);
    public static readonly EnemyAction DeathWaveSpread = DeathElement(UmadConstants.ActionId.DeathWave, stack: false);

    private static EnemyAction DeathElement(uint actionId, bool stack) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [stack ? Damage(MagicVulns, split: Stack(3)) : Damage(MagicVulns), LongMagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.63f },
    };

    public static readonly EnemyAction DeathShriekLookAway = DeathShriek(lookAway: true);
    public static readonly EnemyAction DeathShriekLookAt = DeathShriek(lookAway: false);

    private static EnemyAction DeathShriek(bool lookAway) => new(UmadConstants.ActionId.DeathShriek)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Gaze(lookAway)],
    };

    public static readonly EnemyAction StrayFlamesChariot = StrayBait(UmadConstants.ActionId.StrayFlames_Chariot);
    public static readonly EnemyAction StrayFlamesDonut = StrayBait(UmadConstants.ActionId.StrayFlames_Donut);
    public static readonly EnemyAction StraySprayDonut = StrayBait(UmadConstants.ActionId.StraySpray_Donut);
    public static readonly EnemyAction StraySprayChariot = StrayBait(UmadConstants.ActionId.StraySpray_Chariot);

    private static EnemyAction StrayBait(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f, OmenDelay = 4f },
        Area = new() { Size = 6f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction DeathBomb = new(UmadConstants.ActionId.DeathBomb)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
        Timing = new() { DamageDelay = 0.09f },
        DeathExplanation = "failed Acceleration Bomb",
    };

    public static readonly EnemyAction DeathSurge = new(UmadConstants.ActionId.DeathSurge)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(kindOverride: DamageKind.Unique)],
        Timing = new() { DamageDelay = 0.8f },
    };

    public static readonly EnemyAction DeathSurgeWipe = DeathSurge with
    {
        Effects = [Damage(Lethal, kindOverride: DamageKind.Unique)],
        DeathExplanation = "Allagan Field holder died",
    };

    // -- P5 Celestriad --

    public static readonly EnemyAction CelestriadFireTower = CelestriadTower(
        UmadConstants.ActionId.CelestriadFireIII, UmadConstants.ActionId.StardustFireIII, UmadConstants.StatusId.FireResistanceDownII);

    public static readonly EnemyAction CelestriadIceTower = CelestriadTower(
        UmadConstants.ActionId.CelestriadBlizzardIII, UmadConstants.ActionId.StardustBlizzardIII, UmadConstants.StatusId.IceResistanceDownII);

    public static readonly EnemyAction CelestriadLightningTower = CelestriadTower(
        UmadConstants.ActionId.CelestriadThunderIII, UmadConstants.ActionId.StardustThunderIII, UmadConstants.StatusId.LightningResistanceDownII);

    private static EnemyAction CelestriadTower(uint actionId, uint stardustId, ushort resistanceDown) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(VulnerableTo(resistanceDown), split: Stack(2)),
            ApplyStatus(resistanceDown, 20f),
            FollowUp(Stardust(stardustId), ctx => ctx.Hits.Count == 0),
        ],
        Timing = new() { DamageDelay = 0.64f },
    };

    private static EnemyAction Stardust(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(),
            ApplyStatus(UmadConstants.StatusId.DamageDown, 30f), // UNVERIFIED duration
        ],
        Timing = new() { DamageDelay = 0.68f },
    };

    public static readonly EnemyAction CatastrophicChoiceAero = new(UmadConstants.ActionId.CatastrophicChoiceAero_Resolve)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = 10f },
        Effects = [Damage(MagicVulns, Lethal)],
        Timing = new() { DamageDelay = 0.23f },
    };

    public static readonly EnemyAction CatastrophicChoiceEarth = new(UmadConstants.ActionId.CatastrophicChoiceEarth_Resolve)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
        Timing = new() { DamageDelay = 0.11f },
    };

    // -- P5 Exaflares --

    public static readonly EnemyAction ExaflareHit = new(UmadConstants.ActionId.ExaflareHit)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
        Timing = new() { DamageDelay = 0.62f },
    };

    public static readonly EnemyAction ExaflareSpread = new(UmadConstants.ActionId.ExaflareSpread)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns), LongMagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.57f },
    };

    // -- P5 Flood --

    public static readonly EnemyAction FloodWave = new(UmadConstants.ActionId.FloodAOE)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, Lethal)],
    };

    public static readonly EnemyAction ChaoticFlood = new(UmadConstants.ActionId.ChaoticFlood)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(MagicVulns, split: Stack(8))],
        Timing = new() { DamageDelay = 0.64f },
    };

    private sealed class NothingnessEffect : IEnemyActionEffect
    {
        public void Apply(EnemyActionContext ctx)
        {
            foreach (var target in ctx.Hits)
            {
                if (ctx.IsKilled(target) || !target.IsAlive()) continue;
                if (target.HasStatus(UmadConstants.StatusId.MeanestExistence))
                {
                    if (!target.HasStatus(UmadConstants.StatusId.PrimordialCrust))
                    {
                        ctx.Kill(target, "hit with Meanest Existence and no Primordial Crust");
                        continue;
                    }
                    target.RemoveStatus(UmadConstants.StatusId.PrimordialCrust);
                    target.RemoveStatus(UmadConstants.StatusId.FirstInLine);
                    target.RemoveStatus(UmadConstants.StatusId.SecondInLine);
                    target.RemoveStatus(UmadConstants.StatusId.ThirdInLine);
                }
                else if (target.HasStatus(UmadConstants.StatusId.Unbecoming))
                {
                    target.RemoveStatus(UmadConstants.StatusId.Unbecoming);
                    target.AddStatus(UmadConstants.StatusId.MeanestExistence);
                }
                else
                {
                    target.AddStatus(UmadConstants.StatusId.Unbecoming);
                }
            }
        }
    }
}
