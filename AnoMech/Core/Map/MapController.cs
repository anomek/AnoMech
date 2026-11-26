using System;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Map;

// Map effects, director updates and layer suppression for the loaded zone. Owned by SimWorld as
// world.Map; zone and effects state are reset by Unload().
public sealed class MapController
{
    // The engine brings layers up over several seconds, so one reading can't tell a slow load
    // from one that never completes.
    private int layerDumpFrame;
    private static readonly int[] LayerDumpFrames = [60, 180, 360, 600, 1200];

    private bool InSim(string what)
    {
        if (IsInInstance) return true;
        DiagnosticLog.Warn($"[MapEffect] {what} ignored -- no sim in progress.");
        return false;
    }

    // ── Zone ─────────────────────────────────────────────────────────────────

    // True while a scenario was started by loading a client-side zone.
    // Cleared by Unload() and Reset().
    public bool IsInInstance { get; private set; }
    public uint TerritoryId { get; private set; }

    // Apply weather after a zone load (1-second delayed to let the engine settle).
    public void ApplyWeather(byte weatherId) => Natives.Zone.ApplyWeather(weatherId);

    // Immediately change the active weather (mid-scenario). transition = fade seconds.
    public void SetWeather(byte weatherId, float transition = 0.5f) => Natives.Zone.SetWeather(weatherId, transition);

    // After the zone reverted to the inn.
    public void Unload()
    {
        IsInInstance = false;
        // Otherwise the native ProcessMapEffect path stays reachable from a replayed message
        // outside any sim.
        Natives.MapEffects.Loaded = false;
        layerDumpFrame = int.MaxValue;
    }

    // Per-frame poll. Called from SimWorld.Tick.
    internal void Tick()
    {
        if (IsInInstance && layerDumpFrame <= LayerDumpFrames[^1])
        {
            layerDumpFrame++;
            if (Array.IndexOf(LayerDumpFrames, layerDumpFrame) >= 0)
            {
                var player = Natives.BattleCharas.LocalPlayer;
                var where = player.Exists ? $"({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1})" : "no player";
                DiagnosticLog.Info($"[MapController] Active layout at frame {layerDumpFrame}, player {where}: {Natives.Layout.DescribeActiveLayers()}");
            }
        }
        Natives.Zone.TickWeather();
    }

    // Once the zone is loaded.
    public void Enter(uint territoryId)
    {
        IsInInstance = true;
        TerritoryId = territoryId;
        layerDumpFrame = 0;
        Natives.MapEffects.Loaded = true;
        DiagnosticLog.Info($"[MapController] Enter: territoryId={territoryId}.");
    }

    // A fresh load delays the weather so the engine settles; a loaded zone takes it now.
    public void SetPhaseWeather(byte? weatherId, bool freshLoad)
    {
        if (weatherId is not { } wid) return;
        if (freshLoad) ApplyWeather(wid);
        else SetWeather(wid);
    }

    // ── Map effects ───────────────────────────────────────────────────────────

    // The zone is loaded before any of these can run (Game.ZoneIn waits for it), so a refusal
    // is a bug, not a call to retry.

    // Replay a single MapEffect state change (packetFlags as in MapEffects).
    public void AddEffect(uint packetFlags, byte index)
    {
        if (!InSim(nameof(AddEffect))) return;
        if (!Natives.MapEffects.Apply(packetFlags, index))
            DiagnosticLog.Warn($"[MapEffect] packetFlags=0x{packetFlags:X8} index=0x{index:X} refused by the zone.");
    }

    // Every slot's resting state in one packet, as the server sends at zone-in and on each reset
    // (states[i] = slot i). Plays no timeline, so nothing transitions.
    public void SetEffectSnapshot(ushort[] states, bool zoneIn = true)
    {
        if (!InSim(nameof(SetEffectSnapshot))) return;
        if (!Natives.MapEffects.ApplySnapshot(states, zoneIn))
            DiagnosticLog.Warn("[MapEffect] snapshot refused by the zone.");
    }

    public void LogArena(string label)
    {
        if (!InSim(nameof(LogArena))) return;
        Natives.MapEffects.LogAllSlots(label);
        DiagnosticLog.Info($"[MapController] {label} live VFX and lights: {Natives.Layout.DescribeLiveEffects()}");
    }

    // Replay a native DirectorUpdate event (instance progress / state sync).
    public void DirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
    {
        if (!InSim(nameof(DirectorUpdate))) return;
        if (!Natives.Director.ProcessDirectorUpdate(category, arg1, arg2, arg3, arg4, arg5, arg6))
            DiagnosticLog.Warn($"[MapEffect] DirectorUpdate category=0x{category:X8} refused by the zone.");
    }

    // A line no actor says (IInstanceContentDirector.BattleTalk); an enemy's own goes through
    // SimEnemy.BattleTalk.
    public void BattleTalk(uint speakerNameId, uint textId, uint durationMs, uint speakerEntityId = 0)
    {
        if (!InSim(nameof(BattleTalk))) return;
        if (!Natives.Director.BattleTalk(speakerNameId, textId, durationMs, speakerEntityId))
            DiagnosticLog.Warn($"[MapEffect] BattleTalk text={textId} refused by the zone.");
    }
}
