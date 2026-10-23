using System.Collections.Generic;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Tea.P2BjCc;

// What the Ai reads off the arena; a peer's copies are filled in as they arrive.
public sealed class TeaP2BjCcScenarioObjects
{
    public SimEnemy? BruteJustice { get; set; }
    public SimEnemy? CruiseChaser { get; set; }
    public List<SimEnemy> Chakrams { get; } = [];
    public List<SimEnemy> Tornadoes { get; } = [];
    public SimEnemy? GelidGaol { get; set; }
    public SimEnemy? PlasmaShield { get; set; }
}
