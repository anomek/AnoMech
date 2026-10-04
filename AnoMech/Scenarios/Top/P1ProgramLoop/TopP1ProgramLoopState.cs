using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P1ProgramLoop;

// One set of two towers. Cardinals are 0 = N, 1 = E, 2 = S, 3 = W; both towers sit 3y off their
// cardinal to the same side (Shift +1 = clockwise, -1 = counter-clockwise).
public sealed record LoopTowerSet(int CardinalA, int CardinalB, int Shift)
{
    public IReadOnlyList<int> Cardinals => CardinalA < CardinalB ? [CardinalA, CardinalB] : [CardinalB, CardinalA];
    public IReadOnlyList<int> FreeCardinals => Enumerable.Range(0, 4).Except(Cardinals).ToList();

    public Vector3 Position(int cardinal) => Direction.Cardinal[cardinal].Apply(new Vector3(Shift * 3f, 0f, -12f));

    public uint LayoutId(int cardinal) => TopConstants.EObjId.LoopTowerLayoutIds[cardinal][Shift > 0 ? 0 : 1];
}

public sealed class TopP1ProgramLoopState
{
    private static readonly (int, int)[][] Pairings = [[(0, 1), (2, 3)], [(0, 2), (1, 3)], [(0, 3), (1, 2)]];

    private readonly Rng rng = Rng.Detached;

    public Rng Rng => rng;

    // InLine[2k] and InLine[2k + 1] are In Line k + 1.
    public RoleList InLine { get; }
    public IReadOnlyList<LoopTowerSet> Towers { get; }
    public IReadOnlyList<PartyRole> FirstTetherHolders { get; }

    public TopP1ProgramLoopState(Rng rng, SimParty party, TopP1ProgramLoopStateOverrides overrides)
    {
        this.rng = rng;
        InLine = new RoleListBuilder
        {
            Slots = overrides.Number.Resolve(party.PlayerRole)
                             .Where(r => r.Value is >= 1 and <= 4)
                             .ToDictionary(r => r.Role, r => new[] { 2 * r.Value - 2, 2 * r.Value - 1 }),
        }.Build(rng, party);
        Towers = RollTowers();
        FirstTetherHolders = rng.Shuffle(PerRole.All).Take(2).ToList();
    }

    private TopP1ProgramLoopState(SimParty party, PartyRole[] inLine, LoopTowerSet[] towers, PartyRole[] firstTetherHolders)
    {
        InLine = new RoleList(party, inLine);
        Towers = towers;
        FirstTetherHolders = firstTetherHolders;
    }

    public static TopP1ProgramLoopState? FromNetworkReplay(SimParty party, PartyRole[]? inLine, int[]? towerCardinals, int[]? towerShifts, PartyRole[]? firstTetherHolders)
    {
        if (inLine is not { Length: 8 } || inLine.Distinct().Count() != 8 || !inLine.All(Enum.IsDefined)
            || towerCardinals is not { Length: 8 } || towerCardinals.Any(c => c is < 0 or > 3)
            || towerShifts is not { Length: 4 } || towerShifts.Any(s => s is not (1 or -1))
            || firstTetherHolders is not { Length: 2 } || !firstTetherHolders.All(Enum.IsDefined))
            return null;
        var towers = Enumerable.Range(0, 4).Select(i => new LoopTowerSet(towerCardinals[2 * i], towerCardinals[2 * i + 1], towerShifts[i])).ToArray();
        if (towers.Any(set => set.CardinalA == set.CardinalB)) return null;
        return new(party, inLine, towers, firstTetherHolders);
    }

    public int NumberOf(PartyRole role) => InLine.List.ToList().IndexOf(role) / 2 + 1;

    public IEnumerable<PartyRole> WithNumber(int number) => [InLine[2 * number - 2], InLine[2 * number - 1]];

    // Every one of the eight tower spots is used once: the two clockwise sets split the four
    // cardinals into two pairs, and so do the two counter-clockwise sets.
    private IReadOnlyList<LoopTowerSet> RollTowers()
    {
        var pairs = new Dictionary<int, Queue<(int, int)>>
        {
            [1] = new(rng.Shuffle(rng.NextObj(Pairings))),
            [-1] = new(rng.Shuffle(rng.NextObj(Pairings))),
        };
        return rng.Shuffle(1, 1, -1, -1)
                  .Select(shift =>
                  {
                      var (a, b) = pairs[shift].Dequeue();
                      return new LoopTowerSet(a, b, shift);
                  })
                  .ToList();
    }
}
