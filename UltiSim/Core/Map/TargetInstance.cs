using System.Numerics;

namespace UltiSim.Core.Map;

// Declares that a scenario wants to load a specific FFXIV territory client-side.
// Origin is used as ScenarioOrigin (Y taken from the live player). PlayerPosition
// is where the player is teleported on entry; Y may need tuning after the first
// in-game test. WeatherId, if set, is applied 1 second after zone load.
public sealed record TargetInstance(
    uint TerritoryId,
    Vector3 Origin,
    Vector3 PlayerPosition,
    byte? WeatherId = null);
