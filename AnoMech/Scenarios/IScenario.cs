using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios;

public interface IScenario
{
    string Name { get; }
    // When set, Game loads this territory client-side if the player is in the Inn.
    TargetInstance? TargetInstance => null;
    IReadOnlyList<ScenarioOriginOverride> OriginOverrides => Array.Empty<ScenarioOriginOverride>();
    IReadOnlyList<uint> HiddenBaseIds => Array.Empty<uint>();
    IReadOnlyList<Waymark> Waymarks => Array.Empty<Waymark>();
    // Content-scene BGM row to play when Game starts this scenario. 0 = no music.
    ushort Bgm => 0;
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

