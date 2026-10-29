using System.Diagnostics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// Every hit a strat hands a tank seat that only the tank's own mitigation survives, played by
// people. The busters are whatever the host's damage checks judge on mitigation while debug bots
// play the fight, so a new buster, strat or fight is covered as it lands. Each is then played with
// the tank seats held by humans who walk where the bots walked and press their own job's buttons
// (Paladin, Warrior, Dark Knight, Gunbreaker): the host's own player, a peer, or both tanks at
// once, with every other seat a bot. Pressing a legal schedule, the run has to clear with every
// buster judged on what that player's own client put up; pressing nothing, the tank has to die to
// the first one. A job that has no legal schedule for a buster fails the test, naming it.
// A hit that needs mitigation on a seat no tank sits in is played the same way with a human in
// that seat who presses nothing, on the host or a peer: what covers them has to come from the
// party, the human tanks' party-wide presses or, with both tanks the host's bots, the strat's own.
public class TankBusterTests
{
    private const int Seed = 1;

    // Each job tanks MT once and OT once; the explicit test plays every pair.
    private static readonly (JobId Mt, JobId Ot)[] CoveringPairs =
    [
        (JobId.Paladin, JobId.Warrior), (JobId.Warrior, JobId.DarkKnight),
        (JobId.DarkKnight, JobId.Gunbreaker), (JobId.Gunbreaker, JobId.Paladin),
    ];

    private static IEnumerable<TestCaseData> Strats() =>
        from scenario in ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer)
        from strat in scenario.AiStrats.Select((ai, index) => (ai, index))
        select new TestCaseData(scenario.GetType(), strat.index).SetArgDisplayNames(scenario.FullName(), strat.ai.Name);

    [TestCaseSource(nameof(Strats))]
    public void EveryHumanTankSurvivesEveryBusterOnItsOwnPresses(Type scenarioType, int strat)
        => Verify(scenarioType, strat, CoveringPairs);

    [Explicit("Every pair of tank jobs; the default run covers each job in each seat.")]
    [Category("TankBusterMatrix")]
    [TestCaseSource(nameof(Strats))]
    public void EveryPairOfHumanTanksSurvivesEveryBusterOnTheirOwnPresses(Type scenarioType, int strat)
        => Verify(scenarioType, strat, TankKit.Jobs.SelectMany(mt => TankKit.Jobs.Select(ot => (mt, ot))).ToArray());

    // The tank seats humans hold, on their jobs, and the seats of humans who press nothing, the
    // layout's clients holding them.
    private sealed record Case(TankLayout Layout, IReadOnlyDictionary<PartyRole, JobId> Humans, IReadOnlyList<PartyRole> Bystanders)
    {
        public IEnumerable<PartyRole> HumanSeats => Humans.Keys.Concat(Bystanders);

        public override string ToString()
            => string.Join(" + ", Humans.Select(h => $"{Layout.Who(h.Key)} {h.Key} {h.Value}")
                   .Concat(Bystanders.Select(seat => $"{Layout.Who(seat)} {seat} pressing nothing")))
               + (Humans.Count == 0 ? ", bot tanks" : "") + $" [{Layout.Name}]";
    }

    private static void Verify(Type scenarioType, int strat, IReadOnlyList<(JobId Mt, JobId Ot)> pairs)
    {
        var clock = Stopwatch.StartNew();
        var scenario = ScenarioCatalog.Create().Scenarios.Single(s => s.GetType() == scenarioType);
        var level = new DataminingGameData(new FakeRsvFunctions(new SimTimeline())).DutyLevel(scenario.Phase.Zone.TerritoryId) ?? 100;
        foreach (var job in TankKit.Jobs) TankKit.Of(job, level);
        var problems = new List<string>();
        var references = new Dictionary<TankLayout, TankBusterRun>();
        TankBusterRun Reference(TankLayout layout)
        {
            if (references.TryGetValue(layout, out var known)) return known;
            var reference = TankBusterRun.Reference(scenarioType, strat, Seed, layout);
            references[layout] = reference;
            Report(layout, reference);
            if (reference.Run.Failure is { } failure) problems.Add($"[{layout.Name}] the debug bots' run failed: {failure}");
            if (reference.Run.Deaths.Count > 0) problems.Add($"[{layout.Name}] the debug bots didn't clear: {Describe(reference.Run.Deaths)}");
            return reference;
        }

        var first = Reference(TankLayout.HostMtPeerOt);
        var anyBusters = Busters(first, TankLayout.Tanks).Count > 0;
        var partyBusters = Busters(first, Enum.GetValues<PartyRole>().Except(TankLayout.Tanks)).Count > 0;
        if (anyBusters)
            foreach (var layout in TankLayout.All) Reference(layout);
        if (partyBusters)
            foreach (var layout in TankLayout.WithBystanders) Reference(layout);
        if (problems.Count > 0) Assert.Fail(string.Join(Environment.NewLine, problems));

        var limitBreakStatuses = TankKit.Jobs.SelectMany(job => TankKit.Of(job, level)).Where(b => b.IsLimitBreak)
            .SelectMany(b => b.Grants.Concat(b.PartyGrants)).Select(g => g.StatusId).ToHashSet();
        var seatSets = new HashSet<string>();
        foreach (var @case in Cases(anyBusters, partyBusters, pairs))
            Play(scenarioType, strat, level, @case, Reference(@case.Layout), limitBreakStatuses, seatSets, problems);

        TestContext.Out.WriteLine($"{scenario.FullName()} [{scenario.AiStrats[strat].Name}]: {clock.Elapsed.TotalSeconds:F1}s");
        foreach (var problem in problems.Take(60)) TestContext.Out.WriteLine(problem);
        Assert.That(problems, Is.Empty, $"{problems.Count} problems");
    }

    // A fight with no busters only has to let both tanks, pressing nothing, clear it.
    private static IEnumerable<Case> Cases(bool anyBusters, bool partyBusters, IReadOnlyList<(JobId Mt, JobId Ot)> pairs)
    {
        if (!anyBusters && !partyBusters)
        {
            yield return new Case(TankLayout.HostMtPeerOt, new Dictionary<PartyRole, JobId> { [MainTank] = JobId.Paladin, [OffTank] = JobId.Warrior }, []);
            yield break;
        }
        if (anyBusters)
        {
            foreach (var layout in new[] { TankLayout.HostMtPeerOt, TankLayout.HostOtPeerMt })
            foreach (var seat in TankLayout.Tanks)
            foreach (var job in TankKit.Jobs)
                yield return new Case(layout, new Dictionary<PartyRole, JobId> { [seat] = job }, []);
            foreach (var layout in TankLayout.All)
            foreach (var (mt, ot) in pairs)
                yield return new Case(layout, new Dictionary<PartyRole, JobId> { [MainTank] = mt, [OffTank] = ot }, []);
        }
        if (!partyBusters) yield break;
        foreach (var layout in TankLayout.WithBystanders)
        {
            var bystanders = layout.NonTankSeats.ToList();
            var tankSeats = layout.Seats.Where(TankLayout.Tanks.Contains).ToList();
            if (tankSeats.Count == 0) yield return new Case(layout, new Dictionary<PartyRole, JobId>(), bystanders);
            foreach (var seat in tankSeats)
            foreach (var job in TankKit.Jobs)
                yield return new Case(layout, new Dictionary<PartyRole, JobId> { [seat] = job }, bystanders);
            if (tankSeats.Count < 2) continue;
            foreach (var (mt, ot) in pairs)
                yield return new Case(layout, new Dictionary<PartyRole, JobId> { [MainTank] = mt, [OffTank] = ot }, bystanders);
        }
    }

    // seatSets: the layouts and seats already played once. A seat set with no busters has nothing
    // to press, and pressing nothing doesn't depend on the job, so each of those runs once a set.
    private static void Play(Type scenarioType, int strat, byte level, Case @case, TankBusterRun reference, IReadOnlySet<ushort> limitBreakStatuses,
        HashSet<string> seatSets, List<string> problems)
    {
        var busters = Busters(reference, @case.HumanSeats);
        var firstOfItsSeats = seatSets.Add($"{@case.Layout.Name}: {string.Join(",", @case.Humans.Keys.Order())} / {string.Join(",", @case.Bystanders.Order())}");
        if (busters.Count == 0 && !firstOfItsSeats) return;
        // A peer tank alone presses each set as early as it still covers the hit, so a reported
        // status has to last its whole duration on the host; everyone else as late as they can.
        var early = @case.Humans.Count == 1 && @case.Layout.ClientOf(@case.Humans.Keys.Single()) > 0;
        var seats = @case.Humans.Select(h => new PressPlan.Seat(h.Key, h.Value, TankKit.Of(h.Value, level),
            at => reference.Walks[h.Key].FreeToAct(at - 0.3f, at + 0.3f), early)).ToList();
        // With no human tank, the strat's own bots cover whoever presses nothing.
        var (schedule, failure) = seats.Count > 0 ? PressPlan.Solve(seats, busters, reference.LimitBreakReadyAt) : (PressPlan.Schedule.None, null);
        if (schedule is null)
        {
            problems.Add($"{@case}: no legal press schedule: {failure}");
            return;
        }
        var pressed = TankBusterRun.Humans(scenarioType, strat, Seed, @case.Layout, @case.Humans, @case.Bystanders, reference, schedule.Presses);
        var before = problems.Count;
        CheckPressed(@case, busters, schedule, pressed, limitBreakStatuses, problems);
        TestContext.Out.WriteLine($"  {@case}: {busters.Count} busters, pressed {Describe(schedule.Presses)} -> "
                                  + (problems.Count == before ? "cleared" : "FAILED"));

        if (busters.Count == 0 || !firstOfItsSeats || @case.Humans.Count == 0) return;
        var idle = TankBusterRun.Humans(scenarioType, strat, Seed, @case.Layout, @case.Humans, @case.Bystanders, reference, []);
        before = problems.Count;
        CheckIdle(@case, busters, idle, problems);
        TestContext.Out.WriteLine($"  {@case}: pressing nothing -> " + (problems.Count == before ? $"dies as it should ({Describe(idle.Run.Deaths.Take(1))})" : "FAILED"));
    }

    private static void CheckPressed(Case @case, IReadOnlyList<PressPlan.Buster> busters, PressPlan.Schedule schedule, TankBusterRun pressed,
        IReadOnlySet<ushort> limitBreakStatuses, List<string> problems)
    {
        var run = pressed.Run;
        if (run.Failure is { } failure)
        {
            problems.Add($"{@case}: the run failed: {failure}");
            return;
        }
        if (run.Deaths.Count > 0)
            problems.Add($"{@case}: pressing {Describe(schedule.Presses)}, {Describe(run.Deaths)}");
        else if (run.HostOutcome != SessionStatus.Succeeded)
            problems.Add($"{@case}: the run ended {run.HostOutcome?.ToString() ?? "before the fight's end"}, not cleared");
        if (run.InputsLeft > 0) problems.Add($"{@case}: {run.InputsLeft} presses were never made");
        problems.AddRange(pressed.Refused.Select(r => $"{@case}: {r}"));

        var humanSeats = @case.HumanSeats.ToHashSet();
        var humanChecks = pressed.Checks.Where(c => humanSeats.Contains(c.Seat)).ToList();
        foreach (var buster in busters)
        {
            var landed = humanChecks.Where(c => c.Seat == buster.Seat && MathF.Abs(c.Time - buster.Time) < SameFrame).ToList();
            if (landed.Count != buster.Hits.Count)
            {
                problems.Add($"{@case}: {buster} landed {landed.Count} times instead of {buster.Hits.Count}: the human didn't stand where the bot did");
                continue;
            }
            for (var i = 0; i < landed.Count; i++)
            {
                var check = landed[i];
                if (!check.Judged)
                    problems.Add($"{@case}: the host didn't judge the human's own mitigation: {check}");
                else if (check.Effective < check.Required - 0.0005f)
                    problems.Add($"{@case}: the host counted too little: {check}");
            }
            if (!schedule.Counted.TryGetValue(buster, out var counted)) continue;
            var missing = counted.Except(landed[0].Statuses).ToList();
            if (missing.Count > 0)
                problems.Add($"{@case}: the host's copy of {buster.Seat} lacked [{string.Join(",", missing)}] the party's clients put up: {landed[0]}");
        }
        foreach (var extra in humanChecks.Where(c => !busters.Any(b => b.Seat == c.Seat && MathF.Abs(c.Time - b.Time) < SameFrame)))
            problems.Add($"{@case}: a hit the bot never took landed on the human: {extra}");
        // The party has one gauge: whoever's client spent it, once its limit break is up on anyone
        // the host's is empty.
        foreach (var check in humanChecks.Where(c => c.Statuses.Any(limitBreakStatuses.Contains)))
            if (pressed.HostGauge.LastOrDefault(g => g.Time <= check.Time) is { Bars: > 0 } left)
                problems.Add($"{@case}: a limit break was up for {check}, but the host's gauge still held {left.Bars} bars");
    }

    private static void CheckIdle(Case @case, IReadOnlyList<PressPlan.Buster> busters, TankBusterRun idle, List<string> problems)
    {
        var run = idle.Run;
        if (run.Failure is { } failure)
        {
            problems.Add($"{@case}, pressing nothing: the run failed: {failure}");
            return;
        }
        var first = busters[0];
        var expected = busters.Where(b => MathF.Abs(b.Time - first.Time) < SameFrame).Select(b => b.Seat).ToList();
        if (run.Deaths.FirstOrDefault() is not { } death)
            problems.Add($"{@case}, pressing nothing: nobody died, but {first} needs mitigation");
        else if (!expected.Contains(death.Role) || death.ActionId != first.Hits[0].ActionId)
            problems.Add($"{@case}, pressing nothing: expected {string.Join(" or ", expected)} to die to {Name(first.Hits[0].ActionId)} first, "
                         + $"but {Describe([death])}");
        else if (!idle.Checks.Any(c => c.Seat == death.Role && MathF.Abs(c.Time - first.Time) < SameFrame && c.Judged && c.Effective < c.Required - 0.0005f))
            problems.Add($"{@case}, pressing nothing: {Describe([death])}, but no judged check found it short");
    }

    private const float SameFrame = ScenarioRun.FrameSeconds / 2;

    // The hits each of `seats` took that only mitigation survives, one buster per seat and frame.
    private static IReadOnlyList<PressPlan.Buster> Busters(TankBusterRun reference, IEnumerable<PartyRole> seats)
    {
        var wanted = seats.ToHashSet();
        var busters = new List<PressPlan.Buster>();
        foreach (var check in reference.Checks.Where(c => wanted.Contains(c.Seat)))
        {
            var hit = new PressPlan.Hit(check.ActionId, check.Kind, check.Required);
            var index = busters.FindIndex(b => b.Seat == check.Seat && MathF.Abs(b.Time - check.Time) < SameFrame);
            if (index < 0) busters.Add(new PressPlan.Buster(check.Seat, check.Time, [hit]));
            else busters[index] = busters[index] with { Hits = [.. busters[index].Hits, hit] };
        }
        return busters.OrderBy(b => b.Time).ToList();
    }

    private static void Report(TankLayout layout, TankBusterRun reference)
    {
        var busters = Busters(reference, TankLayout.Tanks);
        TestContext.Out.WriteLine($"[{layout.Name}] debug bots: {busters.Count} tank busters"
                                  + (reference.LimitBreakReadyAt is { } at ? $", limit break ready at {at:F2}s" : ""));
        foreach (var buster in busters)
            TestContext.Out.WriteLine($"    {buster.Time,7:F2}s {buster.Seat,-8} {Name(buster.Hits[0].ActionId),-28} "
                                      + $"{buster.Hits[0].Kind,-8} {string.Join(" ", buster.Hits.Select(h => $"{h.Required:P0}"))}");
        var others = reference.Checks.Where(c => !TankLayout.Tanks.Contains(c.Seat)).GroupBy(c => (c.ActionId, c.Required)).ToList();
        foreach (var group in others)
            TestContext.Out.WriteLine($"    also on other seats: {Name(group.Key.ActionId)} needs {group.Key.Required:P0} "
                                      + $"({group.Count()} hits on {string.Join(", ", group.Select(c => c.Seat).Distinct())})");
    }

    private static string Name(uint actionId) => $"{Natives.Data.ActionName(actionId)} ({actionId})";

    private static string Describe(IEnumerable<PressPlan.Press> presses)
        => string.Join(", ", presses.Select(p => $"{p.Seat} {p.Button}@{p.At:F2}"));

    private static string Describe(IEnumerable<Death> deaths)
        => string.Join("; ", deaths.Select(d => $"{d.Role} died to \"{d.Cause}\" ({(d.ActionId is { } id ? $"action {id}" : "no action")}) at t={d.Time:F2}"));
}
