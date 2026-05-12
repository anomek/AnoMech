using System;
using System.Numerics;

namespace UltiSim.Core.Map;

// Unified entry point for zone loading and map effects. Owned by SimWorld as
// world.Map. Zone and effects state are reset by Reset(); zone hooks are
// released by Dispose().
public sealed class MapController : IDisposable
{
    private readonly MapEffects effects = new();
    private readonly ZoneSession zone = new();

    // Spawn-barrier deactivation state. Zone-load is async (resources stream in
    // over several frames), so we re-try DisableSpawnAreaColliders each frame
    // until at least one SharedGroup is found near the spawn point, or we time out.
    private Vector3 barrierDropCenter;
    private float barrierDropRadius;
    private int barrierDropFramesLeft;
    private const int BarrierDropMaxFrames = 300; // ~5s at 60fps

    // ── Zone ─────────────────────────────────────────────────────────────────

    // True while a scenario was started by loading a client-side zone.
    // Cleared by Unload() and Reset().
    public bool IsInInstance { get; private set; }

    public bool IsZoneLoaded => zone.IsActive;
    public bool IsInInn() => ZoneSession.IsInInn();

    // Load the target territory client-side. Must be called from the Inn.
    public void Load(uint territoryId, Vector3 playerPosition) => zone.Enter(territoryId, playerPosition);

    // Apply weather after a zone load (1-second delayed to let the engine settle).
    public void ApplyWeather(byte weatherId) => zone.ApplyWeather(weatherId);

    // Revert to the saved inn territory and restore position.
    public void Unload()
    {
        zone.Revert();
        IsInInstance = false;
        barrierDropFramesLeft = 0;
    }

    // Per-frame poll. Called from SimWorld.Tick.
    internal void Tick()
    {
        if (barrierDropFramesLeft <= 0) return;
        var disabled = DirectorFunctions.DisableSpawnAreaColliders(barrierDropCenter, barrierDropRadius);
        if (disabled > 0) { barrierDropFramesLeft = 0; return; }
        barrierDropFramesLeft--;
        if (barrierDropFramesLeft == 0)
            Plugin.Log.Warning($"[BarrierDrop] Gave up after {BarrierDropMaxFrames} frames — no SGs found near ({barrierDropCenter.X:F2},{barrierDropCenter.Y:F2},{barrierDropCenter.Z:F2})");
    }

    // Enter the scenario's target instance if conditions are met.
    // Sets IsInInstance when the zone is already active or the Inn load succeeds.
    // No-op (IsInInstance stays false) when target is null or the player isn't in the Inn.
    public void TryLoad(TargetInstance? target)
    {
        if (target == null) return;
        if (IsZoneLoaded)
        {
            IsInInstance = true;
            effects.Loaded = true;
            DirectorFunctions.Commence();
            ArmBarrierDrop(target.PlayerPosition, 10f);
            return;
        }
        if (!IsInInn()) return;
        Load(target.TerritoryId, target.PlayerPosition);
        if (target.WeatherId is { } wid) ApplyWeather(wid);
        IsInInstance = true;
        effects.Loaded = true;
        DirectorFunctions.Commence();
        ArmBarrierDrop(target.PlayerPosition, 10f);
    }

    private void ArmBarrierDrop(Vector3 center, float radius)
    {
        barrierDropCenter = center;
        barrierDropRadius = radius;
        barrierDropFramesLeft = BarrierDropMaxFrames;
    }

    // ── Map effects ───────────────────────────────────────────────────────────

    // Replay a single MapEffect state change. packetFlags: high16=State, low8=Flags.
    public void AddEffect(uint packetFlags, byte index) => effects.Apply(packetFlags, index);

    // Replay a server-sent DirectorUpdate (`33|` ACT log opcode) against the
    // live InstanceContentDirector. Mirrors the FireDirectorUpdate signature.
    public void DirectorUpdate(uint category, uint arg1, uint arg2 = 0,
                               int a6 = 0, int a7 = 0, int a8 = 0, int a9 = 0)
        => DirectorFunctions.FireDirectorUpdate(category, arg1, arg2, a6, a7, a8, a9);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        effects.Dispose();
        zone.Dispose();
    }
}
