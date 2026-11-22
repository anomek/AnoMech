using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// A run where the player (seated in the role under test) breaks the strat on purpose, and the
// mechanic must kill exactly the expected roles:
//
//     Negative<TopP2PartySynergyScenario>(PartyRole.MeleeDpsA)
//         .Overrides<TopP2PartySynergyStateOverrides>(o => o.AttackM = OmegaAttack.Shield)
//         .TeleportAt(12f, to: new(0, 0))
//         .ShouldKill(ActionId.BeyondStrength, PartyRole.MeleeDpsA);
internal static class NegativeRun
{
    public const uint TheEnvironment = SimCharacterDeathExtensions.Environment;

    public static NegativeRun<TScenario> Negative<TScenario>(PartyRole role, int strat = 0) where TScenario : IScenario
        => new(role, strat);

    public static PartyRole[] AllBut(params PartyRole[] roles) => PerRole.All.Except(roles).ToArray();
}

internal sealed class NegativeRun<TScenario>(PartyRole role, int strat) where TScenario : IScenario
{
    private ScenarioRunOptions options = new() { PlayerRole = role, WriteArtifactsOnFailure = false };
    private int? seed;

    public NegativeRun<TScenario> Overrides<TOverrides>(Action<TOverrides> set)
    {
        options = options with { Overrides = o => set((TOverrides)o) };
        return this;
    }

    // Once taken off the AI, the player then needs the mitigation a hit asks for (an invuln for 100%).
    public NegativeRun<TScenario> WithMitigationChecks()
    {
        options = options with { UserActionsEnabled = true };
        return this;
    }

    // The first player step takes the player off the AI for good; later ones move them again.
    public NegativeRun<TScenario> TeleportAt(float time, Vector2 to, float? facing = null)
        => Add(new Takeover(time, to, facing));

    // Below InstanceSession's FallFloor: the client's own fall, which only the local player can take.
    public NegativeRun<TScenario> DropBelowArenaAt(float time, Vector2 to)
        => Add(new Takeover(time, to, Y: -10f));

    public NegativeRun<TScenario> FreezeAt(float time) => Add(new Takeover(time, null));

    // The bot stays on its AI, which moves it on at its next step.
    public NegativeRun<TScenario> MoveBotAt(float time, PartyRole bot, Vector2 to) => Add(new Takeover(time, to, Bot: bot));

    // State the strat can't reach on its own (extra stacks, a debuff), set on the frame the clock passes `time`.
    public NegativeRun<TScenario> At(float time, Action<ScenarioProbe> setUp)
    {
        var previous = options.Probe;
        options = options with { Probe = p => { previous?.Invoke(p); if (p.Crossed(time)) setUp(p); } };
        return this;
    }

    private NegativeRun<TScenario> Add(Takeover takeover)
    {
        options = options with { Takeovers = options.Takeovers.Append(takeover).OrderBy(t => t.At).ToList() };
        return this;
    }

    public NegativeRun<TScenario> Seed(int value)
    {
        seed = value;
        return this;
    }

    // The run starts at the first ShouldKill or ShouldFail; each ShouldKill judges the next group of
    // deaths. Groups go in frame order, but within one frame the calls can come in any order. Deaths no call judges are
    // consequences (a stack one short, a tether partner left alone), not what the test broke.
    // Matched on the action the death names, so only Die(actionId, ...) counts.
    public NegativeRun<TScenario> ShouldKill(uint actionId, params PartyRole[] roles)
        => Judge(() => $"{string.Join(", ", roles)} to die to {NameOf(actionId)}", d => d.ActionId == actionId, roles, roles);

    // When the roll decides who is hit.
    public NegativeRun<TScenario> ShouldKillSomeone(uint actionId)
        => Judge(() => $"someone to die to {NameOf(actionId)}", d => d.ActionId == actionId, PerRole.All, []);

    // A death no action deals, only DieOfStatus.
    public NegativeRun<TScenario> ShouldDieOfStatus(ushort statusId, params PartyRole[] roles)
        => Judge(() => $"{string.Join(", ", roles)} to die of {Natives.Data.StatusName(statusId)} ({statusId})", d => d.StatusId == statusId, roles, roles);

    // After the last ShouldKill: the deaths it judged are all there are, so nothing they set off
    // (a fail raidwide) followed within the 5 s the run records after the first death.
    public NegativeRun<TScenario> ShouldKillNoOneElse()
    {
        EnsureRun();
        var rest = Enumerable.Range(0, run!.Deaths.Count).Where(i => !judged.Contains(i)).Select(i => run.Deaths[i]).ToList();
        if (run.Failure is null && rest.Count == 0) return this;
        var problem = run.Failure is not null ? "expected no other deaths, but the run failed." : $"expected no other deaths, but {Describe(rest)}.";
        var detailed = ScenarioRun.Execute(typeof(TScenario), strat, runSeed, options with { AlwaysWriteArtifacts = true });
        Assert.Fail($"{problem}{Environment.NewLine}{detailed}{Environment.NewLine}  replay: .Seed({runSeed})");
        return this;
    }

    // A failed mechanic nobody dies to (world.Fail).
    public NegativeRun<TScenario> ShouldFail()
    {
        EnsureRun();
        if (run!.Failure is null && run.MechanicFailures.Count > 0) return this;
        var problem = run.Failure is not null ? "expected a mechanic failure, but the run failed." : "expected a mechanic failure, but none was reported.";
        var detailed = ScenarioRun.Execute(typeof(TScenario), strat, runSeed, options with { AlwaysWriteArtifacts = true });
        Assert.Fail($"{problem}{Environment.NewLine}{detailed}{Environment.NewLine}  replay: .Seed({runSeed})");
        return this;
    }

    private static string NameOf(uint actionId)
        => actionId == NegativeRun.TheEnvironment ? "the environment" : $"{Natives.Data.ActionName(actionId)} ({actionId})";

    private ScenarioRun? run;
    private int runSeed;
    private readonly HashSet<int> judged = [];

    // `expected` is lazy: names resolve only once the run has installed the game data.
    private void EnsureRun()
    {
        if (run is not null) return;
        runSeed = seed ?? Random.Shared.Next();
        run = ScenarioRun.Execute(typeof(TScenario), strat, runSeed, options);
    }

    private NegativeRun<TScenario> Judge(Func<string> expected, Func<Death, bool> matches, PartyRole[] allowed, PartyRole[] mustDie)
    {
        EnsureRun();
        var message = (judged.Count == 0 ? "expected " : "then expected ") + expected();
        if (GroupMismatch(run!, judged, message, matches, allowed, mustDie, out var group) is not { } problem)
        {
            judged.UnionWith(group);
            return this;
        }

        // Deterministic, so a rerun reproduces the failure with its artifacts on disk.
        var detailed = ScenarioRun.Execute(typeof(TScenario), strat, runSeed, options with { AlwaysWriteArtifacts = true });
        Assert.Fail($"{problem}{Environment.NewLine}{detailed}{Environment.NewLine}  replay: .Seed({runSeed})");
        return this;
    }

    // A group is the unjudged deaths to the expected action in the frame of the first unjudged death.
    // A raidwide they set off (a tether partner gone, a Hello World holder down) can land in that
    // same frame; it stays unjudged.
    private static string? GroupMismatch(
        ScenarioRun run, IReadOnlySet<int> judged, string expected, Func<Death, bool> matches, PartyRole[] allowed, PartyRole[] mustDie,
        out List<int> group)
    {
        group = [];
        if (run.Failure is not null) return $"{expected}, but the run failed.";
        var unjudged = Enumerable.Range(0, run.Deaths.Count).Where(i => !judged.Contains(i)).ToList();
        if (unjudged.Count == 0) return $"{expected}, but nobody {(judged.Count == 0 ? "" : "else ")}died.";
        var first = run.Deaths[unjudged[0]];
        group = unjudged.Where(i => run.Deaths[i].Time <= first.Time + ScenarioRun.FrameSeconds / 2
                                    && matches(run.Deaths[i]))
                        .ToList();
        var deaths = group.Select(i => run.Deaths[i]).ToList();
        if (deaths.Count == 0) return $"{expected}, but {Describe([first])} first.";
        var wrong = deaths.Where(d => !allowed.Contains(d.Role)).ToList();
        if (wrong.Count > 0) return $"{expected}, but {Describe(wrong)}.";
        var survived = mustDie.Where(r => deaths.All(d => d.Role != r)).ToList();
        if (survived.Count > 0) return $"{expected}, but {string.Join(", ", survived)} did not die with them.";
        return null;
    }

    private static string Describe(IEnumerable<Death> deaths)
        => string.Join("; ", deaths.Select(d => $"{d.Role} died to \"{d.Cause}\" ({(d.ActionId is { } id ? $"action {id}" : d.StatusId is { } status ? $"status {status}" : "no action")}) at t={d.Time:F2}"));
}
