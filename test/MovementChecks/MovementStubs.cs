using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Geometry;

namespace AnoMech.Core.SimObjects;

// Only the native actor boundary is replaced. Tests execute production Movement,
// PlayerMovement, Placement and obstacle geometry, rather than a slide model.
internal class SimCharacter : IPositioned
{
    public Vector3 Position { get; set; }
    public float Rotation { get; set; }
    public bool Dead { get; set; }
    public bool AnimationLock { get; set; }
    public float HitboxRadius => 0.5f;
    public ObstacleField Obstacles { get; } = new();
    public int PositionWrites { get; private set; }
    public int AnimationResets { get; private set; }
    public List<ushort> Animations { get; } = [];
    public void SetPosition(Placement placement)
    {
        Position = placement.Position;
        Rotation = placement.Rotation;
        PositionWrites++;
    }
    public void SetRotation(float rotation) => Rotation = rotation;
    public void PlayActionTimeline(ushort timeline, ushort baseOverride = 0) => Animations.Add(timeline);
    public void ResetActionTimeline() => AnimationResets++;
}

internal sealed class SimPlayer : SimCharacter;
internal sealed class SimTether
{
    public SimCharacter? A { get; set; }
    public SimCharacter? B { get; set; }
    public bool IsActive { get; set; } = true;
}
internal static class DeathExtensions
{
    public static bool IsAlive(this SimCharacter? character) => character is { Dead: false };
}
