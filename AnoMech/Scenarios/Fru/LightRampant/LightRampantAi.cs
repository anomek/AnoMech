using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.LightRampant;

internal sealed class LightRampantAi : IScenarioAi
{
    public string Name => "NA · Conga";
    public string Group => "NAUR";
    internal void Run(LightRampantPattern pattern, SimWorld world, Func<bool> active, Func<PartyRole, int> stacks)
    {
        void Move(float time, Func<PartyRole, Vector3> position, Func<PartyRole, bool>? include = null)
            => world.Events.Add(time, () => {
                if (!active()) return;
                foreach (var role in LightRampantPattern.Roles)
                    if ((include == null || include(role)) && world.Party.Get(role) is SimPartyNpc bot && bot.IsAlive())
                        bot.MoveTo(position(role), 6);
            });
        Move(3, pattern.Preposition);
        Move(9.75f, pattern.Lineup, pattern.Puddle);
        Move(10.75f, pattern.Lineup);
        Move(12.3f, pattern.TowerSpot, r => !pattern.Puddle(r));
        for (var wave = 0; wave < 5; wave++)
        {
            var dropped = wave + 1;
            Move(15.7f + 1.6f * wave, r => pattern.PuddleSpot(r, dropped), pattern.Puddle);
        }
        // Leave as soon as towers resolve: the reference delay clips the fifth bait at six yalms/s.
        Move(19.1f, pattern.GroupSpot, r => !pattern.Puddle(r));
        Move(23.3f, pattern.Intermediate);
        Move(24.5f, r => pattern.SafeSpot(r, true));
        Move(27, r => pattern.SafeSpot(r, false));
        Move(29.7f, r => stacks(r) == 2 ? Vector3.Zero : pattern.MiddleWait(r));
        Move(34.4f, pattern.BanishSpot);
        Move(38, LightRampantPattern.ClockSpot);
    }
}
