using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top.P2PartySynergy;
using AnoMech.Scenarios.Top.P5Delta;
using AnoMech.Scenarios.Top.P5Omega;
using AnoMech.Scenarios.Top.P5Sigma;
using AnoMech.Scenarios.Top.P6WaveCannon2;
using AnoMech.Scenarios.Umad.P2Forsaken;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;

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
    public IReadOnlyList<IScenario> Scenarios { get; }
    public Bgm Bgm { get; } = new();

    // Multiplier applied only to the EventScheduler's delta. Intentionally does not
    // scale enemy/party/tether/status ticks so cast bars, animations, and movement
    // run at real time — only the timeline of scheduled events stretches/compresses.
    public float EventTimeScale { get; set; } = 1f;

    // Set by Game.Kill once the post-first-death freeze timer fires. While true,
    // Tick is a no-op so scenario events, scheduler, and world all stop.
    public bool Paused { get; set; }

    // When true, Game.Kill still posts the chat line for learning but skips every
    // gameplay side effect (HP=0, KO timeline, stun hooks, freeze timer).
    public bool GodMode { get; set; }

    private IScenario? activeScenario;
    private float scenarioElapsed;
    private bool firstDeathScheduled;
    private bool firstFreezeScheduled;
    private readonly OpcodeUpdater opcodeUpdater;

    public Game()
    {
        World = new SimWorld(Events);
        opcodeUpdater = new OpcodeUpdater();
        Scenarios = new IScenario[]
        {
            new UmadP2ForsakenScenario(),
            new TopP2PartySynergyScenario(),
            new TopP5DeltaScenario(),
            new TopP5SigmaScenario(),
            new TopP5OmegaScenario(),
            new TopP6WaveCannon2Scenario(),
        };
    }

    // selectedAi: index into the scenario's AiStrats of the strat to run, or null for
    // solo (no doppels, no AI). Defaults to 0 = run the first strat with a full party.
    public void RunScenario(IScenario scenario, PartyRole? roleOverride = null, int? selectedAi = 0)
    {
        Plugin.Framework.Run(() => RunScenarioInternal(scenario, roleOverride, selectedAi));
    }

    private void RunScenarioInternal(IScenario scenario, PartyRole? roleOverride, int? selectedAi)
    {
        var solo = selectedAi is null;
        // Hard gate: scenarios are only ever run from an inn. Everything
        // downstream (CharacterManager registration, zone load, doppel spawn)
        // assumes that invariant.
        if (!ZoneSession.IsInInn())
        {
            Plugin.Log.Warning("Game: scenarios can only run from an inn; aborting.");
            return;
        }

        ResetInternal();

        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
        {
            Plugin.Log.Warning("Game: no local player; aborting scenario start");
            return;
        }

        World.HideObject(ExitObjectBaseId);
        World.Map.TryLoad(scenario.TargetInstance);
        World.ScenarioOrigin = scenario.TargetInstance.Origin;
        World.Map.ArmColliderDrops(scenario.ColliderRemovalPoints.Select(World.Coordinates.ToGlobal));
        World.PlaceWaymarks(scenario.Waymarks);
        World.CreateParty(player.ClassJob.RowId, roleOverride, solo);
        TeleportPlayerToSpawn(scenario);   // begin every run at the canonical spawn point
        scenario.Run(World, selectedAi);
        ResetSprintCooldown();
        activeScenario = scenario;
        scenarioElapsed = 0f;

        // Reconcile BGM to the new scenario. Bgm.Play is idempotent, so switching
        // between same-track scenarios (e.g. the P5 phases) keeps playing without
        // restarting the song; a different track swaps; suppressed/no-track reverts.
        if (Plugin.Config.SuppressBgm || scenario.Bgm == 0)
            Bgm.Reset();
        else
            Bgm.Play(scenario.Bgm);

        Plugin.ChatGui.Print(new XivChatEntry
        {
            Type = XivChatType.SystemMessage,
            Message = new SeStringBuilder().AddText($"[AnoMech] Starting: {scenario.Name}{(solo ? " (Solo)" : "")}").Build(),
        });
    }

    // Sprint goes on cooldown when the player presses it inside a scenario
    // (LocalPlayerInputHooks lets Original run so the recast starts). Clear it
    // here so each scenario starts with Sprint ready, regardless of whether
    // the player pressed it just before clicking Start.
    private static unsafe void ResetSprintCooldown()
    {
        var am = ActionManager.Instance();
        if (am == null) return;
        var group = am->GetRecastGroup((int)ActionType.Action, LocalPlayerInputHooks.SprintActionId);
        if (group < 0) return;
        var detail = am->GetRecastGroupDetail(group);
        if (detail == null) return;
        detail->IsActive = false;
        detail->Elapsed = 0f;
    }

    public void Tick(float deltaSeconds)
    {
        if (Paused) return;
        Events.Tick(deltaSeconds * EventTimeScale);
        World.Tick(deltaSeconds);
        if (activeScenario != null)
        {
            scenarioElapsed += deltaSeconds;
            activeScenario.Tick(deltaSeconds, scenarioElapsed);
        }
    }

    // Single entry point for "this character died". Always posts the cause
    // to chat and, on the first call of a run, fires the on-screen overlay
    // — both happen even in godmode so the user can learn what would have
    // killed them. Gameplay side effects (OnKilled, which flips Dead, plus
    // the 5s freeze) only run outside godmode; the freeze fires once per run
    // on the first non-godmode death.
    public void Kill(ISimPartyMember target, string cause)
    {
        if (target == null) return;
        if (target.Dead) return;

        PrintDeath(target, cause);
        if (!firstDeathScheduled)
        {
            firstDeathScheduled = true;
            ShowFirstDeathOverlay(target, cause);
        }

        if (GodMode) return;
        target.OnKilled();
        if (!firstFreezeScheduled)
        {
            firstFreezeScheduled = true;
#if DEBUG
            AnoMech.Windows.DamageDebugWindow.Instance?.Freeze();
#endif
            Events.Add(5f, () => Paused = true);
        }
    }

    private static void PrintDeath(ISimPartyMember target, string cause)
    {
        Plugin.ChatGui.Print(new XivChatEntry
        {
            Type = XivChatType.SystemMessage,
            Message = new SeStringBuilder().AddText($"[AnoMech] {DescribeName(target)} died: {cause}").Build(),
        });
    }

    private static string DescribeName(ISimPartyMember target) => target switch
    {
        SimPlayer => "You",
        SimPartyNpc pm => pm.DisplayName,
        _ => "Character",
    };

    private static unsafe void ShowFirstDeathOverlay(ISimPartyMember target, string cause)
    {
        var ui = UIModule.Instance();
        if (ui == null) return;
        ui->ShowErrorText($"{DescribeName(target)} died: {cause}", true);
    }

    public void Reset() => Plugin.Framework.Run(() =>
    {
        TeleportPlayerToSpawnIfOutsideArena();
        ResetInternal();
        Bgm.Reset();
    });

    // On reset, pull the player back to the scenario's spawn point if they ended up
    // outside the arena ring (e.g. knocked out of bounds). Must run before
    // ResetInternal clears activeScenario / Party / ScenarioOrigin.
    private void TeleportPlayerToSpawnIfOutsideArena()
    {
        if (activeScenario is not { } scenario) return;
        if (Player is not { } player) return;
        if (!World.IsOutsideArena(player.Position)) return;
        TeleportPlayerToSpawn(scenario);
    }

    // Place the local player at the scenario's spawn point. ScenarioOrigin must already
    // be set so the world<->local conversion resolves correctly.
    private void TeleportPlayerToSpawn(IScenario scenario)
        => Player?.SetPosition(World.Coordinates.ToLocal(scenario.TargetInstance.PlayerPosition));

    // Leave returns to the inn. Only meaningful when IsInInstance is true.
    // Resets the encounter first, then reverts the zone — Reset stays in-zone.
    public void Leave()
    {
        Plugin.Framework.Run(() =>
        {
            ResetInternal();
            Bgm.Reset();
            World.Map.Unload();
        });
    }

    private void ResetInternal()
    {
        activeScenario = null;
        scenarioElapsed = 0f;
        Events.Clear();
        World.Despawn();
        // BGM is owned by the callers: a scenario start reconciles it to the new
        // track (keeping it playing when unchanged); Reset/Leave stop it. Resetting
        // here would force a same-track restart on every scenario switch.

        Paused = false;
        firstDeathScheduled = false;
        firstFreezeScheduled = false;
#if DEBUG
        AnoMech.Windows.DamageDebugWindow.Instance?.ResetFreeze();
#endif
        // Input-lock flags are owned by SimPlayer (reconciled each tick, cleared on
        // its Despawn during World.Reset above) — nothing to clear here.
    }

    // Plugin.Dispose is invoked on the framework thread during unload — run
    // teardown synchronously here. The previous Framework.Run wrapper queued
    // the lambda for the *next* tick, which never fired during shutdown and
    // leaked all six LocalPlayerInputHooks hooks.
    public void Dispose()
    {
        activeScenario = null;
        Events.Clear();
        Bgm.Dispose();
        World.Dispose();
        opcodeUpdater.Dispose();
    }
}
