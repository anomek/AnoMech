using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Umad;

// All IDs and tunables for the UMAD (Kefka) scenarios, in one flat class.
// One name per value, ordered by value. Action / status / timeline ids in hex,
// the rest in decimal. Each action carries its AOE shape + size + cast time
// (CastType / EffectRange / XAxisModifier / Cast100ms from the Action sheet).
public static class UmadConstants
{
    public static class BNpcBaseId
    {
        public const uint KefkaHelper = 9020;  // generic invisible helper (also used as a Chaos helper in P3)
        public const uint Kefka       = 18475; // "Kefka Says" boss (P4); distinct row from GodKefka (19506) and KefkaP3 (19504)
        public const uint KefkaCloneP3 = 19451; // the eight Limit Cut clones; cf. KefkaClone 19513
        public const uint KefkaP3     = 19504; // visible boss
        public const uint GravenImage = 19505;
        public const uint GodKefka    = 19506;
        public const uint Chaos       = 19507;
        public const uint ChaosP3     = 19508; // distinct row from Chaos 19507
        public const uint Exdeath     = 19509; // cf. NeoExdeath 19510
        public const uint NeoExdeath  = 19510;
        public const uint KefkaP5     = 19511; // P5 exaflares boss (Simulant spawns 19511 / name 7131)
        public const uint BlackHole   = 19512;
        public const uint KefkaClone  = 19513;
    }

    public static class BNpcNameId
    {
        public const uint Exdeath     = 6052;
        public const uint NeoExdeath  = 6055;
        public const uint Kefka       = 7131;
        public const uint GravenImage = 7132;
        public const uint Chaos       = 7691;
        public const uint BlackHole   = 8343;
    }

    public static class EObjId
    {
        public const uint GravenStatue       = 2015156;
        // Each statue spawns twice per pull; only one of each pair animates the gaze.
        public const uint GazeStatueNormal   = 2015166; // NE, IndolentWill
        public const uint GazeStatueInverted = 2015167; // NW, AveMaria
        public const uint TelePortent        = 2015267;
        public const uint WindCrystal        = 2015292;
        public const uint EarthCore          = 2015293;
        public const uint CelestriadFireTower      = 2015294;
        public const uint CelestriadIceTower       = 2015295;
        public const uint CelestriadLightningTower = 2015296;

        // The statue SGBs are pre-placed in the zone, so a spawn must bind to their layout
        // instance; a LayoutId-less spawn loads a detached copy nothing ever activates.
        public const uint GazeStatueNormalLayoutId       = 0xBC1793U; // tether pair
        public const uint GazeStatueNormalAnimLayoutId   = 0xBC1794U; // gaze pair
        public const uint GazeStatueInvertedLayoutId     = 0xBC1795U; // tether pair
        public const uint GazeStatueInvertedAnimLayoutId = 0xBC1796U; // gaze pair
        public const uint GravenStatueLayoutId           = 0xBC7C91U;
        public const uint GravenStatueArg2               = 0x00400003U;

        // Hidden state; "appear" (0x0001/0x0002) brings the statue up.
        public const ushort GazeStatueSpawnState = 0x0004;

        // The InstanceContentDirector these props bind to; their EObj rows are Invisibility=7.
        public const uint PropEventId = 0x800375D2U;
    }

    public static class EObjState
    {
        public const uint TelePortentUsed = 7;
        public const ushort CelestriadTowerDormant = 2;
        public const ushort CelestriadTowerActive  = 16;
    }

    public static class Spawn
    {
        public const int TelePortentArg2Serial = 0x50;
        // Clear of the statue props' 0x4000EB80-8A.
        public const uint TelePortentEntityIdBase = 0x4000EB90U;
    }

    public static class ActionId
    {
        public const uint ConfusedAttack               = 0x0007U; // the generic "attack" a Confused player swings
        public const uint RingOfFire                   = 0x8E34U;
        public const uint MysteryMagic                 = 0xBA94U; // single-target, cast 5.0s
        public const uint BlizzardIIIBlowout_Cast      = 0xBA95U; // single-target, cast 5.0s
        public const uint BlizzardIIIBlowout_Real      = 0xBA98U; // cone r=40, cast 5.0s
        public const uint BlizzardIIIBlowout_FakeOmen  = 0xBA9BU; // cone r=40, cast 5.0s
        public const uint BlizzardIIIBlowout_FakeAnim  = 0xBA9EU; // cone r=40, cast 5.0s
        public const uint ThrummingThunderIII_Real     = 0xBA9FU; // rect 40x10 (len x width), cast 5.0s
        public const uint ThrummingThunderIII_FakeOmen = 0xBAA0U; // rect 40x10 (len x width), cast 5.0s
        public const uint ThrummingThunderIII_FakeAnim = 0xBAA1U; // rect 40x10 (len x width), cast 5.0s
        public const uint FlagrantFireSpread           = 0xBAA2U;
        public const uint FlagrantFireStack            = 0xBAA3U;
        public const uint ManaCharge                   = 0xBAA4U; // single-target, cast 3.0s
        public const uint ManaRelease                  = 0xBAA5U; // single-target, cast 7.0s
        public const uint DoubleTroubleTrapStack       = 0xBAA7U;
        public const uint AveMaria                     = 0xBAB3U; // inverted gaze (look toward)
        public const uint IndolentWill                 = 0xBAB4U; // normal gaze (look away)
        public const uint IndulgentWill                = 0xBAB5U;
        public const uint IdyllicWill                  = 0xBAB6U;
        public const uint TeleTrouncing                = 0xBAB9U;
        public const uint TeleTrouncingArrowSpawn      = 0xBABAU; // circle r=2, instant; 0-damage tell
        public const uint LightOfJudgment_Enrage       = 0xBABBU; // circle r=100, cast 5.0s; BossMod LightOfJudgmentP1Enrage (BossP1->self): the lethal fail raidwide (P1 Tele-trouncing arrow soaks), also cast by P4 Kefka
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
        public const uint UltimaBlaster                = 0xBAE3U; // each Limit Cut clone's appearance hit
        public const uint UltimaBlasterCharge          = 0xBAE4U; // rect 100x6 (len x width)
        public const uint SlapHappy_Left                    = 0xBAE6U; // single-target, cast 5.0s
        public const uint SlapHappy_Right               = 0xBAE7U; // single-target, cast 5.0s
        public const uint SlapHappy_Slap               = 0xBAE8U; // circle r=13, instant
        public const uint SlapHappy_FinalSlap               = 0xBAE9U; // circle r=6, cast 1.5s
        public const uint ShockingImpact               = 0xBAEAU; // cone r=100, instant
        public const uint ShockwaveCone               = 0xBAEBU; // cone r=100, instant (cf. Shockwave 0xBAFF)
        public const uint LookUponMeAndDespair         = 0xBAECU; // single-target, cast 4.0s
        public const uint LookUponMeAndDespair2    = 0xBAEDU; // single-target, cast 4.0s
        public const uint LookUponMeAndDespair_Omen    = 0xBAEEU; // rect 100x16 (len x width), cast 5.0s
        public const uint StompAMole_Cast              = 0xBAEFU; // single-target, cast 5.0s (cf. StompAMole 0xBAF0)
        public const uint StompAMole                   = 0xBAF0U; // circle r=5, cast 1.5s
        public const uint UnmitigatedImpact            = 0xBAF1U; // circle r=100, instant; StompAMole (0xBAF0) failure resolution
        public const uint Cyclone                      = 0xBAF8U; // circle r=6, instant
        public const uint Earthquake_Cleanse           = 0xBAFAU; // circle r=100, instant (cf. Earthquake 0xC571)
        public const uint BlackHole                    = 0xBAFBU; // single-target, cast 3.0s
        public const uint Nothingness                  = 0xBAFCU; // rect 125x6 (len x width), instant
        public const uint LongitudinalImplosion        = 0xBAFDU; // single-target, cast 5.0s
        public const uint LatitudinalImplosion         = 0xBAFEU; // single-target, cast 5.0s
        public const uint Shockwave                    = 0xBAFFU; // cone r=40, instant
        public const uint UmbraSmash                   = 0xBB00U; // falloff centred on the farthest player at cast start
        public const uint DamningEdict                 = 0xBB01U; // rect 60x80 (len x width), cast 5.0s
        public const uint KnockDown_Cast               = 0xBB02U; // single-target, cast 5.0s (cf. KnockDown 0xBB03)
        public const uint KnockDown                    = 0xBB03U; // circle r=6, instant
        public const uint BigBang_Cast                 = 0xBB05U; // single-target, cast 5.0s (cf. BigBang 0xBB06)
        public const uint BigBang                      = 0xBB06U; // circle r=6, instant
        public const uint ThunderIII_Cast              = 0xBB09U; // single-target, cast 5.0s
        public const uint ThunderIII_Resolve           = 0xBB0CU; // circle r=5, instant
        public const uint BlizzardIII                  = 0xBB0DU; // circle r=6, cast 3.0s
        public const uint BlizzardIII_Cast             = 0xBB0FU; // single-target, cast 3.0s (cf. BlizzardIII 0xBB0D)
        public const uint BlizzardIII_Raidwide         = 0xBB11U; // circle r=100, cast 4.0s
        public const uint VacuumWave                   = 0xBB13U;
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
        public const uint ChaosEnd1                    = 0xBB3BU; // P5 body cast (Chaos End), launches the first exaflare set
        public const uint ExaflareOmen                 = 0xBB3CU; // P5 exaflare line telegraph (omen only, no damage)
        public const uint ExaflareHit                  = 0xBB3DU; // P5 exaflare explosion, circle r~6; snapshot resolves on this
        public const uint ChaosEnd2                    = 0xBB3EU; // P5 second body cast
        public const uint ExaflareSpread               = 0xBB3FU; // P5 final spread, circle r~5
        public const uint Celestriad                   = 0xBB42U; // targets only Kefka, no damage
        public const uint CelestriadFireIII            = 0xBB43U;
        public const uint CelestriadBlizzardIII        = 0xBB44U;
        public const uint CelestriadThunderIII         = 0xBB45U;
        public const uint StardustFireIII              = 0xBB46U;
        public const uint StardustBlizzardIII          = 0xBB47U;
        public const uint StardustThunderIII           = 0xBB48U;
        public const uint CatastrophicChoiceEarth_Resolve = 0xBB4AU; // Quake, circle r=10
        public const uint CatastrophicChoiceAero_Resolve  = 0xBB4BU; // Tornado, donut inner r=10
        public const uint FloodCast                    = 0xC13FU; // P5 Flood boss windup, self-target, 5.0s cast, no AoE
        public const uint FloodTelegraph               = 0xC183U; // P5 Flood line telegraph, rect 40x10, 1.5s cast, Omen=464
        public const uint FloodAOE                     = 0xC269U; // P5 Flood line resolve, rect 40x10, instant
        public const uint ChaoticFlood                 = 0xBB4FU; // P5 Flood stack call, circle r=6, centered on caster, instant
        public const uint BlackSpark                   = 0xBCCDU; // single-target, instant
        public const uint GravenImage                  = 0xBCF2U;
        public const uint WhiteHole                    = 0xBD66U; // circle r=80, cast 5.0s
        public const uint UltimaUpsurge                = 0xC24AU; // circle r=100, cast 5.0s
        public const uint UltimateEmbrace              = 0xC24CU; // circle r=5, cast 5.0s
        public const uint CatastrophicChoiceEarth      = 0xC24EU;
        public const uint CatastrophicChoiceAero       = 0xC24FU;
        public const uint AutoAttack2                  = 0xC250U; // single-target, instant
        public const uint AutoAttack1                    = 0xC252U; // single-target, instant
        public const uint KefkaSays                    = 0xC2DCU; // single-target, cast 5.0s
        public const uint DecisiveBattle_Chaos         = 0xC2E2U;
        public const uint DecisiveBattle_Exdeath       = 0xC2E3U;
        public const uint Aetherlink_Chaos                   = 0xC2E4U; // single-target, instant
        public const uint Aetherlink_Exdeath              = 0xC2E5U; // single-target, instant
        public const uint FloodOfNaught_WhiteTrue      = 0xC392U; // single-target, cast 5.0s
        public const uint FloodOfNaught_BlackTrue      = 0xC393U; // single-target, cast 5.0s
        public const uint WhiteAntilight               = 0xC394U; // rect 47x21 (len x width), cast 5.5s
        public const uint BlackAntilight               = 0xC395U; // rect 47x21 (len x width), cast 5.5s
        public const uint EdgeOfDeath                  = 0xC396U; // rect 48x2 (len x width), cast 5.5s
        public const uint FloodOfNaught_WhiteFake      = 0xC3A1U; // single-target, cast 5.0s
        public const uint FloodOfNaught_BlackFake      = 0xC3A2U; // single-target, cast 5.0s
        public const uint KefkaPoof                    = 0xC3FDU; // single-target, instant
        public const uint StandUp_ToWall                  = 0xC4BAU; // single-target, instant
        public const uint StandUp_Levitate                  = 0xC533U; // single-target, instant
        public const uint KefkaRest                    = 0xC554U; // single-target, cast 3.0s
        public const uint KefkaUnrest                  = 0xC555U; // single-target, instant
        public const uint Earthquake                   = 0xC571U; // single-target, cast 5.0s
        public const uint Earthquake_Visual              = 0xC572U; // circle r=100, cast 5.0s
        public const uint ThrummingThunderIII_Cast     = 0xC5DEU; // single-target, cast 5.0s; P3 cast variant, base = 0xBA9F
    }

    public static class StatusId
    {
        public const ushort Weakness                  = (ushort)0x2B;
        public const ushort AllaganField              = (ushort)0x1C6;
        public const ushort WindResistanceDownII      = (ushort)0x41C;
        public const ushort Confused                  = (ushort)0x503;
        public const ushort BeyondDeath               = (ushort)0x566;
        public const ushort ManaCharge                = (ushort)0x5CA;
        public const ushort Headwind                  = (ushort)0x642;
        public const ushort Tailwind                  = (ushort)0x643;
        public const ushort BlizzardCharged           = (ushort)0x5CC;
        public const ushort ThunderCharged            = (ushort)0x5CD;
        public const ushort Accretion                 = (ushort)0x644;
        public const ushort KefkaLiesVfx              = (ushort)0x808;
        // Kefka's P3 aura: param 0x1FF after Trance, 0x22B after Ring of Fire.
        public const ushort KefkaTrance               = (ushort)0x8E1;
        public const ushort Bind                      = (ushort)0x9D6;
        public const ushort Max                       = (ushort)0x9E8;
        public const ushort FireResistanceDownII      = (ushort)0xB56;
        public const ushort IceResistanceDownII       = (ushort)0xB57;
        public const ushort DamageDown                = (ushort)0xB5F;
        public const ushort MagicVulnerabilityUp      = (ushort)0xB7D;
        public const ushort LightningResistanceDownII = (ushort)0xBB6;
        public const ushort FirstInLine               = (ushort)0xBBC;
        public const ushort SecondInLine              = (ushort)0xBBD;
        public const ushort ThirdInLine               = (ushort)0xBBE;
        public const ushort EarthResistanceDownII     = (ushort)0xD2C;
        public const ushort DeepFreeze                = (ushort)0xD98;
        public const ushort EpicHero                  = (ushort)0x1060; // Chaos side
        public const ushort EpicVillain               = (ushort)0x1061; // Chaos
        public const ushort FatedHero                 = (ushort)0x1062; // Exdeath side
        public const ushort FatedVillain              = (ushort)0x1063; // Exdeath
        // All 8 are named "Tele-portent"; the direction is icon-only. Primary = both slots of a
        // matching pair, secondary = one half of a different pair.
        public const ushort TelePortentUpPrimary      = (ushort)0x130C;
        public const ushort TelePortentDownPrimary    = (ushort)0x130D;
        public const ushort TelePortentRightPrimary   = (ushort)0x130E;
        public const ushort TelePortentLeftPrimary    = (ushort)0x130F;
        public const ushort Sleep                     = (ushort)0x131E;
        public const ushort DoubleTroubleTrap         = (ushort)0x13D6;
        public const ushort TelePortentUpSecondary    = (ushort)0x13D7;
        public const ushort TelePortentDownSecondary  = (ushort)0x13D8;
        public const ushort TelePortentRightSecondary = (ushort)0x13D9;
        public const ushort TelePortentLeftSecondary  = (ushort)0x13DA;
        public const ushort SpellsTrouble             = (ushort)0x13DB;
        public const ushort Unknown13DC               = (ushort)0x13DC;
        public const ushort Unknown13DD               = (ushort)0x13DD;
        public const ushort Unknown13DE               = (ushort)0x13DE;
        public const ushort Unbecoming                = (ushort)0x154C;
        public const ushort MeanestExistence          = (ushort)0x154D;
        public const ushort PrimordialCrust           = (ushort)0x154E;
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
        // Neo Exdeath's own appear (mon_sp/m0418/show/mon_sp001), which carries his warp sound and
        // VFX; the generic WarpEnd has neither on his model.
        public const ushort NeoExdeathShow = (ushort)0x11D1;
    }

    public static class TetherId
    {
        public const ushort GravenImage  = (ushort)0x2D;
        public const ushort GrabbyTether = (ushort)0x54;
    }

    public static class KnockbackId
    {
        public const uint DoubleTroubleTrapStack = 240;
    }

    public static class LockonId
    {
        public const uint FireSpread      = 127;
        public const uint FireStack       = 128;
        public const uint Stack           = 161;
        // Limit Cut numbers 1-8.
        public static readonly uint[] LimitCutNumbers = [336, 337, 338, 339, 437, 438, 439, 440];
        public const uint FireFalse       = 673;
        public const uint FireTrue        = 674;
        public const uint ColdFalse       = 675;
        public const uint ColdTrue        = 676;
        public const uint LightningFalse  = 677;
        public const uint LightningTrue   = 678;
        public const uint ForsakenStack   = 715;
        public const uint ForsakenChariot = 716;
        public const uint ForsakenCone    = 717;
    }

    public static class VfxPath
    {
        // The statue SGBs' own VFX children: "mari" = AveMaria, "nemu" = IndolentWill.
        public const string StatueMariaAppear   = "bg/ex2/05_zon_z3/common/vfx/eff/b1328mari2_u.avfx";
        public const string StatueMariaWindUp   = "bg/ex2/05_zon_z3/common/vfx/eff/b1328mari1_u.avfx";
        public const string StatueMariaResolve  = "bg/ex2/05_zon_z3/common/vfx/eff/b1328mari3_u.avfx";
        public const string StatueNemuriAppear  = "bg/ex2/05_zon_z3/common/vfx/eff/b1329nemu2_u.avfx";
        public const string StatueNemuriWindUp  = "bg/ex2/05_zon_z3/common/vfx/eff/b1329nemu1_u.avfx";
        public const string StatueNemuriResolve = "bg/ex2/05_zon_z3/common/vfx/eff/b1329nemu3_u.avfx";

        public const string TeleTrouncingArrowSpawnHit = "vfx/monster/gimmick6/eff/z3oy_b0_g05c0c.avfx";
        public const string DoubleTroubleTrapStackHit  = "vfx/monster/gimmick6/eff/z3oy_b0_g02c0c.avfx";
        public const string IndulgentWillCasterShoot   = "vfx/monster/gimmick2/eff/shoot_mgc00f.avfx";
        public const string IndulgentWillCasterBurst   = "vfx/monster/gimmick2/eff/f1d8_b2_g02c0f.avfx";
        public const string IndulgentWillTarget        = "vfx/monster/gimmick2/eff/f1d8_b2_g02t0f.avfx";
        public const string IdyllicWillCaster          = "vfx/monster/gimmick6/eff/z3oy_b0_g04c0c.avfx";
        public const string IdyllicWillTarget          = "vfx/monster/gimmick6/eff/z3oy_b0_g04t0c.avfx";
        public const string AveMariaHit                = "vfx/monster/gimmick6/eff/z3oy_b0_g29c0c.avfx";
        public const string IndolentWillHit            = "vfx/monster/gimmick6/eff/z3oy_b0_g30c0c.avfx";
        // The wind-up packet is identical for both gazes; only which statue glows tells them apart.
        public const string GazeWindUp                 = "vfx/monster/gimmick6/eff/z3oy_b0_g27c0c.avfx";
    }

    public static class Geometry
    {
        public const float AllThingsEndHalfCone = MathF.PI / 2;
        public const float ArenaRadius = 20f;
    }

}
