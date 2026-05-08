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
    }

    // Enter the scenario's target instance if conditions are met.
    // Sets IsInInstance when the zone is already active or the Inn load succeeds.
    // No-op (IsInInstance stays false) when target is null or the player isn't in the Inn.
    public void TryLoad(TargetInstance? target)
    {
        if (target == null) return;
        if (IsZoneLoaded) { IsInInstance = true; effects.Loaded = true; DirectorFunctions.Commence(); return; }
        if (!IsInInn()) return;
        Load(target.TerritoryId, target.PlayerPosition);
        if (target.WeatherId is { } wid) ApplyWeather(wid);
        IsInInstance = true;
        effects.Loaded = true;
        DirectorFunctions.Commence();
    }

    // ── Map effects ───────────────────────────────────────────────────────────

    // Replay a single MapEffect state change. packetFlags: high16=State, low8=Flags.
    public void AddEffect(uint packetFlags, byte index) => effects.Apply(packetFlags, index);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        effects.Dispose();
        zone.Dispose();
    }
}
