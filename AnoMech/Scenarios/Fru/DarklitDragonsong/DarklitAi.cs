using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.DarklitDragonsong;

internal sealed class DarklitAi : IScenarioAi
{
    public string Name => "NA";
    public string Group => "NA";
    internal void Run(DarklitPattern pattern, SimWorld world, Func<bool> active, Func<SimEnemy?> oracle, Func<bool> baitEast, PartyRole somberTank)
    {
        void Move(float time, Func<PartyRole, Vector3> position, Func<PartyRole, bool>? filter = null)
            => world.Events.Add(time, () => {
                if (!active()) return;
                foreach (var role in DarklitPattern.Roles)
                    if ((filter == null || filter(role)) && world.Party.Get(role) is SimPartyNpc bot && bot.IsAlive()) bot.MoveTo(position(role), 6);
            });
        Move(2, DarklitPattern.Middle);
        Move(7.2f, DarklitPattern.OpeningSpread);
        Move(14.5f, DarklitPattern.Lineup);
        Move(25.2f, r => pattern.Bowtie(r, false));
        Move(28.2f, r => pattern.Bowtie(r));
        Move(33.3f, pattern.Spirit);
        Move(36.9f, pattern.Water);
        // The assigned tank takes both hits; everyone else stays clear.
        Move(40.6f, r => DarklitPattern.Convert(pattern.North(r) ? 17.3f : -17.3f, baitEast() ? 7.2f : -7.2f), r => r == somberTank);
        Move(41.8f, _ => DarklitPattern.DanceTank(baitEast()), r => r == somberTank);
        Move(42.4f, pattern.DanceParty, r => r != somberTank);
        // After the far jump finishes, become the nearest target for hit two.
        Move(45.5f, _ => oracle()?.Position ?? Vector3.Zero, r => r == somberTank);
        Move(50, DarklitPattern.AkhMorn);
    }
}
