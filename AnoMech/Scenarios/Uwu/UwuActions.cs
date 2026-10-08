using System.Linq;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Native.Interfaces;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Uwu.UwuConstants;

namespace AnoMech.Scenarios.Uwu;

public static class UwuActions
{
    private static readonly DamageSpec Magic = DamageType.Magic;
    private static readonly DamageSpec Physical = DamageType.Physical;

    private static readonly AreaSpec SparesGaoled = new() { AdjustTargets = (_, hits) =>
        hits.Where(h => !h.HasStatus(StatusId.Fetters)).ToList() };

    // TODO: find proper delay and knockbackId
    private static readonly IEnemyActionEffect LandslideKnockback = Knockback(distance: 30, speed: 50, knockbackDelay: 0.7f);

    // -- Shared --
    public static readonly EnemyAction FeatherRain = new(ActionId.FeatherRain)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = SparesGaoled,
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.4f },
    };

    public static readonly EnemyAction EruptionPuddle = new(ActionId.EruptionPuddle)
    {
        Cast = new() { AnimationLock = 0.1f },
        Area = SparesGaoled,
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.96f }, // TODO: verify with replay
    };

    public static readonly EnemyAction LandslideLine = Landslide(ActionId.LandslideLine, 2.1f);
    public static readonly EnemyAction LandslideAwaken = Landslide(ActionId.LandslideAwaken, 1.1f);
    public static readonly EnemyAction LandslideLineUltima = Landslide(ActionId.LandslideLineUltima, 1.1f);

    private static EnemyAction Landslide(uint actionId, float animationLock) => new(actionId)
    {
        Cast = new() { AnimationLock = animationLock },
        // TODO: add damage as well and verify damage delay
        Effects = [LandslideKnockback],
        Timing = new() { DamageDelay = 1.03f }, // TODO: verify with replay
    };

    // -- Ultima --
    public static readonly EnemyAction UltimaAttack = new(ActionId.UltimaAttack)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage(Physical)],
        Timing = new() { DamageDelay = 1.16f },
    };

    public static readonly EnemyAction ViscousAetheroplasmUltima = new(ActionId.ViscousAetheroplasmUltima) 
    {
       Cast = new() { AnimationLock = 2.1f },
       Effects = [Damage(Magic), ApplyStatus(1532, 10)], 
       Timing = new() { DamageDelay = 1.1f },
    };
    
    public static readonly EnemyAction ViscousAetheroplasm = new(ActionId.ViscousAetheroplasmEffect)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [
            Damage(Magic.VulnerableTo(StatusId.ViscousVuln), split: Distribution.Stack(8, understacked: TankBuster.MinMit(1f))),
            ApplyStatus(StatusId.ViscousVuln, 1f),
        ],
        Timing = new() { DamageDelay = 0.6f },
    };
        
    public static readonly EnemyAction CeruleumVent = new(ActionId.CeruleumVent)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.6f }, // TODO: verify with replay
    };

    public static readonly EnemyAction RadiantPlumePuddle = new(ActionId.RadiantPlumePuddle)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.86f }, // TODO: verify with replay
    };

    public static readonly EnemyAction HomingLasers = new(ActionId.HomingLasers)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Magic, TankBuster)],
        Timing = new() { DamageDelay = 2.6f },
    };

    // TODO: real tank-purge damage
    public static readonly EnemyAction TankPurge = Raidwide(ActionId.TankPurge, 2.1f, 0.8f);

    public static readonly EnemyAction LightPillarCircle = new(ActionId.LightPillarCircle)
    {
        Cast = new() { AnimationLock = 0.1f },
        Area = SparesGaoled,
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.9f }, // TODO: verify with replay
    };

    public static readonly EnemyAction AetherochemicalLaserCenter = AetherochemicalLaser(ActionId.AetherochemicalLaserCenter, 0f);
    public static readonly EnemyAction AetherochemicalLaserRight = AetherochemicalLaser(ActionId.AetherochemicalLaserRight, -45f);
    public static readonly EnemyAction AetherochemicalLaserLeft = AetherochemicalLaser(ActionId.AetherochemicalLaserLeft, 45f);

    public static EnemyAction AetherochemicalLaserById(uint actionId) => actionId switch
    {
        ActionId.AetherochemicalLaserRight => AetherochemicalLaserRight,
        ActionId.AetherochemicalLaserLeft => AetherochemicalLaserLeft,
        _ => AetherochemicalLaserCenter,
    };

    private static EnemyAction AetherochemicalLaser(uint actionId, float rotationDegrees) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f, Rotation = float.DegreesToRadians(rotationDegrees) },
        Area = SparesGaoled,
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.8f }, // TODO: verify with replay
    };

    // -- Garuda --
    public static readonly EnemyAction WickedWheelAwaken = new(ActionId.WickedWheelAwaken)
    {
        Cast = new() { AnimationLock = 2.8f },
        Effects = [Damage(Physical, Lethal)],
        Timing = new() { DamageDelay = 1.31f }, // TODO: verify with replay
    };

    // The donut's inner radius is Wicked Wheel's reach.
    public static readonly EnemyAction WickedTornado = new(ActionId.WickedTornado)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = new() { Size = 8.7f }, 
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.5f }, // TODO: verify with replay
    };

    public static readonly EnemyAction WickedWheel = new(ActionId.WickedWheel)
    {
        Cast = new() { AnimationLock = 2.8f },
        Effects = [Damage(Physical, Lethal)],
        Timing = new() { DamageDelay = 1.37f }, // TODO: verify with replay
    };

    public static readonly EnemyAction MistralShriek = Raidwide(ActionId.MistralShriek, 2.3f, damage: 0.90f);

    public static readonly EnemyAction MistralSong = new(ActionId.MistralSong)
    {
        Cast = new() { AnimationLock = 1.8f },
        Area = SparesGaoled,
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.6f }, // TODO: verify with replay
    };
    public static readonly EnemyAction MistralSongSuparnaChirada = new(ActionId.MistralSongSuparnaChirada)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = new() { CastTypeOverride = CastType.Cone2 },
        Effects = [
            Damage(Magic, split: Distribution.WildCharge(front: 1, frontHit: new Hit(Magic.VulnerableTo(StatusId.MistralSongVuln), TankBuster))),
            OnFront(1, ApplyStatus(StatusId.MistralSongVuln, 1f)),
        ],
        Timing = new() { DamageDelay = 0.4f },
    };
    public static readonly EnemyAction GreatWhirlwind = new(ActionId.GreatWhirlwind)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = SparesGaoled,
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.6f }, // TODO: verify with replay
    };
    public static readonly EnemyAction Mesohigh = new(ActionId.Mesohigh)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [
            Damage(Magic.ProtectedBy(StatusId.ThermalLow), Lethal),
        ],
        Timing = new() { DamageDelay = 0.83f }, // TODO: verify with replay
    };

    public static EnemyAction SuperCyclone(int thermalLowStacks) => thermalLowStacks switch
    {
        1 => SuperCyclone1,
        2 => SuperCyclone2,
        _ => SuperCyclone3,
    };

    private static readonly EnemyAction SuperCyclone1 = new(ActionId.SuperCyclone1)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Magic)],
        Timing = new() { DamageDelay = 0.5f },
    };

    private static readonly EnemyAction SuperCyclone2 = new(ActionId.SuperCyclone2)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [
            Damage(Magic.VulnerableTo(StatusId.SuperCycloneVuln)),
            ApplyStatus(StatusId.SuperCycloneVuln, 2f),
        ],
        Timing = new() { DamageDelay = 0.5f }, 
    };

    private static readonly EnemyAction SuperCyclone3 = new(ActionId.SuperCyclone3)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.5f }, 
    };

    public static readonly EnemyAction Featherlance = new(ActionId.Featherlance)
    {
        Cast = new() { AnimationLock = 2.1f },
        Area = SparesGaoled,
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.5f }, // TODO: verify with replay
    };

    // -- Ifrit --
    public static readonly EnemyAction CrimsonCyclone = new(ActionId.CrimsonCyclone)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.4f },
    };

    public static readonly EnemyAction CrimsonCycloneAwaken = new(ActionId.CrimsonCycloneAwaken)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.6f },
        DeathExplanation = "Awaken",
    };

    public static readonly EnemyAction FlamingCrush = new(ActionId.FlamingCrush)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [
            Damage(Magic, split: Distribution.Stack(6)), 
            ApplyStatus(StatusId.AccursedFlame, 3f)
        ],
        Timing = new() { DamageDelay = 0.73f }, // TODO: verify with replay
    };

    // -- Titan --
    public static readonly EnemyAction Tumult = Raidwide(ActionId.Tumult, 1.1f, damage: 0.9f);

    // Cancelled before it goes off: the sim always breaks the gaol in time.
    public static readonly EnemyAction GraniteImpact = new(ActionId.GraniteImpact);

    public static readonly EnemyAction Bury = new(ActionId.Bury)
    {
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.53f }, // TODO: verify with replay
    };

    public static readonly EnemyAction Burst = new(ActionId.Burst)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Magic, Lethal)],
        Timing = new() { DamageDelay = 0.46f }, // TODO: verify with replay
    };

    private static EnemyAction Raidwide(uint actionId, float animationLock, float damage) => new(actionId)
    {
        Cast = new() { AnimationLock = animationLock },
        Effects = [Damage(Magic)],
        Timing = new() { DamageDelay = damage },
    };
}
