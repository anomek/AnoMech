using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Dsr;

public static class DsrConstants
{
    public const byte Level = 90;

    public static class Geometry
    {
        // P3 is a square: players reach 20y along either axis but 25y out to the corners.
        public const float P3ArenaHalfWidth = 21f;
    }

    // A-D on the cardinals, 1-4 on the intercardinals clockwise from NW.
    public static IReadOnlyList<Waymark> NaurWaymarks =>
    [
        new(WaymarkSlot.A,     new Vector3(     0f, 0f,  -9.500f)),
        new(WaymarkSlot.B,     new Vector3( 12.728f, 0f, -12.728f)),
        new(WaymarkSlot.C,     new Vector3(  9.500f, 0f,      0f)),
        new(WaymarkSlot.D,     new Vector3( 12.728f, 0f,  12.728f)),
        new(WaymarkSlot.One,   new Vector3(     0f, 0f,   9.500f)),
        new(WaymarkSlot.Two,   new Vector3(-12.728f, 0f,  12.728f)),
        new(WaymarkSlot.Three, new Vector3( -9.500f, 0f,      0f)),
        new(WaymarkSlot.Four,  new Vector3(-12.728f, 0f, -12.728f)),
    ];

    public static class BgmId
    {
        public const ushort Freefall = 922;
    }

    public static class BNpcBaseId
    {
        public const uint NidhoggP3 = 12605;
        public const uint NidhoggClone = 12606;
    }

    public static class BNpcNameId
    {
        // The boss and its clones are both named Nidhogg.
        public const uint Nidhogg = 3458;
    }

    public static class ActionId
    {
        public const uint NidhoggAutoAttack = 0x6730;
        public const uint FinalChorus = 0x6708;
        public const uint FinalChorusRaidwide = 0x6709;
        public const uint Geirskogul = 0x670A;
        public const uint Drachenlance = 0x670B;
        public const uint DrachenlanceCleave = 0x670C;
        public const uint DiveFromGrace = 0x670D;
        public const uint DarkHighJump = 0x670E;
        public const uint DarkSpineshatterDive = 0x670F;
        public const uint DarkElusiveJump = 0x6710;
        public const uint DarkdragonDive = 0x6711;
        public const uint GnashAndLash = 0x6712;
        public const uint LashAndGnash = 0x6713;
        public const uint EyeOfTheTyrant = 0x6714;
        public const uint GnashingWheel = 0x6715;
        public const uint LashingWheel = 0x6716;
        public static readonly uint[] DarkdragonDiveForSoakers = [0x6717, 0x6718, 0x6719, 0x671A];
        public const uint DarkdragonDiveFail = 0x671B;
        public const uint SoulTether = 0x671C;
        public const uint RevengeOfTheHorde = 0x7436;
    }

    public static class StatusId
    {
        public const ushort FirstInLine = 3004;
        public const ushort SecondInLine = 3005;
        public const ushort ThirdInLine = 3006;
        public const ushort HighJumpTarget = 2755;
        public const ushort SpineshatterDiveTarget = 2756;
        public const ushort ElusiveJumpTarget = 2757;
        public const ushort FireResistanceDownII = 2902;
        public const ushort PhysicalVulnerabilityUp = 2940;
    }

    public static class LockonId
    {
        public const uint FirstInLine = 0x13F;
        public const uint SecondInLine = 0x140;
        public const uint ThirdInLine = 0x141;
    }

    public static class TetherId
    {
        public const ushort SoulTether = 84;
    }
}

// The eight compass points as indices, 0 = N clockwise to 7 = NW. Scenario-local, centred on
// the arena; north is -Z.
public static class Compass
{
    public const int N = 0, NE = 1, E = 2, SE = 3, S = 4, SW = 5, W = 6, NW = 7;

    public static float Bearing(int index) => index * MathF.PI / 4f;

    public static Vector3 Point(int index, float radius) => AtBearing(Bearing(index), radius);

    public static Vector3 AtBearing(float bearing, float radius) =>
        new(MathF.Sin(bearing) * radius, 0f, -MathF.Cos(bearing) * radius);

    public static float BearingOf(Vector3 position) => MathF.Atan2(position.X, -position.Z);
}
