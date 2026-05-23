using System;

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

    public EightWayDirection NextDirection()
    {
        return EightWayDirection.All[rng.Next(8)];
    }

    public int NextSign()
    {
        return rng.Next(2) * 2 - 1;
    }

    public int NextInt(int i)
    {
        return rng.Next(i);
    }

    public EightWayDirection NextIntercardinal()
    {
        return EightWayDirection.Intercardinal[rng.Next(4)];
    }

    public EightWayDirection NextCardinal()
    {
        return EightWayDirection.Cardinal[rng.Next(4)];
    }
}
