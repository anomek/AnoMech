using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Map;

// Per-frame arena fence. Walks every active party member each tick (player
// included, since SimParty exposes the player slot through ActiveMembers) and
// kills anyone whose XZ distance from `center` exceeds `radius`. Geometry is
// XZ-plane only — Y is ignored, matching how scenarios reason about positions.
//
// One per world: SimWorld.EnforceArenaBoundary replaces any previous boundary.
internal sealed class SimArenaBoundary : ISimObject
{
    private readonly SimParty party;
    private readonly float radiusSq;
    private readonly string cause;
    private readonly HashSet<SimCharacter> outside = new();

    public bool IsAlive => true;
    public bool IsActive => true;

    internal SimArenaBoundary(SimParty party, float radius, string cause)
    {
        this.party = party;
        this.radiusSq = radius * radius;
        this.cause = cause;
    }

    // XZ distance test against the kill radius; shared by the per-frame fence and
    // external callers (teleport-to-spawn on reset) so they always agree.
    internal bool IsOutside(Vector3 local) => local.X * local.X + local.Z * local.Z > radiusSq;

    public void Tick(float deltaSeconds)
    {
        // Member positions are scenario-local; the boundary is centered on local zero.
        foreach (var member in party.ActiveMembers())
        {
            if (!IsOutside(member.Position))
            {
                outside.Remove(member);
                continue;
            }
            // Once per crossing: a death that doesn't take (godmode) would otherwise be retried
            // and printed every frame the member stays out.
            if (outside.Add(member)) member.Die(SimCharacterDeathExtensions.Environment, cause);
        }
    }

    public void Despawn() { }
}
