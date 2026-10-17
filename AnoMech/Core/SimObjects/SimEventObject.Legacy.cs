namespace AnoMech.Core.SimObjects;

public sealed partial record EventObjectSpawnConfig
{
    // False starts a state-gated prop at state 0 (hidden) until SetVisible(true).
    public bool SpawnVisible { get; init; } = true;

    // Holds the SharedGroup active once attached: some zone SGs otherwise stay inactive client-side.
    public bool ForceSharedGroupActive { get; init; } = false;

    // For an EObj bound to a zone SharedGroup: the SG outlives the EObj, so without this a
    // restarted run finds it still in its last animated state. Pair with ForceSharedGroupActive.
    public bool RestoreStateOnDespawn { get; init; } = false;
}

// UWU P1-P3 and Ultimate Annihilation drive their props' SharedGroups directly.
public partial class SimEventObject
{
    // Writes the EObj state and notifies its SharedGroup; which state shows what is per SGB.
    public void SetState(ushort state) => obj?.SetState(state);

    // Between the spawn's TimelineState and 0, the engine default ("hidden" for gated SGs).
    public void SetVisible(bool visible) => SetState(visible ? SpawnConfig.TimelineState : (ushort)0);

    public void UpdateSharedTimelineState(ushort oldState, ushort newState) => obj?.UpdateSharedTimelineState(oldState, newState);

    private void OnLegacySpawned()
    {
        if (!SpawnConfig.SpawnVisible && SpawnConfig.TimelineState != 0) SetVisible(false);
    }

    private void TickLegacy()
    {
        if (SpawnConfig.ForceSharedGroupActive) obj?.ForceSharedGroupActive();
    }

    private void BeforeLegacyDespawn()
    {
        if (obj == null || !SpawnConfig.RestoreStateOnDespawn) return;
        if (obj.SharedTimelineState != SpawnConfig.TimelineState)
            obj.UpdateSharedTimelineState(obj.SharedTimelineState, SpawnConfig.TimelineState);
        obj.DeactivateSharedGroup();
    }
}
