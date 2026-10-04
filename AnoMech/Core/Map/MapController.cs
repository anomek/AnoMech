using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Map;

// Unified entry point for zone loading and map effects. Owned by SimWorld as
// world.Map; zone and effects state are reset by Unload().
public sealed class MapController
{
    // The engine brings layers up over several seconds, so one reading can't tell a slow load
    // from one that never completes.
    private int layerDumpFrame;
    private static readonly int[] LayerDumpFrames = [60, 180, 360, 600, 1200];

    // Collider-deactivation state. Zone-load is async (resources stream in over
    // several frames), so each pending drop re-tries DisableSpawnAreaColliders
    // each frame until at least one SharedGroup is found near its center, or it
    // times out. Holds the spawn-ring barrier (armed by TryLoad) plus any arena
    // points a scenario requested via ArmColliderDrops.
    private readonly List<PendingColliderDrop> pendingColliderDrops = new();
    private const int BarrierDropMaxFrames = 300; // ~5s at 60fps
    private const float ColliderDropRadius = 10f; // per-point radius (matches spawn barrier)

    private struct PendingColliderDrop
    {
        public Vector3 Center;
        public float Radius;
        public int FramesLeft;
    }

    // AddEffect/DirectorUpdate calls the zone can't accept yet (still async-loading) are retried
    // each Tick: a peer's zone load lags the host's by seconds, and a scenario's early events
    // land in that window.
    private readonly List<PendingMapEffect> pendingEffects = new();
    private readonly List<PendingDirectorUpdate> pendingDirectorUpdates = new();
    private readonly List<PendingBattleTalk> pendingBattleTalks = new();

    // IsInInstance goes true in TryLoad and false in Unload, so this also refuses to queue a
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

    private struct PendingDirectorUpdate
    {
        public uint Category, Arg1, Arg2, Arg3, Arg4, Arg5, Arg6;
        public int FramesLeft;
    }

    // A line is a timing cue: shown seconds late it would mistime the cast it announces.
    private const int BattleTalkMaxFrames = 120;

    private struct PendingBattleTalk
    {
        public uint SpeakerNameId, TextId, DurationMs;
        public int FramesLeft;
    }

    // ── Zone ─────────────────────────────────────────────────────────────────

    // True while a scenario was started by loading a client-side zone.
    // Cleared by Unload() and Reset().
    public bool IsInInstance { get; private set; }

    public bool IsZoneLoaded => Natives.Zone.IsActive;
    public bool IsInInn() => Natives.Zone.IsInInn();

    // Load the target territory client-side. Must be called from the Inn. False when refused
    // (see ZoneSession.StartBlockedReason); nothing is loaded then.
    public bool Load(uint territoryId, Vector3 playerPosition, byte levelSync, ushort itemLevelSync) => Natives.Zone.Enter(territoryId, playerPosition, levelSync, itemLevelSync);

    // Apply weather after a zone load (1-second delayed to let the engine settle).
    public void ApplyWeather(byte weatherId) => Natives.Zone.ApplyWeather(weatherId);

    // Mirrored to peers by MultiplayerManager; scenarios call SetWeather mid-fight for
    // arena-transform lighting cues that no SimObject carries.
    public event Action<byte, float>? WeatherChanged;

    // Live toggle of the per-frame fog hold (see ZoneSession.FogHold) -- a scenario's debug knob.
    // Mirrored to peers, whose own load-time value came from the phase and never moves again.
    public event Action<float?>? FogHoldChanged;

    public void SetFogHold(float? value)
    {
        Natives.Zone.FogHold = value;
        FogHoldChanged?.Invoke(value);
    }

    // A captured server packet, replayed through the client's own dispatcher (see
    // ZoneSession.InjectIncomingPacket). Returns false when it could not be delivered.
    public bool InjectIncomingPacket(uint sourceEntityId, ushort opcode, ReadOnlySpan<byte> body, string what)
        => Natives.Zone.InjectIncomingPacket(sourceEntityId, opcode, body, what);

    // Outside a session, block every outbound packet but the heartbeat (see
    // ZoneSession.HoldSendFirewall).
    public void HoldSendFirewall(bool hold) => Natives.Zone.HoldSendFirewall(hold);

    // See ZoneSession.MaxActionEffectCounter.
    public uint MaxSeenActionEffectCounter => Natives.Zone.MaxActionEffectCounter;

    // Immediately change the active weather (mid-scenario). transition = fade seconds.
    public void SetWeather(byte weatherId, float transition = 0.5f)
    {
        Natives.Zone.SetWeather(weatherId, transition);
        WeatherChanged?.Invoke(weatherId, transition);
    }

    // Revert to the saved inn territory and restore position.
    public void Unload()
    {
        Natives.Zone.Revert();
        IsInInstance = false;
        // Otherwise the native ProcessMapEffect path stays reachable from a replayed message
        // outside any sim.
        Natives.MapEffects.Loaded = false;
        pendingColliderDrops.Clear();
        pendingEffects.Clear();
        pendingDirectorUpdates.Clear();
        pendingBattleTalks.Clear();
        suppressedArenaSlots.Clear();
        Natives.MapEffects.ForgetSuppressions();
        Natives.Layout.ClearSuppressedLayers();
        layerDumpFrame = int.MaxValue;
    }

    // Forces one native LGB layer's instances inactive for as long as the zone stays loaded.
    // Some zones' client-side load activates every layer at once (there is no real duty
    // director selecting the current phase's), so competing geometry from different phases can
    // render in the same space and z-fight. A one-shot SetActive doesn't stick — the engine
    // reconciles it back within a frame or two — so this re-asserts every tick until Unload.
    public void SuppressLayer(ushort layerKey)
    {
        var instances = Natives.Layout.SuppressLayer(layerKey);
        DiagnosticLog.Info($"[MapController] Suppressed layer 0x{layerKey:X} -- {instances} instances.");
    }

    // Per-frame poll. Called from SimWorld.Tick.
    internal void Tick()
    {
        Natives.Layout.ReassertSuppressedLayers();

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
        foreach (var slot in suppressedArenaSlots) Natives.MapEffects.KeepSlotSuppressed(slot);

        for (int i = pendingColliderDrops.Count - 1; i >= 0; i--)
        {
            var drop = pendingColliderDrops[i];
            var disabled = Natives.Layout.DisableSpawnAreaColliders(drop.Center, drop.Radius);
            if (disabled > 0) { pendingColliderDrops.RemoveAt(i); continue; }
            drop.FramesLeft--;
            if (drop.FramesLeft <= 0)
            {
                Plugin.Log.Warning($"[BarrierDrop] Gave up after {BarrierDropMaxFrames} frames — no SGs found near ({drop.Center.X:F2},{drop.Center.Y:F2},{drop.Center.Z:F2})");
                pendingColliderDrops.RemoveAt(i);
            }
            else
            {
                pendingColliderDrops[i] = drop;
            }
        }

        // Insertion order: an SGB slot's State is locked in by whichever call reaches it first
        // (see MapEffects), so a later call to the same index must not win the retry.
        for (int i = 0; i < pendingEffects.Count; i++)
        {
            var pending = pendingEffects[i];
            var applied = pending.PacketFlags == SuppressSentinel
                ? Natives.MapEffects.SuppressSlot(pending.Index)
                : Natives.MapEffects.Apply(pending.PacketFlags, pending.Index);
            if (applied) { pendingEffects.RemoveAt(i); i--; continue; }
            pending.FramesLeft--;
            if (pending.FramesLeft <= 0)
            {
                DiagnosticLog.Warn($"[MapEffect] Gave up applying packetFlags=0x{pending.PacketFlags:X8} index=0x{pending.Index:X} after {BarrierDropMaxFrames} frames — zone likely never finished loading.");
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
                DiagnosticLog.Warn($"[MapEffect] Gave up applying DirectorUpdate category=0x{pending.Category:X8} after {BarrierDropMaxFrames} frames — zone likely never finished loading.");
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

    // Enter the scenario's target instance if conditions are met.
    // Sets IsInInstance when the zone is already active or the Inn load succeeds.
    // False (IsInInstance stays false) when target is null or the load was refused.
    public bool TryLoad(TargetInstance? target, byte levelSync, ushort itemLevelSync)
    {
        if (target == null) return false;
        // A restart re-runs the phase's own InitArena, which re-registers what it suppresses.
        suppressedArenaSlots.Clear();
        // Fresh load only when no zone is active yet (must be in the Inn). When a
        // zone is already loaded we're switching scenarios within the same
        // territory — skip the reload but still fall through to re-apply weather.
        bool freshLoad = false;
        if (!IsZoneLoaded)
        {
            if (!Load(target.TerritoryId, target.PlayerPosition, levelSync, itemLevelSync)) return false;
            freshLoad = true;
        }
        // Per phase, before the weather write; a phase without a hold clears a previous one's.
        Natives.Zone.FogHold = target.FogHold;
        if (target.WeatherId is { } wid)
        {
            if (freshLoad) ApplyWeather(wid);   // fresh load: delay so the engine settles
            else SetWeather(wid);               // restart/switch in a loaded zone: apply now
        }
        IsInInstance = true;
        layerDumpFrame = 0;
        Natives.MapEffects.Loaded = true;
        Natives.Director.Commence();
        ArmBarrierDrop(target.PlayerPosition, 10f);
        // freshLoad=false reuses a zone from an earlier run, where a stale SGB never shows up as
        // a retry or failure.
        DiagnosticLog.Info($"[MapController] TryLoad: freshLoad={freshLoad}, territoryId={target.TerritoryId}.");
        return true;
    }

    private void ArmBarrierDrop(Vector3 center, float radius)
    {
        pendingColliderDrops.Add(new PendingColliderDrop
        {
            Center = center,
            Radius = radius,
            FramesLeft = BarrierDropMaxFrames,
        });
    }

    // Arm collider drops at scenario-provided arena points (already converted to
    // world coordinates). Same async-load retry as the spawn-ring barrier.
    public void ArmColliderDrops(IEnumerable<Vector3> worldCenters)
    {
        foreach (var center in worldCenters)
            ArmBarrierDrop(center, ColliderDropRadius);
    }

    // ── Map effects ───────────────────────────────────────────────────────────

    // Mirrored to peers by MultiplayerManager: these are native, this-client-only calls with no
    // other replication path.
    public event Action<uint, byte>? EffectApplied;
    public event Action<uint, uint, uint, uint, uint, uint, uint>? DirectorUpdated;
    public event Action<uint, uint, uint>? BattleTalked;

    // ProcessDirectorUpdate is where the server's own instance-state packets land, so only the
    // two categories scenarios actually emit are accepted from the network.
    private static readonly uint[] ReplayableDirectorCategories = [0x80000004U, 0x80000027U];

    public static bool IsReplayableDirectorCategory(uint category) => ReplayableDirectorCategories.Contains(category);

    // Replay a single MapEffect state change (packetFlags as in MapEffects). Queued for
    // retry if the zone isn't ready yet; a peer whose zone load lags the host's would otherwise
    // silently lose it. broadcast: false for RunInstanceEvents calls, which host and peer both
    // run locally.
    public void AddEffect(uint packetFlags, byte index, bool broadcast = true)
    {
        if (!InSim(nameof(AddEffect))) return;
        // Behind a pending call for the same slot, or it would land first and be overwritten.
        var behind = pendingEffects.Exists(p => p.Index == index);
        if ((behind || !Natives.MapEffects.Apply(packetFlags, index)) && TryReserveRetrySlot(pendingEffects.Count, "MapEffect"))
        {
            DiagnosticLog.Warn($"[MapEffect] packetFlags=0x{packetFlags:X8} index=0x{index:X} {(behind ? "queued behind an earlier call for the same slot" : "not ready yet -- queued for retry")}.");
            pendingEffects.Add(new PendingMapEffect { PacketFlags = packetFlags, Index = index, FramesLeft = BarrierDropMaxFrames });
        }
        if (broadcast) EffectApplied?.Invoke(packetFlags, index);
    }

    // Hard-deactivate one arena scenery slot (SharedGroup, geometry, VFX and sound): AddEffect's
    // hide flag leaves the SGB's Sound children playing. Retried until the slot's SGB has
    // streamed in. Local-only: callers run on every client (IPhase.RunClientSetup), and leaving
    // the sim reloads the territory.
    private const uint SuppressSentinel = 0xFFFFFFFFu;
    private readonly HashSet<byte> suppressedArenaSlots = new();

    public void SuppressArenaSlot(byte index)
    {
        if (!InSim(nameof(SuppressArenaSlot))) return;
        suppressedArenaSlots.Add(index); // Tick re-silences its Sound children every frame
        if (!Natives.MapEffects.SuppressSlot(index) && TryReserveRetrySlot(pendingEffects.Count, "SuppressSlot"))
            pendingEffects.Add(new PendingMapEffect { PacketFlags = SuppressSentinel, Index = index, FramesLeft = BarrierDropMaxFrames });
    }

    public void LogArena(string label)
    {
        if (!InSim(nameof(LogArena))) return;
        Natives.MapEffects.LogAllSlots(label);
        DiagnosticLog.Info($"[MapController] {label} live VFX and lights: {Natives.Layout.DescribeLiveEffects()}");
    }

    // A different phase starting in the loaded zone: suppression outlives a restart (only a
    // territory reload undoes it), so the new phase would find those slots dark.
    public void RestoreSuppressedArenaSlots()
    {
        pendingEffects.RemoveAll(p => p.PacketFlags == SuppressSentinel);
        suppressedArenaSlots.Clear();
        foreach (var slot in new List<byte>(Natives.MapEffects.SuppressedSlots))
            Natives.MapEffects.RestoreSlot(slot);
    }

    // Replay a native DirectorUpdate event (instance progress / state sync); same retry and
    // broadcast rules as AddEffect.
    public void DirectorUpdate(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, bool broadcast = true)
    {
        if (!InSim(nameof(DirectorUpdate))) return;
        if (!Natives.Director.ProcessDirectorUpdate(category, arg1, arg2, arg3, arg4, arg5, arg6)
            && TryReserveRetrySlot(pendingDirectorUpdates.Count, "DirectorUpdate"))
        {
            DiagnosticLog.Warn($"[MapEffect] DirectorUpdate category=0x{category:X8} not ready yet -- queued for retry.");
            pendingDirectorUpdates.Add(new PendingDirectorUpdate { Category = category, Arg1 = arg1, Arg2 = arg2, Arg3 = arg3, Arg4 = arg4, Arg5 = arg5, Arg6 = arg6, FramesLeft = BarrierDropMaxFrames });
        }
        if (broadcast) DirectorUpdated?.Invoke(category, arg1, arg2, arg3, arg4, arg5, arg6);
    }

    // A boss line: InstanceContentTextData textId in the BattleTalk box under BNpcName
    // speakerNameId, voiced, for durationMs. Same broadcast rules as AddEffect.
    public void BattleTalk(uint speakerNameId, uint textId, uint durationMs, bool broadcast = true)
    {
        if (!InSim(nameof(BattleTalk))) return;
        if (!Natives.Director.BattleTalk(speakerNameId, textId, durationMs)
            && TryReserveRetrySlot(pendingBattleTalks.Count, "BattleTalk"))
        {
            DiagnosticLog.Warn($"[MapEffect] BattleTalk text={textId} not ready yet -- queued for retry.");
            pendingBattleTalks.Add(new PendingBattleTalk { SpeakerNameId = speakerNameId, TextId = textId, DurationMs = durationMs, FramesLeft = BattleTalkMaxFrames });
        }
        if (broadcast) BattleTalked?.Invoke(speakerNameId, textId, durationMs);
    }
}
