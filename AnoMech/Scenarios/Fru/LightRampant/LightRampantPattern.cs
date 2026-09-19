using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.LightRampant;

// NA assignment/waypoints adapted from FRU-Sim, copyright 2025 William Craycroft (GPL-3.0):
// https://github.com/WCGH/FRU-Sim/tree/2a77c857ce1bb6eb472a59a95c7544faba01c55a/scenes/p2
// Its north is +X and arena scale is 2.358. Convert to AnoMech north (-Z).
internal sealed class LightRampantPattern
{
    public static readonly PartyRole[] Roles = Enum.GetValues<PartyRole>();
    private static readonly int[] PuddlePriority = [6, 2, 7, 3, 4, 0, 5, 1];
    private readonly PartyRole[] slots;
    public bool NorthOrbsFirst { get; }
    public bool Pairs { get; }
    public bool SupportPairs { get; }
    public int InitialMask { get; }
    public int WeightSlot { get; }
    public PartyRole Slot(int index) => slots[index];
    public int Index(PartyRole role) => Array.IndexOf(slots, role);
    public bool Puddle(PartyRole role) => Index(role) >= 6;
    public bool NorthGroup(PartyRole role) => Index(role) % 2 == 0;
    public PartyRole[] Weights => [slots[WeightSlot], slots[(WeightSlot + 1) % 6]];
    public int InitialStacks(PartyRole role) => ((InitialMask >> (int)role) & 1) + (Puddle(role) ? 1 : 0);

    public LightRampantPattern(PartyRole puddleA, PartyRole puddleB, int initialMask, int weightSlot, bool northOrbsFirst, bool pairs, bool supportPairs = false)
    {
        if (!Roles.Contains(puddleA) || !Roles.Contains(puddleB) || puddleA == puddleB) throw new ArgumentException("Two distinct puddle roles required.");
        if (initialMask is < 0 or > 255 || BitOperations.PopCount((uint)initialMask) != 4) throw new ArgumentOutOfRangeException(nameof(initialMask));
        if (weightSlot is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(weightSlot));
        InitialMask = initialMask; WeightSlot = weightSlot; NorthOrbsFirst = northOrbsFirst; Pairs = pairs; SupportPairs = supportPairs;
        var puddles = new[] { puddleA, puddleB }.OrderBy(r => Array.IndexOf(PuddlePriority, (int)r)).ToArray();
        // Remove by identity: removing two indices from a shrinking array skips a role.
        var north = new[] { Roles[1], Roles[0], Roles[3], Roles[2] }.Except(puddles).ToList();
        var south = new[] { Roles[6], Roles[7], Roles[4], Roles[5] }.Except(puddles).ToList();
        if (north.Count == 4) { south.Add(north[0]); north.RemoveAt(0); }
        if (south.Count == 4) { north.Add(south[0]); south.RemoveAt(0); }
        slots = [.. north, .. south, .. puddles];
    }
    public static LightRampantPattern Random(Random random)
    {
        var shuffled = Roles.OrderBy(_ => random.Next()).ToArray();
        var initial = Roles.OrderBy(_ => random.Next()).Take(4).Aggregate(0, (mask, r) => mask | (1 << (int)r));
        return new(shuffled[0], shuffled[1], initial, random.Next(6), random.Next(2) == 0, random.Next(2) == 0, random.Next(2) == 0);
    }
    internal static Vector3 FromSim(float north, float east) => new(east / 2.358f, 0, -north / 2.358f);
    public Vector3 Preposition(PartyRole role) => (int)role switch
    {
        0 => FromSim(14.5f, 4.5f), 1 => FromSim(11, 11), 2 => FromSim(11, -11), 3 => FromSim(14.5f, -4.5f),
        4 => FromSim(-14.5f, 4.5f), 5 => FromSim(-11, 11), 6 => FromSim(-11, -11), _ => FromSim(-14.5f, -4.5f),
    };
    public Vector3 Lineup(PartyRole role) => Index(role) switch
    {
        0 => FromSim(7, 13.7f), 1 => FromSim(16.1f, 0), 2 => FromSim(7, -13.7f),
        3 => FromSim(-7, -13.7f), 4 => FromSim(-16.1f, 0), 5 => FromSim(-7, 13.7f),
        6 => FromSim(0, -45), _ => FromSim(0, 45),
    };
    public static Vector3 Tower(int slot) => slot switch
    {
        0 => new(-8 * MathF.Sqrt(3), 0, -8), 1 => new(0, 0, 16), 2 => new(8 * MathF.Sqrt(3), 0, -8),
        3 => new(-8 * MathF.Sqrt(3), 0, 8), 4 => new(0, 0, -16), 5 => new(8 * MathF.Sqrt(3), 0, 8),
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };
    public Vector3 TowerSpot(PartyRole role) => Puddle(role) ? Lineup(role) : Tower(Index(role));
    public Vector3 PuddleSpot(PartyRole role, int dropped)
    {
        var p = dropped switch
        {
            0 => FromSim(0, -45), 1 => FromSim(0, -30), 2 => FromSim(0, -15),
            3 => FromSim(15, -15), 4 => FromSim(30, -15), _ => FromSim(45, 0),
        };
        return Index(role) == 6 ? p : -p;
    }
    public Vector3 GroupSpot(PartyRole role) => FromSim(NorthGroup(role) ? 45 : -45, 0);
    public Vector3 Intermediate(PartyRole role) => FromSim(43, 10.8f) * (NorthGroup(role) ? 1 : -1);
    public Vector3 SafeSpot(PartyRole role, bool first)
    {
        var wideNorth = first == NorthOrbsFirst;
        return NorthGroup(role) ? FromSim(wideNorth ? 36.2f : 42, wideNorth ? 26.6f : 16.7f)
            : FromSim(wideNorth ? -42 : -36.2f, wideNorth ? -16.7f : -26.6f);
    }
    public Vector3 MiddleWait(PartyRole role) => NorthGroup(role)
        ? NorthOrbsFirst ? FromSim(16.1f, 0) : FromSim(7, 12)
        : NorthOrbsFirst ? FromSim(-7, -12) : FromSim(-16.1f, 0);
    public Vector3 BanishSpot(PartyRole role) => Pairs && (int)role < 4
        ? ClockSpot(Roles[(int)role switch { 0 => 6, 1 => 7, 2 => 4, _ => 5 }]) : ClockSpot(role);
    public static Vector3 ClockSpot(PartyRole role) => (int)role switch
    {
        0 => FromSim(18, 0), 1 => FromSim(0, 18), 2 => FromSim(0, -18), 3 => FromSim(-18, 0),
        4 => FromSim(-12, -12), 5 => FromSim(-12, 12), 6 => FromSim(12, -12), _ => FromSim(12, 12),
    };
    public Vector3[] Orbs(bool first)
    {
        var north = first == NorthOrbsFirst;
        return north ? [Tower(4), Tower(5), Tower(3)] : [Tower(1), Tower(2), Tower(0)];
    }
}
