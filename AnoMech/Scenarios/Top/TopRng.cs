namespace AnoMech.Scenarios.Top;

public static class TopRng
{
    public static float NextFloat(this Rng rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
}
