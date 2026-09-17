using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.ParadiseRegained;

internal sealed record PolarizingLine(bool Light, Vector3 Origin, float Rotation, PartyRole Bait)
{
    public PartyRole[] Targets(IReadOnlyList<ParadiseMember> members)
    {
        var forward = new Vector3(MathF.Sin(Rotation), 0f, MathF.Cos(Rotation));
        var right = new Vector3(forward.Z, 0f, -forward.X);
        return members.Where(m =>
        {
            var delta = m.Position - Origin;
            var depth = Vector3.Dot(delta, forward);
            return depth >= 0f && depth <= PolarizingStrikesPlan.Length
                && MathF.Abs(Vector3.Dot(delta, right)) <= PolarizingStrikesPlan.HalfWidth;
        }).Select(m => m.Role).ToArray();
    }
}

internal static class PolarizingStrikesSnapshot
{
    // The native mechanic baits the closest living member on each side. The
    // Godot script's random-side selection is an approximation, not used here.
    public static PolarizingLine? Capture(IReadOnlyList<ParadiseMember> members, Vector3 bossPosition, float facing, bool light)
    {
        var right = new Vector3(MathF.Cos(facing), 0f, -MathF.Sin(facing));
        var side = members.Where(m => (Vector3.Dot(m.Position - bossPosition, right) > 0f) == light)
            .OrderBy(m => HorizontalDistanceSquared(m.Position, bossPosition)).ToArray();
        if (side.Length == 0) return null;
        var target = side[0];
        var offset = target.Position - bossPosition;
        return new(light, bossPosition, MathF.Atan2(offset.X, offset.Z), target.Role);
    }

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        => (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
}
