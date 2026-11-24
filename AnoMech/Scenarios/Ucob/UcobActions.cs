using System;
using AnoMech.Core.EnemyActions;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Ucob.UcobConstants;

namespace AnoMech.Scenarios.Ucob;

public static class UcobActions
{

    public static readonly EnemyAction AutoAttack = new(ActionId.AutoAttack)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage()],
        Timing = new() { DamageDelay = 1.03f },
    };

    // -- Exaflare --

    public static readonly EnemyAction Exaflare = new(ActionId.Exaflare)
    {
        Cast = new() { AnimationLock = 3.1f },
    };

    public static readonly EnemyAction ExaflareFirst = new(ActionId.ExaflareFirst)
    {
        Cast = new() { AnimationLock = 1.6f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.52f },
    };

    public static readonly EnemyAction ExaflareRest = new(ActionId.ExaflareRest)
    {
        Cast = new() { AnimationLock = 1.5f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.22f },
    };

    // -- P4 Adds --

    public static readonly EnemyAction TwintaniaAttack = AddAutoAttack(ActionId.TwintaniaAttack, StatusId.SlashingResistanceDownII, "Twintania");
    public static readonly EnemyAction NaelAttack = AddAutoAttack(ActionId.NaelAttack, StatusId.PiercingResistanceDownII, "Nael");

    // An auto from the add whose buster left its resistance down on this tank is lethal: the other
    // tank should have taken the add first.
    private static EnemyAction AddAutoAttack(uint actionId, ushort ownBusterVuln, string add) => new(actionId)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage(VulnerableTo(ownBusterVuln))],
        Timing = new() { DamageDelay = 1.03f },
        DeathExplanation = $"still had the vulnerability from {add}'s tank buster; the other tank needed to take {add} right after it",
    };

    public static readonly EnemyAction DeathSentence = Buster(ActionId.DeathSentence, StatusId.SlashingResistanceDownII, 34f);
    public static readonly EnemyAction Ravensbeak = Buster(ActionId.Ravensbeak, StatusId.PiercingResistanceDownII, 45f);

    private static EnemyAction Buster(uint actionId, ushort vuln, float vulnSeconds) => new(actionId)
    {
        Effects = [Damage(VulnerableTo(vuln), TankBuster), ApplyStatus(vuln, vulnSeconds)],
    };

    public static readonly EnemyAction Plummet = new(ActionId.Plummet)
    {
        Area = new() { Size = MathF.PI / 4f },
        Effects = [OnTarget(Damage()), OnOthers(Damage(Lethal))],
        DeathExplanation = "only Twintania's tank should be in front of her",
    };

    public static readonly EnemyAction BahamutsClaw = new(ActionId.BahamutsClaw)
    {
        Effects = [Damage()],
    };

    public static readonly EnemyAction LiquidHell = new(ActionId.LiquidHell)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage()],
    };

    // Kills the player who walked into the twister and throws everyone near it into the wall.
    public static readonly EnemyAction TwisterBurst = new(ActionId.TwisterBurst)
    {
        Effects = [OnTarget(Damage(Lethal)), OnOthers(Knockback(KnockbackId.TwisterBurst))],
        DeathExplanation = "walked into a Twister; step off your spot as Twintania's cast finishes and don't walk back through it",
    };

    public static readonly EnemyAction Hatch = new(ActionId.Hatch)
    {
        Effects = [OnTarget(Damage(VulnerableTo(StatusId.ManaHypersensitivity)), ApplyStatus(StatusId.ManaHypersensitivity, 16f))],
        DeathExplanation = "took a second Hatch while still Mana Hypersensitive from the previous one",
    };

    public static readonly EnemyAction DeepHatch = new(ActionId.DeepHatch)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "a Hatch orb reached its target outside a Neurolink",
    };

    public static readonly EnemyAction IronChariot = new(ActionId.IronChariot)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "get out of Nael for Chariot",
    };

    // The safe spot reaches past Nael's hitbox, as Chariot does.
    public static readonly EnemyAction LunarDynamo = new(ActionId.LunarDynamo)
    {
        Area = new() { Size = 6f + Geometry.NaelHitboxRadius },
        Effects = [Damage(Lethal)],
        DeathExplanation = "get in close to Nael for Dynamo",
    };

    // UNVERIFIED: a clear survived the beam with 6 of 8 in it; fewer is assumed lethal.
    public static EnemyAction ThermionicBeam(int stackOf) => new(ActionId.ThermionicBeam)
    {
        Effects = [Damage(split: Distribution.Stack(stackOf))],
        DeathExplanation = "stack together for the beam",
    };

    public static readonly EnemyAction RavenDive = new(ActionId.RavenDive)
    {
        Effects = [OnTarget(Damage()), OnOthers(Damage(Lethal))],
        DeathExplanation = "spread out for the dive",
    };

    public static readonly EnemyAction Megaflare = new(ActionId.Megaflare)
    {
        Effects = [Damage()],
    };
}
