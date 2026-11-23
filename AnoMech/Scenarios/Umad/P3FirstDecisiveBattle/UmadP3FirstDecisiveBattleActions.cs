using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;

namespace AnoMech.Scenarios.Umad.P3FirstDecisiveBattle;

internal static class UmadP3FirstDecisiveBattleActions
{
    internal static EnemyAction CrystalWithReaction(bool fire, Action<int> consumed)
    {
        var action = fire ? Inferno : Tsunami;
        return action with { Effects = [.. action.Effects, OnOthers(new WindReaction(consumed))] };
    }

    internal static bool ConsumesWind(SimCharacter member, Vector3 origin)
        => (member.HasStatus(UmadConstants.StatusId.Headwind) || member.HasStatus(UmadConstants.StatusId.Tailwind))
           && (member.Placement().IsLookingAt(origin) || member.Placement().IsLookingAwayFrom(origin));

    private sealed class WindReaction(Action<int> consumed) : IEnemyActionEffect
    {
        public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
        {
            var count = hits.Count(m => !ctx.IsKilled(m) && ConsumesWind(m, ctx.Origin.Position));
            if (count > 0) consumed(count);
        }
    }
    internal const ushort Entropy = 1600;
    internal const ushort Fluid = 1601;
    internal const uint BowelsId = 47858;
    internal const uint FireId = 47859;
    internal const uint WaterId = 47862;
    internal const uint InfernoId = 47860;
    internal const uint TsunamiId = 47861;
    internal static readonly EnemyAction Bowels = new(BowelsId) { Effects = [Damage()] };
    internal static readonly EnemyAction Fire = new(FireId)
    {
        Cast = new() { FaceTarget = false }, Effects = [Damage()],
        Timing = new() { DamageDelay = 0.5f },
    };
    internal static readonly EnemyAction Water = new(WaterId)
    {
        Cast = new() { FaceTarget = false }, Area = new() { Size = 4f },
        Effects = [Damage()], Timing = new() { DamageDelay = 0.5f },
    };
    internal static readonly EnemyAction Inferno = new(InfernoId)
    {
        Cast = new() { FaceTarget = false }, Area = new() { Size = 4f },
        Effects = [Damage(), OnOthers(UmadActions.VacuumWave.Effects.ToArray())],
        Timing = new() { DamageDelay = 0.5f },
    };
    internal static readonly EnemyAction Tsunami = new(TsunamiId)
    {
        Cast = new() { FaceTarget = false },
        Effects = [Damage(), OnOthers(UmadActions.VacuumWave.Effects.ToArray())],
        Timing = new() { DamageDelay = 0.5f },
    };
    internal static readonly EnemyAction ThunderCircle = new(47890)
    {
        Area = new() { CastTypeOverride = CastType.Circle, Size = 14.8f },
        Effects = [Damage(Lethal)],
    };
}
