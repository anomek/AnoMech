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
}
