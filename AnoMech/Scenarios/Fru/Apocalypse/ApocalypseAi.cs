using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.Apocalypse;

internal sealed class ApocalypseAi : IScenarioAi
{
    public string Name => "NA priority / static spreads / OT bait";
    public string Group => "NA";
    internal void Run(ApocalypsePattern pattern, SimWorld world, Func<SimEnemy?> boss)
    {
        void Move(Func<PartyRole, Vector3> destination)
        {
            foreach (var role in ApocalypsePattern.Roles)
                if (world.Party.Get(role) is SimPartyNpc bot && bot.IsAlive())
                    bot.MoveTo(destination(role), ApocalypseConstants.RunSpeed);
        }
        void At(float time, Func<PartyRole, Vector3> destination)
            => world.Events.Add(time, () => { if (boss() is { IsActive: true }) Move(destination); });
        At(2.2f, role => pattern.Setup(role, false));
        At(16.9f, role => pattern.Setup(role));
        At(20.2f, pattern.FirstStack);
        At(23.8f, role => pattern.Setup(role, spread: true));
        At(26.5f, role => pattern.Setup(role, false, true));
        At(28, pattern.Spread);
        At(36.2f, pattern.PostEruption);
        At(37, pattern.SecondStack);
        void Tank(float time, bool near) => world.Events.Add(time, () => {
            if (boss() is { IsActive: true } && world.Party.Get(PartyRole.OffTank) is SimPartyNpc bot && bot.IsAlive())
                bot.MoveTo(pattern.TankBait(near), ApocalypseConstants.RunSpeed);
        });
        Tank(41.7f, true);
        // After the water snapshot, gather in the middle while OT baits the
        // eight-yalm splash outside. Keep everyone at ordinary run speed.
        At(42, role => role == PartyRole.OffTank ? pattern.TankBait(false) : Vector3.Zero);
        At(45.4f, role => pattern.KnockbackStack(role, boss()!.Position));
        // Let the 47.6s knockback finish its 0.7-second slide before returning.
        // Stay on separate sides so long water can resolve during the approach.
        At(48.4f, role => pattern.ReturnStack(role, boss()!.Position));
    }
}
