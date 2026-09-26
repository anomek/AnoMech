using System;
using System.Numerics;
using AnoMech.Core.Map;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.SafetyTests.Harness;

public sealed record SimZone(uint TerritoryId, Vector3 Origin, byte Level, ushort ItemLevel, byte? Weather, float ArenaRadius, string Name)
{
    public static readonly SimZone Top = new(VirtualGame.DutyTerritory, new(100f, 0f, 100f), 90, 365, 3, 20f, "TOP");
    public static readonly SimZone Umad = new(VirtualGame.OtherDutyTerritory, new(100f, 0f, 100f), 100, 0, 7, 20f, "UMAD");
    public static readonly SimZone Ucob = new(VirtualGame.OtherDutyTerritory, new(0f, 0f, 0f), 0, 0, null, 22f, "UCOB-like");
}

// The plugin around the zone session, reduced to the calls that reach it: Plugin's framework
// update and game events, the solo start funnel (Plugin.StartRefusal, Game.RunScenario and
// Game.RunScenarioInternal), Reset, Leave and unload. The Architecture tests pin the real plugin to
// these call orders, so the two stay the same.
public sealed unsafe class PluginDriver : IDisposable
{
    public static readonly Vector3 PlayerSpawnLocal = new(0f, 0f, 16f);

    private readonly VirtualGame game;
    private SimZone? waitingStart;
    private SimZone? activeZone;
    private bool disposed;

    public MapController Map { get; }
    public ZoneSession Zone { get; }
    public bool ScenarioActive { get; private set; }
    public string? LastRefusal { get; private set; }
    public int StartsCompleted { get; private set; }
    public string? StartWaitingOn { get; private set; }

    public PluginDriver(VirtualGame game)
    {
        this.game = game;
        ZoneSessionProbe.ResetStatics();
        Map = new MapController();
        Zone = ZoneSession.Current ?? throw new InvalidOperationException("MapController did not create a ZoneSession.");
        game.PluginUpdate = Update;
        game.TerritoryChangedHandler = ZoneSession.NoteTerritoryChanged;
        game.LogoutHandler = ZoneSession.NoteLogout;
        game.UseActionHandler = ZoneSession.NoteActionPressed;
        game.StartSafetyOracle = spec => StartSpecification.Violation(spec, ZoneSessionProbe.GuardArmed(Zone))
            ?? (ZoneSessionProbe.SendHoldActive(Zone) ? "the debug send hold was on (the server has not seen the character's last moves)" : null);
        game.StuckOracle = Stuck;
        game.ExtraInvariant = InstanceImpliesFirewall;
    }

    private string? InstanceImpliesFirewall()
    {
        if (disposed || !Map.IsInInstance) return null;
        if (!Zone.IsActive) return "the map reports a sim in progress but the zone session is not active";
        if (!game.SendFilterUp || !game.ReceiveFilterUp) return "a sim is in progress with a filter down";
        return null;
    }

    // ---- Plugin.OnFrameworkUpdate ----------------------------------------------------------

    private void Update()
    {
        if (disposed) return;
        try
        {
            ZoneSession.TickGuard();
        }
        catch (GameTerminated)
        {
            throw;
        }
        catch (Exception e)
        {
            // The real update swallows this; a guard that throws every frame is a guard that
            // never trips.
            game.Violation($"ZoneSession.TickGuard threw {e.GetType().Name}: {e.Message}");
        }
        RetryWaitingStart();
        Map.Tick();
    }

    // ---- starting -------------------------------------------------------------------------

    // The Start button: Plugin.StartRefusal, then Game.RunScenario.
    public string? ClickStart(SimZone zone)
    {
        if (disposed || game.Dead) return "the plugin is gone";
        if (StartWaitingOn is { } waiting) return $"Waiting for {waiting} to settle before starting...";
        if (ZoneSession.StartBlockedReason(out var settling) is { } blocked && settling == null)
            return LastRefusal = $"Cannot start: {blocked}.";
        RunScenario(zone);
        return null;
    }

    private void RunScenario(SimZone zone)
    {
        if (ZoneSession.StartBlockedReason(out var settling) != null && settling != null)
        {
            waitingStart = zone;
            StartWaitingOn = settling;
            return;
        }
        waitingStart = null;
        StartWaitingOn = null;
        game.Framework.Run(() => RunScenarioInternal(zone));
    }

    private void RetryWaitingStart()
    {
        if (waitingStart is not { } waiting) return;
        if (ZoneSession.StartBlockedReason(out var settling) != null && settling != null)
        {
            StartWaitingOn = settling;
            return;
        }
        RunScenario(waiting);
    }

    // Game.RunScenarioInternal, the part that touches the zone and the real character.
    private void RunScenarioInternal(SimZone zone)
    {
        // Queued before an unload: in game it runs into disposed hooks and throws before loading
        // anything.
        if (disposed) return;
        if (ZoneSession.StartBlockedReason() is { } blocked)
        {
            LastRefusal = blocked;
            return;
        }
        ScenarioActive = false;
        if (game.LocalPlayerVisible == null)
        {
            LastRefusal = "no local player";
            return;
        }
        var freshLoad = !Map.IsZoneLoaded;
        if (!Map.TryLoad(new TargetInstance(zone.TerritoryId, zone.Origin, zone.Origin + PlayerSpawnLocal, zone.Weather, null), zone.Level, zone.ItemLevel))
        {
            LastRefusal = "the zone was not entered (see the log)";
            return;
        }
        activeZone = zone;
        if (freshLoad) TeleportPlayerToSpawn();
        else TeleportPlayerToSpawnIfOutsideArena();
        ScenarioActive = true;
        LastRefusal = null;
        StartsCompleted++;
    }

    // SimPlayer.SetPosition: the real character's GameObject, moved client-side.
    private void MovePlayerNative(Vector3 world)
    {
        if (game.LocalPlayerVisible is not { } player) return;
        ((GameObject*)player.Address)->SetPosition(world.X, world.Y, world.Z);
    }

    private void TeleportPlayerToSpawn()
    {
        if (activeZone is { } zone) MovePlayerNative(zone.Origin + PlayerSpawnLocal);
    }

    private void TeleportPlayerToSpawnIfOutsideArena()
    {
        if (activeZone is not { } zone || game.LocalPlayerVisible is not { } player) return;
        var local = player.Position - zone.Origin;
        if (new Vector2(local.X, local.Z).Length() <= zone.ArenaRadius) return;
        TeleportPlayerToSpawn();
    }

    // What a running scenario does to the real character: a knockback, a forced carry, a
    // teleport to a mechanic's spot. Only ever while ScenarioActive.
    public void SimMovesPlayer(Vector3 world)
    {
        if (!ScenarioActive) throw new InvalidOperationException("No scenario is running.");
        MovePlayerNative(world);
    }

    // ---- reset, leave, unload --------------------------------------------------------------

    public void ClickReset() => game.Framework.Run(() =>
    {
        if (disposed) return;
        waitingStart = null;
        StartWaitingOn = null;
        if (ScenarioActive) TeleportPlayerToSpawnIfOutsideArena();
        ScenarioActive = false;
    });

    // Plugin.LeaveInstance -> Game.Leave.
    public void ClickLeave()
    {
        if (!Map.IsInInstance) return;
        game.Framework.Run(() =>
        {
            if (disposed) return;
            waitingStart = null;
            StartWaitingOn = null;
            ScenarioActive = false;
            Map.Unload();
        });
    }

    // Plugin.Dispose reaching MapController.Dispose; Dalamud then disposes the plugin's hooks.
    public void Unload(bool dalamudSweepsHooks = true)
    {
        if (disposed) return;
        disposed = true;
        game.PluginUnloaded = true;
        game.RunPluginCode(Map.Dispose, "plugin unload");
        if (dalamudSweepsHooks) game.DalamudDisposesPluginHooks();
    }

    public void Dispose()
    {
        if (!disposed) Unload();
    }

    // ---- judgement -------------------------------------------------------------------------

    private double? postRevertSince;
    // The delayed lift, its three-second verification window and a margin.
    private const double PostRevertLimitSeconds = 1 + 3 + 1.5;

    private string? Stuck()
    {
        // Gone, the plugin can clear nothing it left behind.
        if (disposed)
        {
            if (game.PluginHoldsOccupied) return "the unloaded plugin left the Occupied condition set";
            if (game.PluginSetStatusAffliction) return "the unloaded plugin left a status affliction condition set";
            return null;
        }
        var armed = ZoneSessionProbe.GuardArmed(Zone);
        if (armed && !Zone.IsActive)
        {
            postRevertSince ??= game.Now;
            if (game.Now - postRevertSince > PostRevertLimitSeconds)
                return $"the stay ended {game.Now - postRevertSince:F1}s ago and its lift has neither happened nor stopped the game";
            return null;
        }
        postRevertSince = null;
        if (Zone.IsActive || armed) return null;
        if (game.PluginHoldsOccupied) return "the plugin left the Occupied condition set with no session to clear it";
        if (game.PluginSetStatusAffliction) return "the plugin left a status affliction condition set with no session to clear it";
        if (!game.SendFilterUp && !game.ReceiveFilterUp) return null;
        if (game.SendFilterUp && !game.ReceiveFilterUp && ZoneSessionProbe.SendHoldActive(Zone)) return null;
        return $"the firewall is up (send {game.SendFilterUp}, receive {game.ReceiveFilterUp}) with no session and no pending lift, so nothing will lower it";
    }
}
