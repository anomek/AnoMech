using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Dsr.P3Nidhogg;

public enum JumpKind
{
    High,
    Forward,
    Backward,
}

// Dive from Grace hands out three Firsts, two Seconds and three Thirds. A group either all take a
// High Jump (land on you) or split into one of each, where Spineshatter and Elusive drop the tower
// 14y in front of or behind the jumper, along their facing. Each Gnash and Lash is in-then-out or
// out-then-in. The four towers after Drachenlance need 1 to 4 soakers, 8 in all.
public sealed class DsrP3NidhoggState
{
    public const float ArrowTowerOffset = 14f;
    public const float CounterTowerRadius = 11.31f;

    // The orders that can carry arrows, for forcing a roll; never all three or none.
    public static readonly int[][] ArrowPatterns = [[1, 2], [3], [2, 3], [1], [1, 3], [2]];

    // The four shapes come up equally often, each in any arrangement.
    public static readonly int[][] CounterTowerShapes = [[2, 2, 2, 2], [1, 1, 2, 4], [1, 1, 3, 3], [1, 2, 2, 3]];

    public IReadOnlyDictionary<PartyRole, int> Order { get; }
    public IReadOnlyDictionary<PartyRole, JumpKind> Jump { get; }
    public IReadOnlySet<int> OrdersWithArrows { get; }
    // Per Gnash and Lash: true for Gnashing Wheel first (out, then in).
    public IReadOnlyList<bool> OutFirst { get; }
    // Soakers per tower, NE, SE, SW, NW.
    public IReadOnlyList<int> CounterTowerSoakers { get; }
    public int TetherTower { get; }

    // The Dive from Grace towers, by order, filled in as each round's jumps land.
    public Dictionary<int, List<Vector3>> DiveTowers { get; } = new();
    // Nidhogg's and the clone's Soul Tethers while they are up.
    public List<(SimEnemy Source, SimTether Tether)> SoulTethers { get; } = new();

    public DsrP3NidhoggState(Rng rng, DsrP3NidhoggStateOverrides overrides)
    {
        var shuffled = rng.Shuffle(Enum.GetValues<PartyRole>());
        Order = shuffled.Select((role, i) => (role, order: i < 3 ? 1 : i < 5 ? 2 : 3)).ToDictionary(x => x.role, x => x.order);

        OrdersWithArrows = (overrides.ArrowPattern is { } forced ? ArrowPatterns[forced] : RollOrdersWithArrows(rng)).ToHashSet();
        var jump = new Dictionary<PartyRole, JumpKind>();
        foreach (var order in new[] { 1, 2, 3 })
        {
            var group = InOrder(order);
            var kinds = !OrdersWithArrows.Contains(order)
                ? group.Select(_ => JumpKind.High).ToList()
                : rng.Shuffle<JumpKind>(group.Count == 3 ? [JumpKind.High, JumpKind.Forward, JumpKind.Backward] : [JumpKind.Forward, JumpKind.Backward]).ToList();
            for (var i = 0; i < group.Count; i++) jump[group[i]] = kinds[i];
        }
        Jump = jump;

        OutFirst = [overrides.FirstOutFirst ?? rng.NextBool(), overrides.SecondOutFirst ?? rng.NextBool()];
        CounterTowerSoakers = rng.Shuffle(CounterTowerShapes[overrides.CounterTowerShape ?? rng.Next(CounterTowerShapes.Length)]).ToList();
        TetherTower = rng.Next(4);
    }

    public List<PartyRole> InOrder(int order) => Order.Where(e => e.Value == order).Select(e => e.Key).OrderBy(r => r).ToList();

    // NE, SE, SW, NW at the corners of a 16y square.
    public static Vector3 CounterTower(int index) => Compass.Point(1 + 2 * index, CounterTowerRadius);

    // Each order flips a coin for arrows. When all three land the same, the Thirds' flip is inverted,
    // so all-arrows becomes 1 & 2 and no-arrows becomes 3. That makes 1 & 2 and 3 each 2/8 and the
    // other four 1/8, which matches 1,850 FFLogs pulls: 3 24.6%, 1 & 2 24.5%, 1 14.2%, 2 & 3 13.3%,
    // 2 12.6%, 1 & 3 10.8%. Each order gets arrows half the time.
    private static int[] RollOrdersWithArrows(Rng rng)
    {
        var first = rng.NextBool();
        var second = rng.NextBool();
        var third = rng.NextBool();
        if (first == second && second == third) third = !third;
        return new[] { (1, first), (2, second), (3, third) }.Where(o => o.Item2).Select(o => o.Item1).ToArray();
    }
}
