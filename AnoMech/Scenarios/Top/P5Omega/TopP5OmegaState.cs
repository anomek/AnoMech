using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P5Omega;

public sealed record MonitorSide(int Mul, uint ActionId)
{
    public static readonly MonitorSide Left = new(1, TopConstants.ActionId.DeltaOversampledWaveCannonLeft);
    public static readonly MonitorSide Right = new(-1, TopConstants.ActionId.DeltaOversampledWaveCannonRight);
}

public sealed class TopP5OmegaState
{
    private readonly Rng rng = Rng.Detached;

    public Rng Rng => rng;
    
    public RoleList HelloWorldTargets { get; }
    public RoleList DoubleDynamicTargets { get; }
    // Resolved here, not in the Ai: a peer's replay would re-roll it and disagree with the host.
    public RoleList MonitorTargets { get; }
    // The four outside HelloWorldTargets[0..1] and MonitorTargets, in first Hello World jump
    // order; resolved here for the same reason.
    public RoleList HelloWorld1JumpOrder { get; }
    public RoleList BlasterTetherTargets { get; }

    public IReadOnlyList<Direction> AttackDirections { get; }
    public IReadOnlyList<OmegaAttack> OmegaAttacks { get; } 
    public Direction BettleSpawnDirection { get; }
    public bool FirstWaveCannonFront { get; }

    public MonitorSide MonitorSide { get; }

    public TopP5OmegaState(Rng rng, SimParty party, TopP5OmegaStateOverrides overrides)
    {
        this.rng = rng;
        var firstAttackDirection = overrides.FirstAttackDirection ?? rng.NextIntercardinal();
        var secondAttackDirection = overrides.SecondAttackDirection ?? firstAttackDirection.Rotate(rng.NextSign() * 2);
        AttackDirections = [firstAttackDirection, firstAttackDirection.Flip(), secondAttackDirection, secondAttackDirection.Flip()];
        var (helloSlots, helloMembership) = overrides.ResolveHelloWorld(party.PlayerRole);
        HelloWorldTargets = new RoleListBuilder
        {
            Size = 4,
            Slots = helloSlots,
            Membership = helloMembership,
        }.Build(rng, party);
        DoubleDynamicTargets = new RoleListBuilder
        {
            Size = 4,
            Membership = overrides.ResolveExtraDynamis(party.PlayerRole),
        }.Build(rng, party);
        MonitorTargets = new RoleList(party, overrides.MonitorTargets ?? ResolveMonitorTargets());
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
        HelloWorld1JumpOrder = overrides.HelloWorld1JumpOrder is { } pinnedJumpOrder
            ? new RoleList(party, pinnedJumpOrder)
            : new RoleList(party, Enum.GetValues<PartyRole>())
                .Random(rng, 4, MonitorTargets[0], MonitorTargets[1], HelloWorldTargets[0], HelloWorldTargets[1]);
        BlasterTetherTargets = overrides.BlasterTethers is { } pinnedBlasterTethers
            ? new RoleList(party, pinnedBlasterTethers)
            : RoleList.Random(rng, party, 2);
        HelloWorld2 = overrides.HelloWorld2?.ToArray();
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

    // Network replay: only the fields TopP5OmegaAi reads; MonitorSide travels as a bool naming
    // the static instance.
    private TopP5OmegaState(
        SimParty party, PartyRole[] helloWorldTargets, PartyRole[] doubleDynamicTargets, PartyRole[] monitorTargets,
        PartyRole[] helloWorld1JumpOrder, float[] attackDirectionsRadians, OmegaAttack[] omegaAttacks,
        float bettleSpawnDirectionRadians, bool firstWaveCannonFront, bool monitorIsLeft)
    {
        HelloWorldTargets = new RoleList(party, helloWorldTargets);
        DoubleDynamicTargets = new RoleList(party, doubleDynamicTargets);
        MonitorTargets = new RoleList(party, monitorTargets);
        HelloWorld1JumpOrder = new RoleList(party, helloWorld1JumpOrder);
        BlasterTetherTargets = RoleList.Empty();
        AttackDirections = attackDirectionsRadians.Select(r => new Direction(r)).ToList();
        OmegaAttacks = omegaAttacks;
        BettleSpawnDirection = new Direction(bettleSpawnDirectionRadians);
        FirstWaveCannonFront = firstWaveCannonFront;
        MonitorSide = monitorIsLeft ? MonitorSide.Left : MonitorSide.Right;
    }

    public static TopP5OmegaState FromNetworkReplay(
        SimParty party, PartyRole[] helloWorldTargets, PartyRole[] doubleDynamicTargets, PartyRole[] monitorTargets,
        PartyRole[] helloWorld1JumpOrder, float[] attackDirectionsRadians, OmegaAttack[] omegaAttacks,
        float bettleSpawnDirectionRadians, bool firstWaveCannonFront, bool monitorIsLeft)
        => new(party, helloWorldTargets, doubleDynamicTargets, monitorTargets, helloWorld1JumpOrder,
               attackDirectionsRadians, omegaAttacks, bettleSpawnDirectionRadians, firstWaveCannonFront, monitorIsLeft);

    // Resolved by the host at t=40.5s and broadcast via TopP5OmegaHelloWorld2UpdateMessage.
    public PartyRole[]? HelloWorld2;
}
