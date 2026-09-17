using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.ParadiseRegained;

internal readonly record struct ParadiseMember(PartyRole Role, Vector3 Position);

// Capture every hit before applying deaths: a bad cleave must not change which
// player receives the simultaneous nearest/farthest buster or tower hit.
internal sealed record ParadiseRegainedSnapshot(
    PartyRole[] CleaveTargets, PartyRole? BusterTarget, Vector3 BusterPosition, PartyRole[] BusterTargets)
{
    public static ParadiseRegainedSnapshot Capture(IReadOnlyList<ParadiseMember> members,
        Vector3 bossPosition, float bossFacing, bool dark)
    {
        if (members.Count == 0) return new([], null, default, []);
        var ordered = members.OrderBy(m => DistanceSquared(m.Position, bossPosition)).ToArray();
        var bait = dark ? ordered[0] : ordered[^1];
        var direction = bossFacing + ParadiseRegainedPattern.CleaveOffset(dark);
        var forward = new Vector3(MathF.Sin(direction), 0f, MathF.Cos(direction));
        var cleave = members.Where(m =>
        {
            var offset = m.Position - bossPosition;
            offset.Y = 0;
            var distance = offset.Length();
            return distance <= ParadiseRegainedPattern.CleaveRange
                && (distance < 0.001f || Vector3.Dot(offset, forward) >= distance * MathF.Cos(ParadiseRegainedPattern.CleaveHalfAngle));
        }).Select(m => m.Role).ToArray();
        var buster = InCircle(members, bait.Position, ParadiseRegainedPattern.BusterRadius);
        return new(cleave, bait.Role, bait.Position, buster);
    }

    public static PartyRole[] InCircle(IReadOnlyList<ParadiseMember> members, Vector3 center, float radius)
        => members.Where(m => DistanceSquared(m.Position, center) <= radius * radius).Select(m => m.Role).ToArray();

    private static float DistanceSquared(Vector3 a, Vector3 b)
        => (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
}
