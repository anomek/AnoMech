using System;
using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Map;

// Per-frame arena fence. Walks every active party member each tick (player
// included, since SimParty exposes the player slot through ActiveMembers) and
// kills anyone outside it: past `radius` from the center, or for a square arena past
// `radius` on either axis. Geometry is XZ-plane only; Y is ignored, matching how
// scenarios reason about positions.
//
// One per world: SimWorld.EnforceArenaBoundary replaces any previous boundary.
internal sealed class SimArenaBoundary : ISimObject
{
    private readonly SimParty party;
    private readonly float radius;
    private readonly bool square;
    private readonly string cause;

    public bool IsAlive => true;
    public bool IsActive => true;

    internal SimArenaBoundary(SimParty party, float radius, string cause, bool square = false)
    {
        this.party = party;
        this.radius = radius;
        this.square = square;
        this.cause = cause;
    }

    // XZ distance test against the kill radius; shared by the per-frame fence and
    // external callers (teleport-to-spawn on reset) so they always agree.
    internal bool IsOutside(Vector3 local) => square
        ? MathF.Abs(local.X) > radius || MathF.Abs(local.Z) > radius
        : local.X * local.X + local.Z * local.Z > radius * radius;

    public void Tick(float deltaSeconds)
    {
        // Member positions are scenario-local; the boundary is centered on local zero.
        foreach (var member in party.ActiveMembers())
        {
            if (!IsOutside(member.Position)) continue;
            member.Die(SimCharacterDeathExtensions.Environment, cause);
        }
    }

    public void Despawn() { }
}
