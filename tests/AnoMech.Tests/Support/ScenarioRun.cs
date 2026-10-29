using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;

namespace AnoMech.Tests;

// ActionId is null for a death no action dealt: StatusId names the status for DieOfStatus, neither for the obsolete Die(string).
internal sealed record Death(PartyRole Role, string Cause, uint? ActionId, ushort? StatusId, float Time, string Snapshot);

internal sealed record MechanicFailure(string Reason, float Time);

internal sealed record ScenarioRunOptions
{
    // Ends the run at this scenario time, as a pass unless something already failed.
    public float? StopAt { get; init; }

    // Called after every frame; see ScenarioProbe.
    public Action<ScenarioProbe>? Probe { get; init; }

    // Writes the artifact folder even for a passing run.
    public bool AlwaysWriteArtifacts { get; init; }

    // A run expected to end in deaths only writes artifacts when asked to.
    public bool WriteArtifactsOnFailure { get; init; } = true;

    // Seats the player here instead of the seed's pick, so per-role overrides (PerRoleSetting.Mine) reach it.
    public PartyRole? PlayerRole { get; init; }

    // Edits the scenario's SettingsOverrides before the run starts. Runs act as a host, so per-role
    // settings go in their seats, not in Mine.
    public Action<object>? Overrides { get; init; }

    // In time order.
    public IReadOnlyList<Takeover> Takeovers { get; init; } = [];

    // Hits check a human player's mitigation only with the UserActions module on.
    public bool UserActionsEnabled { get; init; }

    // Writes what the sim sent to the fakes here, as a replay-timeline-shaped CSV (SimTimeline).
    public string? TimelinePath { get; init; }

    // Edits the plugin configuration before the run.
    public Action<Configuration>? Configure { get; init; }

    // How many frames an NPC's spawn packet takes to build its actor; none by default.
    public (int Min, int Max) PacketSpawnFrames { get; init; }
}

// From the first player takeover on, the player stops following the AI, as a human who froze there
// would; knockbacks and forced moves still apply. A bot (`Bot` set) is only put somewhere: its AI
// moves it on at its next step. `TeleportTo` is scenario-local XZ, at height `Y`; `Facing` uses
// Placement's convention (0 = +Z).
internal sealed record Takeover(float At, Vector2? TeleportTo, float? Facing = null, PartyRole? Bot = null, float Y = 0f);

// One headless scenario run: every seat (the player's included) on the strat's AI, on a fresh
// fake game, ticked at a fixed rate until it ends.
internal sealed record ScenarioRun(
    Type ScenarioType, int Strat, int Seed, PartyRole PlayerRole, float Elapsed, IReadOnlyList<Death> Deaths,
    IReadOnlyList<MechanicFailure> MechanicFailures, string? Failure, IReadOnlyList<string> Warnings, string? ArtifactDirectory)
{
    public const float FrameSeconds = 1f / 60f;
    public const float TimeoutSeconds = 600f;
    private const float AoeCheckWindowSeconds = 1f;

    public bool Passed => Failure is null && Deaths.Count == 0 && MechanicFailures.Count == 0;

    public static ScenarioRun Execute(Type scenarioType, int strat, int seed, ScenarioRunOptions? options = null)
    {
        options ??= new ScenarioRunOptions();
        var fake = FakeGame.Install();
        fake.UserActions.Enabled = options.UserActionsEnabled;
        fake.BattleCharas.PacketSpawnFrames = options.PacketSpawnFrames;
        Game? game = null;
        var log = TraceLog.Create(() => game?.World.Events.Elapsed ?? 0f);
        fake.Timeline.Clock = () => game?.World.Events.Elapsed ?? 0f;
        fake.Timeline.Source = () => log.LastFired;
        var role = options.PlayerRole ?? (PartyRole)new Rng(seed).Fork("player-seat").Next(8);
        // A real player's job always fits their seat; job-keyed actions read it.
        fake.BattleCharas.Player.ClassJob = PartyPresets.Standard[(int)role].ClassJob;
        var deaths = new List<Death>();
        var mechanicFailures = new List<MechanicFailure>();
        var aoeChecks = new List<AoeCheck>();
        var elapsed = 0f;
        var nextTakeover = 0;
        string? failure = null;
        IScenario? scenario = null;

        void OnAoeEvaluated(AoeQuery query)
        {
            var now = game?.World.Events.Elapsed ?? 0f;
            aoeChecks.RemoveAll(check => check.Time < now - AoeCheckWindowSeconds);
            var positions = game is null
                ? []
                : WorldSnapshot.Members(game.World).Where(m => m.Member.IsAlive()).Select(m => (m.Role, m.Member.Position)).ToList();
            aoeChecks.Add(new AoeCheck(now, query, positions));
        }

        IEnumerable<AoeCheck> RecentAoeChecks()
        {
            var now = game?.World.Events.Elapsed ?? 0f;
            return aoeChecks.Where(check => check.Time >= now - AoeCheckWindowSeconds);
        }

        DalamudServices.Install(nameof(Plugin.Log), log);
        var config = new Configuration();
        options.Configure?.Invoke(config);
        DalamudServices.Install(nameof(Plugin.Config), config);
        DebugBotControl.Enabled = true;
        PerRole.ForceSeats = true;
        AoeQuery.Evaluated += OnAoeEvaluated;
        game = new Game();
        DalamudServices.Install(nameof(Plugin.Game), game);
        var probe = new ScenarioProbe(game, log, RecentAoeChecks);
        try
        {
            scenario = game.Catalog.Scenarios.Single(s => s.GetType() == scenarioType);
            game.PartyMemberKilled += (r, cause, actionId, statusId) =>
            {
                var snapshot = WorldSnapshot.Describe(game.World, RecentAoeChecks(), game.World.Party.Get(r), includeHidden: true);
                deaths.Add(new Death(r, cause, actionId, statusId, game.World.Events.Elapsed, snapshot));
            };
            game.MechanicFailed += reason => mechanicFailures.Add(new MechanicFailure(reason, game.World.Events.Elapsed));
            game.World.HelperPoolExhausted += actionId => failure ??=
                $"helper pool exhausted at t={game.World.Events.Elapsed:F2}: {Natives.Data.ActionName(actionId)} ({actionId}) dropped";
            if (options.Overrides is { } setOverrides)
                setOverrides(scenario.SettingsOverrides
                             ?? throw new InvalidOperationException($"{scenarioType.Name} has no settings overrides."));
            game.ZoneIn(scenario.Phase.Zone, SessionMode.Local);
            game.StartScenario(new RunScenarioParams(scenario, new LocalPartyProvider { RoleOverride = role }, strat, 0, seed));
            fake.Frame(FrameSeconds, game.Tick);
            if (game.Status < SessionStatus.Running)
                failure = "did not start";

            while (failure is null && game.Status is not (SessionStatus.Succeeded or SessionStatus.CompletedFailed or SessionStatus.Frozen))
            {
                if (elapsed >= TimeoutSeconds)
                {
                    failure = $"not finished after {TimeoutSeconds} s";
                    break;
                }
                if (options.StopAt is { } stopAt && game.World.Events.Elapsed >= stopAt)
                    break;
                while (nextTakeover < options.Takeovers.Count && game.World.Events.Elapsed >= options.Takeovers[nextTakeover].At)
                    TakeOver(game.World, options.Takeovers[nextTakeover++], log);
                fake.Frame(FrameSeconds, game.Tick);
                elapsed += FrameSeconds;
                if (options.Probe is { } probeAction)
                {
                    probeAction(probe);
                    probe.EndFrame();
                }
            }
        }
        catch (Exception e)
        {
            failure = $"threw at t={game.World.Events.Elapsed:F2}: {e}";
        }

        var endTime = game.World.Events.Elapsed;
        if (options.TimelinePath is { } timelinePath)
            fake.Timeline.Write(timelinePath);
        string? artifacts = null;
        try
        {
            var run = new ScenarioRun(scenarioType, strat, seed, role, endTime, deaths, mechanicFailures, failure, log.Warnings, null);
            if ((!run.Passed && options.WriteArtifactsOnFailure) || options.AlwaysWriteArtifacts)
            {
                var final = WorldSnapshot.Describe(game.World, RecentAoeChecks());
                artifacts = ScenarioArtifacts.Write(run, log.Lines, scenario, final);
            }
        }
        finally
        {
            AoeQuery.Evaluated -= OnAoeEvaluated;
            DebugBotControl.Enabled = false;
            PerRole.ForceSeats = false;
            game.Dispose();
        }
        return new ScenarioRun(scenarioType, strat, seed, role, endTime, deaths, mechanicFailures, failure, log.Warnings, artifacts);
    }

    private static void TakeOver(SimWorld world, Takeover takeover, TraceLog log)
    {
        if (takeover.Bot is null) DebugBotControl.Enabled = false;
        var role = takeover.Bot ?? world.Party.PlayerRole;
        if (world.Party.Get(role) is not { } member) return;
        member.StopMoving();
        if (takeover.TeleportTo is { } to)
            member.SetPosition(new Vector3(to.X, takeover.Y, to.Y));
        if (takeover.Facing is { } facing)
            member.SetRotation(facing);
        log.Add("TEST", $"{(takeover.Bot is null ? "player" : "bot")} {role} taken over"
                        + (takeover.TeleportTo is { } p ? $", teleported to ({p.X:F1},{p.Y:F1})" : "")
                        + (takeover.Facing is { } f ? $", facing {f:F2}" : ""));
    }

    public string ReplayTestCase => $"[TestCase(typeof(global::{ScenarioType.FullName}), {Strat}, {Seed})]";

    // Bash quoting: PowerShell 5.1 strips the inner double quotes when calling a native exe.
    public string ReplayCommand
        => "dotnet test tests/AnoMech.Tests --filter FullyQualifiedName~ScenarioCatalogTests.ReplayFromParameters -- "
           + $"'TestRunParameters.Parameter(name=\"Scenario\", value=\"{ScenarioType.FullName}\")' "
           + $"'TestRunParameters.Parameter(name=\"Strat\", value=\"{Strat}\")' "
           + $"'TestRunParameters.Parameter(name=\"Seed\", value=\"{Seed}\")'";

    public override string ToString()
    {
        var lines = new List<string> { $"seed {Seed} (player {PlayerRole}, t={Elapsed:F1}):" };
        if (Failure is not null) lines.Add($"  {Failure}");
        lines.AddRange(Deaths.Select(d => $"  {d.Role} died at t={d.Time:F2}: {d.Cause}"));
        lines.AddRange(MechanicFailures.Select(f => $"  mechanic failed at t={f.Time:F2}: {f.Reason}"));
        lines.AddRange(Warnings.Distinct().Select(w => $"  warning: {w}"));
        if (ArtifactDirectory is not null) lines.Add($"  details: {ArtifactDirectory}");
        return string.Join(Environment.NewLine, lines);
    }
}

// What a probe sees each frame. Probes answer one-off questions about a run from the test side,
// instead of temporary log lines in production code; their output lands in trace.log.
internal sealed class ScenarioProbe(Game game, TraceLog log, Func<IEnumerable<AoeCheck>> recentAoeChecks)
{
    private float previousTime = float.NegativeInfinity;

    public Game Game => game;
    public SimWorld World => game.World;
    public float Time => game.World.Events.Elapsed;

    public SimCharacter? Member(PartyRole role) => World.Party.Get(role);

    // True on the one frame the scenario clock passes `time`.
    public bool Crossed(float time) => previousTime < time && Time >= time;

    // The player keeps walking from now on, as far as stillness mechanics can tell.
    public void HoldMovementInput() => ((FakeLocalPlayerInput)Natives.PlayerInput).MovementInputActive = true;

    // The player presses an instant action this frame.
    public void PressAction() => ((FakeLocalPlayerInput)Natives.PlayerInput).PressAction();

    public void Log(string message) => log.Add("PROBE", message);

    public void Snapshot(string label)
        => log.Add("PROBE", $"=== {label} ==={Environment.NewLine}{WorldSnapshot.Describe(World, recentAoeChecks())}=== end {label} ===");

    internal void EndFrame() => previousTime = Time;
}
