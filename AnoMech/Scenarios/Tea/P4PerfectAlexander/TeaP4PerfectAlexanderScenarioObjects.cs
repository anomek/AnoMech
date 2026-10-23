using System.Collections.Generic;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Tea.P4PerfectAlexander;

// What the Ai reads off the arena; a peer's copies are filled in as they arrive.
public sealed class TeaP4PerfectAlexanderScenarioObjects
{
    public SimEnemy? PerfectAlexander { get; set; }
    // West to east: P1 to P4.
    public SimEnemy?[] Clones { get; } = new SimEnemy?[4];
    // The projections in sight; each player tells its own by the tether to it.
    public List<SimEnemy> Projections { get; } = [];
}
