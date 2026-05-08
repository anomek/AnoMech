using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using UltiSim.Core.Map;
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
    public SimPlayer? Player => World.Party.Player;
    public IReadOnlyList<IScenario> Scenarios { get; }
    public LocalPlayerInputHooks PlayerInputHooks { get; }
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
    private readonly OpcodeUpdater opcodeUpdater;

    public Game()
    {
        World = new SimWorld(Events);
        PlayerInputHooks = new LocalPlayerInputHooks(Plugin.GameInterop);
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
        World.Map.TryLoad(scenario.TargetInstance);
        World.ScenarioOrigin = ResolveScenarioOrigin(scenario, player.Position);
        World.PlaceWaymarks(scenario.Waymarks);
        var party = World.CreateParty(player.ClassJob.RowId);
        scenario.Run(World, party.PlayerRole);
        activeScenario = scenario;
        scenarioElapsed = 0f;

        if (!Plugin.Config.SuppressBgm && scenario.Bgm != 0)
            Bgm.Play(scenario.Bgm);

        Plugin.ChatGui.Print(new XivChatEntry
        {
            Type = XivChatType.SystemMessage,
            Message = new SeStringBuilder().AddText($"[UltiSim] Starting: {scenario.Name}").Build(),
        });
    }

    private Vector3 ResolveScenarioOrigin(IScenario scenario, Vector3 playerPosition)
    {
        if (World.Map.IsInInstance && scenario.TargetInstance is { } target)
            return new Vector3(target.Origin.X, target.Origin.Y, target.Origin.Z);
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
            World.Map.Unload();
        });
    }

    private void ResetInternal()
    {
        activeScenario = null;
        scenarioElapsed = 0f;
        Events.Clear();
        World.Reset();
        Bgm.Reset();

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
        Bgm.Dispose();
        World.Dispose();
        PlayerInputHooks.Dispose();
        opcodeUpdater.Dispose();
    }
}
