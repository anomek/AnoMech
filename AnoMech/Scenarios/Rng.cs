using System;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios;

public class Rng
{
    private readonly Random rng = new();

    public bool NextBool()
    {
        return (rng.Next(2) == 0);
    }

    public T NextObj<T>(params T[] values)
    {
        return values[rng.Next(values.Length)];
    }

    public Direction NextDirection()
    {
        return Direction.All[rng.Next(8)];
    }

    public int NextSign()
    {
        return rng.Next(2) * 2 - 1;
    }

    public int NextInt(int i)
    {
        return rng.Next(i);
    }

    public Direction NextIntercardinal()
    {
        return Direction.Intercardinal[rng.Next(4)];
    }

    public Direction NextCardinal()
    {
        return Direction.Cardinal[rng.Next(4)];
    }

    public PartyRole NextRole()
    {
        return (PartyRole)rng.Next(8);
    }
}
