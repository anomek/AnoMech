using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios;

// Builds one of a mechanic's target lists, honouring however many seats asked to be in it, out
// of it, or at a particular index. Every request is per seat: several players can each ask for
// their own spot, and conflicts resolve toward the earlier seat rather than silently replacing
// the others (see PerRoleSetting).
public class RoleListBuilder
{
    private static readonly Random Rng = new();
    private static readonly Dictionary<PartyRole, bool> NoMembership = new();
    private static readonly Dictionary<PartyRole, int[]> NoSlots = new();

    public int Size { get; init; } = 8;

    // role -> must be in the list, or must be kept out of it. A role in Slots is implicitly in.
    public IReadOnlyDictionary<PartyRole, bool>? Membership { get; init; }

    // role -> the indices it may occupy. Two roles wanting the same one: the earlier seat takes
    // it, the other falls back to any free index.
    public IReadOnlyDictionary<PartyRole, int[]>? Slots { get; init; }

    public RoleList Build(SimParty party)
    {
        var membership = Membership ?? NoMembership;
        var slots = Slots ?? NoSlots;
        var pool = Enum.GetValues<PartyRole>().Shuffle().ToList();

        // Seat order, so dropping the overflow is predictable rather than luck of the shuffle.
        var required = pool.Where(r => slots.ContainsKey(r) || membership.GetValueOrDefault(r))
                           .OrderBy(r => (int)r)
                           .Take(Size)
                           .ToList();
        var requiredSet = required.ToHashSet();
        var excluded = pool.Where(r => membership.TryGetValue(r, out var wanted) && !wanted && !requiredSet.Contains(r))
                           .ToHashSet();

        var chosen = new List<PartyRole>(required);
        var chosenSet = new HashSet<PartyRole>(required);
        foreach (var role in pool)
        {
            if (chosen.Count >= Size) break;
            if (chosenSet.Contains(role) || excluded.Contains(role)) continue;
            chosen.Add(role);
            chosenSet.Add(role);
        }
        // More seats asked to stay out than the list can spare; it still has to be Size long.
        foreach (var role in pool)
        {
            if (chosen.Count >= Size) break;
            if (chosenSet.Add(role)) chosen.Add(role);
        }

        var placed = new PartyRole?[Size];
        var placedSet = new HashSet<PartyRole>();
        foreach (var role in chosen.Where(slots.ContainsKey).OrderBy(r => (int)r))
        {
            var free = slots[role].Where(i => i >= 0 && i < Size && placed[i] == null).ToArray();
            if (free.Length == 0) continue;   // taken by an earlier seat; falls through to the fill
            placed[free[Rng.Next(free.Length)]] = role;
            placedSet.Add(role);
        }

        var rest = chosen.Where(r => !placedSet.Contains(r)).ToList();
        var next = 0;
        for (var i = 0; i < Size; i++)
            if (placed[i] == null) placed[i] = rest[next++];
        return new RoleList(party, placed.Select(r => r!.Value).ToList());
    }
}

public class RoleList
{
    private static readonly Random Rng = new Random();

    private readonly List<PartyRole> list;
    private readonly SimParty party;

    public SimParty Party => party;
    public PartyRole[] List => list.ToArray();

    public PartyRole this[int index] => list[index];

    public RoleList(SimParty party, IReadOnlyList<PartyRole> list)
    {
        this.party = party;
        this.list = new List<PartyRole>(list);
    }

    public static RoleList Random(SimParty party)
    {
        return Random(party, 8);
    }

    public static RoleList Random(SimParty party, int count)
    {
        HashSet<int> set = [];
        List<PartyRole> list = [];
        while (list.Count < count)
        {
            var next = Rng.Next(8);
            if (set.Add(next))
                list.Add((PartyRole)next);
        }

        return new RoleList(party, list);
    }
    
    // first 4 slots - random supports, last 4 slots - random dps
    public static RoleList RandomRoleStable(SimParty party)
    {
        var supports = Enumerable.Range(0, 4)
                                             .Select(i => (PartyRole)i)
                                             .Shuffle();
        var dps = Enumerable.Range(4, 4)
                                 .Select(i => (PartyRole)i)
                                 .Shuffle();
        var all = supports.Concat(dps).ToList();
        return new RoleList(party, all);
    }

    public static RoleList AllExcept(SimParty party, params PartyRole[] roles)
    {
        var set = new HashSet<PartyRole>(roles);
        var list = Enum.GetValues<PartyRole>()
                       .Where(role => !set.Contains(role))
                       .Shuffle()
                       .ToList();
        return new RoleList(party, list);
    }

    public RoleList Random(int count, params PartyRole[] except)
    {
        var exceptSet = new HashSet<PartyRole>(except);
        var pool = list.Where(r => !exceptSet.Contains(r)).ToList();
        var picked = new List<PartyRole>(count);
        while (picked.Count < count && pool.Count > 0)
        {
            var idx = Rng.Next(pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        return new RoleList(party, picked);
    }

    // Same pick-without-replacement as the parameterless overload, but from a caller-supplied
    // Rng instead of this class's own unseeded static one.
    public RoleList Random(Rng rng, int count, params PartyRole[] except)
    {
        var exceptSet = new HashSet<PartyRole>(except);
        var pool = list.Where(r => !exceptSet.Contains(r)).ToList();
        var picked = new List<PartyRole>(count);
        while (picked.Count < count && pool.Count > 0)
        {
            var idx = rng.NextInt(pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        return new RoleList(party, picked);
    }


    public List<TResult?> ForEachPair<TResult>(Func<int, SimCharacter, SimCharacter, TResult> func)
    {
        return Enumerable.Range(0, list.Count / 2)
                         .Select(i =>
                         {
                             if (party.Get(list[2 * i]) is { } chara1 && party.Get(list[2 * i + 1]) is { } chara2)
                                 return func(i, chara1, chara2);
                             else
                                 return default;
                         })
                         .ToList();
    }

    public List<TResult?> ForEachPair<TResult>(Func<SimCharacter, SimCharacter, TResult> func)
    {
        return ForEachPair((i, p1, p2) => func(p1, p2));
    }

    public void ForEachPair(Action<int, SimCharacter, SimCharacter> action)
    {
        ForEachPair((i, p1, p2) =>
        {
            action(i, p1, p2);
            return 0;
        });
    }


    public void ForEach(Action<int, SimCharacter> action)
    {
        for (var i = 0; i < list.Count; i++)
            if (party.Get(list[i]) is { } chara)
                action(i, chara);
    }

    public void ForEach(Action<SimCharacter> action)
    {
        ForEach((_, member) => action(member));
    }

    public SimCharacter? Get(int i)
    {
        return party.Get(list[i]);
    }

    public bool Contains(PartyRole role)
    {
        return list.Contains(role);
    }

    public static RoleList Empty()
    {
        return new RoleList(SimParty.Empty, Array.Empty<PartyRole>());
    }

    public void Swap(int i, int i1)
    {
        (list[i], list[i1])  = (list[i1], list[i]);
    }
}
