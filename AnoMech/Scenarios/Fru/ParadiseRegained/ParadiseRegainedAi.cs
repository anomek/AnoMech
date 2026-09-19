using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.ParadiseRegained;

internal sealed class ParadiseRegainedAi : IScenarioAi<ParadiseRegainedPattern>
{
    public string Name => "H first / M1-R1 NW / M2-R2 NE; lines T-M-R-H";

    public void Run(ParadiseRegainedPattern pattern, SimWorld world)
    {
        world.Events.Add(2f, () => Move(0));
        world.Events.Add(10f, () => Move(1));
        world.Events.Add(16.5f, () => Move(2));

        void Move(int stage)
        {
            foreach (var member in world.Party.ActiveMembers())
                if (member is SimPartyNpc bot)
                    bot.MoveTo(pattern.Position(bot.Role, stage), ParadiseRegainedPattern.RunSpeed);
        }
    }
}
