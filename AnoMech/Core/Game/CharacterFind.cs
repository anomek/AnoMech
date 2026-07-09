using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game;

// Geometry and proximity queries over the active party. Accessed via SimParty.Find
// so scenarios can write world.Party.Find.InsideRect(...) etc. without pulling in
// a separate static class. Geometry is XZ-plane only (Y ignored). Rotation 0 → +Z
// (south); forward vector = (sin, 0, cos).
public sealed class CharacterFind<T> where T : IPositioned
{
    private readonly Func<IEnumerable<T>> source;

    internal CharacterFind(Func<IEnumerable<T>> source) => this.source = source;

    internal CharacterFind(List<T> source) => this.source = () => source;
    
    // Members whose XZ position is within `radius` of `center`. Alive only.
    public IReadOnlyList<T> InsideCircle(Vector3 center, float radius)
    {
        var rSq = radius * radius;
        var hits = new List<T>();
        foreach (var m in source())
        {
            var dx = m.Position.X - center.X;
            var dz = m.Position.Z - center.Z;
            if (dx * dx + dz * dz <= rSq) hits.Add(m);
        }
        return hits;
    }

    // Cone fired from `origin` facing `origin.Rotation`, half-angle `halfAngleRad`, max
    // range `length`. Members at exactly the origin count as inside. Alive only.
    public IReadOnlyList<T> InsideCone(Placement origin, float halfAngleRad, float length)
    {
        var lenSq = length * length;
        var forwardX = MathF.Sin(origin.Rotation);
        var forwardZ = MathF.Cos(origin.Rotation);
        var cosHalf = MathF.Cos(halfAngleRad);
        var hits = new List<T>();
        foreach (var m in source())
        {
            var dx = m.Position.X - origin.Position.X;
            var dz = m.Position.Z - origin.Position.Z;
            var distSq = dx * dx + dz * dz;
            if (distSq > lenSq) continue;
            if (distSq < 0.0001f) { hits.Add(m); continue; }
            var dist = MathF.Sqrt(distSq);
            var cos = (dx * forwardX + dz * forwardZ) / dist;
            if (cos >= cosHalf) hits.Add(m);
        }
        return hits;
    }

    // Rectangle with back edge at `origin`, extending `length` forward along
    // `origin.Rotation`, `2 * halfWidth` wide. Matches how line/rect AOEs are authored:
    // caster at the back, AOE projects forward. Alive only.
    public IReadOnlyList<T> InsideRect(Placement origin, float halfWidth, float length)
    {
        var forwardX = MathF.Sin(origin.Rotation);
        var forwardZ = MathF.Cos(origin.Rotation);
        // Right vector = forward rotated 90° CW under atan2(x, z) convention.
        var rightX = MathF.Cos(origin.Rotation);
        var rightZ = -MathF.Sin(origin.Rotation);
        var hits = new List<T>();
        foreach (var m in source())
        {
            var dx = m.Position.X - origin.Position.X;
            var dz = m.Position.Z - origin.Position.Z;
            var fwd = dx * forwardX + dz * forwardZ;
            if (fwd < 0f || fwd > length) continue;
            var side = dx * rightX + dz * rightZ;
            if (MathF.Abs(side) <= halfWidth) hits.Add(m);
        }
        return hits;
    }

    public IReadOnlyList<T> OutsideRect(Placement origin, float halfWidth, float halfLength)
    {
        var inside = InsideRect(origin, halfWidth, halfLength);
        return source()
            .Where(s => !inside.Contains(s))
            .ToList();
    }

    // Members whose XZ position is outside `radius` of `center`. Alive only.
    public IReadOnlyList<T> OutsideCircle(Vector3 center, float radius)
    {
        var rSq = radius * radius;
        var hits = new List<T>();
        foreach (var m in source())
        {
            var dx = m.Position.X - center.X;
            var dz = m.Position.Z - center.Z;
            if (dx * dx + dz * dz > rSq) hits.Add(m);
        }
        return hits;
    }

    // Members in the ring (donut) between `innerRadius` and `outerRadius` of `center` on
    // the XZ plane — outside the safe hole, inside the outer edge. Alive only.
    public IReadOnlyList<T> InsideRing(Vector3 center, float innerRadius, float outerRadius)
    {
        var innerSq = innerRadius * innerRadius;
        var outerSq = outerRadius * outerRadius;
        var hits = new List<T>();
        foreach (var m in source())
        {
            var dx = m.Position.X - center.X;
            var dz = m.Position.Z - center.Z;
            var d = dx * dx + dz * dz;
            if (d > innerSq && d <= outerSq) hits.Add(m);
        }
        return hits;
    }

    // Plus-shaped AOE centered on `origin`: union of two perpendicular centered
    // rects. Each arm extends `halfLength` along its axis and `halfWidth`
    // perpendicular.
    public IReadOnlyList<T> InsideCross(Placement origin, float halfWidth, float halfLength)
    {
        var forwardX = MathF.Sin(origin.Rotation);
        var forwardZ = MathF.Cos(origin.Rotation);
        var rightX = MathF.Cos(origin.Rotation);
        var rightZ = -MathF.Sin(origin.Rotation);
        var hits = new List<T>();
        foreach (var m in source())
        {
            var dx = m.Position.X - origin.Position.X;
            var dz = m.Position.Z - origin.Position.Z;
            var fwd  = dx * forwardX + dz * forwardZ;
            var side = dx * rightX   + dz * rightZ;
            var inForwardArm = MathF.Abs(fwd)  <= halfLength && MathF.Abs(side) <= halfWidth;
            var inSideArm    = MathF.Abs(side) <= halfLength && MathF.Abs(fwd)  <= halfWidth;
            if (inForwardArm || inSideArm) hits.Add(m);
        }
        return hits;
    }

    // The member nearest to `from` on the XZ plane, optionally skipping one.
    public T? Closest(Vector3 from, T? exclude = default)
    {
        T? best = default;
        var bestDist = float.MaxValue;
        foreach (var c in source())
        {
            if (Equals(c, exclude)) continue;
            var dx = c.Position.X - from.X;
            var dz = c.Position.Z - from.Z;
            var d = dx * dx + dz * dz;
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }
    
    public T? Extreme(Vector3 from, bool closest, T? exclude = default)
    {
        return closest ? Closest(from, exclude) : Farest(from, exclude);
    }

    // The member farthest from `from` on the XZ plane, optionally skipping one.
    public T? Farest(Vector3 from, T? exclude = default)
    {
        T? worst = default;
        var worstDist = float.MinValue;
        foreach (var c in source())
        {
            if (Equals(c, exclude)) continue;
            var dx = c.Position.X - from.X;
            var dz = c.Position.Z - from.Z;
            var d = dx * dx + dz * dz;
            if (d > worstDist) { worstDist = d; worst = c; }
        }
        return worst;
    }

    // Up to `count` filled slots ordered by ascending XZ distance from `from`.
    public IReadOnlyList<T> ClosestN(Vector3 from, int count)
    {
        var entries = new List<(T c, float distSq)>(8);
        foreach (var c in source())
        {
            var dx = c.Position.X - from.X;
            var dz = c.Position.Z - from.Z;
            entries.Add((c, dx * dx + dz * dz));
        }
        entries.Sort((a, b) => a.distSq.CompareTo(b.distSq));
        var result = new List<T>(Math.Min(count, entries.Count));
        for (int i = 0; i < count && i < entries.Count; i++)
            result.Add(entries[i].c);
        return result;
    }

    // Picks one member at random from the `count` closest on the XZ plane.
    // Returns null when the pool is empty.
    public T? RandomClosestN(Vector3 from, int count)
    {
        var pool = ClosestN(from, count);
        return pool.Count == 0 ? default : pool[Random.Shared.Next(pool.Count)];
    }

    public IReadOnlyList<T> FarestN(Vector3 from, int count)
    {
        var fromV2 = new Vector2(from.X, from.Z);

        return source()
            .OrderByDescending(x => Vector2.DistanceSquared(new Vector2(x.Position.X, x.Position.Z), fromV2))
            .Take(count)
            .ToList();
    }

    // One member chosen uniformly at random from the live set. Null when empty.
    public T? RandomMember()
    {
        var pool = source() as IReadOnlyList<T> ?? source().ToList();
        return pool.Count == 0 ? default : pool[Random.Shared.Next(pool.Count)];
    }

    // size is extra dimension, that's not present in game data.
    // Exact interpretation depends on spell type
    // 3, 13 (cones) -> halfAngleRad default PI/6
    // 8 (charge) -> charge length default 100
    // 10 (donut) -> inner safe radius default 0
    public IReadOnlyList<T> InsideActionAoe(uint actionId, Placement target, float omenRotate = 0f, float? size = null)
    {
        var actionSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        if (!actionSheet.TryGetRow(actionId, out var action))
        {
            Plugin.Log.Warning($"InsideActionAoe: action {actionId} not found");
            return Array.Empty<T>();
        }
        var range = (float)action.EffectRange;
        var halfWidth = action.XAxisModifier > 0 ? action.XAxisModifier * 0.5f : range;
        var forward = new Placement(target.Position, target.Rotation + omenRotate);
        var hits = action.CastType switch
        {
            2 or 5 or 6 // Probably different targeting: ground / caster / target. Doesn't matter for us
                  => InsideCircle(target.Position, range),
            3 or 13     // 3 - frontal cone, 13 - special variant (no idea what's difference)
                  => InsideCone(forward, size ?? MathF.PI / 6f, range),
            8   // this is charge, from caster (target) to destination (just pass distance as size)
                  => InsideRect(forward, halfWidth, size ?? 100),
            4 or 12 // standard rectangle, 12 seems to just be newer kind of spells
                  => InsideRect(forward, halfWidth, range),
            10  // donut
                  => InsideRing(target.Position, size ?? 0f, range),
            11  // +
                  => InsideCross(forward, halfWidth, range),
            _ =>
                LogUnknownCastType(actionId, action.CastType),
        };
        return SortByDistanceTo(hits, target.Position);
    }

    // Orders hits by ascending XZ distance to `from` (closest first), so callers that
    // prioritize the nearest target don't have to re-sort. Same XZ-distance math as
    // ClosestN.
    private static IReadOnlyList<T> SortByDistanceTo(IReadOnlyList<T> hits, Vector3 from)
    {
        var sorted = hits.ToList();
        sorted.Sort((a, b) =>
        {
            var da = (a.Position.X - from.X) * (a.Position.X - from.X)
                   + (a.Position.Z - from.Z) * (a.Position.Z - from.Z);
            var db = (b.Position.X - from.X) * (b.Position.X - from.X)
                   + (b.Position.Z - from.Z) * (b.Position.Z - from.Z);
            return da.CompareTo(db);
        });
        return sorted;
    }

    private static IReadOnlyList<T> LogUnknownCastType(uint actionId, byte castType)
    {
        Plugin.Log.Warning($"InsideActionAoe: action {actionId} has unsupported CastType {castType}");
        return Array.Empty<T>();
    }

    // Returns up to `count` members on the intended side of `src`. Right vector
    // under the atan2(x,z) convention is (-cos(rot), sin(rot)); `sideMul` is the
    // sign applied to the dot product to choose the preferred side (Side.Mul).
    // Shuffles each group independently, fills from the preferred side first,
    // then from the opposite side. Optionally skips one specific member.
    public IReadOnlyList<T> OnSideN(Placement src, int sideMul, int count = 2, T? exclude = default)
    {
        var rightX = -MathF.Cos(src.Rotation);
        var rightZ = MathF.Sin(src.Rotation);

        var onSide = new List<T>();
        var others = new List<T>();
        foreach (var m in source())
        {
            if (Equals(m, exclude)) continue;
            var dot = (m.Position.X - src.Position.X) * rightX + (m.Position.Z - src.Position.Z) * rightZ;
            (dot * sideMul < 0 ? onSide : others).Add(m);
        }
        Shuffle(onSide);
        Shuffle(others);

        var picked = new List<T>(count);
        foreach (var m in onSide) { picked.Add(m); if (picked.Count == count) return picked; }
        foreach (var m in others) { picked.Add(m); if (picked.Count == count) return picked; }
        return picked;
    }

    private static void Shuffle<TItem>(List<TItem> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

// A pre-bound InsideActionAoe call, replayable against any CharacterFind<T>.
// DamageSolver.Resolve runs it against the live party; the DEBUG damage window
// replays the SAME query against its virtual grid. InsideActionAoe is invoked from
// exactly one place (Run), so any parameter it grows is carried to both callers
// automatically — the debug picture can't drift from the resolved AOE.
public readonly struct AoeQuery(uint actionId, Placement source,
    float omenRotate = 0f, float? size = null)
{
    public Placement Source { get; } = source;

    public IReadOnlyList<T> Run<T>(CharacterFind<T> find) where T : IPositioned =>
        find.InsideActionAoe(actionId, Source, omenRotate, size);
}

