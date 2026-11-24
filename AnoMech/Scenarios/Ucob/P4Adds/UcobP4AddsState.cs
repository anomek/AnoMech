using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Ucob.P4Adds;

public enum NaelMove
{
    Dynamo,
    Chariot,
    Beam,
    Dive,
}

// The four P4 triple quotes, with the order the moves resolve in: 5.1s, 8.2s and 11.3s after
// the line.
public sealed record NaelQuote(string Name, string Text, IReadOnlyList<NaelMove> Moves)
{
    public static readonly NaelQuote BurningEarth = new("Burning earth",
        "From hallowed moon I descend, upon burning earth to tread!",
        [NaelMove.Dynamo, NaelMove.Dive, NaelMove.Beam]);

    public static readonly NaelQuote TakeFire = new("Take fire",
        "Unbending iron, take fire and descend!",
        [NaelMove.Chariot, NaelMove.Beam, NaelMove.Dive]);

    public static readonly NaelQuote FieryEdge = new("Fiery edge",
        "Unbending iron, descend with fiery edge!",
        [NaelMove.Chariot, NaelMove.Dive, NaelMove.Beam]);

    public static readonly NaelQuote BareIron = new("Bare iron",
        "From hallowed moon I bare iron, in my descent to wield!",
        [NaelMove.Dynamo, NaelMove.Chariot, NaelMove.Dive]);

    public static readonly IReadOnlyList<NaelQuote> All = [BurningEarth, TakeFire, FieryEdge, BareIron];

    public string Simplified => string.Join(" > ", Moves.Select(m => m switch
    {
        NaelMove.Dynamo => "In",
        NaelMove.Chariot => "Out",
        NaelMove.Beam => "Stack",
        _ => "Spread",
    }));

    // The second quote always opens with the other in/out. Bare iron and Fiery edge have only
    // been seen leading to one follow-up each.
    public IReadOnlyList<NaelQuote> FollowUps => Name switch
    {
        "Burning earth" => [FieryEdge, TakeFire],
        "Take fire" => [BurningEarth, BareIron],
        "Fiery edge" => [BurningEarth],
        _ => [FieryEdge],
    };
}

// Twintania marks three of the four DPS. NA strat: M1 takes Neurolink 1, M2 Neurolink 3, R1
// Neurolink 2, and R2 fills whichever of those three links its owner left unused.
public sealed class HatchSet
{
    private static readonly PartyRole[] Dps =
        [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];

    public PartyRole Untargeted { get; }
    public IReadOnlyList<PartyRole> Targets { get; }

    public HatchSet(PartyRole untargeted)
    {
        Untargeted = untargeted;
        Targets = Dps.Where(r => r != untargeted).ToList();
    }

    public int? NeurolinkFor(PartyRole role)
    {
        if (role == Untargeted) return null;
        return role == PartyRole.CasterDps ? OwnNeurolink(Untargeted) : OwnNeurolink(role);
    }

    private static int? OwnNeurolink(PartyRole role) => role switch
    {
        PartyRole.MeleeDpsA => 0,
        PartyRole.MeleeDpsB => 2,
        PartyRole.PhysRangedDps => 1,
        _ => null,
    };
}

public sealed class UcobP4AddsState
{
    public const int TwistersPerSet = 4;

    public IReadOnlyList<NaelQuote> Quotes { get; }
    public bool SimplifiedQuotes { get; }
    public IReadOnlyList<HatchSet> Hatches { get; }
    public IReadOnlyList<IReadOnlyList<PartyRole>> TwisterTargets { get; }
    public IReadOnlyList<PartyRole> BeamTargets { get; }
    public IReadOnlyList<PartyRole> DiveTargets { get; }

    public SimEnemy? Twintania { get; set; }
    public SimEnemy? Nael { get; set; }
    public PartyRole TwintaniaTank { get; private set; } = PartyRole.MainTank;
    public PartyRole NaelTank { get; private set; } = PartyRole.OffTank;
    public event Action? TankAssignmentsChanged;

    public void AssignTanks(PartyRole twintaniaTank, PartyRole naelTank)
    {
        if (twintaniaTank == TwintaniaTank && naelTank == NaelTank) return;
        TwintaniaTank = twintaniaTank;
        NaelTank = naelTank;
        TankAssignmentsChanged?.Invoke();
    }

    public List<Vector3> LiveTwisters { get; } = new();
    public List<(PartyRole Role, Vector3 Position)>[] TwisterSnapshots { get; } = [new(), new(), new(), new()];

    public UcobP4AddsState(Rng rng, SimParty party, UcobP4AddsStateOverrides overrides)
    {
        var first = overrides.FirstQuote ?? rng.NextObj(NaelQuote.All.ToArray());
        var second = overrides.SecondQuote ?? rng.NextObj(first.FollowUps.ToArray());
        Quotes = [first, second];
        SimplifiedQuotes = overrides.SimplifiedQuotes;

        Hatches =
        [
            new HatchSet(overrides.FirstHatchUntargeted ?? rng.NextDpsRole()),
            new HatchSet(overrides.SecondHatchUntargeted ?? rng.NextDpsRole()),
        ];

        TwisterTargets = Enumerable.Range(0, 4)
            .Select(_ => (IReadOnlyList<PartyRole>)RoleList.Random(rng, party, TwistersPerSet).List)
            .ToList();

        BeamTargets = [rng.NextRole(), rng.NextRole()];
        DiveTargets = [rng.NextRole(), rng.NextRole()];
    }
}
