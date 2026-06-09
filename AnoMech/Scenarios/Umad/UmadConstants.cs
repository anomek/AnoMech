using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Umad;

// All IDs and tunables for the UMAD (Kefka) scenarios, in one flat class.
// One name per value. Action / status / timeline ids in hex, the rest in decimal.
public static class UmadConstants
{
    public static class BNpcBaseId
    {
        public const uint Kefka = 19506;
        public const uint KefkaHelper = 9020;
        public const uint KefkaClone = 19513;
    }

    public static class BNpcNameId
    {
        public const uint Kefka = 7131;
    }

    public static class ActionId
    {
        public const uint AllThingsEnding_Past     = 0xBADDU; // cone r=100
        public const uint AllThingsEnding_Future   = 0xBADCU; // cone r=100
        public const uint Forsaken                 = 0xBABCU; // circle r=100
        public const uint FutureSEnd               = 0xBAD2U; // single-target
        public const uint FutureSEnd_Resolve       = 0xBAD6U; // circle r=5
        public const uint FutureSEnd_CloneResolve  = 0xBAD8U; // circle r=5
        public const uint LightOfJudgment          = 0xBABDU; // circle r=100
        public const uint PastSEnd                 = 0xBAD3U; // single-target
        public const uint PastSEnd_Resolve         = 0xBAD7U; // circle r=5
        public const uint PastSEnd_CloneResolve    = 0xBAD9U; // circle r=5
        public const uint Spelldriver              = 0xBAC0U; // circle r=5
        public const uint Spellscatter             = 0xBAC1U; // circle r=5
        public const uint Spellwave                = 0xBAC2U; // cone r=40
        public const uint TeleTrouncing            = 0xBABAU; // circle r=2
        public const uint ThePathOfLight           = 0xBABEU; // circle r=4
        public const uint TheRiverOfLight          = 0xBABFU; // circle r=100
        public const uint ThrummingThunderIII      = 0xBA9FU; // rect 40x10 (len x width)
        public const uint ThrummingThunderIII_BAA0 = 0xBAA0U; // rect 40x10 (len x width)
        public const uint ThrummingThunderIII_BAA1 = 0xBAA1U; // rect 40x10 (len x width)
        public const uint UltimateEmbrace          = 0xC24CU; // circle r=5
        public const uint KefkaAuto                = 0xC252U; // single-target
    }

    public static class StatusId
    {
        public const ushort DamageDown = (ushort)0xB5F;
        public const ushort MagicVulnerabilityUp = (ushort)0xB7D;
        public const ushort SpellsTrouble = (ushort)0x13DB;
        public const ushort TelePortent = (ushort)0x13DA;
        public const ushort TelePortent_130C = (ushort)0x130C;
        public const ushort TelePortent_130D = (ushort)0x130D;
        public const ushort TelePortent_130E = (ushort)0x130E;
        public const ushort TelePortent_130F = (ushort)0x130F;
        public const ushort TelePortent_13D7 = (ushort)0x13D7;
        public const ushort TelePortent_13D8 = (ushort)0x13D8;
        public const ushort TelePortent_13D9 = (ushort)0x13D9;
        public const ushort Unknown13DC = (ushort)0x13DC;
        public const ushort Unknown13DD = (ushort)0x13DD;
        public const ushort Unknown13DE = (ushort)0x13DE;
    }

    public static class TimelineId
    {
        public const ushort WarpOut = (ushort)0x1E39;
    }

    public static class LockonId
    {
        public const uint ForsakenStack = 715;
        public const uint ForsakenChariot = 716;
        public const uint ForsakenCone = 717;
    }
    
    public static class Geometry
    {
        public const float AllThingsEndHalfCone = MathF.PI / 2;
    }

    // "12y Waymarks": A/B/C/D on cardinals 12y out, 1-4 on the diagonals (±6,±6 ≈ 8.49y).
    // Scenario-local offsets from the (100,0,100) arena origin.
    public static IReadOnlyList<Waymark> UmadWaymarks =>
    [
        new(WaymarkSlot.A,     new Vector3(  0, 0, -12)),
        new(WaymarkSlot.B,     new Vector3( 12, 0,   0)),
        new(WaymarkSlot.C,     new Vector3(  0, 0,  12)),
        new(WaymarkSlot.D,     new Vector3(-12, 0,   0)),
        new(WaymarkSlot.One,   new Vector3( -6, 0,  -6)),
        new(WaymarkSlot.Two,   new Vector3(  6, 0,  -6)),
        new(WaymarkSlot.Three, new Vector3(  6, 0,   6)),
        new(WaymarkSlot.Four,  new Vector3( -6, 0,   6)),
    ];
}
