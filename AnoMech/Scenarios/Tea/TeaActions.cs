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
    private static readonly VulnSpec MagicallyVulnerable = VulnerableTo(StatusId.MagicVulnerabilityUp).VulnerableTo(StatusId.VulnerabilityUp);
    private static readonly VulnSpec PhysicallyVulnerable = VulnerableTo(StatusId.PhysicalVulnerabilityUp).VulnerableTo(StatusId.VulnerabilityUp);

    // -- Limit Cut --
    public static readonly EnemyAction HawkBlaster = new(ActionId.HawkBlaster)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.63f },
    };

    public static EnemyAction AlphaSword = new(ActionId.AlphaSword)
    {
        Cast = new() { AnimationLock = 1.5f },
        Area = new() { Size = MathF.PI / 4f },
        Effects =
        [
            Damage(PhysicallyVulnerable), 
            ApplyStatus(StatusId.MagicVulnerabilityUp, 10f), 
            ApplyStatus(StatusId.PhysicalVulnerabilityUp, 17f),
            Knockback(KnockbackId.AlphaSword, ignoresKbi: false),
        ],
        Timing = new() { DamageDelay = 0.31f },
    };

    public static readonly EnemyAction SuperBlasstyCharge = new(ActionId.SuperBlasstyCharge)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects =
        [
            Damage(PhysicallyVulnerable),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 10f),
            ApplyStatus(StatusId.PhysicalVulnerabilityUp, 17f),
            OnNotFacingCaster(Knockback(KnockbackId.SuperBlasstyCharge, ignoresKbi: false)),
        ],
        Timing = new() { DamageDelay = 0.53f },
    };

    public static readonly EnemyAction JKick = new(ActionId.JKick)
    {
        Cast = new() { AnimationLock = 3.1f },
        Effects = [Damage()],
        Timing = new() { DamageDelay = 1.16f },
    };

    public static readonly EnemyAction EyeOfTheChakram = new(ActionId.EyeOfTheChakram)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 1.15f },
    };

    // A player standing in both stacks: retail kills everyone in both stacks. Simplified here to kill
    // only that player.
    public static readonly EnemyAction Enumeration = new(ActionId.Enumeration)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(VulnerableTo(StatusId.EnumerationVuln), NoHit, split: Distribution.Exactly(3)),
            ApplyStatus(StatusId.EnumerationVuln, 1f),
        ],
        Timing = new() { DamageDelay = 0.62f },
    };

    public static readonly EnemyAction SuperJumpHit = new(ActionId.SuperJumpHit)
    {
        Cast = new() { AnimationLock = 2.1f, FaceTarget = false },
        Effects = [OnTarget(Damage()), OnOthers(Damage(Lethal))],
        Timing = new() { DamageDelay = 0.98f },
    };

    // UNVERIFIED: 90 degrees wide.
    public static readonly EnemyAction ApocalypticRayHit = new(ActionId.ApocalypticRayHit)
    {
        Cast = new() { AnimationLock = 1.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.18f },
    };

    public static readonly EnemyAction SacramentWormhole = new(ActionId.SacramentWormhole)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.6f },
    };

    // It knocks back; UNVERIFIED that it does so only when the target isn't facing Cruise Chaser.
    public static readonly EnemyAction SuperBlasstyChargeP3 = SuperBlasstyCharge with
    {
        ActionId = ActionId.SuperBlasstyChargeP3,
        Cast = new() { AnimationLock = 1.7f },
    };

    public static readonly EnemyAction Confession = new(ActionId.Confession)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.6f },
        DeathExplanation = "a wormhole nobody soaked",
    };

    public static readonly EnemyAction Repentance1 = Repentance(ActionId.Repentance1);
    public static readonly EnemyAction Repentance2 = Repentance(ActionId.Repentance2);
    public static readonly EnemyAction Repentance3 = Repentance(ActionId.Repentance3);

    private static EnemyAction Repentance(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects =
        [
            Damage(MagicallyVulnerable),
            ApplyStatus(StatusId.MagicVulnerabilityUp, 10f),
            ApplyStatus(StatusId.VulnerabilityUp, 3f),
        ],
        Timing = new() { DamageDelay = 0.15f },
    };
    
    public static readonly EnemyAction IncineratingHeat = new(ActionId.IncineratingHeat)
    {
        Cast = new() { AnimationLock = 2.1f, FaceTarget = false },
        Effects = [Damage(split: Distribution.Stack(8))],
        Timing = new() { DamageDelay = 1.10f },
    };
}
