using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Umad;

// All IDs and tunables for the UMAD (Kefka) scenarios, in one flat class.
// One name per value, ordered by value. Action / status / timeline ids in hex,
// the rest in decimal. Each action carries its AOE shape + size + cast time
// (CastType / EffectRange / XAxisModifier / Cast100ms from the Action sheet).
// Entries tagged "P3 Black Hole" come from that phase's log; the parser does not
// resolve AOE shapes, so they carry no shape comment yet.
public static class UmadConstants
{
    public static class BNpcBaseId
    {
        public const uint KefkaHelper = 9020;  // generic invisible helper (also used as a Chaos helper in P3)
        public const uint Kefka       = 18475; // "Kefka Says" boss (P4); distinct row from GodKefka (19506) and KefkaP3 (19504)
        public const uint KefkaP3     = 19504; // P3 Black Hole visible boss
        public const uint GravenImage = 19505; // P3 Black Hole
        public const uint GodKefka    = 19506;
        public const uint Chaos       = 19507;
        public const uint ChaosP3     = 19508; // P3 Black Hole (distinct row from Chaos 19507)
        public const uint Exdeath     = 19509; // P3 Black Hole (cf. NeoExdeath 19510)
        public const uint NeoExdeath  = 19510;
        public const uint BlackHole   = 19512; // P3 Black Hole
        public const uint KefkaClone  = 19513;
    }

    public static class BNpcNameId
    {
        public const uint Exdeath     = 6052;
        public const uint NeoExdeath  = 6055;
        public const uint Kefka       = 7131;
        public const uint GravenImage = 7132; // P3 Black Hole
        public const uint Chaos       = 7691;
        public const uint BlackHole   = 8343; // P3 Black Hole
    }

    public static class EObjId
    {
        public const uint EventObj1EC03D = 2015293; // P3 Black Hole
    }

    public static class ActionId
    {
        public const uint MysteryMagic                 = 0xBA94U; // single-target, cast 5.0s
        public const uint BlizzardIIIBlowout_Cast      = 0xBA95U; // single-target, cast 5.0s
        public const uint BlizzardIIIBlowout_Real      = 0xBA98U; // cone r=40, cast 5.0s
        public const uint BlizzardIIIBlowout_FakeOmen  = 0xBA9BU; // cone r=40, cast 5.0s
        public const uint BlizzardIIIBlowout_FakeAnim  = 0xBA9EU; // cone r=40, cast 5.0s
        public const uint ThrummingThunderIII_Real     = 0xBA9FU; // rect 40x10 (len x width), cast 5.0s
        public const uint ThrummingThunderIII_FakeOmen = 0xBAA0U; // rect 40x10 (len x width), cast 5.0s
        public const uint ThrummingThunderIII_FakeAnim = 0xBAA1U; // rect 40x10 (len x width), cast 5.0s
        public const uint ManaCharge                   = 0xBAA4U; // single-target, cast 3.0s
        public const uint ManaRelease                  = 0xBAA5U; // single-target, cast 7.0s
        public const uint TeleTrouncing                = 0xBABAU; // circle r=2, instant
        public const uint LightOfJudgment_Enrage       = 0xBABBU; // circle r=100, cast 5.0s; P3 variant of LightOfJudgment (0xBABD)
        public const uint Forsaken                     = 0xBABCU; // circle r=100, cast 7.0s
        public const uint LightOfJudgment              = 0xBABDU; // circle r=100, cast 5.0s
        public const uint ThePathOfLight               = 0xBABEU; // circle r=4, instant
        public const uint TheRiverOfLight              = 0xBABFU; // circle r=100, instant
        public const uint Spelldriver                  = 0xBAC0U; // circle r=5, instant
        public const uint Spellscatter                 = 0xBAC1U; // circle r=5, instant
        public const uint Spellwave                    = 0xBAC2U; // cone r=40, instant
        public const uint FutureSEnd                   = 0xBAD2U; // single-target, cast 6.4s
        public const uint PastSEnd                     = 0xBAD3U; // single-target, cast 6.4s
        public const uint FutureSEnd_Resolve           = 0xBAD6U; // circle r=5, instant
        public const uint PastSEnd_Resolve             = 0xBAD7U; // circle r=5, instant
        public const uint FutureSEnd_CloneResolve      = 0xBAD8U; // circle r=5, instant
        public const uint PastSEnd_CloneResolve        = 0xBAD9U; // circle r=5, instant
        public const uint AllThingsEnding_Future       = 0xBADCU; // cone r=100, cast 5.0s
        public const uint AllThingsEnding_Past         = 0xBADDU; // cone r=100, cast 5.0s
        public const uint SlapHappy                    = 0xBAE6U; // P3 Black Hole
        public const uint SlapHappy_BAE7               = 0xBAE7U; // P3 Black Hole
        public const uint SlapHappy_BAE8               = 0xBAE8U; // P3 Black Hole
        public const uint SlapHappy_BAE9               = 0xBAE9U; // P3 Black Hole
        public const uint ShockingImpact               = 0xBAEAU; // P3 Black Hole
        public const uint Shockwave_BAEB               = 0xBAEBU; // P3 Black Hole (cf. Shockwave 0xBAFF)
        public const uint LookUponMeAndDespair         = 0xBAECU; // P3 Black Hole
        public const uint LookUponMeAndDespair_BAED    = 0xBAEDU; // P3 Black Hole
        public const uint LookUponMeAndDespair_BAEE    = 0xBAEEU; // P3 Black Hole
        public const uint StompAMole_BAEF              = 0xBAEFU; // P3 Black Hole (cf. StompAMole 0xBAF0)
        public const uint StompAMole                   = 0xBAF0U; // circle r=5, cast 1.5s
        public const uint Cyclone                      = 0xBAF8U; // P3 Black Hole
        public const uint Earthquake_BAFA              = 0xBAFAU; // P3 Black Hole (cf. Earthquake 0xC571)
        public const uint BlackHole                    = 0xBAFBU; // P3 Black Hole
        public const uint Nothingness                  = 0xBAFCU; // P3 Black Hole
        public const uint LatitudinalImplosion         = 0xBAFEU; // P3 Black Hole
        public const uint Shockwave                    = 0xBAFFU; // P3 Black Hole
        public const uint DamningEdict                 = 0xBB01U; // P3 Black Hole
        public const uint KnockDown_BB02               = 0xBB02U; // P3 Black Hole (cf. KnockDown 0xBB03)
        public const uint KnockDown                    = 0xBB03U; // circle r=6, instant
        public const uint BigBang_BB05                 = 0xBB05U; // P3 Black Hole (cf. BigBang 0xBB06)
        public const uint BigBang                      = 0xBB06U; // circle r=6, instant
        public const uint ThunderIII_BB09              = 0xBB09U; // P3 Black Hole
        public const uint ThunderIII_BB0C              = 0xBB0CU; // P3 Black Hole
        public const uint BlizzardIII                  = 0xBB0DU; // circle r=6, cast 3.0s
        public const uint BlizzardIII_BB0F             = 0xBB0FU; // P3 Black Hole (cf. BlizzardIII 0xBB0D)
        public const uint BlizzardIII_BB11             = 0xBB11U; // P3 Black Hole
        public const uint GrandCross                   = 0xBB14U; // circle r=100, cast 9.0s
        public const uint DeathBomb                    = 0xBB15U; // single-target, instant
        public const uint DeathShriek                  = 0xBB16U; // circle r=100, instant
        public const uint DeathBolt                    = 0xBB18U; // circle r=8, instant
        public const uint DeathWave                    = 0xBB1AU; // circle r=8, instant
        public const uint DeathSurge                   = 0xBB1CU; // circle r=100, instant
        public const uint Inferno                      = 0xBB1EU; // single-target, cast 9.0s
        public const uint Tsunami                      = 0xBB1FU; // single-target, cast 9.0s
        public const uint Inferno_Visual               = 0xBB20U; // circle r=100, cast 9.0s
        public const uint Tsunami_Visual               = 0xBB21U; // circle r=100, cast 9.0s
        public const uint StrayFlames_Chariot          = 0xBB22U; // circle r=6, cast 5.0s
        public const uint StrayFlames_Donut            = 0xBB23U; // donut inner r=6, cast 5.0s
        public const uint StraySpray_Donut             = 0xBB24U; // donut inner r=6, cast 5.0s
        public const uint StraySpray_Chariot           = 0xBB25U; // circle r=6, cast 5.0s
        public const uint WhiteHole                    = 0xBD66U; // P3 Black Hole
        public const uint UltimaUpsurge                = 0xC24AU; // circle r=100, cast 5.0s
        public const uint UltimateEmbrace              = 0xC24CU; // circle r=5, cast 5.0s
        public const uint UnknownC250                  = 0xC250U; // P3 Black Hole
        public const uint KefkaAuto                    = 0xC252U; // single-target, instant
        public const uint KefkaSays                    = 0xC2DCU; // single-target, cast 5.0s
        public const uint Aetherlink                   = 0xC2E4U; // P3 Black Hole
        public const uint Aetherlink_C2E5              = 0xC2E5U; // P3 Black Hole
        public const uint FloodOfNaught_WhiteTrue      = 0xC392U; // single-target, cast 5.0s
        public const uint FloodOfNaught_BlackTrue      = 0xC393U; // single-target, cast 5.0s
        public const uint WhiteAntilight               = 0xC394U; // rect 47x21 (len x width), cast 5.5s
        public const uint BlackAntilight               = 0xC395U; // rect 47x21 (len x width), cast 5.5s
        public const uint EdgeOfDeath                  = 0xC396U; // rect 48x2 (len x width), cast 5.5s
        public const uint FloodOfNaught_WhiteFake      = 0xC3A1U; // single-target, cast 5.0s
        public const uint FloodOfNaught_BlackFake      = 0xC3A2U; // single-target, cast 5.0s
        public const uint KefkaPoof                    = 0xC3FDU; // single-target, instant
        public const uint UnknownC4ba                  = 0xC4BAU; // P3 Black Hole
        public const uint UnknownC533                  = 0xC533U; // P3 Black Hole
        public const uint KefkaRest                    = 0xC554U; // single-target, cast 3.0s
        public const uint KefkaUnrest                  = 0xC555U; // single-target, instant
        public const uint Earthquake                   = 0xC571U; // P3 Black Hole
        public const uint Earthquake_C572              = 0xC572U; // P3 Black Hole
        public const uint ThrummingThunderIII_Cast     = 0xC5DEU; // single-target, cast 5.0s; P3 cast variant, base = 0xBA9F
    }

    public static class StatusId
    {
        public const ushort Weakness                  = (ushort)0x2B;
        public const ushort AllaganField              = (ushort)0x1C6;
        public const ushort BeyondDeath               = (ushort)0x566;
        public const ushort ManaCharge                = (ushort)0x5CA;
        public const ushort BlizzardCharged           = (ushort)0x5CC;
        public const ushort ThunderCharged            = (ushort)0x5CD;
        public const ushort Accretion                 = (ushort)0x644;  // P3 Black Hole
        public const ushort KefkaLiesVfx              = (ushort)0x808;
        public const ushort Unknown9E8                = (ushort)0x9E8;  // P3 Black Hole
        public const ushort DamageDown                = (ushort)0xB5F;
        public const ushort MagicVulnerabilityUp      = (ushort)0xB7D;
        public const ushort LightningResistanceDownII = (ushort)0xBB6;  // P3 Black Hole
        public const ushort FirstInLine               = (ushort)0xBBC;  // P3 Black Hole
        public const ushort SecondInLine              = (ushort)0xBBD;  // P3 Black Hole
        public const ushort ThirdInLine               = (ushort)0xBBE;  // P3 Black Hole
        public const ushort EarthResistanceDownII     = (ushort)0xD2C;  // P3 Black Hole
        public const ushort DeepFreeze                = (ushort)0xD98;  // P3 Black Hole
        public const ushort TelePortent_130C          = (ushort)0x130C;
        public const ushort TelePortent_130D          = (ushort)0x130D;
        public const ushort TelePortent_130E          = (ushort)0x130E;
        public const ushort TelePortent_130F          = (ushort)0x130F;
        public const ushort TelePortent_13D7          = (ushort)0x13D7;
        public const ushort TelePortent_13D8          = (ushort)0x13D8;
        public const ushort TelePortent_13D9          = (ushort)0x13D9;
        public const ushort TelePortent               = (ushort)0x13DA;
        public const ushort SpellsTrouble             = (ushort)0x13DB;
        public const ushort Unknown13DC               = (ushort)0x13DC;
        public const ushort Unknown13DD               = (ushort)0x13DD;
        public const ushort Unknown13DE               = (ushort)0x13DE;
        public const ushort Unbecoming                = (ushort)0x154C; // P3 Black Hole
        public const ushort MeanestExistence          = (ushort)0x154D; // P3 Black Hole
        public const ushort PrimordialCrust           = (ushort)0x154E; // P3 Black Hole
        public const ushort WhiteWound                = (ushort)0x15A5;
        public const ushort BlackWound                = (ushort)0x15A6;
        public const ushort CursedShriek              = (ushort)0x15A7;
        public const ushort ForkedLightning           = (ushort)0x15A8;
        public const ushort CompressedWater           = (ushort)0x15A9;
        public const ushort AccelerationBomb          = (ushort)0x15AA;
        public const ushort Entropy                   = (ushort)0x15AB;
        public const ushort DynamicFluid              = (ushort)0x15AC;
    }

    public static class TimelineId
    {
        public const ushort WarpOut = (ushort)0x1E39;
        public const ushort Spawn   = (ushort)0x1E43;
    }

    public static class TetherId
    {
        public const ushort Tether0054 = (ushort)0x54; // P3 Black Hole
    }

    public static class LockonId
    {
        public const uint X_A1            = 161; // P3 Black Hole
        public const uint ColdFalse       = 675;
        public const uint ColdTrue        = 676;
        public const uint LightningFalse  = 677;
        public const uint LightningTrue   = 678;
        public const uint ForsakenStack   = 715;
        public const uint ForsakenChariot = 716;
        public const uint ForsakenCone    = 717;

        public static uint Cold(bool state) => state ? ColdTrue : ColdFalse;
        public static uint Lightning(bool state) => state ? LightningTrue : LightningFalse;
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
