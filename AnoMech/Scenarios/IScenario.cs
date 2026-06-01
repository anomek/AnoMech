using System;
using System.Collections.Generic;
using AnoMech.Core.Game;
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
    void Run(SimWorld world, bool solo);
    void Tick(float delta, float elapsed) { }
    void DrawSettings() { }
}
