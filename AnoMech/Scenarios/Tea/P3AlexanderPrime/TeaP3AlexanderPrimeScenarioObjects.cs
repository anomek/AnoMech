using System.Collections.Generic;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Tea.P3AlexanderPrime;

// What the Ai reads off the arena; a peer's copies are filled in as they arrive.
public sealed class TeaP3AlexanderPrimeScenarioObjects
{
    public SimEnemy? AlexanderPrime { get; set; }
    public SimEnemy? BruteJustice { get; set; }
    public SimEnemy? CruiseChaser { get; set; }
    public SimEnemy? TrueHeart { get; set; }
    // Plasmaspheres[k] is orb k, tethered to OrbHolders[k].
    public SimEnemy?[] Plasmaspheres { get; } = new SimEnemy?[TeaP3AlexanderPrimeState.Orbs];
    public List<SimEnemy> Chakrams { get; } = [];
}
