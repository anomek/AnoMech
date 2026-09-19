using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.UltimateRelativity;

internal sealed class UltimateRelativityAi : IScenarioAi
{
    public string Name => "NA";
    public string Group => "NA";
    internal void Run(UltimateRelativityPattern pattern, SimWorld world, Func<bool> active)
    {
        void Each(Action<SimPartyNpc> action)
        {
            if (!active()) return;
            foreach (var role in UltimateRelativityPattern.Roles)
                if (world.Party.Get(role) is SimPartyNpc bot && bot.IsAlive()) action(bot);
        }
        void Move(float time, Func<PartyRole, Vector3> position)
            => world.Events.Add(time, () => Each(bot => bot.MoveTo(position(bot.Role), 6)));
        Move(1, pattern.CenterSpot);
        Move(19.2f, r => pattern.FireSpot(r, 0));
        Move(23.9f, r => pattern.BaitSpot(r, 0));
        Move(28.6f, r => pattern.FireSpot(r, 1, true));
        Move(30.6f, r => pattern.FireSpot(r, 1));
        Move(34, r => pattern.BaitSpot(r, 1));
        Move(38.7f, r => pattern.FireSpot(r, 2));
        Move(45, r => pattern.BaitSpot(r, 2));
        Move(48.8f, pattern.CenterSpot);
        world.Events.Add(51, () => Each(bot => {
            bot.StopMoving();
            bot.Face(bot.Position + pattern.Direction(pattern.Assignment(bot.Role)) * 100);
        }));
        Move(55.8f, pattern.FinalSpot);
    }
}
