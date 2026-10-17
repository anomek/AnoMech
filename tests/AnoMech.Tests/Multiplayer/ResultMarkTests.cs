using System.Globalization;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// The run's success and failure marks on every player, as every client shows them: the host's
// helper casts each one at its player's seat, this client's own player's and the other players'
// alike, never a player at itself, in every multiplayer scenario. A run the bots clear gives
// every player one success mark; a run whose players never move, under god mode, gives every
// player a failure mark each time the host's own player gets one.
public class ResultMarkTests
{
    private static readonly PlayerSeat[] Seats = [new(PartyRole.MeleeDpsA), new(PartyRole.MainTank), new(PartyRole.CasterDps)];

    private static IEnumerable<TestCaseData> Scenarios() =>
        from scenario in ScenarioCatalog.Create().Scenarios.Where(s => s.SupportsMultiplayer)
        select new TestCaseData(scenario.GetType()).SetArgDisplayNames(scenario.FullName());

    [TestCaseSource(nameof(Scenarios))]
    public void EveryPlayerSeesEveryPlayersSuccessMark(Type scenarioType)
    {
        var run = Play(scenarioType, bots: true);
        Assert.That(run.HostOutcome, Is.EqualTo(SessionStatus.Succeeded));
        Verify(run, ScenarioSession.MechanicSuccessActionId, 1);
        Verify(run, ScenarioSession.MechanicFailureActionId, 0);
    }

    [TestCaseSource(nameof(Scenarios))]
    public void EveryPlayerSeesEveryPlayersFailureMarks(Type scenarioType)
    {
        var run = Play(scenarioType, bots: false);
        var mistakes = Marks(run.Host, run.Host.Seat, ScenarioSession.MechanicFailureActionId);
        Assume.That(mistakes, Is.Positive, "nobody standing still made a mistake");
        Verify(run, ScenarioSession.MechanicFailureActionId, mistakes);
        Verify(run, ScenarioSession.MechanicSuccessActionId, 0);
    }

    private static MultiplayerRun Play(Type scenarioType, bool bots)
    {
        var seats = Seats.Select(s => s with { BotControlled = bots }).ToArray();
        var run = MultiplayerRun.Play(scenarioType, 0, 1, seats[0], seats[1..], immortal: true,
            configure: client => client.Config.EnableMechanicResultMarks = true);
        Assert.That(run.Failure, Is.Null);
        return run;
    }

    private static void Verify(MultiplayerRun run, uint mark, int perPlayer)
    {
        var wrong = new List<string>();
        foreach (var client in run.Peers.Prepend(run.Host))
        {
            foreach (var seat in Seats.Select(s => s.Role))
                if (Marks(client, seat, mark) is var count && count != perPlayer)
                    wrong.Add($"{client.Name} showed {count} of mark {mark} on {seat}, not {perPlayer}");
            if (MarkRows(client, mark).Count(row => row.GetValueOrDefault("actor") == row["target"]) is var self and > 0)
                wrong.Add($"{client.Name} showed {self} of mark {mark} cast by a player at itself");
        }
        Assert.That(wrong, Is.Empty);
    }

    private static int Marks(TestClient client, PartyRole seat, uint mark)
        => MarkRows(client, mark).Count(row => client.NameAt(uint.Parse(row["target"], NumberStyles.HexNumber),
            double.Parse(row["t"], CultureInfo.InvariantCulture)) == seat.ToString());

    private static IEnumerable<Dictionary<string, string>> MarkRows(TestClient client, uint mark)
    {
        var id = mark.ToString(CultureInfo.InvariantCulture);
        return client.Fake.Timeline.Rows.Where(row => row["kind"] == "ability" && row["id"] == id
                                                      && row.GetValueOrDefault("target") is { Length: > 0 });
    }
}
