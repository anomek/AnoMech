using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Scenarios.Tea.P3Wormhole;

// Per-run randomization: Wormhole's layout (Brute Justice north-west or north-east, the wormholes'
// pair), the Limit Cut numbers and the two Enumerations. The chakrams' and Incinerating Heat's
// targets are picked live.
public sealed class TeaP3WormholeState
{
    public static readonly PartyRole[] Dps = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];

    private readonly Rng rng = Rng.Detached;

    public TeaP3WormholeScenarioObjects Objects { get; } = new();
    public bool BruteJusticeNorthWest { get; }
    // False: the wormholes west-north-west and east-south-east, true: west-south-west and east-north-east.
    public bool PuddlesMirrored { get; }
    // Numbers[k] holds number k+1.
    public IReadOnlyList<PartyRole> Numbers { get; }
    public IReadOnlyList<PartyRole> Enumerations { get; }

    public int NumberOf(PartyRole role)
    {
        for (var k = 0; k < Numbers.Count; k++)
            if (Numbers[k] == role) return k + 1;
        return 0;
    }

    // The run's own dice, for the choices made mid-run: who a chakram aims at, who Incinerating Heat marks.
    public T PickRandom<T>(IReadOnlyList<T> values) => values[rng.Next(values.Count)];

    public TeaP3WormholeState(Rng rng, SimParty party, TeaP3WormholeStateOverrides overrides)
    {
        this.rng = rng;
        var local = party.PlayerRole;
        BruteJusticeNorthWest = overrides.BruteJusticeNorthWest ?? rng.NextBool();
        PuddlesMirrored = overrides.PuddlesMirrored ?? rng.NextBool();
        Numbers = BuildNumbers(overrides.Number.Resolve(local));
        Enumerations = PickEnumerations(overrides.Enumeration.Resolve(local));
    }

    // A forced number takes its slot outright; a second seat asking for the same one keeps the roll.
    private List<PartyRole> BuildNumbers(IReadOnlyList<(PartyRole Role, int Value)> asked)
    {
        var numbers = rng.Shuffle(PerRole.All).ToList();
        var taken = new bool[8];
        foreach (var (role, wanted) in asked)
        {
            if (wanted is < 1 or > 8) continue;
            var index = wanted - 1;
            if (taken[index])
            {
                DiagnosticLog.Warn($"[TeaP3Wormhole] {role} asked for number {wanted}, already forced for {numbers[index]} -- leaving {role} to the roll.");
                continue;
            }
            var current = numbers.IndexOf(role);
            if (current != index) (numbers[current], numbers[index]) = (numbers[index], numbers[current]);
            taken[index] = true;
        }
        return numbers;
    }

    private List<PartyRole> PickEnumerations(IReadOnlyList<(PartyRole Role, bool Value)> asked)
    {
        var yes = asked.Where(a => a.Value && a.Role.IsDps()).Select(a => a.Role).Take(2).ToList();
        var no = asked.Where(a => !a.Value).Select(a => a.Role).ToHashSet();
        var rest = rng.Shuffle(Dps.Except(yes)).OrderBy(no.Contains);
        return yes.Concat(rest).Take(2).ToList();
    }

    // A peer's shadow of the host's roll: nothing rolled.
    private TeaP3WormholeState(bool bruteJusticeNorthWest, bool puddlesMirrored, IReadOnlyList<PartyRole> numbers,
        IReadOnlyList<PartyRole> enumerations)
    {
        BruteJusticeNorthWest = bruteJusticeNorthWest;
        PuddlesMirrored = puddlesMirrored;
        Numbers = numbers;
        Enumerations = enumerations;
    }

    public static TeaP3WormholeState? FromNetworkReplay(bool bruteJusticeNorthWest, bool puddlesMirrored, PartyRole[] numbers,
        PartyRole[] enumerations)
    {
        if (numbers.Length != 8 || numbers.Distinct().Count() != 8 || numbers.Any(r => !PerRole.All.Contains(r))) return null;
        if (enumerations.Length != 2 || enumerations.Distinct().Count() != 2 || enumerations.Any(r => !r.IsDps())) return null;
        return new(bruteJusticeNorthWest, puddlesMirrored, numbers, enumerations);
    }
}
