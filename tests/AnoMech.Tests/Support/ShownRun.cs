using System.Globalization;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// A headless run with what its client was shown (SimTimeline rows) and the seat behind each party
// entity id, for tests that judge a fight by what a player would see.
internal sealed class ShownRun
{
    public ScenarioRun Run { get; }
    public IReadOnlyList<Dictionary<string, string>> Rows { get; }
    // By entity id as the rows write it.
    public IReadOnlyDictionary<string, PartyRole> Seats { get; }

    private ShownRun(ScenarioRun run, IReadOnlyList<Dictionary<string, string>> rows, IReadOnlyDictionary<string, PartyRole> seats)
        => (Run, Rows, Seats) = (run, rows, seats);

    public static ShownRun Execute(Type scenarioType, int strat, int seed, ScenarioRunOptions? options = null)
    {
        options ??= new ScenarioRunOptions();
        var seats = new Dictionary<string, PartyRole>();
        var probe = options.Probe;
        var run = ScenarioRun.Execute(scenarioType, strat, seed, options with
        {
            Probe = p =>
            {
                if (seats.Count < 8)
                    foreach (var role in PerRole.All)
                        if (p.Member(role) is { EntityId: not 0 } member)
                            seats[member.EntityId.ToString("X8")] = role;
                probe?.Invoke(p);
            },
        });
        return new ShownRun(run, ((FakeBattleCharas)Natives.BattleCharas).Timeline.Rows, seats);
    }

    public string IdOf(PartyRole role) => Seats.Single(s => s.Value == role).Key;

    public PartyRole? SeatOf(string entityId) => Seats.TryGetValue(entityId, out var role) ? role : null;

    public IEnumerable<Dictionary<string, string>> Of(string kind) => Rows.Where(r => r["kind"] == kind);

    public IEnumerable<Dictionary<string, string>> Of(string kind, uint id) => Of(kind).Where(r => r["id"] == id.ToString(CultureInfo.InvariantCulture));

    // The entity ids an ability hit, in its order.
    public static string[] Hits(Dictionary<string, string> ability)
        => ability.GetValueOrDefault("targets", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static float Time(Dictionary<string, string> row) => Number(row, "t");

    public static float Number(Dictionary<string, string> row, string column)
        => float.Parse(row[column], CultureInfo.InvariantCulture);
}
