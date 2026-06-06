using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios;

public interface IScenario
{
    string Name { get; }
    TargetInstance TargetInstance { get; }
    IReadOnlyList<Waymark> Waymarks => Array.Empty<Waymark>();
    ushort Bgm => 0;
    bool SupportsSolo => false;
    // Scenario-local arena positions whose BG SharedGroup colliders should be
    // removed at scenario start (same mechanism as the spawn-ring barrier drop).
    IReadOnlyList<Vector3> ColliderRemovalPoints => Array.Empty<Vector3>();
    // Selectable AI strats, ordered. The main-window strat picker is shown only when
    // there is more than one. The user's choice is passed to Run as selectedAi.
    IReadOnlyList<IScenarioAi> AiStrats { get; }
    // selectedAi: index into AiStrats of the strat to run, or null for solo (no AI).
    void Run(SimWorld world, int? selectedAi);
    void Tick(float delta, float elapsed) { }
    void DrawSettings() { }
}
