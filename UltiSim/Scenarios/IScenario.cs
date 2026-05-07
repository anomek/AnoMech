using System;
using System.Collections.Generic;
using System.Numerics;
using UltiSim.Core;
using UltiSim.Core.SimObjects;

namespace UltiSim.Scenarios;

public interface IScenario
{
    string Name { get; }
    // When set, Game loads this territory client-side if the player is in the Inn.
    TargetInstance? TargetInstance => null;
    IReadOnlyList<ScenarioOriginOverride> OriginOverrides => Array.Empty<ScenarioOriginOverride>();
    IReadOnlyList<uint> HiddenBaseIds => Array.Empty<uint>();
    IReadOnlyList<Waymark> Waymarks => Array.Empty<Waymark>();
    void Run(SimWorld world, PartyRole playerRole);
    // Per-frame hook driven by Game after World.Tick. `elapsed` is seconds since
    // the scenario's Run was called (real time, not EventTimeScale-scaled).
    void Tick(float delta, float elapsed) { }
    void DrawSettings() { }
}

// When the active territory matches TerritoryId, Game.ResolveScenarioOrigin uses
// (X, Z) as the scenario origin instead of the player's current position.
// Y is taken from the player so spawned objects stay on the floor.
public sealed record ScenarioOriginOverride(uint TerritoryId, float X, float Z);

// Declares that a scenario wants to load a specific FFXIV territory client-side.
// Origin is used as ScenarioOrigin (Y taken from player). PlayerPosition is where
// the player is teleported on entry; Y may need tuning after first in-game test.
// WeatherId, if set, is applied 1 second after zone load.
public sealed record TargetInstance(
    uint TerritoryId,
    Vector3 Origin,
    Vector3 PlayerPosition,
    byte? WeatherId = null);
