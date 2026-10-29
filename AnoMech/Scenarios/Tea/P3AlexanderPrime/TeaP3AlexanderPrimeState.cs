using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Scenarios.Tea.P3AlexanderPrime;

// Per-run randomization: which four players the Plasmaspheres tether and to which orb (the other
// four drop crystals), Inception's debuffs (Restraining Order on a tank and a DPS, Shared Sentence
// on a DPS, Aggravated Assault on the other two), which edge Alexander Prime takes for Inception,
// where the True Heart takes him for Sacrament, Wormhole's layout (Brute Justice north-west or north-east, the wormholes' pair), the
// Limit Cut numbers and the two Enumerations. The chakrams' and Incinerating Heat's targets are
// picked live.
public sealed class TeaP3AlexanderPrimeState
{
    public static readonly PartyRole[] Dps = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
    public const int Orbs = 4;
    public const int SacramentSpots = 8;

    private readonly Rng rng = Rng.Detached;

    public TeaP3AlexanderPrimeScenarioObjects Objects { get; } = new();
    public P3Practice Practice { get; }
    // OrbHolders[k] is tethered to orb k.
    public IReadOnlyList<PartyRole> OrbHolders { get; }
    public PartyRole RestrainingOrderTank { get; }
    public PartyRole RestrainingOrderDps { get; }
    public PartyRole SharedSentence { get; }
    // False: Alexander Prime on the south edge and the crystals north, true: all of it mirrored.
    public bool AlexanderPrimeNorth { get; }
    // 0..7: 22.5° + 45° × k clockwise from north.
    public int SacramentSpot { get; }
    public bool BruteJusticeNorthWest { get; }
    // False: the wormholes west-north-west and east-south-east, true: west-south-west and east-north-east.
    public bool PuddlesMirrored { get; }
    // Numbers[k] holds number k+1.
    public IReadOnlyList<PartyRole> Numbers { get; }
    public IReadOnlyList<PartyRole> Enumerations { get; }

    public IEnumerable<PartyRole> CrystalHolders => PerRole.All.Except(OrbHolders);
    public IEnumerable<PartyRole> AggravatedAssault => Dps.Except([RestrainingOrderDps, SharedSentence]);

    public int NumberOf(PartyRole role)
    {
        for (var k = 0; k < Numbers.Count; k++)
            if (Numbers[k] == role) return k + 1;
        return 0;
    }

    // The run's own dice, for the choices made mid-run: who a chakram aims at, who Incinerating Heat marks.
    public T PickRandom<T>(IReadOnlyList<T> values) => values[rng.Next(values.Count)];

    public TeaP3AlexanderPrimeState(Rng rng, SimParty party, TeaP3AlexanderPrimeStateOverrides overrides)
    {
        this.rng = rng;
        var local = party.PlayerRole;
        Practice = overrides.Practice;
        OrbHolders = PickOrbHolders(overrides.Orb.Resolve(local));
        RestrainingOrderTank = overrides.RestrainingOrderTank is { } tank && tank.IsTank() ? tank : rng.NextTankRole();
        (RestrainingOrderDps, SharedSentence) = PickDpsDebuffs(overrides.Debuff.Resolve(local));
        SacramentSpot = overrides.SacramentSpot is >= 0 and < SacramentSpots ? overrides.SacramentSpot.Value : rng.NextInt(SacramentSpots);
        BruteJusticeNorthWest = overrides.BruteJusticeNorthWest ?? rng.NextBool();
        PuddlesMirrored = overrides.PuddlesMirrored ?? rng.NextBool();
        Numbers = BuildNumbers(overrides.Number.Resolve(local));
        Enumerations = PickEnumerations(overrides.Enumeration.Resolve(local));
        AlexanderPrimeNorth = overrides.AlexanderPrimeNorth ?? rng.NextBool();
    }

    // Any four, those asking first, those refusing last; each draws its orb at random.
    private List<PartyRole> PickOrbHolders(IReadOnlyList<(PartyRole Role, bool Value)> asked)
    {
        var yes = asked.Where(a => a.Value).Select(a => a.Role).Take(Orbs).ToList();
        var no = asked.Where(a => !a.Value).Select(a => a.Role).ToHashSet();
        var rest = rng.Shuffle(PerRole.All.Except(yes)).OrderBy(no.Contains);
        return rng.Shuffle(yes.Concat(rest).Take(Orbs)).ToList();
    }

    private (PartyRole RestrainingOrder, PartyRole SharedSentence) PickDpsDebuffs(IReadOnlyList<(PartyRole Role, InceptionDebuff Value)> asked)
    {
        var order = rng.Shuffle(Dps).ToList();
        var restraining = Asked(InceptionDebuff.RestrainingOrder);
        var shared = Asked(InceptionDebuff.SharedSentence);
        var assaulted = asked.Where(a => a.Value == InceptionDebuff.AggravatedAssault && a.Role.IsDps()).Select(a => a.Role).ToHashSet();
        if (restraining is null) restraining = order.FirstOrDefault(r => r != shared && !assaulted.Contains(r), order.First(r => r != shared));
        if (shared is null) shared = order.FirstOrDefault(r => r != restraining && !assaulted.Contains(r), order.First(r => r != restraining));
        return (restraining.Value, shared.Value);

        PartyRole? Asked(InceptionDebuff debuff)
            => asked.Where(a => a.Value == debuff && a.Role.IsDps()).Select(a => (PartyRole?)a.Role).FirstOrDefault();
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
                DiagnosticLog.Warn($"[TeaP3AlexanderPrime] {role} asked for number {wanted}, already forced for {numbers[index]} -- leaving {role} to the roll.");
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
    private TeaP3AlexanderPrimeState(P3Practice practice, IReadOnlyList<PartyRole> orbHolders, PartyRole restrainingOrderTank,
        PartyRole restrainingOrderDps, PartyRole sharedSentence, bool alexanderPrimeNorth, int sacramentSpot, bool bruteJusticeNorthWest,
        bool puddlesMirrored, IReadOnlyList<PartyRole> numbers, IReadOnlyList<PartyRole> enumerations)
    {
        Practice = practice;
        OrbHolders = orbHolders;
        RestrainingOrderTank = restrainingOrderTank;
        RestrainingOrderDps = restrainingOrderDps;
        SharedSentence = sharedSentence;
        AlexanderPrimeNorth = alexanderPrimeNorth;
        SacramentSpot = sacramentSpot;
        BruteJusticeNorthWest = bruteJusticeNorthWest;
        PuddlesMirrored = puddlesMirrored;
        Numbers = numbers;
        Enumerations = enumerations;
    }

    public static TeaP3AlexanderPrimeState? FromNetworkReplay(P3Practice practice, PartyRole[] orbHolders, PartyRole restrainingOrderTank,
        PartyRole restrainingOrderDps, PartyRole sharedSentence, bool alexanderPrimeNorth, int sacramentSpot, bool bruteJusticeNorthWest,
        bool puddlesMirrored, PartyRole[] numbers, PartyRole[] enumerations)
    {
        if (!System.Enum.IsDefined(practice)) return null;
        if (orbHolders.Length != Orbs || orbHolders.Distinct().Count() != Orbs || orbHolders.Any(r => !PerRole.All.Contains(r))) return null;
        if (!restrainingOrderTank.IsTank() || !restrainingOrderDps.IsDps() || !sharedSentence.IsDps() || restrainingOrderDps == sharedSentence) return null;
        if (sacramentSpot is < 0 or >= SacramentSpots) return null;
        if (numbers.Length != 8 || numbers.Distinct().Count() != 8 || numbers.Any(r => !PerRole.All.Contains(r))) return null;
        if (enumerations.Length != 2 || enumerations.Distinct().Count() != 2 || enumerations.Any(r => !r.IsDps())) return null;
        return new(practice, orbHolders, restrainingOrderTank, restrainingOrderDps, sharedSentence, alexanderPrimeNorth, sacramentSpot,
            bruteJusticeNorthWest, puddlesMirrored, numbers, enumerations);
    }
}
