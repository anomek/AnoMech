using System.Collections.Generic;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P5Omega;

public sealed record MonitorSide(int Mul, uint ActionId)
{
    public static readonly MonitorSide Left = new(1, TopConstants.ActionId.OversampledWaveCannonLeft);
    public static readonly MonitorSide Right = new(-1, TopConstants.ActionId.OversampledWaveCannonRight);
}

public sealed class TopP5OmegaState
{
    private readonly Rng rng = new();
    
    public RoleList HelloWorldTargets { get; }
    public RoleList DoubleDynamicTargets { get; }
    public RoleList InitialTetherTargets { get; }

    public IReadOnlyList<EightWayDirection> AttackDirections { get; }
    public IReadOnlyList<OmegaAttack> OmegaAttacks { get; } 
    public EightWayDirection BettleSpawnDirection { get; }
    public bool FirstWaveCannonFront { get; }

    public MonitorSide MonitorSide { get; }

    public TopP5OmegaState(SimParty party, TopP5OmegaStateOverrides overrides)
    {
        var firstAttackDirection = rng.NextIntercardinal();
        var secondAttackDirection = firstAttackDirection.Rotate(rng.NextSign() * 2);
        AttackDirections = [firstAttackDirection, firstAttackDirection.Flip(), secondAttackDirection, secondAttackDirection.Flip()];
        HelloWorldTargets = new RoleListBuilder
        {
            Size = 4,
            ForcePlayerIndex = (overrides.HelloWorldOrder, overrides.HelloWorldType) switch
            {
                (HelloWorldOrderOption.Auto,   HelloWorldTypeOption.Near) => [0, 2],
                (HelloWorldOrderOption.Auto,   HelloWorldTypeOption.Far)  => [1, 3],
                (HelloWorldOrderOption.Any,    HelloWorldTypeOption.Auto) => [0, 1, 2, 3],
                (HelloWorldOrderOption.Any,    HelloWorldTypeOption.Near) => [0, 2],
                (HelloWorldOrderOption.Any,    HelloWorldTypeOption.Far)  => [1, 3],
                (HelloWorldOrderOption.First,  HelloWorldTypeOption.Auto) => [0, 1],
                (HelloWorldOrderOption.First,  HelloWorldTypeOption.Near) => [0],
                (HelloWorldOrderOption.First,  HelloWorldTypeOption.Far)  => [1],
                (HelloWorldOrderOption.Second, HelloWorldTypeOption.Auto) => [2, 3],
                (HelloWorldOrderOption.Second, HelloWorldTypeOption.Near) => [2],
                (HelloWorldOrderOption.Second, HelloWorldTypeOption.Far)  => [3],
                _ => [],
            },
            IncludePlayer = overrides.HelloWorldOrder == HelloWorldOrderOption.None ? false : null,
        }.Build(party);
        DoubleDynamicTargets = new RoleListBuilder
        {
            Size = 4,
            IncludePlayer = overrides.ExtraDynamis,
        }.Build(party);
        InitialTetherTargets = RoleList.Random(party, 2);
        BettleSpawnDirection = overrides.BettleSpawnDirection ?? rng.NextCardinal();
        MonitorSide = overrides.MonitorSide ?? rng.NextObj(MonitorSide.Left, MonitorSide.Right);
        FirstWaveCannonFront = overrides.FirstWaveCannonFront ?? rng.NextBool();
        var firstFAttack = overrides.FirstFAttack ?? RandomFAttack();
        var firstMAttack = overrides.FirstMAttack ?? RandomMAttack();
        OmegaAttack secondFAttack;
        OmegaAttack secondMAttack;
        while (true)
        {
            secondFAttack = overrides.SecondFAttack ?? RandomFAttack();
            secondMAttack = overrides.SecondMAttack ?? RandomMAttack();
            if ((firstFAttack, firstMAttack) != (secondFAttack, secondMAttack)) break;
            // Both seconds user-set to match firsts: trust the user.
            if (overrides is { SecondFAttack: not null, SecondMAttack: not null }) break;
        }
        OmegaAttacks = [firstFAttack, firstMAttack, secondFAttack, secondMAttack];
    }

    private OmegaAttack RandomFAttack() => rng.NextObj(OmegaAttack.Legs, OmegaAttack.Staff);
    private OmegaAttack RandomMAttack() => rng.NextObj(OmegaAttack.Shield, OmegaAttack.Sword);
}
