using System;

namespace AnoMech.Scenarios.Fru.DiamondDust;

internal sealed class DiamondDustSettings
{
    // Zero means random, 1 = Axe, 2 = Scythe. All other mechanics stay random.
    public int Kick;

    public void Reset() => Kick = 0;

    public DiamondDustPattern CreatePattern(Random random)
        => new(random.Next(4), random.Next(2) == 0,
            Kick == 0 ? random.Next(2) == 0 : Kick == 1,
            random.Next(8), random.Next(2) == 0, random.Next(8));
}
