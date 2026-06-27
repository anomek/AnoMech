using System;
using AnoMech.Core.Game;

namespace AnoMech.Core.SimObjects;

// One end of a SimTether. An end is both the *spec* (how the scenario declares
// it) and the *behaviour* (how it resolves to a live character each tick). The
// resolve is intentionally pure — SimTether owns the per-tick state (its current
// source/target) and hands each end its own current value (`self`) plus the
// opposite end (`other`). That keeps the End objects immutable and freely
// reusable: a PassableEnd holds only its immutable initial seed, not a mutable
// "who holds it now" field.
//
// Two kinds:
//   - FixedEnd       — a constant character (the anchor). At the SimWorld.Tether
//                      API a fixed end is written as a raw SimCharacter; FixedEnd
//                      is the internal wrapper the overloads build around it.
//   - DynamicEnd     — migrates each tick relative to the *other* end:
//       · PassableEnd        — the closest member intercepting the beam toward
//                              the anchor takes it over.
//       · FarthestPlayerEnd  — the party member farthest from the anchor.
//
// Dynamic ends are defined relative to a stable reference, so a tether must have
// at least one fixed end. That invariant is enforced at the call site by the
// SimWorld.Tether overload set (a fixed end is a SimCharacter, a dynamic end is a
// DynamicEnd) — a tether with two dynamic ends matches no overload and simply
// doesn't compile, no runtime guard needed.
//
// `from` hosts the channeling VFX (drawn from `from` toward `to`), so the choice
// of which end is dynamic — and the argument order — together pick both the
// passable direction and the visual direction.
public interface ITetherEnd
{
    // self  = this end's current character (null on the first, seeding resolve).
    // other = the opposite end's current character (the reference for migration).
    SimCharacter? Resolve(SimCharacter? self, SimCharacter? other, TetherContext ctx);
}

// What dynamic ends need to look up: the party (proximity queries) and the
// channeling id (to skip members already holding a parallel tether of the same id).
public readonly record struct TetherContext(CharacterFind<SimCharacter> Party, ushort TetherId);

// Internal — scenarios express a fixed end by passing the SimCharacter directly
// (see the SimWorld.Tether overloads); this is just the ITetherEnd wrapper.
internal sealed class FixedEnd : ITetherEnd
{
    private readonly SimCharacter? character;
    internal FixedEnd(SimCharacter? character) => this.character = character;
    public SimCharacter? Resolve(SimCharacter? self, SimCharacter? other, TetherContext ctx) => character;
}

public abstract class DynamicEnd : ITetherEnd
{
    public abstract SimCharacter? Resolve(SimCharacter? self, SimCharacter? other, TetherContext ctx);
}

// Beam-pass: each tick, whichever alive member stands inside the corridor between
// the current holder and the anchor (and isn't already holding this tether id)
// becomes the new holder — the closest such member to the current holder wins.
// Skipping members who already host this id lets parallel passable tethers of the
// same id coordinate without an external shared set.
//
// The initial holder is the seed passed to End.Passable(member); with no seed
// (End.Passable()) a random alive party member is chosen on the first resolve.
public sealed class PassableEnd : DynamicEnd
{
    private readonly SimCharacter? initial;
    private readonly float halfWidth;

    internal PassableEnd(SimCharacter? initial, float halfWidth)
    {
        this.initial = initial;
        this.halfWidth = halfWidth;
    }

    public override SimCharacter? Resolve(SimCharacter? self, SimCharacter? other, TetherContext ctx)
    {
        var holder = self ?? initial ?? ctx.Party.RandomMember();
        if (holder is null || !holder.IsAlive()) return holder;
        if (other is null || !other.IsAlive()) return holder;   // anchor gone → don't migrate

        var holderPos = holder.Position;
        var anchorPos = other.Position;
        var dx = anchorPos.X - holderPos.X;
        var dz = anchorPos.Z - holderPos.Z;
        var len = MathF.Sqrt(dx * dx + dz * dz);
        if (len < 0.01f) return holder;

        var placement = new Placement(holderPos, MathF.Atan2(dx, dz));
        SimCharacter? best = null;
        var bestDistSq = float.MaxValue;
        foreach (var c in ctx.Party.InsideRect(placement, halfWidth, len))
        {
            if (ReferenceEquals(c, holder)) continue;
            if (c.HasTetherInSlot0(ctx.TetherId)) continue;
            var ddx = c.Position.X - holderPos.X;
            var ddz = c.Position.Z - holderPos.Z;
            var d = ddx * ddx + ddz * ddz;
            if (d < bestDistSq) { bestDistSq = d; best = c; }
        }
        return best ?? holder;
    }
}

// Re-targets to the party member farthest from the anchor each tick (auto-target).
public sealed class FarthestPlayerEnd : DynamicEnd
{
    public override SimCharacter? Resolve(SimCharacter? self, SimCharacter? other, TetherContext ctx)
    {
        if (other is null) return self;
        return ctx.Party.Farest(other.Position) ?? self;
    }
}

// Factory for the dynamic end specs — keeps call sites reading as
// world.Tether(boss, End.Passable(member), ...). A fixed end is the raw
// SimCharacter, so End.Fixed is internal (used only by the SimWorld overloads).
public static class End
{
    internal static FixedEnd Fixed(SimCharacter? character) => new(character);
    // initial = null → a random alive party member is chosen on the first resolve.
    public static PassableEnd Passable(SimCharacter? initial = null, float halfWidth = 0.5f) => new(initial, halfWidth);
    public static FarthestPlayerEnd FarthestPlayer() => new();
}
