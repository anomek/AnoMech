using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// What a player's own press does to what the host keeps doesn't depend on whose client pressed it.
// Every action the sim gives an effect, and every job's limit breaks on a full gauge, is pressed in
// turn from one seat: once by the host's own player and once by a peer's, in two sessions
// otherwise the same, the three players standing at the boss. Before each press, what the host
// keeps has to read the same in both: the party's limit break gauge, the statuses it decides with
// (HostReport) on each player's seat, and every enemy's statuses; and every client has to show
// what the host keeps: its gauge, and those statuses on every player. No case names an action, a
// job or a status, so a new effect, action or limit break is covered as it lands.
public class PressOutcomeTests
{
    private const PartyRole Presser = PartyRole.MainTank;
    // The host while the peer presses; a peer holds Target in both sessions.
    private const PartyRole Other = PartyRole.MeleeDpsA;
    private const PartyRole Target = PartyRole.CasterDps;
    private const double FirstPress = 1, Gap = 0.15, Settle = 0.05, Refill = 0.15, BatchSeconds = 25, Tail = 1;
    // A peer's press reaches the host a frame or two after the host's own would.
    private const float Tolerance = 0.25f;

    // Where each player stands, around the boss as the host has it, so a party-wide press reaches
    // the others and an enemy-wide one the boss.
    private static readonly Dictionary<PartyRole, Vector3> AroundTheBoss = new()
    {
        [Presser] = new(0f, 0f, 1f), [Other] = new(1f, 0f, 0f), [Target] = new(-1f, 0f, 0f),
    };

    private sealed record Press(uint ActionId, double At, bool LimitBreak, float CastSeconds)
    {
        public override string ToString() => $"{Natives.Data.ActionName(ActionId)} ({ActionId}) at {At:F2}s";
    }

    // What the host keeps at one moment, and what each client shows then.
    private sealed record Moment(int Gauge, List<Held> Statuses, Dictionary<PartyRole, View> Shown);

    // A client's own limit break gauge, and the statuses the host decides with as it shows them on
    // the players.
    private sealed record View(int Gauge, List<Held> Statuses);

    private readonly record struct Held(string Who, ushort StatusId, ushort Stacks, float Remaining)
    {
        public string What => $"{Who} {Natives.Data.StatusName(StatusId)} ({StatusId}){(Stacks > 0 ? $" x{Stacks}" : "")}";
    }

    private sealed record Session(List<Moment> Moments, List<string> Refused);

    [Test]
    public void EveryPressLeavesTheHostTheSameWhicheverClientPressedIt()
    {
        var batches = Batches();
        var scenario = ScenarioCatalog.Create().Scenarios.First(s => s.SupportsMultiplayer).GetType();
        var problems = new Dictionary<string, (string First, int Count)>();
        void Problem(string key, string message)
            => problems[key] = problems.TryGetValue(key, out var seen) ? (seen.First, seen.Count + 1) : (message, 1);

        var kept = new HashSet<string>();
        foreach (var presses in batches)
        {
            var byHost = Play(scenario, presses, hostPresses: true, Problem);
            var byPeer = Play(scenario, presses, hostPresses: false, Problem);
            if (byHost is null || byPeer is null) continue;
            Compare(presses, byHost, byPeer, Problem);
            kept.UnionWith(byHost.Moments.SelectMany(m => m.Statuses).Select(h => h.What));
        }
        TestContext.Out.WriteLine($"{batches.Sum(b => b.Count)} presses ({batches.Sum(b => b.Count(p => p.LimitBreak))} limit breaks) left "
                                  + $"{kept.Count} statuses to compare: {string.Join("; ", kept.Order())}");
        if (kept.Count == 0) Problem("nothing kept", "no press left the host anything to keep: the presses did nothing");

        var lines = problems.Values.Select(p => p.Count > 1 ? $"{p.First} ({p.Count} times)" : p.First).ToList();
        foreach (var line in lines.Take(60)) TestContext.Out.WriteLine(line);
        Assert.That(lines, Is.Empty, $"{lines.Count} differences over {batches.Sum(b => b.Count)} presses");
    }

    // Every action the sim gives an effect, then every job's limit breaks, each after the last
    // one's cast, in batches that fit a fight.
    private static List<List<Press>> Batches()
    {
        FakeGame.Install();
        var limitBreaks = Enum.GetValues<JobId>()
            .SelectMany(job => Natives.Data.ClassJob((uint)job) is { } row ? new[] { row.LimitBreak1, row.LimitBreak2, row.LimitBreak3 } : [])
            .Where(id => id != 0)
            .ToHashSet();
        var ids = DataminingGameData.ActionIds.Where(id => JobActions.Knows(id) && !limitBreaks.Contains(id))
            .Order()
            .Concat(limitBreaks.Order());
        var batches = new List<List<Press>>();
        var batch = new List<Press>();
        var at = FirstPress;
        foreach (var id in ids)
        {
            var limitBreak = LimitBreakHandler.IsLimitBreak(id);
            var lead = limitBreak ? Refill : 0;
            var cast = Natives.Data.Action(id)?.CastSeconds ?? 0f;
            var span = lead + cast + Gap;
            if (batch.Count > 0 && at + span > FirstPress + BatchSeconds)
            {
                batches.Add(batch);
                batch = [];
                at = FirstPress;
            }
            batch.Add(new Press(id, at + lead, limitBreak, cast));
            at += span;
        }
        if (batch.Count > 0) batches.Add(batch);
        return batches;
    }

    // One session of a batch: the presser's seat on the host's own client or a peer's.
    private static Session? Play(Type scenario, List<Press> presses, bool hostPresses, Action<string, string> problem)
    {
        var host = new PlayerSeat(hostPresses ? Presser : Other, BotControlled: false);
        PlayerSeat[] peers = [new(hostPresses ? Other : Presser, BotControlled: false), new(Target, BotControlled: false)];
        var presser = hostPresses ? 0 : 1;
        var who = hostPresses ? "the host" : "a peer";

        var times = presses.Select(p => p.At - (p.LimitBreak ? Refill : 0) - Settle)
            .Append(presses[^1].At + presses[^1].CastSeconds + Tail)
            .ToList();
        var moments = times.Select(_ => new Moment(0, [], [])).ToArray();
        var refused = new List<string>();
        var inputs = new List<(double At, int Client, Action<TestClient> Input)>();
        for (var i = 0; i < times.Count; i++)
        {
            var index = i;
            inputs.Add((times[i], 0, c => moments[index] = moments[index] with { Gauge = c.Game.World.Party.LimitBreak.FilledBars, Statuses = Kept(c) }));
            for (var client = 0; client < 3; client++)
                inputs.Add((times[i], client, c => moments[index].Shown[c.Seat] = Shown(c)));
        }
        // The first enemy the host's enemy list shows, and where the host has it now.
        uint? boss = null;
        Vector3? spot = null;
        foreach (var press in presses)
        {
            if (press.LimitBreak) inputs.Add((press.At - Refill, 0, c => c.Game.World.Party.LimitBreak.Set(3)));
            inputs.Add((press.At, presser, c =>
            {
                if (!PressOn(c, press.ActionId, boss)) refused.Add(press.ToString());
            }));
        }

        var run = MultiplayerRun.Play(scenario, 0, 1, host, peers, immortal: true, inputs: inputs,
            configure: c => c.Fake.UserActions.Enabled = true, beforeFrame: c =>
        {
            if (c.Game.ActiveScenario is null) return;
            if (c.IsHost)
            {
                var enemies = c.Game.World.Children.OfType<SimEnemy>().Where(e => e.IsActive);
                boss ??= enemies.FirstOrDefault(e => e.SpawnConfig.EnemyList != EnemyListMode.Never)?.BNpcBaseId;
                spot = enemies.FirstOrDefault(e => e.BNpcBaseId == boss)?.Position ?? spot;
            }
            if (spot is { } at && AroundTheBoss.TryGetValue(c.Seat, out var offset))
                c.Fake.BattleCharas.Player.Position = c.Game.World.Coordinates.ToGlobal(at + offset);
        });
        if (run.Failure is { } failure)
        {
            problem($"failed {who}", $"the session with {who} pressing failed: {failure}");
            return null;
        }
        if (run.InputsLeft > 0)
        {
            problem($"short {who}", $"the fight ended before {run.InputsLeft} of the session's inputs, {who} pressing");
            return null;
        }
        return new Session(moments.ToList(), refused);
    }

    // The presser's own press, as its client takes it (FakeLocalPlayerInput): an action a friend
    // can take on the peer in Target, an enemy's action on the boss, the rest on itself.
    private static bool PressOn(TestClient client, uint actionId, uint? boss)
    {
        if (client.Game.Player is not { Dead: false, ActionsLocked: false } player) return false;
        var action = Natives.Data.Action(actionId);
        var target = action is { CanTargetParty: true } ? client.Game.World.Party.Get(Target)
            : action is { CanTargetSelf: false } ? client.Game.World.Children.OfType<SimEnemy>().FirstOrDefault(e => e.IsActive && e.BNpcBaseId == boss)
            : null;
        return client.Fake.PlayerInput.Press(actionId, (ulong)(target ?? player).GameObjectId);
    }

    // The statuses the host decides with on each player's seat, and every enemy's.
    private static List<Held> Kept(TestClient host)
    {
        var world = host.Game.World;
        var held = new List<Held>();
        foreach (var (role, member) in world.Party.FilledSlots().Where(s => AroundTheBoss.ContainsKey(s.role)))
            held.AddRange(member.ActiveStatusSnapshot.Where(s => HostReport.IsReported(s.StatusId))
                .Select(s => new Held(role.ToString(), s.StatusId, s.Stacks, s.RemainingTime)));
        var enemies = world.Children.OfType<SimEnemy>().Where(e => e.IsActive).OrderBy(e => e.EntityId).ToList();
        for (var k = 0; k < enemies.Count; k++)
            held.AddRange(enemies[k].ActiveStatusSnapshot
                .Select(s => new Held($"enemy {enemies[k].BNpcBaseId}#{k}", s.StatusId, s.Stacks, s.RemainingTime)));
        return held;
    }

    private static View Shown(TestClient client)
    {
        var gauge = client.Fake.BattleCharas.Player.LimitBreak is { BarUnits: > 0 } bars ? bars.CurrentUnits / bars.BarUnits : 0;
        var statuses = new List<Held>();
        foreach (var seat in AroundTheBoss.Keys)
            if (client.Game.World.Party.Get(seat) is { } member && client.Fake.BattleCharas.Find(member.EntityId) is { } shown)
                statuses.AddRange(shown.Statuses.Values.Where(s => HostReport.IsReported(s.StatusId))
                    .Select(s => new Held(seat.ToString(), s.StatusId, s.Param, s.RemainingTime)));
        return new View(gauge, statuses);
    }

    private static void Compare(List<Press> presses, Session byHost, Session byPeer, Action<string, string> problem)
    {
        foreach (var (session, who) in new[] { (byHost, "the host's"), (byPeer, "a peer's") })
            foreach (var press in session.Refused)
                problem($"refused {who} {press}", $"{who} client refused {press}");
        for (var i = 0; i < byHost.Moments.Count; i++)
        {
            var after = i == 0 ? "before any press" : $"after {presses[i - 1]}";
            var (a, b) = (byHost.Moments[i], byPeer.Moments[i]);
            if (i > 0 && presses[i - 1].LimitBreak && a.Gauge != 0)
                problem($"unspent {presses[i - 1].ActionId}", $"{after}: the host's own limit break left its gauge at {a.Gauge} bars");
            if (a.Gauge != b.Gauge)
                problem($"gauge {presses[Math.Max(0, i - 1)].ActionId}", $"{after}: the host's gauge held {a.Gauge} bars with the host pressing, {b.Gauge} with a peer");
            foreach (var (session, who) in new[] { (byHost, "the host"), (byPeer, "a peer") })
            {
                var moment = session.Moments[i];
                var players = moment.Statuses.Where(h => Enum.TryParse<PartyRole>(h.Who, out var seat) && AroundTheBoss.ContainsKey(seat)).ToList();
                // One the host let go since the last moment may still show for a frame.
                var gone = i == 0 ? [] : session.Moments[i - 1].Statuses.Where(h => players.All(k => k.What != h.What)).Select(h => h.What).ToHashSet();
                foreach (var (seat, view) in moment.Shown)
                {
                    if (view.Gauge != moment.Gauge)
                        problem($"shown gauge {who} {seat}", $"{after}, {who} pressing: {seat}'s client showed {view.Gauge} bars of the host's {moment.Gauge}");
                    foreach (var held in players.Where(h => h.Remaining > Tolerance && view.Statuses.All(v => v.What != h.What)))
                        problem($"unshown {who} {seat} {held.What}", $"{after}, {who} pressing: {seat}'s client didn't show {held.What} the host kept");
                    foreach (var shown in view.Statuses.Where(v => players.All(h => h.What != v.What) && !gone.Contains(v.What)))
                        problem($"unkept {who} {seat} {shown.What}", $"{after}, {who} pressing: {seat}'s client showed {shown.What} the host didn't keep");
                }
            }
            foreach (var (held, only) in Differences(a.Statuses, b.Statuses))
                problem($"status {only} {held.What}", $"{after}: the host kept {held.What} ({held.Remaining:F1}s) only with {only} pressing");
        }
    }

    // One about to run out may already be gone in the other session.
    private static IEnumerable<(Held Held, string Only)> Differences(List<Held> byHost, List<Held> byPeer)
    {
        var unmatched = byPeer.ToList();
        foreach (var held in byHost)
        {
            var match = unmatched.FindIndex(p => p.What == held.What && MathF.Abs(p.Remaining - held.Remaining) <= Tolerance);
            if (match >= 0) unmatched.RemoveAt(match);
            else if (held.Remaining > Tolerance) yield return (held, "the host");
        }
        foreach (var held in unmatched.Where(h => h.Remaining > Tolerance)) yield return (held, "a peer");
    }
}
