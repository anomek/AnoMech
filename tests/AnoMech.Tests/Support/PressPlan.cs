using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game.Party;
using AnoMech.Core.UserActions;

namespace AnoMech.Tests;

// When each human tank presses what, so that every hit the strat hands a human finds enough
// mitigation up as the hit snapshots: pressed before the hit, an animation lock apart, within what
// the game's client lets through (each recast group's charges, the Oath gauge, the party's one
// limit break) and only while the player can act. A tank's own hit is met by its own presses, over
// what the other tank's already put on the party; a hit on a seat with no buttons, by a human
// tank's party-wide press. Sets are tried cheapest first: no invuln or limit break unless one is
// needed, then the fewest presses, then the shortest recasts; a later hit that can't be met
// backtracks. A set goes as late as it can or, for an Early seat, first as early as its statuses
// still outlast the hit.
internal static class PressPlan
{
    // The last press of a set lands this long before the hit, time for a peer's report to reach the host.
    public const float Lead = 1f;
    // Between two presses of one player: past retail's 0.6s animation lock.
    public const float Weave = 0.8f;
    // A status counted has to outlast the hit by this.
    private const float HoldMargin = 0.3f;
    private const float FirstPress = 0.5f;
    // The full gauge reaching a peer's HUD, which gates its press.
    private const float GaugeLag = 0.25f;
    private const int SearchBudget = 200_000;

    internal sealed record Hit(uint ActionId, DamageKind Kind, float Required);

    // Every hit one seat takes in one frame, in the order the host judges them.
    internal sealed record Buster(PartyRole Seat, float Time, IReadOnlyList<Hit> Hits)
    {
        public override string ToString()
            => $"{Hits[0].ActionId} on {Seat} at {Time:F2}s ({string.Join(", ", Hits.Select(h => $"{h.Required:P0} {h.Kind}"))})";
    }

    internal sealed record Seat(PartyRole Role, JobId Job, IReadOnlyList<TankButton> Kit, Func<float, bool> FreeToAct, bool Early = false);

    internal sealed record Press(PartyRole Seat, float At, TankButton Button);

    // Counted: for each buster, the statuses its first hit is planned to land on.
    internal sealed record Schedule(IReadOnlyList<Press> Presses, IReadOnlyDictionary<Buster, IReadOnlyList<ushort>> Counted)
    {
        public static readonly Schedule None = new([], new Dictionary<Buster, IReadOnlyList<ushort>>());
    }

    // seats: the human tanks; a buster on any other seat is a human's who presses nothing.
    // limitBreakReadyAt: when the party's gauge first holds the bars a limit break 3 needs.
    public static (Schedule? Schedule, string? Failure) Solve(IReadOnlyList<Seat> seats, IReadOnlyList<Buster> busters, float? limitBreakReadyAt)
    {
        var tanks = seats.ToDictionary(s => s.Role);
        // In a frame, the seats with no buttons first: what covers them may cover the tanks too.
        var ordered = busters.OrderBy(b => b.Time).ThenBy(b => tanks.ContainsKey(b.Seat)).ToList();
        var search = new Search(tanks, ordered, limitBreakReadyAt);
        return search.Run();
    }

    private sealed class Search(IReadOnlyDictionary<PartyRole, Seat> seats, IReadOnlyList<Buster> busters, float? limitBreakReadyAt)
    {
        private readonly List<Press> presses = [];
        private readonly Dictionary<Buster, IReadOnlyList<ushort>> counted = new();
        private int evaluated;
        private int deepest = -1;
        private string deepestReason = "";

        public (Schedule?, string?) Run()
        {
            if (Place(0)) return (new Schedule(presses.OrderBy(p => p.At).ToList(), new Dictionary<Buster, IReadOnlyList<ushort>>(counted)), null);
            return (null, evaluated >= SearchBudget
                ? $"no press schedule found within {SearchBudget} tries; furthest: {deepestReason}"
                : deepestReason);
        }

        private bool Place(int index)
        {
            if (index == busters.Count) return true;
            var buster = busters[index];
            var previous = busters.Take(index).LastOrDefault(b => b.Seat == buster.Seat);
            var from = MathF.Max(FirstPress, (previous?.Time ?? 0f) + 0.1f);
            var to = buster.Time - Lead;
            var who = seats.TryGetValue(buster.Seat, out var own) ? $"{own.Job} as {own.Role}" : $"{buster.Seat}, pressing nothing,";
            var covered = Covered(buster, previous);
            if (covered)
            {
                if (Place(index + 1)) return true;
                counted.Remove(buster);
                if (evaluated >= SearchBudget) return false;
            }
            else if (Ceiling(buster, previous, to) is var ceiling and < 0f)
            {
                Blocked(index, $"{who} can't meet {buster} even with everything its party's clients let them press ({-ceiling:P1} short)");
                return false;
            }

            var bestMargin = float.NegativeInfinity;
            var bestSet = "";
            var anyFeasible = covered;
            foreach (var (presser, kit) in Pressers(buster))
            {
                // Met already, the empty set is feasible.
                var feasible = covered ? new List<int> { 0 } : [];
                foreach (var set in Sets(kit))
                {
                    if (feasible.Any(f => (f & set) == f && !Members(kit, set & ~f).Any(b => Reaches(presser.Role, b, to, index)))) continue;
                    // Early first; late too, for a press the next buster needs as well.
                    foreach (var early in presser.Early ? [true, false] : new[] { false })
                    {
                        if (evaluated++ >= SearchBudget) return false;
                        if (Times(presser, Members(kit, set).ToList(), from, to, buster.Time, early) is not { } times) continue;
                        var added = times.Select(t => new Press(presser.Role, t.At, t.Button)).ToList();
                        if (!Allowed(presser, added)) continue;
                        presses.AddRange(added);
                        var (margin, statuses) = Margin(buster, previous);
                        if (margin < 0f)
                        {
                            presses.RemoveRange(presses.Count - added.Count, added.Count);
                            if (margin > bestMargin) (bestMargin, bestSet) = (margin, Describe(added));
                            continue;
                        }
                        if (!feasible.Contains(set)) feasible.Add(set);
                        anyFeasible = true;
                        counted[buster] = statuses;
                        if (Place(index + 1)) return true;
                        counted.Remove(buster);
                        presses.RemoveRange(presses.Count - added.Count, added.Count);
                        if (evaluated >= SearchBudget) return false;
                    }
                }
            }
            Blocked(index, anyFeasible
                ? $"{who} can meet {buster} but nothing after it holds"
                : $"{who} can't meet {buster} with anything its party's clients let them press"
                  + (bestSet.Length > 0 ? $" (closest: {bestSet}, {-bestMargin:P1} short)" : " (no button free in time)"));
            return false;
        }

        // Met by what is up already, pressing nothing more.
        private bool Covered(Buster buster, Buster? previous)
        {
            var (margin, statuses) = Margin(buster, previous);
            if (margin < 0f) return false;
            counted[buster] = statuses;
            return true;
        }

        // Who can press for the buster, and the buttons that would reach it: a tank its own bar, a
        // seat without one each human tank's party-wide buttons.
        private IEnumerable<(Seat Presser, IReadOnlyList<TankButton> Kit)> Pressers(Buster buster)
            => seats.TryGetValue(buster.Seat, out var own)
                ? [(own, own.Kit)]
                : seats.Values.Select(s => (s, (IReadOnlyList<TankButton>)s.Kit.Where(b => b.PartyGrants.Count > 0).ToList()));

        private void Blocked(int index, string reason)
        {
            if (index < deepest) return;
            deepest = index;
            deepestReason = reason;
        }

        // The most the seat could have up for the buster: every button that would reach it that its
        // client would let through, all pressed at the last moment, over what is up already. Below
        // that, no set works.
        private float Ceiling(Buster buster, Buster? previous, float to)
        {
            var all = Pressers(buster)
                .SelectMany(p => p.Kit.Select(b => new Press(p.Presser.Role, to, b)).Where(press => Allowed(p.Presser, [press], apart: false)))
                .ToList();
            presses.AddRange(all);
            var (margin, _) = Margin(buster, previous);
            presses.RemoveRange(presses.Count - all.Count, all.Count);
            return margin;
        }

        // Whether a press made by `at` is still up at a later buster it counts for: a bigger set
        // whose extra presses reach none only spends more recasts.
        private bool Reaches(PartyRole presser, TankButton button, float at, int index)
            => busters.Skip(index + 1).Any(later =>
                (later.Seat == presser ? button.Grants : button.PartyGrants).Any(g => at + g.Duration >= later.Time + HoldMargin));

        // Every non-empty subset of the kit, as a bit mask, cheapest first.
        private static IEnumerable<int> Sets(IReadOnlyList<TankButton> kit)
            => Enumerable.Range(1, (1 << kit.Count) - 1)
                .OrderBy(set => Has(kit, set, b => b.IsLimitBreak))
                .ThenBy(set => Has(kit, set, b => b.IsInvuln))
                .ThenBy(set => System.Numerics.BitOperations.PopCount((uint)set))
                .ThenBy(set => Members(kit, set).Sum(b => b.Recast));

        private static int Has(IReadOnlyList<TankButton> kit, int set, Func<TankButton, bool> what) => Members(kit, set).Any(what) ? 1 : 0;

        private static IEnumerable<TankButton> Members(IReadOnlyList<TankButton> kit, int set)
            => kit.Where((_, i) => (set & (1 << i)) != 0);

        // Slots a weave apart in [from, to], the shortest-lived status pressed last and a limit
        // break, with its long animation lock, after everything.
        private static List<(float At, TankButton Button)>? Times(Seat seat, List<TankButton> buttons, float from, float to, float hit, bool early)
        {
            var ordered = buttons
                .OrderBy(b => b.IsLimitBreak)
                .ThenByDescending(b => b.Grants.Concat(b.PartyGrants).Min(g => g.Duration))
                .ToList();
            return early ? EarliestTimes(seat, ordered, from, to, hit) : LatestTimes(seat, ordered, from, to);
        }

        private static List<(float At, TankButton Button)>? LatestTimes(Seat seat, List<TankButton> ordered, float from, float to)
        {
            var times = new List<(float, TankButton)>();
            var at = to;
            for (var i = ordered.Count - 1; i >= 0; i--)
            {
                while (at >= from && !seat.FreeToAct(at)) at -= ScenarioRun.FrameSeconds;
                if (at < from) return null;
                times.Add((at, ordered[i]));
                at -= Weave;
            }
            times.Reverse();
            return times;
        }

        // Each press no earlier than every status it grants still outlasts the hit.
        private static List<(float At, TankButton Button)>? EarliestTimes(Seat seat, List<TankButton> ordered, float from, float to, float hit)
        {
            var times = new List<(float, TankButton)>();
            var at = from;
            foreach (var button in ordered)
            {
                at = MathF.Max(at, hit + HoldMargin + ScenarioRun.FrameSeconds - button.Grants.Concat(button.PartyGrants).Min(g => g.Duration));
                while (at <= to && !seat.FreeToAct(at)) at += ScenarioRun.FrameSeconds;
                if (at > to) return null;
                times.Add((at, button));
                at += Weave;
            }
            return times;
        }

        // What the game's client would let through: an animation lock apart, a free charge in the
        // recast group, the gauge, the party's one limit break.
        private bool Allowed(Seat seat, IReadOnlyList<Press> added, bool apart = true)
        {
            var mine = presses.Where(p => p.Seat == seat.Role).Concat(added).OrderBy(p => p.At).ToList();
            foreach (var press in added)
            {
                if (apart && mine.Any(p => !ReferenceEquals(p, press) && MathF.Abs(p.At - press.At) < Weave - 0.001f)) return false;
                var button = press.Button;
                if (button.IsLimitBreak)
                {
                    if (limitBreakReadyAt is not { } ready || press.At < ready + GaugeLag || presses.Concat(added).Count(p => p.Button.IsLimitBreak) > 1) return false;
                    continue;
                }
                var group = mine.Where(p => p.Button.CooldownGroup == button.CooldownGroup && p.At < press.At).Select(p => p.At).ToList();
                if (!ChargeFree(group, press.At, button.Recast, button.Charges)) return false;
            }
            var spent = mine.Sum(p => p.Button.GaugeCost);
            return spent <= TankKit.StartingOath;
        }

        // Charges come back one at a time, a recast each.
        private static bool ChargeFree(IReadOnlyList<float> earlier, float at, float recast, int charges)
        {
            var back = 0f;
            var pending = 0;
            foreach (var use in earlier)
            {
                back = MathF.Max(back, use) + recast;
                if (back > at) pending++;
            }
            return pending < charges;
        }

        // How far past what it needs the seat's weakest hit in the buster lands, and what the first
        // hit counts. A shield goes to the first hit that judges it, as DamageCheck spends it.
        private (float Margin, IReadOnlyList<ushort> Statuses) Margin(Buster buster, Buster? previous)
        {
            var up = Up(buster.Seat, buster.Time, previous?.Time);
            var margin = float.PositiveInfinity;
            for (var i = 0; i < buster.Hits.Count; i++)
            {
                var ids = i == 0 ? up : up.Where(id => !Mitigation.ByStatusId[id].IsShield).ToList();
                var hit = buster.Hits[i];
                margin = MathF.Min(margin, Mitigation.Effective(ids, hit.Kind) - (hit.Required - 0.0005f));
            }
            return (margin, up);
        }

        // Each status the party's presses leave up on the seat at `time`, its own presses' and the
        // others' party-wide ones, the latest press of it counting; a shield only if pressed since
        // the seat's last judged hit.
        private List<ushort> Up(PartyRole role, float time, float? lastHit)
        {
            var latest = new Dictionary<ushort, (float At, float Duration)>();
            foreach (var press in presses.Where(p => p.At < time))
                foreach (var (statusId, duration) in press.Seat == role ? press.Button.Grants : press.Button.PartyGrants)
                    if (!latest.TryGetValue(statusId, out var seen) || press.At >= seen.At)
                        latest[statusId] = (press.At, duration);
            return latest
                .Where(kv => kv.Value.At + kv.Value.Duration >= time + HoldMargin)
                .Where(kv => !Mitigation.ByStatusId[kv.Key].IsShield || lastHit is not { } last || kv.Value.At > last)
                .Select(kv => kv.Key)
                .Order()
                .ToList();
        }

        private static string Describe(IEnumerable<Press> added) => string.Join(" + ", added.Select(p => $"{p.Seat} {p.Button.Name}"));
    }
}
