using System;
using System.Collections.Generic;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Top;

namespace AnoMech.Scenarios;

public sealed record MonitorSide(int Mul, uint ActionId)
{
    public static readonly MonitorSide Left = new(1, TopConstants.ActionId.OversampledWaveCannonLeft);
    public static readonly MonitorSide Right = new(-1, TopConstants.ActionId.OversampledWaveCannonRight);
}

public sealed class TopP5OmegaState
{
    private readonly Random rng = new();
    
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
        var firstAttackDirection = RandomIntercardinal();
        var secondAttackDirection = firstAttackDirection.Rotate(rng.Next(2) * 4 - 2);
        AttackDirections = [firstAttackDirection, firstAttackDirection.Flip(), secondAttackDirection, secondAttackDirection.Flip()];
        HelloWorldTargets = RoleList.Random(party, 4);
        DoubleDynamicTargets = RoleList.Random(party, 4);
        InitialTetherTargets = RoleList.Random(party, 2);
        BettleSpawnDirection = overrides.BettleSpawnDirection ?? RandomCardinal();
        MonitorSide = overrides.MonitorSide ?? (rng.Next(2) == 0 ? MonitorSide.Left : MonitorSide.Right);
        FirstWaveCannonFront = overrides.FirstWaveCannonFront ?? (rng.Next(2) == 0);
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
            if (overrides.SecondFAttack != null && overrides.SecondMAttack != null) break;
        }
        OmegaAttacks = [firstFAttack, firstMAttack, secondFAttack, secondMAttack];
    }

    private OmegaAttack RandomFAttack() => rng.Next(2) == 0 ? OmegaAttack.Legs : OmegaAttack.Staff;
    private OmegaAttack RandomMAttack() => rng.Next(2) == 0 ? OmegaAttack.Shield : OmegaAttack.Sword;

    // EightWayDirection.All indices: 0=N, 2=E, 4=S, 6=W are cardinals;
    // 1=NE, 3=SE, 5=SW, 7=NW are intercardinals.
    private EightWayDirection RandomCardinal() => EightWayDirection.Cardinal[rng.Next(4)];
    private EightWayDirection RandomIntercardinal() => EightWayDirection.Intercardinal[rng.Next(4)];
}
