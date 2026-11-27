using System;
using AnoMech.Core.EnemyActions;
using static AnoMech.Core.EnemyActions.Distribution;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr;

public static class DsrActions
{
    // A jump or a tower on someone still burning from the last one kills them.
    private static readonly VulnSpec Burning =
        VulnerableTo(StatusId.FireResistanceDownII).VulnerableTo(StatusId.PhysicalVulnerabilityUp);

    public static readonly EnemyAction AutoAttack = new(ActionId.NidhoggAutoAttack)
    {
        Effects = [Damage(TankBuster)],
        Timing = new() { DamageDelay = 0.8f },
    };

    public static readonly EnemyAction FinalChorus = new(ActionId.FinalChorusRaidwide)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage()],
        Timing = new() { DamageDelay = 0.18f },
    };

    // The clone lands where the jumper stood, facing their way: the arrow towers follow that facing.
    // Anyone clipped besides the jumper is thrown into the wall.
    private static EnemyAction Jump(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 1.1f, FaceTarget = false },
        Effects =
        [
            Damage(Burning),
            ApplyStatus(StatusId.FireResistanceDownII, 12f),
            ApplyStatus(StatusId.PhysicalVulnerabilityUp, 3f),
            OnOthers(Knockback(KnockbackId.DiveFromGraceJump)),
        ],
        Timing = new() { DamageDelay = 0.58f },
        DeathExplanation = "hit by a jump while still burning from a jump or tower",
    };

    public static readonly EnemyAction DarkHighJump = Jump(ActionId.DarkHighJump);
    public static readonly EnemyAction DarkSpineshatterDive = Jump(ActionId.DarkSpineshatterDive);
    public static readonly EnemyAction DarkElusiveJump = Jump(ActionId.DarkElusiveJump);

    private static readonly IEnemyActionEffect[] TowerEffects =
    [
        Damage(Burning),
        ApplyStatus(StatusId.FireResistanceDownII, 12f),
        ApplyStatus(StatusId.PhysicalVulnerabilityUp, 7f),
    ];

    public static readonly EnemyAction DarkdragonDive = new(ActionId.DarkdragonDive)
    {
        Cast = new() { AnimationLock = 1.6f },
        Effects = TowerEffects,
        Timing = new() { DamageDelay = 0.79f },
        DeathExplanation = "soaked a tower while still burning from a jump or tower",
    };

    public static readonly EnemyAction[] DarkdragonDiveForSoakers =
    [
        .. Array.ConvertAll(ActionId.DarkdragonDiveForSoakers, id => new EnemyAction(id)
        {
            Cast = new() { AnimationLock = 1.6f },
            Effects = TowerEffects,
            Timing = new() { DamageDelay = 0.79f },
            DeathExplanation = "soaked a tower while still burning from a jump or tower",
        }),
    ];

    public static readonly EnemyAction DarkdragonDiveFail = new(ActionId.DarkdragonDiveFail)
    {
        Cast = new() { AnimationLock = 1.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.63f },
        DeathExplanation = "a tower went unsoaked",
    };

    public static readonly EnemyAction EyeOfTheTyrant = new(ActionId.EyeOfTheTyrant)
    {
        Cast = new() { AnimationLock = 3.4f, FaceTarget = false },
        Effects = [Damage(split: Stack(5))],
        Timing = new() { DamageDelay = 0.8f },
    };

    public static readonly EnemyAction GnashingWheel = new(ActionId.GnashingWheel)
    {
        Cast = new() { AnimationLock = 3.1f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.88f },
        DeathExplanation = "inside Gnashing Wheel; get out",
    };

    public static readonly EnemyAction LashingWheel = new(ActionId.LashingWheel)
    {
        Cast = new() { AnimationLock = 3.1f },
        Area = new() { Size = 8f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.88f },
        DeathExplanation = "outside Lashing Wheel; get in",
    };

    public static readonly EnemyAction Geirskogul = new(ActionId.Geirskogul)
    {
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 1.19f },
        Cast = new() { OmenDelay = 3.5f, AnimationLock = 2.1f },
        DeathExplanation = "in a Geirskogul line; each is aimed at whoever is closest to its clone when it starts",
    };

    public static readonly EnemyAction DrachenlanceCleave = new(ActionId.DrachenlanceCleave)
    {
        Cast = new() { OmenDelay = 2.5f, AnimationLock = 1.1f },
        Area = new() { Size = MathF.PI / 4f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.52f },
        DeathExplanation = "in front of Nidhogg for Drachenlance",
    };

    // Both go off together: one tank holding both tethers takes the second on the first's vuln.
    public static readonly EnemyAction SoulTether = new(ActionId.SoulTether)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects = [Damage(VulnerableTo(StatusId.SoulTetherVuln), TankBuster), ApplyStatus(StatusId.SoulTetherVuln, 1f)],
        Timing = new() { DamageDelay = 1.16f },
        DeathExplanation = "Soul Tether is a tank buster on whoever holds the tether; one tank can't take both",
    };
}
