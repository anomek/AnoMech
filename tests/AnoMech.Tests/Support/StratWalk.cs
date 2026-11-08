using System.Numerics;

namespace AnoMech.Tests;

// One seat's walk through a strat, as its own client saw it frame by frame on that client's run
// clock, recorded while a debug bot played the seat. Played back, it is the human's own movement
// input: the client puts the character where the strat had it and holds a key while it walks, as
// someone following the strat by hand would. The seat stays a human's, whose own mitigation the
// host judges; the debug bot itself would make it a bot.
internal sealed class StratWalk
{
    private const float ClockSlack = 1e-4f;

    private readonly List<Step> steps = [];

    // Walking: the player moved during the frame that started here.
    internal readonly record struct Step(float Clock, Vector3 Position, float Rotation, bool Walking, bool ActionsLocked);

    public IReadOnlyList<Step> Steps => steps;

    // At the start of each of the seat's own client's frames.
    public void Record(TestClient client)
    {
        if (client.Game.ActiveScenario is null || client.Game.Player is not { } player) return;
        if (steps.Count > 0) steps[^1] = steps[^1] with { Walking = player.IsMoving };
        var character = client.Fake.BattleCharas.Player;
        steps.Add(new Step(client.Game.Events.Elapsed, character.Position, character.Rotation, false, player.ActionsLocked));
    }

    // A dead player stays where they fell.
    public void Replay(TestClient client)
    {
        if (client.Game.ActiveScenario is null || client.Game.Player is not { } player || steps.Count == 0) return;
        if (player.Dead)
        {
            client.Fake.PlayerInput.MovementInputActive = false;
            return;
        }
        var step = At(client.Game.Events.Elapsed);
        var character = client.Fake.BattleCharas.Player;
        character.Position = step.Position;
        character.Rotation = step.Rotation;
        client.Fake.PlayerInput.MovementInputActive = step.Walking;
    }

    // The last step at or before `clock`; the first, before any.
    public Step At(float clock) => steps[IndexAt(clock)];

    // Whether the seat could act throughout [from, to].
    public bool FreeToAct(float from, float to)
    {
        for (var i = IndexAt(from); i < steps.Count && steps[i].Clock <= to + ClockSlack; i++)
            if (steps[i].ActionsLocked) return false;
        return true;
    }

    private int IndexAt(float clock)
    {
        var lo = 0;
        var hi = steps.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (steps[mid].Clock <= clock + ClockSlack) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }
}
