using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.Text;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Game;

// High-level orchestrator: owns the World, holds the scenario catalog, drives
// the active scenario's lifecycle, and is the single entry point UI talks to.
public sealed class Game : IDisposable
{
    // EventObj for the duty Exit portal — hidden on every scenario start so the
    // teleport-out interactable doesn't sit inside the simulated arena.
    private const uint ExitObjectBaseId = 2000139;

    public EventScheduler Events { get; } = new();
    public SimWorld World { get; }
    public SimPlayer? Player => World.Party.Player;
    // Null once Reset/Leave clears it -- MultiplayerManager's host-side tick reads
    // this to stop broadcasting once the multiplayer run has ended locally.
    public IScenario? ActiveScenario => activeScenario;
    // Flat registry; the zone -> phase -> scenario tree is derived from it in
    // first-appearance order.
    public IReadOnlyList<IScenario> Scenarios { get; }
    public IReadOnlyList<IZone> Zones { get; }
    private readonly Dictionary<IZone, List<IPhase>> phasesByZone = new();
    private readonly Dictionary<IPhase, List<IScenario>> scenariosByPhase = new();

    // Fixed scenario-local player spawn (16y south of centre).
    public static readonly Vector3 PlayerSpawnLocal = new(0f, 0f, 16f);

    // Multiplier applied only to the EventScheduler's delta. Intentionally does not
    // scale enemy/party/tether/status ticks so cast bars, animations, and movement
    // run at real time — only the timeline of scheduled events stretches/compresses.
    public float EventTimeScale { get; set; } = 1f;

    // Set by Game.Kill once the post-first-death freeze timer fires. While true,
    // Tick is a no-op so scenario events, scheduler, and world all stop.
    public bool Paused { get; set; }
    public bool IsScenarioActive => activeScenario is not null;
    public bool HasScenarioMistake => lastMistakeElapsed is not null;
    public bool HasScenarioFailed => deathOccurredThisRun;
    public bool HasScenarioSucceeded => mechanicResultReported
        && !deathOccurredThisRun
        && lastMistakeElapsed is null;

    // When true, Game.Kill still posts the chat line for learning but skips every
    // gameplay side effect (HP=0, KO timeline, stun hooks, freeze timer).
    public bool GodMode { get; set; }

    // When true, a successful run (see UpdateMechanicResult) immediately starts the same
    // scenario again with the same parameters, for hands-free repetition. A real death
    // cancels it (set false directly in Kill) rather than restarting past a failure the
    // freeze/overlay exists to let the user actually see.
    public bool AutoRestart { get; set; }
    private RunScenarioParams? lastRun;

    // Consecutive successful completions of whatever scenario is currently active. Reset by
    // Kill on any real (non-godmode) death and by RunScenarioInternal when a different
    // scenario starts. Incremented automatically once Events runs dry and
    // stays empty for MechanicResultSettleSeconds (see UpdateMechanicResult): no per-scenario
    // reporting needed. In-memory only: does not survive a plugin reload.
    public int MechanicStreak { get; private set; }

    // How long Events has to stay empty before it counts as "reached the end cleanly".
    // Covers a death whose failure check trails a few frames behind the scenario's own
    // last scheduled action (rather than firing in the exact same frame, which the Tick
    // call order below already handles on its own).
    private const float MechanicResultSettleSeconds = 1f;
    private float? scenarioFinishedElapsed;
    private bool mechanicResultReported;

    // Bardam's Mettle's Success/Failure marks (Checkmark/X).
    private const string MechanicSuccessVfx = "vfx/monster/gimmick2/eff/e3d2_b2_g04t0x.avfx";
    private const string MechanicFailureVfx = "vfx/monster/gimmick2/eff/e3d2_b2_g05t0x.avfx";

    // When the last mistake was marked, null while the run is still clean. Godmode deaths return
    // before deathOccurredThisRun is set, so this is the only mistake signal spanning both modes.
    private const float MistakeMarkCooldownSeconds = 1f;
    private float? lastMistakeElapsed;

    // Set by Kill on any real death, scoped to the current run (cleared by ResetInternal): the
    // source of truth for "did this run fail", since a queue running dry says nothing about deaths.
    private bool deathOccurredThisRun;

    private IScenario? activeScenario;
    private float scenarioElapsed;
    private long lastEventTick;
    public long LastEventTick => lastEventTick;

    // Events only advances once per frame, by that frame's whole delta; this is its value between
    // frames, which a peer's clock is lined up against.
    public float EventClockNow => Events.Elapsed + SecondsSinceTick * EventTimeScale;
    // Real time since the last unpaused Tick, for multiplayer clocks read between frames. Never
    // feeds the sim: that advances only through Tick's delta.
    public float SecondsSinceTick => Paused || lastEventTick == 0
        ? 0f
        : (float)Stopwatch.GetElapsedTime(lastEventTick).TotalSeconds;
    // The phase of the last run in the loaded zone, host and peer alike (activeScenario is
    // host-only and cleared by a Reset).
    private IPhase? lastPhase;
    private bool firstDeathScheduled;
    private bool firstFreezeScheduled;

#if DEBUG
    // A run where nobody dies but something went wrong needs the same trace as the auto-freeze.
    private const float PeriodicDumpInterval = 3f;
    private float periodicDumpTimer;
#endif

    public Game()
    {
        World = new SimWorld(Events);
        Scenarios = ScenarioCatalog.Create();

        // Derive the zone tree from the flat registry (first-appearance order).
        var zoneOrder = new List<IZone>();
        foreach (var scenario in Scenarios)
        {
            var phase = scenario.Phase;
            var zone = phase.Zone;
            if (!phasesByZone.TryGetValue(zone, out var phases))
            {
                phases = new List<IPhase>();
                phasesByZone[zone] = phases;
                zoneOrder.Add(zone);
            }
            if (!phases.Contains(phase)) phases.Add(phase);
            if (!scenariosByPhase.TryGetValue(phase, out var phaseScenarios))
            {
                phaseScenarios = new List<IScenario>();
                scenariosByPhase[phase] = phaseScenarios;
            }
            phaseScenarios.Add(scenario);
        }
        Zones = zoneOrder;
    }

    // Derived zone-tree accessors, in registry order.
    public IReadOnlyList<IPhase> PhasesOf(IZone zone) => phasesByZone[zone];
    public IReadOnlyList<IScenario> ScenariosOf(IPhase phase) => scenariosByPhase[phase];

    // selectedAi: index into the scenario's AiStrats of the strat to run, or null for
    // solo (no doppels, no AI). Defaults to 0 = run the first strat with a full party.
    // selectedWaymark: index into the scenario's WaymarkPresets; ignored when it has none.
    // What a solo Start is waiting to settle before it runs.
    public string? StartWaitingOn { get; private set; }
    private RunScenarioParams? waitingStart;

    public void RunScenario(RunScenarioParams p)
    {
        Natives.Zone.EndSpeedBuffs();
        if (Natives.Zone.StartBlockedReason(out var settling) != null && settling != null)
        {
            if (waitingStart == null) AnoMech.Core.DiagnosticLog.Info($"[Game] Start waiting for {settling} to settle.");
            waitingStart = p;
            StartWaitingOn = settling;
            return;
        }
        waitingStart = null;
        StartWaitingOn = null;
        lastRun = p;
        Plugin.Framework.Run(() => { RunScenarioInternal(p, null, null, isPeer: false); });
    }

    private void RetryWaitingStart()
    {
        if (waitingStart is not { } waiting) return;
        Natives.Zone.EndSpeedBuffs();
        if (Natives.Zone.StartBlockedReason(out var settling) != null && settling != null)
        {
            StartWaitingOn = settling;
            return;
        }
        RunScenario(waiting);
    }

    private void CancelWaitingStart()
    {
        waitingStart = null;
        StartWaitingOn = null;
    }

    // Multiplayer host: RunScenario with `networkRoles` spawned as SimNetworkPuppet, wearing
    // their players' names and jobs (`networkSeats`).
    public void RunScenarioAsHost(IScenario scenario, PartyRole roleOverride, int selectedAi, int selectedWaymark, IReadOnlySet<PartyRole> networkRoles, IReadOnlyDictionary<PartyRole, NetworkSeat> networkSeats, Action<string?> resolved)
    {
        // Auto-restart is a solo affordance: a host silently rerunning would desync the session,
        // and a stale lastRun would rerun the wrong scenario entirely.
        lastRun = null;
        RunResolved(() => RunScenarioInternal(new RunScenarioParams(scenario, roleOverride, selectedAi, selectedWaymark), networkRoles, networkSeats, isPeer: false), resolved);
    }

    // Multiplayer peer: same zone/party/waymarks, but never zone/phase/scenario.Run; every
    // other slot is a puppet driven by the host's snapshots.
    public void RunScenarioAsPeer(IScenario scenario, PartyRole roleOverride, int selectedWaymark, IReadOnlySet<PartyRole> networkRoles, IReadOnlyDictionary<PartyRole, NetworkSeat> networkSeats, Action<string?> resolved)
    {
        lastRun = null;
        RunResolved(() => RunScenarioInternal(new RunScenarioParams(scenario, roleOverride, null, selectedWaymark), networkRoles, networkSeats, isPeer: true), resolved);
    }

    // `resolved` runs in the same deferred callback as the start, with why it was refused, or
    // null once the run is up; an exception counts as a refusal.
    private static void RunResolved(Func<string?> start, Action<string?> resolved)
    {
        Plugin.Framework.Run(() =>
        {
            string? refusal;
            try
            {
                refusal = start();
            }
            catch (Exception e)
            {
                resolved($"the scenario threw {e.GetType().Name} while loading");
                throw;
            }
            resolved(refusal);
        });
    }

    // Raised when Kill actually takes a slot down; the host broadcasts RoleKilled from it. A
    // peer's own Kill calls are reactions to a received RoleKilled, so nothing echoes. The action
    // id is null for a death that names none (the obsolete Die(string)).
    public event Action<PartyRole, string, uint?>? PartyMemberKilled;

    // The selected preset, or [0] as the default.
    private static IReadOnlyList<Waymark> ResolveWaymarks(IZone zone, int selectedWaymark)
    {
        var presets = zone.WaymarkPresets;
        if (selectedWaymark >= 0 && selectedWaymark < presets.Count)
            return presets[selectedWaymark].Markers;
        return presets[0].Markers;
    }

    // Null once the run is up, else why it was refused.
    private string? RunScenarioInternal(RunScenarioParams p, IReadOnlySet<PartyRole>? networkRoles, IReadOnlyDictionary<PartyRole, NetworkSeat>? networkSeats, bool isPeer)
    {
        var (scenario, roleOverride, selectedAi, selectedWaymark, requestedSeed) = p;
        var solo = selectedAi is null;
        var phase = scenario.Phase;
        var zone = phase.Zone;
        // Hard gate: scenarios only ever run from an inn, and only from a state the server
        // isn't about to act on. Everything downstream (CharacterManager registration, zone
        // load, doppel spawn) assumes the inn; the deferred start may land in a state the click
        // didn't see, and ZoneSession.Enter asks once more before the firewall goes up.
        if (Natives.Zone.StartBlockedReason(out _) is { } blocked)
        {
            Plugin.Log.Warning($"Game: refusing to start {scenario.Name} -- {blocked}.");
            return blocked;
        }

        // Per-player settings the fight can't produce together. Empty for a peer and for solo,
        // so only a host's own setup is refused here; this is the funnel every entry point
        // (both windows, /ano start) passes through.
        if (scenario.SettingsConflicts is { Count: > 0 } conflicts)
        {
            foreach (var conflict in conflicts)
                Plugin.Log.Warning($"Game: refusing to start {scenario.Name} -- {conflict}");
            return "impossible scenario settings";
        }

        // Captured before ResetInternal clears activeScenario, so restarting the same
        // scenario (the normal way to extend a streak) doesn't look like a switch.
        var previousScenario = activeScenario;
        ResetInternal();

        var player = Natives.BattleCharas.LocalPlayer;
        if (!player.Exists)
        {
            Plugin.Log.Warning("Game: no local player; aborting scenario start");
            return "no local player";
        }

#if DEBUG
        // Here rather than in scenario.Run (which peers skip), and before TryLoad so its
        // freshLoad line survives into the dump.
        AnoMech.Core.DiagnosticLog.Clear();
#endif

        // Captured before TryLoad: false only on the first start from the inn (a true
        // zone entry), true for any restart/switch within the already-loaded zone.
        var freshLoad = !World.Map.IsZoneLoaded;
        if (!freshLoad && lastPhase != phase) World.Map.RestoreSuppressedArenaSlots();

        if (!World.Map.TryLoad(
                new TargetInstance(zone.TerritoryId, zone.Origin, zone.Origin + PlayerSpawnLocal, phase.Weather, phase.FogHold),
                zone.Level, zone.ItemLevel))
        {
            Plugin.Log.Warning($"Game: {scenario.Name} did not enter its zone; aborting.");
            return "the zone was not entered (see the log)";
        }
        // Snapshot the player's pristine job gauge once per session, before any action mutates
        // it, so Leave can restore it. Only on a true zone entry — a restart must keep the
        // original snapshot, not re-capture the already-simulated gauge.
        if (freshLoad) Natives.UserActions.OnSessionStart();

        World.HideObject(ExitObjectBaseId);
        lastPhase = phase;
        World.ScenarioOrigin = zone.Origin;
        World.Map.ArmColliderDrops(zone.ColliderRemovalPoints.Select(World.Coordinates.ToGlobal));
        World.PlaceWaymarks(ResolveWaymarks(zone, selectedWaymark));
        // Before CreateParty: the spawn ring draws from the run's party-spawn stream.
        var seed = requestedSeed ?? Random.Shared.Next();
        World.Reseed(seed);
        Plugin.Log.Info($"Game: {scenario.Name} seed {seed}");
        World.CreateParty(player.ClassJob, roleOverride, solo, networkRoles, networkSeats);
        // Client-asset setup a peer needs too (see IZone.RunClientSetup).
        zone.RunClientSetup(World);
        phase.RunClientSetup(World);
        // A peer runs no scenario logic; zone.Run also creates the arena boundary the
        // out-of-arena check below reads, so that check no-ops for a peer too.
        if (!isPeer)
        {
            // Log-and-rethrow, so the exception reaches our own log too.
            try
            {
                zone.Run(World);
                phase.Run(World);
                scenario.Run(World, selectedAi);
            }
            catch (Exception e)
            {
                AnoMech.Core.DiagnosticLog.Warn($"[Game.RunScenarioInternal] zone/phase/scenario.Run threw -- scenario load aborted here: {e}");
                throw;
            }
        }
        // Both host and peer: RunInstanceEvents carries no RNG/AI/damage-check dependency.
        scenario.RunInstanceEvents(World);
        // Entering the zone always starts at spawn; a restart only recenters the player
        // if they're standing outside the arena ring (otherwise they keep their position).
        if (freshLoad)
            TeleportPlayerToSpawn();
        else
            TeleportPlayerToSpawnIfOutsideArena();
        if (previousScenario != scenario)
            MechanicStreak = 0;
        Natives.UserActions.OnScenarioStart();
        if (!isPeer)
        {
            activeScenario = scenario;
            scenarioElapsed = 0f;
        }

        // Reconcile BGM to the new scenario. Bgm.Play is idempotent, so switching
        // between same-track scenarios (e.g. the P5 phases) keeps playing without
        // restarting the song; a different track swaps; suppressed/no-track reverts.
        AnoMech.Core.DiagnosticLog.Info($"[Bgm] Suppress scenario BGM: {(Plugin.Config.SuppressBgm ? "on" : "off")}.");
        if (Plugin.Config.SuppressBgm || phase.Bgm == 0)
            Natives.Bgm.Reset();
        else
            Natives.Bgm.Play(phase.Bgm, scenario.BgmSecondsAtStart);

        // networkRoles null, not solo: a peer passes selectedAi null too.
        Plugin.ChatGui.Print(new XivChatEntry { Type = XivChatType.SystemMessage, Message = $"[AnoMech] Starting: {FullName(scenario)}{(networkRoles is null ? " (Solo)" : "")}" });
        return null;
    }

    public void Tick(float deltaSeconds)
    {
        Natives.Bgm.Tick(deltaSeconds);
        RetryWaitingStart();
        if (Paused) return;
        lastEventTick = Stopwatch.GetTimestamp();
        Events.Tick(deltaSeconds * EventTimeScale);
        World.Tick(deltaSeconds);
        if (activeScenario != null)
        {
            scenarioElapsed += deltaSeconds;
            activeScenario.Tick(deltaSeconds, scenarioElapsed);
            UpdateMechanicResult(deltaSeconds);
        }
#if DEBUG
        // Gated so an idle client doesn't spam empty snapshots into the size-capped log.
        if (activeScenario != null || (Plugin.MultiplayerInstance?.IsRunning ?? false))
        {
            periodicDumpTimer += deltaSeconds;
            if (periodicDumpTimer >= PeriodicDumpInterval)
            {
                periodicDumpTimer = 0f;
                AnoMech.Windows.DamageDebugWindow.Instance?.DumpToFile();
            }
        }
        else
        {
            periodicDumpTimer = 0f;
        }
#endif
    }

    // Infers a clean run from Events running dry and staying empty, rather than needing each
    // scenario to report its own completion; deathOccurredThisRun gates out a failed run.
    private void UpdateMechanicResult(float deltaSeconds)
    {
        if (mechanicResultReported) return;
        if (activeScenario is null || !Events.IsEmpty)
        {
            scenarioFinishedElapsed = null;
            return;
        }
        scenarioFinishedElapsed = (scenarioFinishedElapsed ?? 0f) + deltaSeconds;
        if (scenarioFinishedElapsed < MechanicResultSettleSeconds) return;
        mechanicResultReported = true;
        if (deathOccurredThisRun) return;
        if (lastMistakeElapsed is null)
        {
            MechanicStreak++;
            if (Plugin.Config.EnableMechanicResultMarks)
                World.Party.Player?.AddVfx(MechanicSuccessVfx, persistent: false);
        }
        if (AutoRestart && lastRun is { } p)
            RunScenario(p);
    }

    // Godmode preview: how long a swallowed-death HP-bar drop stays down before healing back.
    private const float GodmodeHealSeconds = 1.2f;

    // Single entry point for "this character died". Always posts the cause
    // to chat and, on the first call of a run, fires the on-screen overlay
    // — both happen even in godmode so the user can learn what would have
    // killed them. Gameplay side effects (OnKilled, which flips Dead, plus
    // the 5s freeze) only run outside godmode; the freeze fires once per run
    // on the first non-godmode death.
    //
    // Returns true only when the member actually went down (OnKilled ran):
    // false when it was already dead or godmode swallowed it. Callers that run
    // extra on-death logic should gate on this so a godmode'd "death" doesn't
    // trigger gameplay consequences.
    public bool Kill(ISimPartyMember target, string cause, uint? actionId = null)
    {
        if (target == null) return false;
        if (target.Dead) return false;

        AnoMech.Core.DiagnosticLog.Warn(
            $"[Game] Kill: {target.Role} died at ({(target as IPositioned)?.Position.X:F1},{(target as IPositioned)?.Position.Z:F1}) -- {cause}");
        PrintDeath(target, cause);
        if (!firstDeathScheduled)
        {
            firstDeathScheduled = true;
            ShowFirstDeathOverlay(target, cause);
        }
        // Above the godmode return so every swallowed mistake marks.
        // Prevent Mark stacking by enforcing a cooldown.
        if (lastMistakeElapsed is not { } last || scenarioElapsed - last > MistakeMarkCooldownSeconds)
        {
            lastMistakeElapsed = scenarioElapsed;
            if (Plugin.Config.EnableMechanicResultMarks)
                World.Party.Player?.AddVfx(MechanicFailureVfx, persistent: false);
        }

        if (GodMode)
        {
            // Godmode still previews the death: drop the player's bar and heal it back a beat later.
            if (target is SimPlayer player)
            {
                player.DropHpBar();
                Events.Add(GodmodeHealSeconds, player.RestoreHpBar);
            }
            return false;
        }
        target.OnKilled();
        PartyMemberKilled?.Invoke(target.Role, cause, actionId);
        MechanicStreak = 0;
        deathOccurredThisRun = true;
        AutoRestart = false;
        if (!firstFreezeScheduled)
        {
            firstFreezeScheduled = true;
#if DEBUG
            AnoMech.Windows.DamageDebugWindow.Instance?.Freeze();
#endif
            Events.Add(5f, () => Paused = true);
        }
        return true;
    }

    private static void PrintDeath(ISimPartyMember target, string cause)
        => Plugin.ChatGui.Print(new XivChatEntry { Type = XivChatType.SystemMessage, Message = $"[AnoMech] {DescribeName(target)} died: {cause}" });

    private static string DescribeName(ISimPartyMember target) => target switch
    {
        SimPlayer => "You",
        SimPartyNpc pm => pm.DisplayName,
        SimNetworkPuppet pm => pm.DisplayName,
        _ => "Character",
    };

    private static void ShowFirstDeathOverlay(ISimPartyMember target, string cause)
        => Plugin.ToastGui.ShowError($"{DescribeName(target)} died: {cause}");

    public void Reset() => Plugin.Framework.Run(() =>
    {
        CancelWaitingStart();
        if (activeScenario is not null)
            TeleportPlayerToSpawnIfOutsideArena();
        ResetInternal();
        Natives.Bgm.Reset();
    });

    // Pull the player back to the scenario's spawn point only if they're standing
    // outside the arena ring (e.g. knocked out of bounds, or wandered off). No-op
    // when the scenario enforces no boundary. Reads the live game-object position:
    // at scenario start the SimPlayer was just created and hasn't ticked, so its
    // cached Position is still zero. At reset this must run before ResetInternal
    // clears Party / ScenarioOrigin.
    private void TeleportPlayerToSpawnIfOutsideArena()
    {
        var lp = Natives.BattleCharas.LocalPlayer;
        if (!lp.Exists) return;
        if (!World.IsOutsideArena(World.Coordinates.ToLocal(lp.Position))) return;
        TeleportPlayerToSpawn();
    }

    // ScenarioOrigin must already be set (SetPosition resolves local -> world through it).
    private void TeleportPlayerToSpawn() => Player?.SetPosition(PlayerSpawnLocal);

    // Menu label, e.g. "P5 Delta".
    public static string DisplayName(IScenario scenario)
    {
        var phase = scenario.Phase;
        return string.IsNullOrEmpty(phase.Name) ? scenario.Name : $"{phase.Name} {scenario.Name}";
    }

    public static string FullName(IScenario scenario)
        => $"{scenario.Phase.Zone.Name} — {DisplayName(scenario)}";

    // Leave returns to the inn. Only meaningful when IsInInstance is true.
    // Resets the encounter first, then reverts the zone — Reset stays in-zone.
    public void Leave()
    {
        // Leaving always finalizes its own log segment.
        AnoMech.Core.DiagnosticLog.RotateNow();
        Plugin.Framework.Run(() =>
        {
            CancelWaitingStart();
            ResetInternal();
            Natives.UserActions.OnSessionEnd();   // restore the job gauge captured at session start
            Natives.Bgm.Reset();
            World.Map.Unload();
        });
    }

    private void ResetInternal()
    {
        activeScenario = null;
        scenarioElapsed = 0f;
        Events.Clear();
        World.Despawn();
        // A wipe or Leave never reaches the scenario's own cleanup.
        Natives.VfxSpawnLog.Disable();
        // BGM is the callers': resetting here would restart a same-track scenario switch.

        Paused = false;
        firstDeathScheduled = false;
        firstFreezeScheduled = false;
        scenarioFinishedElapsed = null;
        mechanicResultReported = false;
        deathOccurredThisRun = false;
        lastMistakeElapsed = null;
#if DEBUG
        periodicDumpTimer = 0f;
        AnoMech.Windows.DamageDebugWindow.Instance?.ResetFreeze();
#endif
    }

    // Synchronous: Plugin.Dispose runs on the framework thread during unload, and a
    // Framework.Run wrapper would never fire.
    public void Dispose()
    {
        activeScenario = null;
        Events.Clear();
        Natives.UserActions.OnSessionEnd();   // restore the gauge if the plugin unloads mid-session (no-op otherwise)
        World.Dispose();
    }
}

// A single RunScenario call's arguments, bundled so Game can replay the exact same run (see
// AutoRestart) without tracking each argument as its own field. A null Seed draws a fresh one on
// every start, AutoRestart included; a set one replays the same rolls each time.
public sealed record RunScenarioParams(IScenario Scenario, PartyRole? RoleOverride, int? SelectedAi, int SelectedWaymark, int? Seed = null);
