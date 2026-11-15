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
        Effects = [Damage()],
        Timing = new() { DamageDelay = 0.8f },
    };

    public static readonly EnemyAction FinalChorus = new(ActionId.FinalChorusRaidwide)
    {
        Effects = [Damage()],
        Timing = new() { DamageDelay = 0.18f },
    };

    private static EnemyAction Jump(uint actionId) => new(actionId)
    {
        Effects =
        [
            Damage(Burning),
            ApplyStatus(StatusId.FireResistanceDownII, 12f),
            ApplyStatus(StatusId.PhysicalVulnerabilityUp, 3f),
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
        Effects = TowerEffects,
        Timing = new() { DamageDelay = 0.79f },
        DeathExplanation = "soaked a tower while still burning from a jump or tower",
    };

    public static readonly EnemyAction[] DarkdragonDiveForSoakers =
    [
        .. Array.ConvertAll(ActionId.DarkdragonDiveForSoakers, id => new EnemyAction(id)
        {
            Effects = TowerEffects,
            Timing = new() { DamageDelay = 0.79f },
            DeathExplanation = "soaked a tower while still burning from a jump or tower",
        }),
    ];

    public static readonly EnemyAction DarkdragonDiveFail = new(ActionId.DarkdragonDiveFail)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "a tower went unsoaked",
    };

    // Four sharing it is the fewest who survive.
    public static readonly EnemyAction EyeOfTheTyrant = new(ActionId.EyeOfTheTyrant)
    {
        Effects = [Damage(split: Stack(4))],
        Timing = new() { DamageDelay = 0.8f },
    };

    public static readonly EnemyAction GnashingWheel = new(ActionId.GnashingWheel)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "inside Gnashing Wheel; get out",
    };

    public static readonly EnemyAction LashingWheel = new(ActionId.LashingWheel)
    {
        Area = new() { Size = 8f },
        Effects = [Damage(Lethal)],
        DeathExplanation = "outside Lashing Wheel; get in",
    };

    public static readonly EnemyAction Geirskogul = new(ActionId.Geirskogul)
    {
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 1.19f },
        DeathExplanation = "in a Geirskogul line; each is aimed at whoever is closest to its clone when it starts",
    };

    public static readonly EnemyAction DrachenlanceCleave = new(ActionId.DrachenlanceCleave)
    {
        Area = new() { Size = MathF.PI / 4f },
        Effects = [Damage(Lethal)],
        DeathExplanation = "in front of Nidhogg for Drachenlance",
    };

    public static readonly EnemyAction SoulTether = new(ActionId.SoulTether)
    {
        Effects = [Damage(TankBuster)],
        Timing = new() { DamageDelay = 1.16f },
        DeathExplanation = "Soul Tether is a tank buster on whoever holds the tether",
    };
}
