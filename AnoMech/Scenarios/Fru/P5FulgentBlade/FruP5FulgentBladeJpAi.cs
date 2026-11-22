using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.P5FulgentBlade;

public sealed class FruP5FulgentBladeJpAi : IScenarioAi<FruP5FulgentBladeState>
{
    public string Name => "JP · Mana";
    public string Group => "JP";

    public void Run(FruP5FulgentBladeState state, SimWorld world) => new FruP5FulgentBladeAi().Run(state, world);
}
