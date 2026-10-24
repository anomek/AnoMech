using System.Globalization;
using System.Text.RegularExpressions;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using AnoMech.Multiplayer;

namespace AnoMech.Tests;

// At: seconds into the host's run it came in.
internal sealed record ParityGap(string Kind, string What, double At, int Count)
{
    public override string ToString() => $"{Kind}: {What} (first at t={At:F2}{(Count > 1 ? $", {Count} times" : "")})";
}

// What the host's game client was told against what a peer's was, row for row of their timelines
// (SimTimeline), in each of the host's runs: every row one client got has to reach the other
// within Slack, naming the same actors and carrying the same values. Left out is only what each client makes for itself: its
// party's doppels and their ZoneIn, its own player's forced moves and falls, the players'
// movement, which goes out as their poses, and what the two clients' own players press, which
// each one's own game plays.
//
// What each client sets up for itself at a run's start (OwnSetup) has to be in place on the peer
// before everything the host's client was shown after it: the host's packets reaching a client
// still missing that setup show the run without it, like a name or line of text that never
// resolves, or a timeline played before it was loaded.
internal static partial class ClientParity
{
    private const string OwnPlayer = "own player";
    // One without a rule reaches no peer: compared as one everyone gets, it shows as a gap.
    private static readonly ControlRule Unruled = new(Delivery.Everyone);

    // The run's text and file tables, the timelines it preloads, its waymarks and its music.
    private static readonly HashSet<string> OwnSetup = ["rsv", "rsf", "preload", "waymarks", "bgm"];

    private readonly record struct Row(double At, string Key, bool Own);

    // Timeline times are rounded to the millisecond: a row from the start's own frame can read as
    // just before it.
    private const double TimelineRounding = 0.0005;

    // How far apart the two clients may show one thing: the run's worst delivery, a resent
    // message's included, a hitch of the peer's own, its copy of a new actor arriving, and a few
    // frames.
    public static double Slack(MultiplayerRun run)
        => Math.Max(run.Network.MaxHostDelay, run.MaxStreamDelay()) + run.PeerLink.PeerHitchMax + MultiplayerRun.PeerSpawnFrames.Max / run.PeerLink.PeerFps + 0.1;

    public static IReadOnlyList<ParityGap> Compare(MultiplayerRun run, TestClient peer)
    {
        var slack = Slack(run);
        var players = new HashSet<string> { run.Host.Seat.ToString() };
        foreach (var p in run.Peers) players.Add(p.Seat.ToString());
        var host = Rows(run.Host, players, run.Host.Seat, peer.Seat);
        var other = Rows(peer, players, run.Host.Seat, peer.Seat);
        var otherByKey = Enumerable.Range(0, other.Count).ToLookup(i => other[i].Key);
        var matched = new bool[other.Count];
        var gaps = new List<(string Kind, string What, double At)>();
        bool Within(double at, double from) => run.Fights.Any(f => at >= f.StartedAt + from - TimelineRounding && at <= f.EndedAt - slack);
        foreach (var (at, key, _) in host)
        {
            if (!Within(at, 0)) continue;
            var index = Match(at, otherByKey[key], other, matched, slack);
            if (index >= 0) matched[index] = true;
            else gaps.Add(("host only", key, at));
        }
        for (var i = 0; i < other.Count; i++)
        {
            var (at, key, _) = other[i];
            if (matched[i] || !Within(at, slack)) continue;
            gaps.Add(($"{peer.Name} only", key, at));
        }
        gaps.AddRange(SetUpLate(host, other, otherByKey, at => Within(at, 0), slack, peer.Name));
        double IntoItsFight(double at) => at - run.Fights.Last(f => f.StartedAt <= at + TimelineRounding).StartedAt;
        return gaps.GroupBy(g => (g.Kind, Pattern: Numbers().Replace(g.What, "#")))
            .Select(g => new ParityGap(g.Key.Kind, g.First().What, IntoItsFight(g.Min(x => x.At)), g.Count()))
            .OrderBy(g => g.At).ToList();
    }

    // Among the rows with the key, in timeline order.
    private static int Match(double at, IEnumerable<int> candidates, List<Row> other, bool[] matched, double slack)
    {
        var best = -1;
        foreach (var i in candidates)
        {
            if (matched[i] || Math.Abs(other[i].At - at) > slack) continue;
            if (best < 0 || Math.Abs(other[i].At - at) < Math.Abs(other[best].At - at)) best = i;
        }
        return best;
    }

    // Each own setup row the peer had only after every copy it got of a row the host showed later.
    // A row the peer got twice within the slack (a stage and its run sending the same weather)
    // counts at its last copy, an own row at its first. Walked from the host's end: `first` is the
    // earliest such copy of anything the host showed after the row at hand.
    private static IEnumerable<(string Kind, string What, double At)> SetUpLate(List<Row> host, List<Row> other, ILookup<string, int> otherByKey,
        Func<double, bool> inFight, double slack, string peerName)
    {
        var first = int.MaxValue;
        var firstShown = "";
        for (var h = host.Count - 1; h >= 0; h--)
        {
            var (at, key, own) = host[h];
            if (!inFight(at)) continue;
            var copies = otherByKey[key].Where(i => Math.Abs(other[i].At - at) <= slack).ToList();
            if (copies.Count == 0) continue;
            if (!own)
            {
                if (copies.Max() < first) (first, firstShown) = (copies.Max(), key);
                continue;
            }
            if (copies.Min() > first) yield return ($"{peerName} set up late", $"{key[..key.IndexOf(' ')]} after {firstShown}", at);
        }
    }

    // peerSeat: the compared peer's, the one player whose own controls both clients have.
    private static List<Row> Rows(TestClient client, IReadOnlySet<string> players, PartyRole hostSeat, PartyRole peerSeat)
    {
        var rows = new List<Row>();
        foreach (var row in client.Fake.Timeline.Rows)
        {
            var at = double.Parse(row["t"], CultureInfo.InvariantCulture);
            if (Key(client, row, at, players, hostSeat.ToString(), peerSeat.ToString()) is { } key) rows.Add(new Row(at, key, OwnSetup.Contains(row["kind"])));
        }
        return rows;
    }

    private static string? Key(TestClient client, Dictionary<string, string> row, double at, IReadOnlySet<string> players, string hostSeat, string peerSeat)
    {
        string Name(string column) => row.GetValueOrDefault(column) is { Length: > 0 } hex ? client.NameAt(uint.Parse(hex, NumberStyles.HexNumber), at) : "nobody";
        string Value(string column) => row.GetValueOrDefault(column, "");
        string Position() => $"({Value("x")},{Value("y")},{Value("z")})";

        var kind = row["kind"];
        var actor = Name("actor");
        var seat = Enum.TryParse<PartyRole>(actor, out _);
        var player = players.Contains(actor);
        switch (kind)
        {
            case "spawn_doppel":
            case "despawn" when seat:
            case "afflictions":
                return null;
            case "set_pos" or "move" or "npc_knockback" or "forced_follow" or "forced_stop" when player:
                return null;
            case "cast" or "ability" when (actor == hostSeat || actor == peerSeat) && PressedByAPlayer(Value("id")):
                return null;
            case "ac" or "ac_self":
            {
                var category = Convert.ToUInt32(Value("id"), 16);
                var rule = PacketRules.Controls.GetValueOrDefault(category) ?? Unruled;
                if (rule.Delivery == Delivery.ZoneIn && seat) return null;
                if (rule.Delivery == Delivery.Movement && player) return null;
                if (rule.Delivery == Delivery.SeatOwner && player && actor != peerSeat) return null;
                if (rule.Delivery == Delivery.EachOwnPlayer && actor == client.Seat.ToString()) actor = OwnPlayer;
                return $"ac {actor} 0x{category:X} {NamedArgs(client, Value("p"), rule.ActorArg, at)}";
            }
            case "ac_target":
            {
                var category = Convert.ToUInt32(Value("id"), 16);
                var rule = PacketRules.TargetControls.GetValueOrDefault(category) ?? Unruled;
                if (rule.Delivery == Delivery.EachOwnPlayer && actor == client.Seat.ToString()) actor = OwnPlayer;
                return $"ac_target {actor} 0x{category:X} target={Name("target")} {NamedArgs(client, Value("p"), rule.ActorArg, at)}";
            }
            case "cast":
                return $"cast {actor} {Value("id")} target={Name("target")} {Position()} rot={Value("rot")} {RelativeRotation().Replace(Value("detail"), "")} omen={Value("omen_delay")} ballista={Name("ballista")} {Fields(client, row, at)}";
            case "ability":
                var targets = string.Join(" ", Value("targets").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => client.NameAt(uint.Parse(t, NumberStyles.HexNumber), at)));
                return $"ability {actor} {Value("id")} at={Name("target")} hits=[{targets}] rot={Value("rot")} lock={Value("anim_lock")} var={Value("anim_var")} damage={Value("damage_type")} knockback={Value("knockback")} {Fields(client, row, at)}";
            case "impact":
                return $"impact {actor} {Value("id")} target={Name("target")}";
            case "status_gain" or "status_lose":
                return $"{kind} {actor} {Value("id")} source={Name("target")} {Value("detail")}";
            case "spawn" or "eobj_spawn":
                return $"{kind} {actor} {Value("id")} {Position()} rot={Value("rot")} {Value("detail")} {Fields(client, row, at)}";
            case "set_pos" or "move" or "npc_knockback":
                return $"{kind} {actor} {Value("id")} {Position()} rot={Value("rot")} {Value("detail")}";
            case "enemy_list" or "despawn":
                return $"{kind} {actor} {Value("id")} {Value("detail")}";
            default:
                return $"{kind} {actor} {Value("id")} {Value("detail")} {Value("p")}";
        }
    }

    // A job's action or a limit break: only a player presses one.
    private static bool PressedByAPlayer(string id)
        => uint.TryParse(id, CultureInfo.InvariantCulture, out var actionId) && Natives.Data.Action(actionId) is { } action
           && (action.IsPlayerAction || LimitBreakHandler.IsLimitBreak(actionId));

    // Only the host's: its own sim despawns the object, and only the client whose press an effect
    // answers gets the press's sequence.
    public static readonly string[] HostOnly = ["Lifetime", "SourceSequence"];

    // Every field of what the client was given (SimTimeline.Fields), each actor named as the client
    // names it, the host's own left out.
    public static string Fields(TestClient client, Dictionary<string, string> row, double at)
    {
        var fields = ActorId().Replace(row.GetValueOrDefault("fields", ""), m => client.NameAt(uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber), at));
        return HostOnly.Aggregate(fields, (f, name) => Regex.Replace(f, $@"\b{name}=[^\s{{}}\[\],]+ ?", ""));
    }

    [GeneratedRegex(@"@([0-9A-F]{8})")]
    private static partial Regex ActorId();

    // The timeline's hex argument list, with the one that names an actor named.
    private static string NamedArgs(TestClient client, string args, int entityIndex, double at)
    {
        if (entityIndex < 0 || args.Length == 0) return args;
        var parts = args.Split(',');
        if (entityIndex < parts.Length)
            parts[entityIndex] = client.NameAt(Convert.ToUInt32(parts[entityIndex], 16), at);
        return string.Join(",", parts);
    }

    // The cast's facing relative to the caster's model, which each client turns at its own pace.
    [GeneratedRegex(@" ?rel_rot=[-\d.]+")]
    private static partial Regex RelativeRotation();

    [GeneratedRegex(@"\d+(\.\d+)?")]
    private static partial Regex Numbers();
}
