using System.Globalization;
using System.Text.RegularExpressions;
using AnoMech.Core.Game.Party;

namespace AnoMech.Tests;

// At: seconds into the host's run it came in.
internal sealed record ParityGap(string Kind, string What, double At, int Count)
{
    public override string ToString() => $"{Kind}: {What} (first at t={At:F2}{(Count > 1 ? $", {Count} times" : "")})";
}

// What the host's game client was told against what a peer's was, row for row of their timelines
// (SimTimeline), in each of the host's runs: every row one client got has to reach the other
// within Slack, naming the same actors and carrying the same values. Left out is only what each client makes for itself: its
// party's doppels and their ZoneIn, its own player's forced moves and falls, and the players'
// movement, which goes out as their poses.
internal static partial class ClientParity
{
    // ActorControlSelf a player's own client alone gets, and the party-wide ones every client gets
    // for its own player.
    private static readonly HashSet<uint> OwnSelf = [0x01, 0xED, 0x107];
    private static readonly HashSet<uint> PartySelf = [0xB7, 0x1F9];
    private static readonly HashSet<uint> MotionControls = [0xDC, 0xDF, 0xE1, 0xF1, 0x602, 0x603];
    private const uint ZoneIn = 0xC8;
    private const uint Marking = 0x1F6;
    private const string OwnPlayer = "own player";

    // How far apart the two clients may show one thing: the run's worst delivery, a resent
    // message's included, a hitch of the peer's own, its copy of a new actor arriving, and a few
    // frames.
    public static double Slack(MultiplayerRun run)
        => Math.Max(run.Relay.MaxHostDelay, run.MaxStreamDelay()) + run.PeerLink.PeerHitchMax + MultiplayerRun.PeerSpawnFrames.Max / run.PeerLink.PeerFps + 0.1;

    public static IReadOnlyList<ParityGap> Compare(MultiplayerRun run, TestClient peer)
    {
        var slack = Slack(run);
        var players = new HashSet<string> { run.Host.Seat.ToString() };
        foreach (var p in run.Peers) players.Add(p.Seat.ToString());
        var host = Rows(run.Host, players, peer.Seat);
        var other = Rows(peer, players, peer.Seat);
        var matched = new bool[other.Count];
        var gaps = new List<(string Kind, string What, double At)>();
        bool Within(double at, double from) => run.Fights.Any(f => at >= f.StartedAt + from && at <= f.EndedAt - slack);
        foreach (var (at, key) in host)
        {
            if (!Within(at, 0)) continue;
            var index = Match(at, key, other, matched, slack);
            if (index >= 0) matched[index] = true;
            else gaps.Add(("host only", key, at));
        }
        for (var i = 0; i < other.Count; i++)
        {
            var (at, key) = other[i];
            if (matched[i] || !Within(at, slack)) continue;
            gaps.Add(($"{peer.Name} only", key, at));
        }
        double IntoItsFight(double at) => at - run.Fights.Last(f => f.StartedAt <= at).StartedAt;
        return gaps.GroupBy(g => (g.Kind, Pattern: Numbers().Replace(g.What, "#")))
            .Select(g => new ParityGap(g.Key.Kind, g.First().What, IntoItsFight(g.Min(x => x.At)), g.Count()))
            .OrderBy(g => g.At).ToList();
    }

    private static int Match(double at, string key, List<(double At, string Key)> other, bool[] matched, double slack)
    {
        var best = -1;
        for (var i = 0; i < other.Count; i++)
        {
            if (matched[i] || other[i].Key != key || Math.Abs(other[i].At - at) > slack) continue;
            if (best < 0 || Math.Abs(other[i].At - at) < Math.Abs(other[best].At - at)) best = i;
        }
        return best;
    }

    // peerSeat: the compared peer's, the one player whose own controls both clients have.
    public static List<(double At, string Key)> Rows(TestClient client, IReadOnlySet<string> players, PartyRole peerSeat)
    {
        var rows = new List<(double, string)>();
        foreach (var row in client.Fake.Timeline.Rows)
        {
            var at = double.Parse(row["t"], CultureInfo.InvariantCulture);
            if (Key(client, row, at, players, peerSeat.ToString()) is { } key) rows.Add((at, key));
        }
        return rows;
    }

    private static string? Key(TestClient client, Dictionary<string, string> row, double at, IReadOnlySet<string> players, string peerSeat)
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
            case "ac" or "ac_self":
            {
                var category = Convert.ToUInt32(Value("id"), 16);
                if (category == ZoneIn && seat) return null;
                if (MotionControls.Contains(category) && player) return null;
                if (OwnSelf.Contains(category) && player && actor != peerSeat) return null;
                if (PartySelf.Contains(category) && actor == client.Seat.ToString()) actor = OwnPlayer;
                return $"ac {actor} 0x{category:X} {NamedArgs(client, Value("p"), EntityArgIndex(category), at)}";
            }
            case "ac_target":
            {
                var category = Convert.ToUInt32(Value("id"), 16);
                if (category == Marking && actor == client.Seat.ToString()) actor = OwnPlayer;
                return $"ac_target {actor} 0x{category:X} target={Name("target")} {NamedArgs(client, Value("p"), category == Marking ? 1 : -1, at)}";
            }
            case "cast":
                return $"cast {actor} {Value("id")} target={Name("target")} {Position()} rot={Value("rot")} {RelativeRotation().Replace(Value("detail"), "")} omen={Value("omen_delay")} ballista={Name("ballista")}";
            case "ability":
                var targets = string.Join(" ", Value("targets").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => client.NameAt(uint.Parse(t, NumberStyles.HexNumber), at)));
                return $"ability {actor} {Value("id")} at={Name("target")} hits=[{targets}] rot={Value("rot")} lock={Value("anim_lock")} var={Value("anim_var")} damage={Value("damage_type")} knockback={Value("knockback")}";
            case "impact":
                return $"impact {actor} {Value("id")} target={Name("target")}";
            case "status_gain" or "status_lose":
                return $"{kind} {actor} {Value("id")} source={Name("target")} {Value("detail")}";
            case "spawn" or "eobj_spawn" or "set_pos" or "move" or "npc_knockback":
                return $"{kind} {actor} {Value("id")} {Position()} rot={Value("rot")} {Value("detail")}";
            case "enemy_list" or "despawn":
                return $"{kind} {actor} {Value("id")} {Value("detail")}";
            default:
                return $"{kind} {actor} {Value("id")} {Value("detail")} {Value("p")}";
        }
    }

    private static int EntityArgIndex(uint category) => category switch
    {
        0x15 or 0x23 => 2,
        0x22 => 1,
        0x25F => 0,
        _ => -1,
    };

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
