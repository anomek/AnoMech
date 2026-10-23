using System;
using System.Collections.Generic;
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

    private const int RetryMaxFrames = 300; // ~5s at 60fps

    // AddEffect/DirectorUpdate calls the zone can't accept yet (still async-loading) are retried
    // each Tick: a peer's zone load lags the host's by seconds, and a scenario's early events
    // land in that window.
    private readonly List<PendingMapEffect> pendingEffects = new();
    private readonly List<PendingDirectorUpdate> pendingDirectorUpdates = new();
    private readonly List<PendingBattleTalk> pendingBattleTalks = new();

    // IsInInstance goes true in Enter and false in Unload, so this also refuses to queue a
    // call that would otherwise fire on the next run.
    private bool InSim(string what)
    {
        if (IsInInstance) return true;
        DiagnosticLog.Warn($"[MapEffect] {what} ignored -- no sim in progress.");
        return false;
    }

    // The queues are fed by replayed network messages too.
    private static bool TryReserveRetrySlot(int currentCount, string what)
    {
        if (currentCount < AnoMech.Multiplayer.NetGuard.MaxPendingMapCalls) return true;
        DiagnosticLog.Warn($"[MapEffect] Dropping a {what} retry -- over {AnoMech.Multiplayer.NetGuard.MaxPendingMapCalls} already queued.");
        return false;
    }

    private struct PendingMapEffect
    {
        public uint PacketFlags;
        public byte Index;
        public int FramesLeft;
    }

    private sealed class PendingSnapshot
    {
        public required ushort[] States;
        public required bool ZoneIn;
        public int FramesLeft = RetryMaxFrames;
    }

    private PendingSnapshot? pendingSnapshot;

    private struct PendingDirectorUpdate
    {
        public uint Category, Arg1, Arg2, Arg3, Arg4, Arg5, Arg6;
        public int FramesLeft;
    }

    // A line is a timing cue: shown seconds late it would mistime the cast it announces.
    internal const int BattleTalkMaxFrames = 120;

    private struct PendingBattleTalk
    {
        public uint SpeakerNameId, TextId, DurationMs;
        public int FramesLeft;
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
        pendingEffects.Clear();
        pendingSnapshot = null;
        pendingDirectorUpdates.Clear();
        pendingBattleTalks.Clear();
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

        if (pendingSnapshot != null)
        {
            if (Natives.MapEffects.ApplySnapshot(pendingSnapshot.States, pendingSnapshot.ZoneIn))
                pendingSnapshot = null;
            else if (--pendingSnapshot.FramesLeft <= 0)
            {
                DiagnosticLog.Warn($"[MapEffect] Gave up applying a snapshot after {RetryMaxFrames} frames — zone likely never finished loading.");
                pendingSnapshot = null;
            }
        }

        // Insertion order: an SGB slot's State is locked in by whichever call reaches it first
        // (see MapEffects), so a later call to the same index must not win the retry. All of
        // them wait for a pending snapshot.
        for (int i = 0; pendingSnapshot == null && i < pendingEffects.Count; i++)
        {
            var pending = pendingEffects[i];
            if (Natives.MapEffects.Apply(pending.PacketFlags, pending.Index)) { pendingEffects.RemoveAt(i); i--; continue; }
            pending.FramesLeft--;
            if (pending.FramesLeft <= 0)
            {
                DiagnosticLog.Warn($"[MapEffect] Gave up applying packetFlags=0x{pending.PacketFlags:X8} index=0x{pending.Index:X} after {RetryMaxFrames} frames — zone likely never finished loading.");
                pendingEffects.RemoveAt(i);
                i--;
            }
            else
            {
                pendingEffects[i] = pending;
            }
        }

        for (int i = 0; i < pendingDirectorUpdates.Count; i++)
        {
            var pending = pendingDirectorUpdates[i];
            if (Natives.Director.ProcessDirectorUpdate(pending.Category, pending.Arg1, pending.Arg2, pending.Arg3, pending.Arg4, pending.Arg5, pending.Arg6))
            {
                pendingDirectorUpdates.RemoveAt(i);
                i--;
                continue;
            }
            pending.FramesLeft--;
            if (pending.FramesLeft <= 0)
            {
                DiagnosticLog.Warn($"[MapEffect] Gave up applying DirectorUpdate category=0x{pending.Category:X8} after {RetryMaxFrames} frames — zone likely never finished loading.");
                pendingDirectorUpdates.RemoveAt(i);
                i--;
            }
            else
            {
                pendingDirectorUpdates[i] = pending;
            }
        }

        for (int i = 0; i < pendingBattleTalks.Count; i++)
        {
            var pending = pendingBattleTalks[i];
            if (Natives.Director.BattleTalk(pending.SpeakerNameId, pending.TextId, pending.DurationMs))
            {
                pendingBattleTalks.RemoveAt(i);
                i--;
                continue;
            }
            pending.FramesLeft--;
            if (pending.FramesLeft <= 0)
            {
                DiagnosticLog.Warn($"[MapEffect] Dropped BattleTalk text={pending.TextId} after {BattleTalkMaxFrames} frames -- no director yet.");
                pendingBattleTalks.RemoveAt(i);
                i--;
            }
            else
            {
                pendingBattleTalks[i] = pending;
            }
        }
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

    // Replay a single MapEffect state change (packetFlags as in MapEffects). Queued for
    // retry if the zone isn't ready yet; a peer whose zone load lags the host's would otherwise
    // silently lose it.
    public void AddEffect(uint packetFlags, byte index)
    {
        if (!InSim(nameof(AddEffect))) return;
        // Behind a pending call for the same slot, or it would land first and be overwritten.
        var behind = pendingSnapshot != null || pendingEffects.Exists(p => p.Index == index);
        if ((behind || !Natives.MapEffects.Apply(packetFlags, index)) && TryReserveRetrySlot(pendingEffects.Count, "MapEffect"))
        {
            DiagnosticLog.Warn($"[MapEffect] packetFlags=0x{packetFlags:X8} index=0x{index:X} {(behind ? "queued behind an earlier call for the same slot" : "not ready yet -- queued for retry")}.");
            pendingEffects.Add(new PendingMapEffect { PacketFlags = packetFlags, Index = index, FramesLeft = RetryMaxFrames });
        }
    }

    // Every slot's resting state in one packet, as the server sends at zone-in and on each reset
    // (states[i] = slot i). Plays no timeline, so nothing transitions. Replaces a snapshot still
    // waiting for the zone and drops queued single effects, which it overwrites.
    public void SetEffectSnapshot(ushort[] states, bool zoneIn = true)
    {
        if (!InSim(nameof(SetEffectSnapshot))) return;
        pendingEffects.Clear();
        pendingSnapshot = null;
        if (!Natives.MapEffects.ApplySnapshot(states, zoneIn))
        {
            DiagnosticLog.Warn("[MapEffect] snapshot not ready yet -- queued for retry.");
            pendingSnapshot = new PendingSnapshot { States = states, ZoneIn = zoneIn };
        }
    }

    public void LogArena(string label)
    {
        if (!InSim(nameof(LogArena))) return;
        Natives.MapEffects.LogAllSlots(label);
        DiagnosticLog.Info($"[MapController] {label} live VFX and lights: {Natives.Layout.DescribeLiveEffects()}");
    }

    // Replay a native DirectorUpdate event (instance progress / state sync); retried like
    // AddEffect.
    public void DirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0)
    {
        if (!InSim(nameof(DirectorUpdate))) return;
        if (!Natives.Director.ProcessDirectorUpdate(category, arg1, arg2, arg3, arg4, arg5, arg6)
            && TryReserveRetrySlot(pendingDirectorUpdates.Count, "DirectorUpdate"))
        {
            DiagnosticLog.Warn($"[MapEffect] DirectorUpdate category=0x{category:X8} not ready yet -- queued for retry.");
            pendingDirectorUpdates.Add(new PendingDirectorUpdate { Category = category, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3, Arg4 = arg4, Arg5 = arg5, Arg6 = arg6, FramesLeft = RetryMaxFrames });
        }
    }

    // A boss line (IInstanceContentDirector.BattleTalk); retried like DirectorUpdate.
    public void BattleTalk(uint speakerNameId, uint textId, uint durationMs)
    {
        if (!InSim(nameof(BattleTalk))) return;
        if (!Natives.Director.BattleTalk(speakerNameId, textId, durationMs)
            && TryReserveRetrySlot(pendingBattleTalks.Count, "BattleTalk"))
        {
            DiagnosticLog.Warn($"[MapEffect] BattleTalk text={textId} not ready yet -- queued for retry.");
            pendingBattleTalks.Add(new PendingBattleTalk { SpeakerNameId = speakerNameId, TextId = textId, DurationMs = durationMs, FramesLeft = BattleTalkMaxFrames });
        }
    }
}
