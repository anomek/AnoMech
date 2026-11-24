using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P2LightRampant.FruP2LightRampantState;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

public sealed class FruP2LightRampantJpAi : IScenarioAi<FruP2LightRampantState>, IFruP2LightRampantChainRing
{
    public string Name => "JP · Mana";
    public string Group => "JP";

    public IReadOnlyList<PartyRole> ClockwiseRing { get; } =
    [
        PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MainTank, PartyRole.OffTank,
        PartyRole.CasterDps, PartyRole.PhysRangedDps, PartyRole.MeleeDpsB, PartyRole.MeleeDpsA,
    ];

    private const float LastPuddleFadedAt = 32.9f;

    private const float S = 1f / 2.358f;
    private const float RingRadius = 8f;
    private const float ClockRadius = 8.5f;
    private const float PairRadius = 10.5f;
    private const float TowerEdgeRadius = 18f;

    private static readonly Vector3[] ClockwisePuddlesFromNortheast =
        [new(2.2f, 0, -2.2f), new(7.4f, 0, -7.8f), new(11.4f, 0, -2.6f), new(11.4f, 0, 4.6f), new(7f, 0, 10.6f)];

    private FruP2LightRampantState state = null!;
    private SimWorld world = null!;

    public void Run(FruP2LightRampantState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);
        ai.Move(3f, () => ByRole(RingAroundTheBoss), arrivalTime: 8.75f);
        ai.Move(9.75f, () => ByRole(role => state.BaitsHammer(role) ? PuddleDrop(role, 0) : null), arrivalTime: HammerSnapshotAt(0));
        ai.Move(10.75f, () => ByRole(role => state.BaitsHammer(role) ? null : TowerAtTheArenaEdge(role)), jitter: 0f);
        for (var wave = 0; wave < HammerWaves - 1; wave++)
        {
            var next = wave + 1;
            ai.Move(HammerSnapshotAt(wave) + 0.05f, () => ByRole(role => state.BaitsHammer(role) ? PuddleDrop(role, next) : null), jitter: 0f,
                arrivalTime: HammerSnapshotAt(next));
        }
        ai.Move(HammerSnapshotAt(HammerWaves - 1) + 0.05f, () => ByRole(role => state.BaitsHammer(role) ? NorthNortheastOrSouthSouthwest(role) : null),
            arrivalTime: PowerfulLightAt);
        ai.Move(19.1f, () => ByRole(role => state.BaitsHammer(role) ? null : NorthNortheastOrSouthSouthwest(role)), arrivalTime: PowerfulLightAt);
        ai.Move(PowerfulLightAt + 0.05f, () => ByRole(role => OrbSafeSpot(role, first: true)), jitter: 0f, arrivalTime: 27.1f);
        ai.Move(27.2f, () => ByRole(role => OrbSafeSpot(role, first: false)), jitter: 0f, arrivalTime: 30.1f);
        ai.Move(30.15f, () => ByRole(role => Stacks(role) == 2 ? Vector3.Zero : null), arrivalTime: 33.8f);
        ai.Move(LastPuddleFadedAt, () => ByRole(role => Stacks(role) == 2 ? null : BanishSpot(role)), arrivalTime: 37.1f);
        ai.Move(33.85f, () => ByRole(BanishSpot), arrivalTime: 37.1f);
        ai.Move(37.15f, () => ByRole(ClockSpot), arrivalTime: 45.9f);
    }

    private static float HammerSnapshotAt(int wave) => HammerAt(wave) - FruHolyPuddles.SnapshotBeforeHit;

    private int Stacks(PartyRole role) => world.Party.Get(role)?.FindStatus(StatusId.Lightsteeped)?.Stacks ?? 0;

    private static Vector3 AtOctant(float octant, float radius)
        => new(MathF.Sin(octant * MathF.PI / 4) * radius, 0, -MathF.Cos(octant * MathF.PI / 4) * radius);

    private static Vector3 FromSim(float north, float east) => new(east * S, 0, -north * S);

    private static Vector3? RingAroundTheBoss(PartyRole role) => AtOctant(role switch
    {
        PartyRole.MainTank => 0.5f,
        PartyRole.OffTank => 1.5f,
        PartyRole.CasterDps => 2.5f,
        PartyRole.PhysRangedDps => 3.5f,
        PartyRole.MeleeDpsB => 4.5f,
        PartyRole.MeleeDpsA => 5.5f,
        PartyRole.RegenHealer => 6.5f,
        _ => 7.5f,
    }, RingRadius);

    private Vector3? TowerAtTheArenaEdge(PartyRole role)
        => Vector3.Normalize(Tower(state.Index(role))) * TowerEdgeRadius;

    private Vector3? PuddleDrop(PartyRole role, int drop)
        => ClockwisePuddlesFromNortheast[drop] * (state.Index(role) == 6 ? -1 : 1);

    private Vector3? NorthNortheastOrSouthSouthwest(PartyRole role) => FromSim(43, 10.8f) * (state.NorthGroup(role) ? 1 : -1);

    private Vector3? OrbSafeSpot(PartyRole role, bool first)
    {
        var wideNorth = first == state.NorthOrbsFirst;
        return state.NorthGroup(role)
            ? FromSim(wideNorth ? 36.2f : 42, wideNorth ? 26.6f : 16.7f)
            : FromSim(wideNorth ? -42 : -36.2f, wideNorth ? -16.7f : -26.6f);
    }

    private Vector3? BanishSpot(PartyRole role) => state.Pairs ? PairSpot(role) : ClockSpot(role);

    private static Vector3? PairSpot(PartyRole role) => AtOctant(role switch
    {
        PartyRole.MainTank or PartyRole.PhysRangedDps => 7,
        PartyRole.ShieldHealer or PartyRole.CasterDps => 1,
        PartyRole.OffTank or PartyRole.MeleeDpsB => 3,
        _ => 5,
    }, PairRadius);

    private static Vector3? ClockSpot(PartyRole role) => AtOctant(role switch
    {
        PartyRole.MainTank => 0,
        PartyRole.CasterDps => 1,
        PartyRole.ShieldHealer => 2,
        PartyRole.MeleeDpsB => 3,
        PartyRole.OffTank => 4,
        PartyRole.MeleeDpsA => 5,
        PartyRole.RegenHealer => 6,
        _ => 7,
    }, ClockRadius);

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
