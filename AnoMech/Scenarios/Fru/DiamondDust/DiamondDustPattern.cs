using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using static AnoMech.Scenarios.Fru.DiamondDust.DiamondDustConstants;

namespace AnoMech.Scenarios.Fru.DiamondDust;

// NAUR's linked Partner Swap (Echo) presentation: retain P1 clocks, swap
// supports CCW / DPS CW only when needed, then G1 red/purple and G2 blue/yellow.
internal sealed class DiamondDustPattern(int firstIcicle, bool supportsMarked, bool axe, int reflectionOctant, bool stillness, int gazeOctant)
{
    internal static readonly PartyRole[] Roles = Enum.GetValues<PartyRole>();
    private static readonly int[] Clocks = [0, 2, 6, 4, 5, 3, 7, 1];
    public int FirstIcicle { get; } = firstIcicle % 4;
    public bool Axe { get; } = axe;
    public bool Stillness { get; } = stillness;
    public Vector3 ReflectionPosition => Point(reflectionOctant, 12);
    public Vector3 GazePosition => Point(gazeOctant, 19);
    public static DiamondDustPattern Randomize() => new(Random.Shared.Next(4), Random.Shared.Next(2) == 0,
        Random.Shared.Next(2) == 0, Random.Shared.Next(8), Random.Shared.Next(2) == 0, Random.Shared.Next(8));
    public bool Marked(PartyRole role) => ((int)role < 4) == supportsMarked;
    public static bool GroupOne(PartyRole role) => ((int)role & 1) == 0;
    public static Vector3 Point(float octant, float radius) => new(MathF.Sin(octant * MathF.PI / 4) * radius, 0, -MathF.Cos(octant * MathF.PI / 4) * radius);
    public int Clock(PartyRole role)
    {
        var original = Clocks[(int)role];
        var requiredParity = (FirstIcicle + (Marked(role) ? 1 : 0)) & 1;
        return (original + ((original & 1) == requiredParity ? 0 : (int)role < 4 ? 7 : 1)) % 8;
    }
    public Vector3 KickSpot(PartyRole role) => Point(Clock(role), (Axe ? 16 : 0) + (Marked(role) ? 3 : 1));
    public Vector3 StoneSpot(PartyRole role) => Point(Clock(role), Marked(role) ? Axe ? 19 : 8 : Axe ? 4 : 1);
    public int KnockbackOctant(PartyRole role)
    {
        var g1 = FirstIcicle == 0 ? 0 : FirstIcicle + 4;
        return (g1 + (GroupOne(role) ? 0 : 4)) % 8;
    }
    public Vector3 KnockbackSpot(PartyRole role) => Point(KnockbackOctant(role), 6);
    public IEnumerable<Vector3> Icicles(int wave)
    {
        int[] offsets = wave == 0 ? [0, 4] : wave == 1 ? [1, 3, 5, 7] : [2, 6];
        return offsets.Select(o => Point(FirstIcicle + o, 20));
    }
    public bool Cursed => (reflectionOctant - FirstIcicle + 8) % 4 == 0;
    private int Rotation(PartyRole role)
    {
        if (Cursed) return 1;
        var clockwiseDestination = Point(KnockbackOctant(role) + 2, 1);
        return Vector3.Dot(clockwiseDestination, ReflectionPosition) < 0 ? 1 : -1;
    }
    public Vector3 HolySpot(PartyRole role, int step) => Point(KnockbackOctant(role) + (Cursed ? 1f / 3 : 0) + Rotation(role) * step * 0.5f, 18);
    public Vector3 IceSpot(PartyRole role) => HolySpot(role, Cursed ? 5 : 4) * (17f / 18);
    public bool BehindReflection(Vector3 position)
    {
        var offset = position - ReflectionPosition;
        return offset.LengthSquared() > 0.001f && Vector3.Dot(Vector3.Normalize(offset), Vector3.Normalize(ReflectionPosition)) > MathF.Sqrt(0.5f);
    }
    public static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var line = to - from;
        var t = line.LengthSquared() > 0 ? Math.Clamp(Vector3.Dot(point - from, line) / line.LengthSquared(), 0, 1) : 0;
        return Vector3.Distance(point, from + t * line);
    }
    // A fixed-length ice slide, checked against the entire swept path through
    // the actual puddles. Prefer a wide margin from the wall and cone edge.
    public Vector3? SlideDestination(Vector3 from, bool behind, IReadOnlyList<Vector3> puddles, bool prepareNextSlide = false)
    {
        Vector3? best = null;
        var bestMargin = float.NegativeInfinity;
        for (var degree = 0; degree < 720; degree++)
        {
            var end = from + Point(degree / 90f, SlideDistance);
            if (end.Length() > 19 || BehindReflection(end) != behind) continue;
            var clearance = puddles.Count == 0 ? 20 : puddles.Min(p => DistanceToSegment(p, from, end) - 6);
            if (clearance < 0.35f) continue;
            var relative = end - ReflectionPosition;
            var backDot = Vector3.Dot(Vector3.Normalize(relative), Vector3.Normalize(ReflectionPosition));
            var coneMargin = (behind ? backDot - MathF.Sqrt(0.5f) : MathF.Sqrt(0.5f) - backDot) * relative.Length();
            var margin = MathF.Min(MathF.Min(20 - end.Length(), clearance), coneMargin);
            if (margin > bestMargin && (!prepareNextSlide || SlideDestination(end, true, puddles) != null))
            { bestMargin = margin; best = end; }
        }
        return best;
    }
}
