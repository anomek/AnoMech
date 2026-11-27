using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Map;

// Per-frame arena fence. Walks every active party member each tick (player
// included, since SimParty exposes the player slot through ActiveMembers) and
// kills anyone outside the shape.
//
// One per world: SimWorld.EnforceArenaBoundary replaces any previous boundary.
internal sealed class SimArenaBoundary : ISimObject
{
    private readonly SimParty party;
    private readonly IArenaShape shape;
    private readonly string cause;

    public bool IsAlive => true;
    public bool IsActive => true;

    internal SimArenaBoundary(SimParty party, IArenaShape shape, string cause)
    {
        this.party = party;
        this.shape = shape;
        this.cause = cause;
    }

    internal IArenaShape Shape => shape;

    public void Tick(float deltaSeconds)
    {
        // Member positions are scenario-local; the shape is centered on local zero.
        foreach (var member in party.ActiveMembers())
        {
            if (!shape.IsOutside(member.Position)) continue;
            member.Die(SimCharacterDeathExtensions.Environment, cause);
        }
    }

    public void Despawn() { }
}
