using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top;

public static class TopPositioned
{
    public static IPositioned From(Placement placement) => new At(placement.Position, placement.Rotation);

    private readonly record struct At(Vector3 Position, float Rotation) : IPositioned;
}
