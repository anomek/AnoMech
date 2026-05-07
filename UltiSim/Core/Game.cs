using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using UltiSim.Core.SimObjects;
using UltiSim.Scenarios;
using UltiSim.Scenarios.TopP5Delta;

namespace UltiSim.Core;

// High-level orchestrator: owns the World, holds the scenario catalog, drives
// the active scenario's lifecycle, and is the single entry point UI talks to.
public sealed class Game : IDisposable
{
    // EventObj for the duty Exit portal — hidden on every scenario start so the
    // teleport-out interactable doesn't sit inside the simulated arena.
    private const uint ExitObjectBaseId = 2000139;

    public EventScheduler Events { get; } = new();
    public SimWorld World { get; }
    public IReadOnlyList<IScenario> Scenarios { get; }
    public LocalPlayerInputHooks PlayerInputHooks { get; }
    public ZoneSession ZoneSession { get; }

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

    // True while the active scenario was started by loading a zone via ZoneSession.
    // Drives the Leave button visibility in MainWindow.
    public bool IsInInstance { get; private set; }

    private IScenario? activeScenario;
    private float scenarioElapsed;
    private bool firstDeathScheduled;
    private readonly OpcodeUpdater opcodeUpdater;

    public Game()
    {
        World = new SimWorld(Events);
        PlayerInputHooks = new LocalPlayerInputHooks(Plugin.GameInterop);
        ZoneSession = new ZoneSession();
        opcodeUpdater = new OpcodeUpdater();
        Scenarios = new IScenario[]
        {
            new TopP5DeltaScenario(),
        };
    }

    public void RunScenario(IScenario scenario)
    {
        Plugin.Framework.Run(() => RunScenarioInternal(scenario));
    }

    private void RunScenarioInternal(IScenario scenario)
    {
        ResetInternal();

        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
        {
            Plugin.Log.Warning("Game: no local player; aborting scenario start");
            return;
        }

        World.HideObject(ExitObjectBaseId);
        foreach (var baseId in scenario.HiddenBaseIds) World.HideObject(baseId);

        Vector3 origin;
        if (scenario.TargetInstance is { } target)
        {
            if (ZoneSession.IsActive) // TODO: improve. We need to check that zone is proper once we have more scenarios. Good for now.
            {
                // Already inside the target territory — use target origin, no zone load needed.
                // IsInInstance stays false: ZoneSession was never entered so Leave has nothing to revert.
                origin = new Vector3(target.Origin.X, player.Position.Y, target.Origin.Z);
                IsInInstance = true;
            }
            else if (ZoneSession.IsInInn())
            {
                ZoneSession.Enter(target.TerritoryId, target.PlayerPosition);
                origin = new Vector3(target.Origin.X, player.Position.Y, target.Origin.Z);
                IsInInstance = true;
                if (scenario.TargetInstance?.WeatherId is { } wid)
                    ZoneSession.ApplyWeather(wid);
            }
            else
            {
                origin = ResolveScenarioOrigin(scenario, player.Position);
                IsInInstance = false;
            }
        }
        else
        {
            origin = ResolveScenarioOrigin(scenario, player.Position);
            IsInInstance = false;
        }

        World.ScenarioOrigin = origin;
        World.Map.ExpectedTerritoryId = scenario.TargetInstance?.TerritoryId;
        if (IsInInstance)
            DirectorFunctions.Commence();
        World.PlaceWaymarks(scenario.Waymarks);

        var playerJob = player.ClassJob.RowId;
        PartyCreator.Populate(World.Party, World.Player, playerJob, origin);

        scenario.Run(World, World.Party.PlayerRole);


        activeScenario = scenario;
        scenarioElapsed = 0f;
        Plugin.ChatGui.Print(new XivChatEntry
        {
            Type = XivChatType.SystemMessage,
            Message = new SeStringBuilder().AddText($"[UltiSim] Starting: {scenario.Name}").Build(),
        });
    }

    // Scenario origin = (X, Z) anchor for all scenario-relative offsets, with Y
    // taken from the player so spawns stay on the floor. If the scenario declares
    // an OriginOverride matching the active territory, use that fixed (X, Z);
    // otherwise snapshot the player's current position. Orientation is intentionally
    // ignored — offsets are interpreted in world axes (+X east, +Z south) so the
    // arena layout doesn't drift if the player turns.
    private static Vector3 ResolveScenarioOrigin(IScenario scenario, Vector3 playerPosition)
    {
        var territory = Plugin.ClientState.TerritoryType;
        foreach (var ovr in scenario.OriginOverrides)
            if (ovr.TerritoryId == territory)
                return new Vector3(ovr.X, playerPosition.Y, ovr.Z);
        return playerPosition;
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

    // Single entry point for "this character died". Posts the cause to chat
    // unconditionally (the user wants the message even in godmode for
    // learning), then in non-godmode runs the per-subclass side effects and,
    // on the very first death, schedules a 5s freeze so the corpse pose is
    // readable before the run halts.
    public void Kill(SimCharacter target, string cause)
    {
        if (target == null) return;
        if (GodMode) return;
        if (target.Dead) return;
        PrintDeath(target, cause);
        target.Dead = true;
        target.OnKilled();
        if (firstDeathScheduled) return;
        firstDeathScheduled = true;
        Events.Add(5f, () => Paused = true);
    }

    private static void PrintDeath(SimCharacter target, string cause)
    {
        var name = target switch
        {
            SimPlayer => "You",
            SimPartyMember pm => pm.DisplayName,
            SimEnemy se => se.DisplayName,
            _ => "Character",
        };
        Plugin.ChatGui.Print(new XivChatEntry
        {
            Type = XivChatType.SystemMessage,
            Message = new SeStringBuilder().AddText($"[UltiSim] {name} died: {cause}").Build(),
        });
    }

    public void Reset() => Plugin.Framework.Run(ResetInternal);

    // Leave returns to the inn. Only meaningful when IsInInstance is true.
    // Resets the encounter first, then reverts the zone — Reset stays in-zone.
    public void Leave()
    {
        Plugin.Framework.Run(() =>
        {
            ResetInternal();
            ZoneSession.Revert();
            IsInInstance = false;
        });
    }

    private void ResetInternal()
    {
        activeScenario = null;
        scenarioElapsed = 0f;
        Events.Clear();
        World.Reset();
        Paused = false;
        firstDeathScheduled = false;
        PlayerInputHooks.DisableAllActions = false;
        PlayerInputHooks.ZeroMovement = false;
    }

    // Plugin.Dispose is invoked on the framework thread during unload — run
    // teardown synchronously here. The previous Framework.Run wrapper queued
    // the lambda for the *next* tick, which never fired during shutdown and
    // leaked all six LocalPlayerInputHooks hooks.
    public void Dispose()
    {
        activeScenario = null;
        Events.Clear();
        World.Dispose();
        PlayerInputHooks.Dispose();
        ZoneSession.Dispose();
        opcodeUpdater.Dispose();
    }
}
