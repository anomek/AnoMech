using System.Collections.Generic;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Tea.P3Wormhole;

// What the Ai reads off the arena; a peer's copies are filled in as they arrive.
public sealed class TeaP3WormholeScenarioObjects
{
    public SimEnemy? AlexanderPrime { get; set; }
    public SimEnemy? BruteJustice { get; set; }
    public SimEnemy? CruiseChaser { get; set; }
    public List<SimEnemy> Chakrams { get; } = [];
}
