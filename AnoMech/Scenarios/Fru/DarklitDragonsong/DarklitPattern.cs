using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.DarklitDragonsong;

// NA routes adapted from FRU-Sim, copyright 2025 William Craycroft (GPL-3.0):
// https://github.com/WCGH/FRU-Sim/tree/2a77c857ce1bb6eb472a59a95c7544faba01c55a/scenes/p4
internal sealed class DarklitPattern
{
    public static readonly PartyRole[] Roles = Enum.GetValues<PartyRole>();
    private static readonly PartyRole[] LineupDps = [Roles[6], Roles[7], Roles[4], Roles[5]];
    private static readonly PartyRole[] BaitDps = [Roles[7], Roles[6], Roles[4], Roles[5]];
    private readonly PartyRole[] before, after, links;
    // Indices 0..3 = NW, NE, SE, SW tower tethers; 4..7 = corresponding cone baits.
    public PartyRole TetherWater { get; }
    public PartyRole BaitWater { get; }
    public PartyRole SpiritTarget { get; }
    public bool EastWing { get; }
    public PartyRole Slot(int index, bool swapped = true) => (swapped ? after : before)[index];
    public int Index(PartyRole role, bool swapped = true) => Array.IndexOf(swapped ? after : before, role);
    public bool Tethered(PartyRole role) => Index(role) < 4;
    public bool North(PartyRole role) => Index(role) % 4 < 2;
    public PartyRole Link(int index) => links[index % 4];
    public static Vector3 Convert(float north, float east) => new(east / 2.358f, 0, -north / 2.358f);
    private static Vector3 Quadrant(int i, float north, float east) => Convert(i % 4 < 2 ? north : -north, i % 4 is 1 or 2 ? east : -east);

    public DarklitPattern(PartyRole tank, PartyRole healer, PartyRole dpsA, PartyRole dpsB,
        int shape, int tetherWaterIndex, int baitWaterIndex, PartyRole spiritTarget, bool eastWing)
    {
        if ((int)tank is < 0 or > 1 || (int)healer is < 2 or > 3 || !LineupDps.Contains(dpsA)
            || !LineupDps.Contains(dpsB) || dpsA == dpsB || shape is < 0 or > 2
            || tetherWaterIndex is < 0 or > 3 || baitWaterIndex is < 0 or > 3 || !Roles.Contains(spiritTarget))
            throw new ArgumentException("Invalid Darklit assignment.");
        var pair = LineupDps.Where(r => r == dpsA || r == dpsB).ToArray();
        var remaining = BaitDps.Where(r => r != dpsA && r != dpsB).ToArray();
        // Healer's opposite is tank (bowtie), east DPS (box), or west DPS (hourglass).
        links = shape switch {
            0 => [healer, pair[1], tank, pair[0]],
            1 => [healer, tank, pair[1], pair[0]],
            _ => [healer, tank, pair[0], pair[1]],
        };
        before = [healer, tank, pair[1], pair[0], Roles[5 - (int)healer], Roles[1 - (int)tank], remaining[1], remaining[0]];
        if (shape == 1) (before[1], before[2]) = (before[2], before[1]);
        if (shape == 2) (before[1], before[3]) = (before[3], before[1]);
        TetherWater = before[tetherWaterIndex]; BaitWater = before[4 + baitWaterIndex];
        after = (PartyRole[])before.Clone();
        if ((tetherWaterIndex < 2) == (baitWaterIndex < 2))
            (after[4 + baitWaterIndex], after[4 + 3 - baitWaterIndex]) = (after[4 + 3 - baitWaterIndex], after[4 + baitWaterIndex]);
        SpiritTarget = spiritTarget; EastWing = eastWing;
    }
    public static DarklitPattern Random(Random random)
    {
        var dps = LineupDps.OrderBy(_ => random.Next()).ToArray();
        return new(Roles[random.Next(2)], Roles[2 + random.Next(2)], dps[0], dps[1], random.Next(3), random.Next(4), random.Next(4), Roles[random.Next(8)], random.Next(2) == 0);
    }
    public static Vector3 Middle(PartyRole role) => Convert((int)role % 2 * 0.3f, (int)role * 0.08f);
    public static Vector3 OpeningSpread(PartyRole role) => (int)role switch {
        0 => Convert(16.4f, 10.1f), 1 => Convert(10.1f, 16.4f),
        2 => Convert(10.1f, -16.4f), 3 => Convert(16.4f, -10.1f),
        4 => Convert(-16.4f, 10.1f), 5 => Convert(-10.1f, 16.4f),
        6 => Convert(-10.1f, -16.4f), _ => Convert(-16.4f, -10.1f),
    };
    public static Vector3 Lineup(PartyRole role) => (int)role switch {
        0 => Convert(4.5f, 5), 1 => Convert(0.2f, 11.3f), 2 => Convert(0.2f, -11.3f), 3 => Convert(4.5f, -5),
        4 => Convert(-25.5f, 5), 5 => Convert(-21.2f, 11.3f), 6 => Convert(-21.2f, -11.3f), _ => Convert(-25.5f, -5),
    };
    public Vector3 Bowtie(PartyRole role, bool swapped = true)
    {
        var i = Index(role, swapped);
        return i < 4 ? Quadrant(i, 19, 5.2f) : Quadrant(i, 6.7f, 14.6f);
    }
    public Vector3 Spirit(PartyRole role)
    {
        var i = Index(role);
        if (i < 4) return Quadrant(i, 19, 17.2f);
        // Supports/DPS keep their spread identities even after a water swap.
        var west = i is 4 or 7;
        return Convert(0, west ? ((int)role < 4 ? -38 : -19) : ((int)role < 4 ? 19 : 0));
    }
    public Vector3 Water(PartyRole role) => Convert(North(role) ? 17.3f : -17.3f, EastWing ? -15.5f : 15.5f) + Middle(role) * 0.3f;
    public Vector3 DanceParty(PartyRole role) => Convert(North(role) ? 15 : -15, 0) + Middle(role) * 0.3f;
    public static Vector3 DanceTank(bool east) => Convert(0, east ? 36 : -36);
    public static Vector3 AkhMorn(PartyRole role) => Convert(role == PartyRole.OffTank ? -13 : 13, 0) + Middle(role) * 0.3f;
    public static bool InsideCone(Vector3 position, Vector3 origin, Vector3 direction)
    {
        var offset = position - origin;
        return offset.LengthSquared() < 0.0001f || offset.LengthSquared() <= 3600 && Vector3.Dot(Vector3.Normalize(offset), direction) >= MathF.Cos(MathF.PI / 6);
    }
}
