using System.Globalization;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Tests;

// A player's own actions as the rest of the party sees them. Every action the Action sheet gives
// players is pressed once, in turn by the host and by each peer, as an instant action, a cast
// that completes and a cast that's interrupted, aimed at a bot's seat, an actor the host spawned
// or nobody. Every other client has to show it on that player's seat with its target, its cast
// bar, its cancel or the action, and the player's own client, whose game played it already, none
// of it again. No case names an action or a job, so a new action is covered as the sheet has it.
public class PlayerActionTests
{
    private static readonly PlayerSeat Host = new(PartyRole.MeleeDpsA, BotControlled: false);
    private static readonly PlayerSeat[] Peers = [new(PartyRole.MainTank, BotControlled: false), new(PartyRole.CasterDps, BotControlled: false)];
    private const PartyRole TargetSeat = PartyRole.RegenHealer;
    private const double FirstPress = 1, PressEvery = 0.05, CastFor = 0.02;
    private const float CastSeconds = 2.5f;
    private const uint CancelCastCategory = 0x0F;

    private enum Kind { Instant, CastCompleted, CastInterrupted }

    private enum Aim { Seat, Spawned, Nobody }

    private sealed record Press(int Client, uint ActionId, Kind Kind, Aim Aim, double At);

    [Test]
    public void EveryOtherClientShowsEveryPlayersAction()
    {
        var presses = DataminingGameData.PlayerActionIds().Order()
            .Select((id, k) => new Press(k % 3, id, (Kind)(k / 3 % 3), (Aim)(k / 9 % 3), FirstPress + k / 3 * PressEvery))
            .ToList();
        var targets = new Dictionary<Press, string>();
        var inputs = presses.SelectMany(p => Steps(p, targets)).ToList();
        var scenario = ScenarioCatalog.Create().Scenarios.First(s => s.SupportsMultiplayer).GetType();
        var run = MultiplayerRun.Play(scenario, 0, 1, Host, Peers, immortal: true, inputs: inputs);
        Assert.That(run.Failure, Is.Null);
        Assert.That(run.InputsLeft, Is.Zero, "presses the fight ended before");

        var clients = run.Peers.Prepend(run.Host).ToList();
        var shownBy = clients.ToDictionary(c => c, Shown);
        var problems = new List<string>();
        foreach (var press in presses)
        {
            var presser = clients[press.Client];
            foreach (var client in clients)
            {
                var shown = shownBy[client].GetValueOrDefault((presser.Seat.ToString(), press.ActionId)) ?? [];
                if (ReferenceEquals(client, presser))
                {
                    if (shown.Count > 0) problems.Add($"{presser.Name}'s own {press} came back to it: {string.Join(", ", shown)}");
                    continue;
                }
                foreach (var expected in Expected(press, presser.Seat, targets.GetValueOrDefault(press)))
                    if (!shown.Contains(expected))
                        problems.Add($"{client.Name} never showed {presser.Name}'s {press}: no {expected} (has {string.Join(", ", shown)})");
            }
        }
        var refused = clients.SelectMany(c => c.Log.Warnings).Where(w => w.Contains("[SimAssets]")).Distinct().ToList();
        foreach (var problem in problems.Take(40)) TestContext.Out.WriteLine(problem);
        Assert.That(refused, Is.Empty, "player actions a client refused");
        Assert.That(problems, Is.Empty, $"{problems.Count} of {presses.Count} presses not shown as they should be");
    }

    // What the player's game reports of a press (IUserActions.Acted): a cast's start, then its end.
    private static IEnumerable<(double At, int Client, Action<TestClient> Input)> Steps(Press p, Dictionary<Press, string> targets)
    {
        void Act(TestClient client, PlayerActionStep step, float castSeconds = 0f)
            => client.Fake.UserActions.Act(new PlayerAction(step, ActionType.Action, p.ActionId, TargetOf(client, p, targets), castSeconds));

        if (p.Kind == Kind.Instant)
        {
            yield return (p.At, p.Client, c => Act(c, PlayerActionStep.Resolved));
            yield break;
        }
        yield return (p.At, p.Client, c => Act(c, PlayerActionStep.CastStarted, CastSeconds));
        yield return (p.At + CastFor, p.Client, c => Act(c, p.Kind == Kind.CastCompleted ? PlayerActionStep.Resolved : PlayerActionStep.Interrupted));
    }

    // The presser's own GameObjectId for what it aims at, remembering how every client names it.
    private static ulong TargetOf(TestClient client, Press p, Dictionary<Press, string> targets)
    {
        SimCharacter? target = p.Aim switch
        {
            Aim.Seat => client.Game.World.Party.Get(TargetSeat),
            Aim.Spawned => client.Game.World.Children.OfType<SimEnemy>().FirstOrDefault(e => e.IsActive),
            _ => null,
        };
        if (target == null) return 0;
        targets.TryAdd(p, client.NameAt(target.EntityId, double.MaxValue));
        return (ulong)target.GameObjectId;
    }

    // An action aimed at nobody plays on its own player, its cast bar on nobody.
    private static IEnumerable<string> Expected(Press p, PartyRole seat, string? target) => p.Kind switch
    {
        Kind.Instant => [$"ability at={target ?? seat.ToString()}"],
        Kind.CastCompleted => [$"cast target={target ?? "nobody"}", $"ability at={target ?? seat.ToString()}"],
        _ => [$"cast target={target ?? "nobody"}", "cancel"],
    };

    // Each cast, action and cast cancel a client showed, by the character's name and the action.
    private static Dictionary<(string Actor, uint Action), List<string>> Shown(TestClient client)
    {
        var shown = new Dictionary<(string, uint), List<string>>();
        void Add(string actor, uint action, string what)
        {
            if (!shown.TryGetValue((actor, action), out var list)) shown[(actor, action)] = list = [];
            list.Add(what);
        }
        foreach (var row in client.Fake.Timeline.Rows)
        {
            var kind = row["kind"];
            if (kind is not ("cast" or "ability" or "ac" or "ac_self")) continue;
            var at = double.Parse(row["t"], CultureInfo.InvariantCulture);
            var actor = Name(client, row, "actor", at);
            switch (kind)
            {
                case "cast":
                    Add(actor, uint.Parse(row["id"], CultureInfo.InvariantCulture), $"cast target={Name(client, row, "target", at)}");
                    break;
                case "ability":
                    Add(actor, uint.Parse(row["id"], CultureInfo.InvariantCulture), $"ability at={Name(client, row, "target", at)}");
                    break;
                default:
                    if (Convert.ToUInt32(row["id"], 16) != CancelCastCategory) break;
                    foreach (var arg in row.GetValueOrDefault("p", "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                        Add(actor, Convert.ToUInt32(arg, 16), "cancel");
                    break;
            }
        }
        return shown;
    }

    private static string Name(TestClient client, Dictionary<string, string> row, string column, double at)
        => row.GetValueOrDefault(column) is { Length: > 0 } hex ? client.NameAt(uint.Parse(hex, NumberStyles.HexNumber), at) : "nobody";
}
