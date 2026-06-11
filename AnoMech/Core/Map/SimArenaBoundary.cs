using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace AnoMech.Core.Map;

// Per-frame arena fence. Walks every active party member each tick (player
// included, since SimParty exposes the player slot through ActiveMembers) and
// kills anyone whose XZ distance from `center` exceeds `radius`. Geometry is
// XZ-plane only — Y is ignored, matching how scenarios reason about positions.
//
// Also spawns a floor-ring omen VFX at the boundary so the limit is visible.
//
// Added to SimWorld.children via MapController.EnforceArenaBoundary so it gets
// cleared as a normal scenario child on Reset.
internal sealed unsafe class SimArenaBoundary : ISimObject
{
    // Donut omen has a fixed inner/outer ratio of 0.82. Scale by radius/0.82 so the
    // inner edge aligns with the kill boundary (outer edge extends ~4.4y beyond it).
    private const string RingVfxPath = "vfx/omen/eff/gl_sircle_1109w.avfx";

    private readonly SimParty party;
    private readonly float radiusSq;
    private readonly string cause;
    private readonly VfxObject* ringVfx;

    public bool IsAlive => true;
    public bool IsActive => true;

    internal SimArenaBoundary(SimParty party, SimWorld world, float radius, string cause, bool showVfx = true)
    {
        this.party = party;
        this.radiusSq = radius * radius;
        this.cause = cause;

        if (showVfx && Plugin.DataManager.FileExists(RingVfxPath))
            ringVfx = VfxFunctions.SpawnStaticVfx(RingVfxPath, new Placement(world.ScenarioOrigin, 0f), new Vector3(radius / 0.82f, 1f, radius / 0.82f));
    }

    // XZ distance test against the kill radius; shared by the per-frame fence and
    // external callers (teleport-to-spawn on reset) so they always agree.
    internal bool IsOutside(Vector3 local) => local.X * local.X + local.Z * local.Z > radiusSq;

    public void Tick(float deltaSeconds)
    {
        // Member positions are scenario-local; the boundary is centered on local zero.
        foreach (var member in party.ActiveMembers())
        {
            if (IsOutside(member.Position)) member.Die(cause);
        }
    }

    public void Despawn()
    {
        VfxFunctions.RemoveStaticVfx(ringVfx);
    }
}
