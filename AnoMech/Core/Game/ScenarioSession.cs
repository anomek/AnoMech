using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;

namespace AnoMech.Core.Game;

public enum SessionMode
{
    // Single player.
    Local,
    // Runs the scenario for a multiplayer lobby.
    Host,
    // Sets the same stage but never runs or ticks the scenario's own logic; the host's packets
    // drive the run.
    Peer,
}

// One scenario run inside an InstanceSession, from Start to the reset, restart or leave that Ends
// it. Owns everything a restart has to redo: seating the party, the scenario's spawns and
// timeline, the run's death/mistake bookkeeping and its music.
public sealed class ScenarioSession
{
    // How long Events has to stay empty before it counts as "reached the end cleanly". Covers a
    // death whose failure check trails a few frames behind the scenario's own last scheduled
    // action.
    private const float MechanicResultSettleSeconds = 1f;
    private const float MistakeMarkCooldownSeconds = 1f;

    // Bardam's Mettle's Success/Failure marks (Checkmark/X): unnamed actions whose AnimationEnd
    // gimmick timeline plays the mark on the target. Every player in the party gets the run's.
    public const uint MechanicSuccessActionId = 9450;
    public const uint MechanicFailureActionId = 9451;

    // The dungeon's invisible helper, which casts both marks at each player.
    public const uint MarkHelperBNpcBase = 0x18D6;
    private static readonly EnemySpawnConfig MarkHelper = new(BNpcBaseId: MarkHelperBNpcBase, Level: 1,
        EnemyList: EnemyListMode.Never, Visibility: SpawnVisibility.InvisibleHelper);

    private readonly SimWorld world;
    private readonly InstanceSession instance;
    private readonly RunScenarioParams run;
    private readonly IReadOnlyList<PartySlot?> roster;

    private float elapsed;
    private float? finishedElapsed;
    private bool resultReported;
    private bool firstDeathShown;
    private bool freezeScheduled;
    // Godmode deaths never set deathOccurred, so this is the only mistake signal spanning both modes.
    private float? lastMistakeElapsed;
    // The source of truth for "did this run fail": a queue running dry says nothing about deaths.
    private bool deathOccurred;
    private SimEnemy? markHelper;

    public IScenario Scenario => run.Scenario;
    public SessionMode Mode { get; }

    // Set once the post-first-death freeze timer fires; Game stops ticking the sim while true.
    public bool Paused { get; private set; }

    public SessionStatus Status =>
        Paused ? SessionStatus.Frozen
        : deathOccurred ? SessionStatus.Failed
        : resultReported ? (lastMistakeElapsed is null ? SessionStatus.Succeeded : SessionStatus.CompletedWithMistake)
        : lastMistakeElapsed is null ? SessionStatus.Running
        : SessionStatus.RunningWithMistake;

    // Raised once, when the timeline ran dry and stayed so with nobody dead. clean: no mistake
    // marked either.
    public event Action<bool>? Finished;

    private ScenarioSession(SimWorld world, InstanceSession instance, RunScenarioParams run, IReadOnlyList<PartySlot?> roster)
    {
        this.world = world;
        this.instance = instance;
        this.run = run;
        this.roster = roster;
        Mode = instance.Mode;
    }

    // Null, with why, when the run can't start in `instance`. Touches nothing, so a refused restart
    // leaves the current run playing.
    public static ScenarioSession? Create(SimWorld world, InstanceSession instance, RunScenarioParams run, out string? refusal)
    {
        refusal = Refusal(instance, run, out var roster);
        if (refusal is null) return new ScenarioSession(world, instance, run, roster!);
        Plugin.Log.Warning($"ScenarioSession: refusing to start {run.Scenario.Name} -- {refusal}.");
        return null;
    }

    private static string? Refusal(InstanceSession instance, RunScenarioParams run, out IReadOnlyList<PartySlot?>? roster)
    {
        roster = null;
        var scenario = run.Scenario;
        if (!instance.IsLoaded || instance.Zone != scenario.Phase.Zone) return $"not in {scenario.Phase.Zone.Name}";
        // A deferred start may land in a state the click didn't see.
        if (Natives.Zone.StartBlockedReason(out _) is { } blocked) return blocked;

        // Per-player settings the fight can't produce together. Empty for a peer and for solo,
        // so only a host's own setup is refused here; this is the funnel every entry point
        // (both windows, /ano start) passes through.
        if (scenario.SettingsConflicts is { Count: > 0 } conflicts)
        {
            foreach (var conflict in conflicts)
                Plugin.Log.Warning($"ScenarioSession: {scenario.Name} settings conflict -- {conflict}");
            return "impossible scenario settings";
        }

        var player = Natives.BattleCharas.LocalPlayer;
        if (!player.Exists) return "no local player";

        roster = run.Party.Roster(player.ClassJob);
        if (instance.Mode != SessionMode.Peer && roster.Any(slot => slot is null))
        {
            if (!scenario.SupportsSolo) return $"{scenario.Name} does not support Solo mode";
            if (run.SelectedAi is not null) return "a strat needs a full party";
        }
        return null;
    }

    public void Start()
    {
#if DEBUG
        // Here rather than in scenario.Run (which peers skip).
        DiagnosticLog.Clear();
#endif
        // The run's clock starts at 0, as EventScheduler.FromRunStart's offsets assume: a stay's
        // first run follows no End to have cleared it.
        world.Events.Clear();
        var phase = Scenario.Phase;
        var zone = phase.Zone;
        world.PlaceWaymarks(ResolveWaymarks(zone, run.SelectedWaymark));
        // Before ApplyParty: the spawn ring draws from the run's party-spawn stream.
        var seed = run.Seed ?? Random.Shared.Next();
        world.Reseed(seed);
        Plugin.Log.Info($"Game: {Scenario.Name} seed {seed}");
        world.ApplyParty(roster);
        // Client-asset setup a peer needs too (see IZone.RunClientSetup).
        zone.RunClientSetup(world);
        phase.RunClientSetup(world);
        instance.ApplyPhase(phase);
        Natives.Director.Commence();
        // zone.Run also creates the arena boundary PlacePlayer's out-of-arena check reads, so that
        // check no-ops for a peer.
        if (Mode != SessionMode.Peer)
        {
            // Log-and-rethrow, so the exception reaches our own log too.
            try
            {
                zone.Run(world);
                phase.Run(world);
                Scenario.Run(world, run.SelectedAi);
            }
            catch (Exception e)
            {
                DiagnosticLog.Warn($"[ScenarioSession.Start] zone/phase/scenario.Run threw -- scenario load aborted here: {e}");
                throw;
            }
            if (Plugin.Config.EnableMechanicResultMarks) markHelper = world.SpawnEnemy(MarkHelper);
        }
        // Both host and peer: RunInstanceEvents carries no RNG/AI/damage-check dependency.
        Scenario.RunInstanceEvents(world);
        instance.PlacePlayer();
        Natives.UserActions.OnScenarioStart();
        StartMusic();
        Plugin.ChatGui.Print(new XivChatEntry { Type = XivChatType.SystemMessage, Message = $"[AnoMech] Starting: {Scenario.FullName()}{(Mode == SessionMode.Local ? " (Solo)" : "")}" });
    }

    public void Tick(float deltaSeconds)
    {
        if (Mode == SessionMode.Peer) return;
        elapsed += deltaSeconds;
        Scenario.Tick(deltaSeconds, elapsed);
        UpdateMechanicResult(deltaSeconds);
    }

    // Infers a clean run from Events running dry and staying empty, rather than needing each
    // scenario to report its own completion.
    private void UpdateMechanicResult(float deltaSeconds)
    {
        if (resultReported) return;
        if (!world.Events.IsEmpty)
        {
            finishedElapsed = null;
            return;
        }
        finishedElapsed = (finishedElapsed ?? 0f) + deltaSeconds;
        if (finishedElapsed < MechanicResultSettleSeconds) return;
        resultReported = true;
        if (deathOccurred) return;
        var clean = lastMistakeElapsed is null;
        if (clean) PlayMark(MechanicSuccessActionId);
        Finished?.Invoke(clean);
    }

    // A death reported in this run, godmode or not: the first one's overlay, and the mistake mark.
    public void OnDeathReported(string describedTarget, string cause)
    {
        if (!firstDeathShown)
        {
            firstDeathShown = true;
            Plugin.ToastGui.ShowError($"{describedTarget} died: {cause}");
        }
        if (lastMistakeElapsed is not { } last || elapsed - last > MistakeMarkCooldownSeconds)
        {
            lastMistakeElapsed = elapsed;
            PlayMark(MechanicFailureActionId);
        }
    }

    private void PlayMark(uint actionId)
    {
        if (Plugin.Config.EnableMechanicResultMarks && markHelper is { IsActive: true } helper)
            foreach (var player in world.Party.Players()) helper.Cast(actionId, player, animationLock: 0f);
    }

    // A death that took a member down; the first one freezes the run 5s later.
    public void OnKilled()
    {
        deathOccurred = true;
        if (freezeScheduled) return;
        freezeScheduled = true;
#if DEBUG
        AnoMech.Windows.DamageDebugWindow.Instance?.Freeze();
#endif
        world.Events.Add(5f, () => Paused = true);
    }

    public void End()
    {
        world.Events.Clear();
        world.Despawn();
#if DEBUG
        AnoMech.Windows.DamageDebugWindow.Instance?.ResetFreeze();
#endif
    }

    private void StartMusic()
    {
        var phase = Scenario.Phase;
        DiagnosticLog.Info($"[Bgm] Suppress scenario BGM: {(Plugin.Config.SuppressBgm ? "on" : "off")}.");
        if (!Plugin.Config.SuppressBgm && phase.Bgm != 0)
            world.PlayBgm(phase.Bgm, Scenario.BgmSecondsAtStart);
    }

    // The selected preset, or [0] as the default.
    private static IReadOnlyList<Waymark> ResolveWaymarks(IZone zone, int selectedWaymark)
    {
        var presets = zone.WaymarkPresets;
        if (selectedWaymark >= 0 && selectedWaymark < presets.Count)
            return presets[selectedWaymark].Markers;
        return presets[0].Markers;
    }
}
