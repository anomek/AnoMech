using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
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
    public RoleList MonitorTargets { get; }
    // The four outside HelloWorldTargets[0..1] and MonitorTargets, in first Hello World jump
    // order.
    public RoleList HelloWorld1JumpOrder { get; }

    public IReadOnlyList<Direction> AttackDirections { get; }
    public IReadOnlyList<OmegaAttack> OmegaAttacks { get; } 
    public Direction BettleSpawnDirection { get; }
    public bool FirstWaveCannonFront { get; }

    public MonitorSide MonitorSide { get; }

    public TopP5OmegaState(SimParty party, TopP5OmegaStateOverrides overrides)
    {
        var firstAttackDirection = rng.NextIntercardinal();
        var secondAttackDirection = firstAttackDirection.Rotate(rng.NextSign() * 2);
        AttackDirections = [firstAttackDirection, firstAttackDirection.Flip(), secondAttackDirection, secondAttackDirection.Flip()];
        var (helloSlots, helloMembership) = overrides.ResolveHelloWorld(party.PlayerRole);
        HelloWorldTargets = new RoleListBuilder
        {
            Size = 4,
            Slots = helloSlots,
            Membership = helloMembership,
        }.Build(party);
        DoubleDynamicTargets = new RoleListBuilder
        {
            Size = 4,
            Membership = overrides.ResolveExtraDynamis(party.PlayerRole),
        }.Build(party);
        MonitorTargets = new RoleList(party, ResolveMonitorTargets());
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
        HelloWorld1JumpOrder = new RoleList(party, Enum.GetValues<PartyRole>())
            .Random(rng, 4, MonitorTargets[0], MonitorTargets[1], HelloWorldTargets[0], HelloWorldTargets[1]);
    }

    private OmegaAttack RandomFAttack() => rng.NextObj(OmegaAttack.Legs, OmegaAttack.Staff);
    private OmegaAttack RandomMAttack() => rng.NextObj(OmegaAttack.Shield, OmegaAttack.Sword);

    private List<PartyRole> ResolveMonitorTargets()
    {
        List<PartyRole> mustTakeMonitor = [];
        List<PartyRole> canTakeMonitor = [];
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (HelloWorldTargets[0] == role || HelloWorldTargets[1] == role) continue;
            if (!DoubleDynamicTargets.Contains(role)) continue;
            if (HelloWorldTargets[2] == role || HelloWorldTargets[3] == role)
                mustTakeMonitor.Add(role);
            else
                canTakeMonitor.Add(role);
        }
        while (mustTakeMonitor.Count < 2)
        {
            var selected = canTakeMonitor[rng.NextInt(canTakeMonitor.Count)];
            canTakeMonitor.Remove(selected);
            mustTakeMonitor.Add(selected);
        }
        return rng.Shuffle(mustTakeMonitor.ToArray()).ToList();
    }

    // Resolved live at t=46s.
    public PartyRole[]? HelloWorld2;
}
