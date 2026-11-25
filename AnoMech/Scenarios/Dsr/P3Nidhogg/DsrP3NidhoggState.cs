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
    // Who the first Drachenlance is aimed at. Only the host reads it, so a peer's copy keeps the default.
    public PartyRole FirstDrachenlanceTarget { get; }

    // The Dive from Grace towers, by order, filled in as each round's jumps land.
    public Dictionary<int, List<Vector3>> DiveTowers { get; } = new();
    // Nidhogg's and the clone's Soul Tethers while they are up.
    public List<(SimEnemy Source, SimTether Tether)> SoulTethers { get; } = new();

    public DsrP3NidhoggState(Rng rng, PartyRole playerRole, DsrP3NidhoggStateOverrides overrides)
    {
        var shuffled = rng.Shuffle(Enum.GetValues<PartyRole>());
        var picks = Picks(overrides, playerRole);
        OrdersWithArrows = (overrides.ArrowPattern is { } forced ? ArrowPatterns[forced] : RollOrdersWithArrows(rng, picks)).ToHashSet();

        var slots = Slots(OrdersWithArrows, rng);
        var seated = SeatPicks(picks, slots, rng);
        var openSlots = Enumerable.Range(0, slots.Count).Where(i => !seated.ContainsValue(i)).GetEnumerator();
        foreach (var role in shuffled.Where(r => !seated.ContainsKey(r)))
        {
            openSlots.MoveNext();
            seated[role] = openSlots.Current;
        }
        Order = seated.ToDictionary(e => e.Key, e => slots[e.Value].Order);
        Jump = seated.ToDictionary(e => e.Key, e => slots[e.Value].Kind);

        OutFirst = [overrides.FirstOutFirst ?? rng.NextBool(), overrides.SecondOutFirst ?? rng.NextBool()];
        CounterTowerSoakers = rng.Shuffle(CounterTowerShapes[overrides.CounterTowerShape ?? rng.Next(CounterTowerShapes.Length)]).ToList();
        TetherTower = rng.Next(4);
        FirstDrachenlanceTarget = rng.NextRole();
    }

    private DsrP3NidhoggState(
        IReadOnlyDictionary<PartyRole, int> order, IReadOnlyDictionary<PartyRole, JumpKind> jump, IReadOnlySet<int> ordersWithArrows,
        IReadOnlyList<bool> outFirst, IReadOnlyList<int> counterTowerSoakers, int tetherTower)
    {
        Order = order;
        Jump = jump;
        OrdersWithArrows = ordersWithArrows;
        OutFirst = outFirst;
        CounterTowerSoakers = counterTowerSoakers;
        TetherTower = tetherTower;
    }

    // Null unless the host's roll is one this fight can deal.
    public static DsrP3NidhoggState? FromNetworkReplay(
        IReadOnlyDictionary<PartyRole, int> order, IReadOnlyDictionary<PartyRole, JumpKind> jump, int[] ordersWithArrows,
        bool[] outFirst, int[] counterTowerSoakers, int tetherTower)
    {
        var arrows = ordersWithArrows.ToHashSet();
        if (!ArrowPatterns.Any(p => arrows.SetEquals(p))) return null;
        if (outFirst.Length != 2 || tetherTower is < 0 or > 3) return null;
        if (!CounterTowerShapes.Any(shape => shape.Order().SequenceEqual(counterTowerSoakers.Order()))) return null;
        var roles = Enum.GetValues<PartyRole>();
        if (order.Count != roles.Length || jump.Count != roles.Length || !roles.All(r => order.ContainsKey(r) && jump.ContainsKey(r))) return null;
        var dealt = roles.Select(r => (order[r], jump[r])).Order();
        if (!dealt.SequenceEqual(Slots(arrows, null).Order())) return null;
        return new DsrP3NidhoggState(
            new Dictionary<PartyRole, int>(order), new Dictionary<PartyRole, JumpKind>(jump), arrows,
            outFirst.ToList(), counterTowerSoakers.ToList(), tetherTower);
    }

    // The rolled player, or whoever stands closest to Nidhogg if they're dead.
    public SimCharacter? FirstDrachenlanceTargetIn(SimParty party, Vector3 nidhogg) =>
        party.Get(FirstDrachenlanceTarget) is { } rolled && rolled.IsAlive() ? rolled : party.Find.Closest(nidhogg);

    public static int OrderSize(int order) => order == 2 ? 2 : 3;

    // Whether some roll gives every picked seat its number and jump, under the forced arrows if any.
    public static bool CanDeal(DsrP3NidhoggStateOverrides overrides, PartyRole playerRole)
    {
        var picks = Picks(overrides, playerRole);
        var patterns = overrides.ArrowPattern is { } forced ? [ArrowPatterns[forced]] : ArrowPatterns;
        return patterns.Any(p => Place(picks, Slots(p.ToHashSet(), null), Enumerable.Range(0, 8).ToList()) != null);
    }

    private static List<(PartyRole Role, int? Order, JumpKind? Jump)> Picks(DsrP3NidhoggStateOverrides overrides, PartyRole playerRole)
    {
        var picks = new Dictionary<PartyRole, (int? Order, JumpKind? Jump)>();
        foreach (var (role, order) in overrides.Order.Resolve(playerRole))
            if (order is >= 1 and <= 3) picks[role] = (order, null);
        foreach (var (role, jump) in overrides.Jump.Resolve(playerRole))
            picks[role] = (picks.GetValueOrDefault(role).Order, jump);
        return picks.OrderBy(p => (int)p.Key).Select(p => (p.Key, p.Value.Order, p.Value.Jump)).ToList();
    }

    // The eight debuffs in order 1, 2, 3; an arrow group's kinds shuffled when an rng is given.
    private static List<(int Order, JumpKind Kind)> Slots(IReadOnlySet<int> ordersWithArrows, Rng? rng)
    {
        var slots = new List<(int, JumpKind)>(8);
        foreach (var order in new[] { 1, 2, 3 })
        {
            var size = OrderSize(order);
            IReadOnlyList<JumpKind> kinds = !ordersWithArrows.Contains(order)
                ? Enumerable.Repeat(JumpKind.High, size).ToList()
                : size == 3 ? [JumpKind.High, JumpKind.Forward, JumpKind.Backward] : [JumpKind.Forward, JumpKind.Backward];
            if (rng != null && ordersWithArrows.Contains(order)) kinds = rng.Shuffle(kinds);
            slots.AddRange(kinds.Select(kind => (order, kind)));
        }
        return slots;
    }

    // Picks the roll can't honour are dropped from the last seat up, and the seat left to chance.
    private static Dictionary<PartyRole, int> SeatPicks(
        List<(PartyRole Role, int? Order, JumpKind? Jump)> picks, List<(int Order, JumpKind Kind)> slots, Rng rng)
    {
        if (picks.Count == 0) return new();
        var tryOrder = rng.Shuffle(Enumerable.Range(0, slots.Count)).ToList();
        for (var kept = picks.Count; kept > 0; kept--)
        {
            if (Place(picks.Take(kept).ToList(), slots, tryOrder) is { } placed)
            {
                foreach (var (role, _, _) in picks.Skip(kept))
                    Plugin.Log.Warning($"[DsrP3Nidhogg] {role}'s Dive from Grace pick can't be dealt alongside the others; rolling it instead.");
                return placed;
            }
        }
        return new();
    }

    private static Dictionary<PartyRole, int>? Place(
        List<(PartyRole Role, int? Order, JumpKind? Jump)> picks, List<(int Order, JumpKind Kind)> slots, List<int> tryOrder)
    {
        var placed = new Dictionary<PartyRole, int>();

        bool PlaceFrom(int i)
        {
            if (i == picks.Count) return true;
            var (role, order, jump) = picks[i];
            foreach (var slot in tryOrder)
            {
                if (placed.ContainsValue(slot) || (order ?? slots[slot].Order) != slots[slot].Order || (jump ?? slots[slot].Kind) != slots[slot].Kind)
                    continue;
                placed[role] = slot;
                if (PlaceFrom(i + 1)) return true;
                placed.Remove(role);
            }
            return false;
        }

        return PlaceFrom(0) ? placed : null;
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

    // Rerolled until the picks fit, which keeps the patterns that do fit at their usual odds.
    private static int[] RollOrdersWithArrows(Rng rng, List<(PartyRole Role, int? Order, JumpKind? Jump)> picks)
    {
        var rolled = RollOrdersWithArrows(rng);
        if (picks.Count == 0 || !ArrowPatterns.Any(p => Place(picks, Slots(p.ToHashSet(), null), Enumerable.Range(0, 8).ToList()) != null))
            return rolled;
        while (Place(picks, Slots(rolled.ToHashSet(), null), Enumerable.Range(0, 8).ToList()) == null)
            rolled = RollOrdersWithArrows(rng);
        return rolled;
    }
}
