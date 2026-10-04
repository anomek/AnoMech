using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P3Intermission;

public sealed class TopP3IntermissionState
{
    private readonly Rng rng = Rng.Detached;

    public Rng Rng => rng;

    // [0..3] hold Sniper Cannon Fodder, [4,5] High-powered Sniper Cannon Fodder, [6,7] nothing.
    public IReadOnlyList<PartyRole> Debuffs { get; }
    public FirstHands FirstHands { get; }
    // The six arm spots, north first and clockwise: whether a left arm unit takes it.
    public IReadOnlyList<bool> LeftArms { get; }

    public IEnumerable<PartyRole> Spreads => Debuffs.Take(4);
    public IEnumerable<PartyRole> Stacks => Debuffs.Skip(4).Take(2);
    public IEnumerable<PartyRole> Unmarked => Debuffs.Skip(6);

    public TopP3IntermissionState(Rng rng, SimParty party, TopP3IntermissionStateOverrides overrides)
    {
        this.rng = rng;
        Debuffs = new RoleListBuilder
        {
            Slots = overrides.Debuff.Resolve(party.PlayerRole).ToDictionary(r => r.Role, r => SlotsOf(r.Value)),
        }.Build(rng, party).List;
        FirstHands = overrides.Hands ?? (rng.NextBool() ? FirstHands.North : FirstHands.South);
        LeftArms = rng.Shuffle(true, true, true, false, false, false).ToArray();
    }

    private TopP3IntermissionState(PartyRole[] debuffs, FirstHands firstHands)
    {
        Debuffs = debuffs;
        FirstHands = firstHands;
        LeftArms = new bool[6];
    }

    public static TopP3IntermissionState? FromNetworkReplay(PartyRole[]? debuffs, bool firstHandsNorth)
    {
        if (debuffs is not { Length: 8 } || !debuffs.All(Enum.IsDefined) || debuffs.Distinct().Count() != 8) return null;
        return new TopP3IntermissionState(debuffs, firstHandsNorth ? FirstHands.North : FirstHands.South);
    }

    public static float SpotBearing(int spot) => 60f * spot;

    // The first set is FirstHands's three spots, the second the other three.
    public IReadOnlyList<int> SetSpots(int set)
    {
        var start = (FirstHands == FirstHands.North ? 0 : 1) ^ set;
        return [start, start + 2, start + 4];
    }

    private static int[] SlotsOf(SniperDebuff debuff) => debuff switch
    {
        SniperDebuff.Spread => [0, 1, 2, 3],
        SniperDebuff.Stack => [4, 5],
        _ => [6, 7],
    };
}
