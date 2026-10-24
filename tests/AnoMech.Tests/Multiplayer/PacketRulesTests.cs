using System.Reflection;
using AnoMech.Core.Game;
using AnoMech.Multiplayer;

namespace AnoMech.Tests;

// A category without a rule never reaches a peer. Every category the sim sends needs one, in every
// strat of every scenario played solo, and so does every category ActorControl names, for what a
// run doesn't reach (falls, wipes, limit breaks). A rule for a category neither sent nor named is
// stale: a peer would take it from the host for nothing.
public class PacketRulesTests
{
    [Test]
    public void TheRulesCoverExactlyWhatTheSimSendsOrNames()
    {
        var sent = new Dictionary<(string Packet, uint Category), string>();
        ScenarioCensus.EveryStrat((run, fake) =>
        {
            foreach (var row in fake.Timeline.Rows)
                if (PacketOf(row["kind"]) is { } packet)
                    sent.TryAdd((packet, Convert.ToUInt32(row["id"], 16)), run);
        });
        var named = typeof(ActorControl).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(uint) && f.Name.EndsWith("Category"))
            .Select(f => (Name: f.Name, Category: (uint)f.GetRawConstantValue()!))
            .ToList();

        var problems = new List<string>();
        foreach (var ((packet, category), run) in sent)
            if (!Rules(packet).Contains(category))
                problems.Add($"{run} sent {packet} 0x{category:X}, which has no rule");
        foreach (var (name, category) in named)
            if (!Rules(Control).Contains(category) && !Rules(TargetControl).Contains(category))
                problems.Add($"ActorControl.{name} (0x{category:X}) has no rule");
        foreach (var packet in new[] { Control, TargetControl, Director })
        foreach (var category in Rules(packet))
            if (!sent.ContainsKey((packet, category)) && named.All(n => n.Category != category))
                problems.Add($"the {packet} 0x{category:X} rule is stale: the sim neither sends nor names it");
        Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems));
    }

    private const string Control = "ActorControl", TargetControl = "ActorControlTarget", Director = "DirectorUpdate";

    private static string? PacketOf(string kind) => kind switch
    {
        "ac" or "ac_self" => Control,
        "ac_target" => TargetControl,
        "director" => Director,
        _ => null,
    };

    private static IEnumerable<uint> Rules(string packet) => packet switch
    {
        Control => PacketRules.Controls.Keys,
        TargetControl => PacketRules.TargetControls.Keys,
        _ => PacketRules.DirectorCategories,
    };
}
