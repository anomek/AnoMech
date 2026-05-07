using System.Numerics;

namespace UltiSim.Core.SimObjects;

// Per-frame arena fence. Walks every active party member each tick (player
// included, since SimParty exposes the player slot through ActiveMembers) and
// kills anyone whose XZ distance from `center` exceeds `radius`. Geometry is
// XZ-plane only — Y is ignored, matching how scenarios reason about positions.
//
// Spawned via SimWorld.EnforceArenaBoundary so it gets cleared as a normal
// scenario child on Reset.
internal sealed class SimArenaBoundary : ISimObject
{
    private readonly SimWorld world;
    private readonly Vector3 center;
    private readonly float radiusSq;
    private readonly string cause;

    public bool IsAlive => true;

    internal SimArenaBoundary(SimWorld world, Vector3 center, float radius, string cause)
    {
        this.world = world;
        this.center = center;
        this.radiusSq = radius * radius;
        this.cause = cause;
    }

    public void Tick(float deltaSeconds)
    {
        foreach (var member in world.Party.ActiveMembers())
        {
            var dx = member.Position.X - center.X;
            var dz = member.Position.Z - center.Z;
            if (dx * dx + dz * dz > radiusSq) member.Die(cause);
        }
    }

    public void Despawn() { }
}
