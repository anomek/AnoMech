using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Forsaken;

// Only CastBarAction goes over the wire; FromNetworkReplay maps it back to the static instance.
public sealed record EndAttack(uint CastBarAction)
{
    // Future's End cleaves the half facing the player as the cast starts, Past's End the other half.
    public static readonly EndAttack FuturesEnd = new(ActionId.FutureSEnd)
    {
        KefkaHit = EndHit(ActionId.FutureSEnd_Resolve),
        CloneHit = EndHit(ActionId.FutureSEnd_CloneResolve),
        AllThingsEnding = AllThingsEndingHit(ActionId.AllThingsEnding_Future, 0f),
    };

    public static readonly EndAttack PastsEnd = new(ActionId.PastSEnd)
    {
        KefkaHit = EndHit(ActionId.PastSEnd_Resolve),
        CloneHit = EndHit(ActionId.PastSEnd_CloneResolve),
        AllThingsEnding = AllThingsEndingHit(ActionId.AllThingsEnding_Past, MathF.PI),
    };

    [JsonIgnore] public EnemyAction KefkaHit { get; private init; } = null!;
    [JsonIgnore] public EnemyAction CloneHit { get; private init; } = null!;
    [JsonIgnore] public EnemyAction AllThingsEnding { get; private init; } = null!;

    public static EndAttack FromCastBarAction(uint castBarAction)
        => castBarAction == PastsEnd.CastBarAction ? PastsEnd : FuturesEnd;

    private static EnemyAction EndHit(uint actionId) => new(actionId)
    {
        Cast = new() { AnimationLock = 6f },
        Effects = [Damage(UmadActions.Magic), UmadActions.LongMagicVulnerabilityUp],
        Timing = new() { DamageDelay = 0.75f },
    };

    // Snapshots as the ability lands, not at bar end: from bar end there is no time left to reach
    // the tower that follows. UNVERIFIED in game.
    private static EnemyAction AllThingsEndingHit(uint actionId, float rotation) => new(actionId)
    {
        Cast = new() { AnimationLock = 3f, Rotation = rotation },
        Area = new() { Size = Geometry.AllThingsEndHalfCone },
        Effects = [Damage(UmadActions.Magic, Lethal)],
        Timing = new() { ResolveSnapshotOffset = CastSpec.ActionEffectOffset },
    };
}

// Per-run randomized assignments the scenario and AI consume. Filled in the ctor
// (apply override if set, otherwise pick at random) so Run stays deterministic
// for the duration of one play. See TopP5DeltaState for the canonical shape.
public sealed class UmadP2ForsakenState
{
    private readonly Rng rng = Rng.Detached;

    public EndAttack[] EndAttacks { get; }
    
    public Direction NewNorth { get; }

    public int Rotation { get; }

    public bool ReassignLockonsInRoleOrder { get; }
    
    public Dictionary<PartyRole, uint> Lockons = [];

    public UmadP2ForsakenState(Rng rng, SimParty party, UmadP2ForsakenStateOverrides overrides)
    {
        this.rng = rng;
        EndAttacks = overrides.EndAttacks.Select(e => e ?? NextEnd()).ToArray();
        NewNorth = overrides.NewNorth ?? rng.NextDirection();
        Rotation = overrides.Rotation ?? rng.NextSign();
        ReassignLockonsInRoleOrder = overrides.ReassignLockonsInRoleOrder;

        var supportLockon = overrides.SupportLockon ?? rng.NextObj(LockonId.ForsakenChariot, LockonId.ForsakenCone);
        var dpsLockon = supportLockon == LockonId.ForsakenChariot ? LockonId.ForsakenCone : LockonId.ForsakenChariot;

        List<PartyRole> supports = [PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer];
        List<PartyRole> dps = [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];
        supports.ForEach(role => Lockons[role] = supportLockon);
        dps.ForEach(role => Lockons[role] = dpsLockon);
        Lockons[overrides.SupportStackRole ?? rng.NextObj(supports.ToArray())] = LockonId.ForsakenStack;
        Lockons[overrides.DpsStackRole ?? rng.NextObj(dps.ToArray())] = LockonId.ForsakenStack;
        DiagnosticLog.Info($"Lockon assigments: {string.Join(",", Enum.GetValues<PartyRole>().Select(r => Lockons[r]))}");
    }

    // Network-replay constructor: reconstructs the full state from values the
    // host already rolled and broadcast, instead of drawing fresh RNG. Used
    // exclusively by a peer's local "debug: bot controls my character" mode
    // (see MultiplayerManager) so its AI choreography matches what a
    // host-side bot in that role would actually do. Unlike UmadP3BlackHoleState,
    // every public property here is a plain value (no live SimEnemy handles),
    // so this reconstructs everything rather than a curated subset -- there's
    // no per-strat "only some fields are read" distinction to worry about
    // across P2's 7 debug-bot Ai variants.
    private UmadP2ForsakenState(EndAttack[] endAttacks, Direction newNorth, int rotation, Dictionary<PartyRole, uint> lockons)
    {
        EndAttacks = endAttacks;
        NewNorth = newNorth;
        Rotation = rotation;
        Lockons = lockons;
    }

    public static UmadP2ForsakenState FromNetworkReplay(
        EndAttack[] endAttacks, float newNorthRadians, int rotation, Dictionary<PartyRole, uint> lockons)
        => new(endAttacks.Select(e => EndAttack.FromCastBarAction(e.CastBarAction)).ToArray(), new Direction(newNorthRadians), rotation, lockons);

    public (Direction, Direction) GetTowers(int index)
    {
        var north =  NewNorthAt(index);
        return (north.Rotate(-1), north.Rotate(1));
    }

    public Direction NewNorthAt(int index)
    {
        return NewNorth.Rotate(index*Rotation);
    }
    
    private EndAttack NextEnd()
    {
        return rng.NextObj(EndAttack.FuturesEnd, EndAttack.PastsEnd);
    }

}
