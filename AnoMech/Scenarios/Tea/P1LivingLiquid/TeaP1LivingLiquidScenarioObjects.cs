using System.Collections.Generic;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Tea.P1LivingLiquid;

// The live actors the AI reads.
public sealed class TeaP1LivingLiquidScenarioObjects
{
    public SimEnemy? LivingLiquid { get; set; }
    public SimEnemy? LiquidHand { get; set; }
    public List<SimEnemy> Emboli { get; } = [];
    // Doll k spawned at TeaP1LivingLiquidState.DollBearings[k].
    public SimEnemy?[] Dolls { get; } = new SimEnemy?[4];
}
