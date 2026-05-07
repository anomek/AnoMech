using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace UltiSim.Core.SimObjects;

// Two-ended tether. Each side writes the channeling-sheet id at slot 0 of its
// VfxContainer.Tethers, pointing at the other side's GameObjectId. Optionally
// asks each character to host a matching debuff for the tether's duration —
// SimCharacter.AddStatus(id, duration) owns the countdown and auto-removes on expiry.
//
// SimTether ticks its own elapsed counter so it can clear the tether VFX when
// duration is reached. Both auto-expire and Despawn (called via SimWorld.Reset
// or directly) use a sentinel check: only clear the slot when our TetherId is
// still occupying it. This handles chained tethers that overwrite the same
// slot in the same frame (e.g. P5 Delta prep → real at t=29s) without racing
// our own ClearTether against the new SetTether.
//
// Distance / endpoint-death break logic lives in the owning scenario — SimTether
// only renders the visual and ticks expiry.
public sealed unsafe class SimTether : ISimObject
{
    private const byte Slot = 0;

    private readonly SimCharacter a;
    private readonly SimCharacter b;
    private readonly SimStatus? statusA;
    private readonly SimStatus? statusB;
    private readonly float duration;
    private float elapsed;
    private bool active;

    public ushort TetherId { get; }
    public SimCharacter A => a;
    public SimCharacter B => b;
    // Set by the scenario when this tether has been resolved (broken or failed) to
    // prevent duplicate processing if multiple triggers fire in the same frame.
    public bool Resolved { get; set; }

    public static bool IsAnyDead(SimTether t) => !t.a.IsAlive || !t.b.IsAlive;
    public bool StretchGt(float distance) => Vector3.DistanceSquared(a.Position, b.Position) > distance * distance;
    public bool StretchLt(float distance) => Vector3.DistanceSquared(a.Position, b.Position) < distance * distance;

    internal SimTether(SimCharacter a, SimCharacter b, ushort tetherId, ushort debuffStatusId, float duration)
    {
        this.a = a;
        this.b = b;
        TetherId = tetherId;
        this.duration = duration;
        VfxFunctions.SetTether((Character*)a.BattleCharaPtr, Slot, TetherId, b.GameObjectId, 1);
        if (debuffStatusId != 0 && duration > 0f)
        {
            statusA = a.AddStatus(debuffStatusId, duration);
            statusB = b.AddStatus(debuffStatusId, duration);
        }
        active = true;
    }

    public void Tick(float deltaSeconds)
    {
        if (!active) return;
        if (duration <= 0f) return; // permanent tether — only Despawn clears it
        elapsed += deltaSeconds;
        if (elapsed >= duration)
        {
            ClearTetherVfxIfOwned();
            active = false;
        }
    }

    public void Despawn()
    {
        if (active)
        {
            ClearTetherVfxIfOwned();
            active = false;
        }
        // SimStatus.Despawn is idempotent — safe even if already auto-expired.
        statusA?.Despawn();
        statusB?.Despawn();
    }

    // Sentinel-checked clear: only wipe a slot we still own. A chained tether
    // (new SetTether on the same slot via the new SimTether ctor) will have
    // overwritten Vfx.Tethers[slot].Id; we leave that alone.
    private void ClearTetherVfxIfOwned()
    {
        var ca = (Character*)a.BattleCharaPtr;
        if (VfxFunctions.GetTetherId(ca, Slot) == TetherId) VfxFunctions.ClearTether(ca, Slot);
    }
}
