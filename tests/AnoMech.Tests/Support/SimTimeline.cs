using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using AnoMech.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Tests;

// A spawn as the client got it: its config, and the world placement handed with it.
internal sealed record SpawnData(object Config, Placement Placement);

// What the sim sent to the game client, one row per packet-shaped call on the fakes, in the
// replay timeline CSV's columns (.claude/tools/replay_timeline.py), so a run can be diffed against
// a replay. ActorControls stay raw (`ac` rows, category in `id`); the comparer names them.
internal sealed class SimTimeline
{
    public static readonly string[] Columns =
        ["t", "kind", "actor", "actor_name", "id", "name", "target", "target_name", "x", "z", "rot", "detail",
         "omen_delay", "ballista", "anim_lock", "anim_var", "knockback", "damage_type", "y", "p", "source"];

    private readonly List<Dictionary<string, string>> rows = [];
    private readonly List<(Dictionary<string, string> Row, object Data)> given = [];
    private int runRows, runGiven;

    public Func<float> Clock { get; set; } = () => 0f;

    // file:line of the scheduler entry fired last: the one that sent a row, unless the row came from a
    // per-frame tick (cast bar ends, movement) after it.
    public Func<string> Source { get; set; } = () => "";

    public IReadOnlyList<Dictionary<string, string>> Rows => rows;

    // The rows of the calls that carried data (spawns, casts, action effects), with that data as the
    // client got it.
    public IReadOnlyList<(Dictionary<string, string> Row, object Data)> Given => given;

    // From StartRun on: what the zone-in gave the client before it is each client's own, never the
    // host's to send.
    public IEnumerable<Dictionary<string, string>> RunRows => rows.Skip(runRows);
    public IEnumerable<(Dictionary<string, string> Row, object Data)> RunGiven => given.Skip(runGiven);

    public void StartRun()
    {
        runRows = rows.Count;
        runGiven = given.Count;
    }

    // data: also kept whole in Given, and every field of it in the row's `fields` (not a replay
    // column).
    public void Add(string kind, uint actor = 0, string actorName = "", string id = "", string name = "",
        uint target = 0, string targetName = "", Vector3? position = null, float? rotation = null, string detail = "",
        string p = "", IReadOnlyDictionary<string, string>? extra = null, object? data = null)
    {
        var row = new Dictionary<string, string>
        {
            ["t"] = F(Clock(), "F3"),
            ["kind"] = kind,
            ["actor"] = actor == 0 ? "" : actor.ToString("X8"),
            ["actor_name"] = actorName,
            ["id"] = id,
            ["name"] = name,
            ["target"] = target == 0 ? "" : target.ToString("X8"),
            ["target_name"] = targetName,
            ["detail"] = detail,
            ["p"] = p,
            ["source"] = Source(),
        };
        if (position is { } pos)
        {
            row["x"] = F(pos.X);
            row["y"] = F(pos.Y);
            row["z"] = F(pos.Z);
        }
        if (rotation is { } rot) row["rot"] = F(rot);
        if (extra != null)
            foreach (var (key, value) in extra)
                row[key] = value;
        if (data != null)
        {
            row["fields"] = Fields(data);
            given.Add((row, data));
        }
        rows.Add(row);
    }

    // Every property of a call's data, nested ones included, as Name=value. An actor shows as @ and
    // its entity id, for the comparer to name as each client does: a GameObjectId, an OwnerId, or a
    // uint named for an EntityId.
    public static string Fields(object value)
    {
        var text = new StringBuilder();
        AppendProperties(text, value);
        return text.ToString();
    }

    // Its properties, or for a struct that has none its public fields.
    private static void AppendProperties(StringBuilder text, object value)
    {
        var type = value.GetType();
        var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => (p.Name, Value: p.GetValue(value)))
            .ToList();
        if (members.Count == 0)
            members = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => (f.Name, Value: f.GetValue(value))).ToList();
        var first = true;
        foreach (var (name, field) in members)
        {
            if (!first) text.Append(' ');
            first = false;
            text.Append(name).Append('=');
            if (field is uint entityId && (name == "OwnerId" || name.EndsWith("EntityId", StringComparison.Ordinal)))
                text.Append(Actor(entityId));
            else
                AppendValue(text, field);
        }
    }

    private static void AppendValue(StringBuilder text, object? value)
    {
        switch (value)
        {
            case null:
                text.Append("null");
                return;
            case float f:
                text.Append(Number(f));
                return;
            case string or bool or Enum or byte or sbyte or short or ushort or int or uint or long or ulong:
                text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case GameObjectId id:
                text.Append(Actor(id.ObjectId));
                return;
            case Vector3 v:
                text.Append($"({Number(v.X)},{Number(v.Y)},{Number(v.Z)})");
                return;
            case Vector2 v:
                text.Append($"({Number(v.X)},{Number(v.Y)})");
                return;
            case IEnumerable items:
                text.Append('[');
                var first = true;
                foreach (var item in items)
                {
                    if (!first) text.Append(", ");
                    first = false;
                    AppendValue(text, item);
                }
                text.Append(']');
                return;
        }
        text.Append('{');
        AppendProperties(text, value);
        text.Append('}');
    }

    // A value that rounds to zero shows as 0.00 whichever side of it it fell: a peer's local position
    // comes back from world space a rounding off the host's.
    private static string Number(float value) => F(value) is "-0.00" ? "0.00" : F(value);

    private static string Actor(uint entityId) => entityId is 0 or 0xE0000000 ? "@none" : $"@{entityId:X8}";

    // Hex, trailing zeros dropped, like the replay's `p`.
    public static string Params(params uint[] values)
    {
        var count = values.Length;
        while (count > 0 && values[count - 1] == 0) count--;
        return string.Join(",", values.Take(count).Select(v => $"0x{v:X}"));
    }

    public static string F(float value, string format = "F2") => value.ToString(format, CultureInfo.InvariantCulture);

    public void Write(string path)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(",", Columns));
        foreach (var row in rows)
            text.AppendLine(string.Join(",", Columns.Select(c => Quote(row.GetValueOrDefault(c, "")))));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, text.ToString());
    }

    private static string Quote(string value)
        => value.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
