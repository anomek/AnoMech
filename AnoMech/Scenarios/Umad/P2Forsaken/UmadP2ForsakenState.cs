using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Forsaken;

public sealed record EndAttack(uint CastBarAction, uint KefkaResolveAction, uint CloneResolveAction, uint AllThingsEnding, float RotationOverride)
{
    public static readonly EndAttack FuturesEnd =
        new (ActionId.FutureSEnd, ActionId.FutureSEnd_Resolve, ActionId.FutureSEnd_CloneResolve, ActionId.AllThingsEnding_Future, 0);

    public static readonly EndAttack PastsEnd =
        new (ActionId.PastSEnd, ActionId.PastSEnd_Resolve, ActionId.PastSEnd_CloneResolve, ActionId.AllThingsEnding_Past, MathF.PI);
}

// Per-run randomized assignments the scenario and AI consume. Filled in the ctor
// (apply override if set, otherwise pick at random) so Run stays deterministic
// for the duration of one play. See TopP5DeltaState for the canonical shape.
public sealed class UmadP2ForsakenState
{
    private readonly Rng rng = new();

    public EndAttack[] EndAttacks { get; }
    
    public Direction NewNorth { get; }

    public int Rotation { get; }
    
    public Dictionary<PartyRole, uint> Lockons = [];

    public UmadP2ForsakenState(SimParty party, UmadP2ForsakenStateOverrides overrides)
    {
        EndAttacks = [overrides.FirstEndAttack ?? NextEnd(), NextEnd(), NextEnd(), NextEnd()];
        NewNorth = overrides.NewNorth ?? rng.NextDirection();
        Rotation = rng.NextSign();

        var supportLockon = overrides.SupportLockon ?? rng.NextObj(LockonId.ForsakenChariot, LockonId.ForsakenCone);
        var dpsLockon = supportLockon == LockonId.ForsakenChariot ? LockonId.ForsakenCone : LockonId.ForsakenChariot;

        List<PartyRole> supports = [PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer];
        List<PartyRole> dps = [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];
        supports.ForEach(role => Lockons[role] = supportLockon);
        dps.ForEach(role => Lockons[role] = dpsLockon);
        Lockons[overrides.SupportStackRole ?? rng.NextObj(supports.ToArray())] = LockonId.ForsakenStack;
        Lockons[overrides.DpsStackRole ?? rng.NextObj(dps.ToArray())] = LockonId.ForsakenStack;
        Plugin.Log.Info($"Lockon assigments: {string.Join(",", Enum.GetValues<PartyRole>().Select(r => Lockons[r]))}");
    }
    
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
