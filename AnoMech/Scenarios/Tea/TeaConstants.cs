using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Tea;

// IDs and tunables shared by the phases of The Epic of Alexander (Ultimate).
public static class TeaConstants
{
    public const byte Level = 80;
    public const ushort ItemLevel = 475;

    public static IReadOnlyList<Waymark> PfTrinesWaymarks =>
    [
        new(WaymarkSlot.A,     new Vector3(     0f, 0f, -13.75f)),
        new(WaymarkSlot.B,     new Vector3( 13.75f, 0f,  -8.25f)),
        new(WaymarkSlot.C,     new Vector3(     0f, 0f,  13.75f)),
        new(WaymarkSlot.D,     new Vector3(-13.75f, 0f,      0f)),
        new(WaymarkSlot.One,   new Vector3(     8f, 0f,      0f)),
        new(WaymarkSlot.Two,   new Vector3( 13.75f, 0f,      0f)),
        new(WaymarkSlot.Three, new Vector3(     0f, 0f,      8f)),
        new(WaymarkSlot.Four,  new Vector3(     8f, 0f,      8f)),
    ];

    public static class BNpcBaseId
    {
        public const uint BruteJustice = 11340;
        public const uint SteamChakram = 11341;
        public const uint CruiseChaser = 11342;
        public const uint AlexanderPrime = 11347;
    }

    public static class BNpcNameId
    {
        public const uint BruteJustice = 9216;
        public const uint SteamChakram = 9217;
        public const uint CruiseChaser = 9218;
        public const uint AlexanderPrime = 9220;
    }

    public static class BNpcLayoutId
    {
        public const uint BruteJustice = 0x7C2580;
        public const uint SteamChakram = 0x7C2581;
        public const uint CruiseChaser = 0x7C2582;
        public const uint AlexanderPrime = 0x7C2586;
    }

    public static class EObjId
    {
        // The content director every real voidzone is bound to.
        public const uint DirectorEventId = 0x80037586;

        // The wormholes' three sizes, radius 8, 6 and 3: each soak swaps in the next.
        public const uint Wormhole1 = 0x1EA1DF;
        public const uint Wormhole2 = 0x1EA1E0;
        public const uint Wormhole3 = 0x1EA1E1;
        public const uint WormholesEntityId = 0x4000EC28u;
    }

    public static class ActionId
    {
        // Limit Cut
        public const uint HawkBlaster = 18480;
        public const uint AlphaSword = 18484;
        public const uint SuperBlasstyCharge = 19279;
        public const uint JKick = 18516;

        // Wormhole's robots
        public const uint LinkUp = 18495;
        public const uint EyeOfTheChakram = 18517;
        public const uint MissileCommand = 18509;
        public const uint Enumeration = 18512;
        public const uint SuperJump = 18505;
        public const uint SuperJumpHit = 18506;
        public const uint ApocalypticRay = 18507;
        public const uint ApocalypticRayHit = 18508;

        // P3
        public const uint WormholeFormation = 18542;
        public const uint LimitCutP3 = 19215;
        public const uint VoidOfRepentance = 18534;
        public const uint SuperBlasstyChargeP3 = 18883;
        public const uint SacramentWormhole = 18519;
        public const uint Repentance1 = 18537;
        public const uint Repentance2 = 18536;
        public const uint Repentance3 = 18535;
        public const uint Confession = 18538;
        public const uint IncineratingHeat = 19025;
    }

    public static class StatusId
    {
        public const ushort EnumerationVuln = SimOnlyStatus.First + 0;

        public const ushort MagicVulnerabilityUp = 1138;
        public const ushort PhysicalVulnerabilityUp = 695;
        public const ushort EnigmaCodex = 2146;
        public const ushort VulnerabilityUp = 444;
    }

    // Limit Cut numbers 1..8.
    public static readonly uint[] LimitCutLockons = [79, 80, 81, 82, 83, 84, 85, 86];

    public static class LockonId
    {
        public const uint Enumeration = 65;
        public const uint IncineratingHeat = 93;
    }

    public static class KnockbackId
    {
        public const uint AlphaSword = 2;
        public const uint SuperBlasstyCharge = 3;
    }

    // InstanceContentTextData rows.
    public static class BattleTalkId
    {
        public const uint DesignationBlassty = 24000;
        public const uint BruteJusticeArrives = 24001;
    }

    public static class Weather
    {
        public const byte FairSkies = 2;
        // Also named Fair Skies; the weather from J Kick on.
        public const byte FairSkiesP2 = 105;
        public const byte BeyondTime = 71;
    }

    public static class Bgm
    {
        public const ushort P1 = 706;
        public const ushort P2 = 723;
        public const ushort P3 = 725;
    }

    // MapEffect slots 1..7 as every restart sets them.
    public static readonly ushort[] ArenaStates = [1, 4, 1, 4, 4, 4, 4];

    // The arena's SharedGroups, each bound to its own EObj, all in timeline state 4.
    public const uint ArenaEObjId = 2007457;
    public static readonly (uint Layout, uint Gimmick)[] ArenaLayouts =
    [
        (0x7C86C6, 0x7C5C3E), (0x7C86C8, 0x7C5C3F), (0x7C86C9, 0x7C5C40), (0x7C86CA, 0x7C5C41),
        (0x7C86CB, 0x7C5C42), (0x7C86CC, 0x7C5C43), (0x7C86CD, 0x7C5C44), (0x7C86CE, 0x7C5C45),
    ];

    public static class Geometry
    {
        // Crossing it kills; the wall a knockback stops at stands past it, about 23.5y out.
        public const float ArenaRadius = 20f;

        // Hawk Blaster pairs land this far out.
        public const float BlasterRadius = 14f;

        // Scenario-local position at `radius` along compass heading `degrees` (0 = north, clockwise).
        public static Vector3 OnCompass(float degrees, float radius)
        {
            var heading = float.DegreesToRadians(degrees);
            return new Vector3(radius * MathF.Sin(heading), 0f, -radius * MathF.Cos(heading));
        }
    }
}
