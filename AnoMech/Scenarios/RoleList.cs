using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios;

public class RoleListBuilder
{
    private static readonly Random Rng = new();

    public int Size { get; init; } = 8;
    public bool? IncludePlayer { get; init; }

    public int[] ForcePlayerIndex { get; init; } = [];

    public RoleList Build(SimParty party)
    {
        var player = party.PlayerRole;
        if (ForcePlayerIndex.Length > 0)
            return WithPlayerAt(party, player, ForcePlayerIndex[Rng.Next(ForcePlayerIndex.Length)]);

        return IncludePlayer switch
        {
            true => WithPlayerAt(party, player, Size - 1),
            false => RoleList.AllExcept(party, player).Random(Size),
            _ => RoleList.Random(party, Size),
        };
    }

    private RoleList WithPlayerAt(SimParty party, PartyRole player, int index)
    {
        var others = RoleList.AllExcept(party, player).Random(Size - 1).List;
        var list = new List<PartyRole>(Size);
        var o = 0;
        for (var i = 0; i < Size; i++)
            list.Add(i == index ? player : others[o++]);
        return new RoleList(party, list);
    }
}

public class RoleList
{
    private static readonly Random Rng = new Random();

    private readonly IReadOnlyList<PartyRole> list;
    private readonly SimParty party;

    public SimParty Party => party;
    public PartyRole[] List => list.ToArray();

    public PartyRole this[int index] => list[index];

    public RoleList(SimParty party, IReadOnlyList<PartyRole> list)
    {
        this.party = party;
        this.list = list;
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
}
