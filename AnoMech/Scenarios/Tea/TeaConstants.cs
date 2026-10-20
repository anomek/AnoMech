using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Tea;

// IDs and tunables shared by the phases of The Epic of Alexander (Ultimate).
public static class TeaConstants
{
    // Decree Nisi's four letters (the game's α, β, γ, δ).
    public enum NisiLetter { Alpha, Beta, Gamma, Delta }

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
        public const uint LivingLiquid = 11335;
        public const uint LiquidHand = 11336;
        public const uint LiquidRage = 11337;
        public const uint JagdDoll = 11338;
        public const uint Embolus = 11339;
        public const uint BruteJustice = 11340;
        public const uint SteamChakram = 11341;
        public const uint CruiseChaser = 11342;
        public const uint PlasmaShield = 11343;
        public const uint Plasmasphere = 11344;
        public const uint Shanoa = 11345;
        public const uint TrueHeart = 11346;
        public const uint AlexanderPrime = 11347;
        public const uint Alexander = 6358;
        public const uint JudgmentCrystal = 11348;
        public const uint PerfectAlexander = 11349;
        public const uint FateProjection = 11350;
        public const uint GelidGaol = 11393;
        public const uint Helper = 9020;
    }

    // Retail's spawn-packet parts for the Jagd Doll's demihuman model.
    public static class Appearance
    {
        public static readonly NpcAppearance JagdDoll = new(MainHand: 0x100020200, Equipment: [0x0, 0x10006]);
    }

    public static class BNpcNameId
    {
        public const uint LivingLiquid = 9211;
        public const uint LiquidHand = 9212;
        public const uint LiquidRage = 9213;
        public const uint JagdDoll = 9214;
        public const uint Embolus = 9215;
        public const uint BruteJustice = 9216;
        public const uint SteamChakram = 9217;
        public const uint CruiseChaser = 9218;
        public const uint GelidGaol = 9219;
        public const uint PlasmaShield = 8658;
        public const uint AlexanderPrime = 9220;
        public const uint Plasmasphere = 9221;
        public const uint Shanoa = 9222;
        public const uint TrueHeart = 9223;
        public const uint JudgmentCrystal = 9224;
        public const uint Alexander = 9225;
        // The countdown's speaker, an older row also named Alexander.
        public const uint AlexanderCountdown = 5174;
        public const uint PerfectAlexander = 9042;
        // Empty: retail names each projection 名無し.
        public const uint FateProjection = 108;
    }

    public static class EObjId
    {
        public const uint VoidzoneLiquidRage = 0x1E9998;
        // The content director every real voidzone is bound to.
        public const uint DirectorEventId = 0x80037586;
        // Each set's three voidzones take consecutive ids from these: a DirectorEObjMod finds its EObj
        // by entity id, so each needs its own.
        public const uint FirstVoidzonesEntityId = 0x4000EC00u;
        public const uint SecondVoidzonesEntityId = 0x4000EC03u;

        // P2
        public const uint VoidzoneEarthMissile = 0x1E958C;
        public const uint VoidzoneIceSmall = 0x1E958D;
        public const uint VoidzoneIceLarge = 0x1E958E;
        public const uint MissileVoidzonesEntityId = 0x4000EC10u;
        public const uint IceVoidzonesEntityId = 0x4000EC12u;

        // P3
        public const uint VoidzoneJudgmentCrystal = 0x1E9E3C;
        public const uint VoidzonePlasmasphere = 0x1EA1C9;
        // The wormholes' three sizes, radius 8, 6 and 3: each soak swaps in the next.
        public const uint Wormhole1 = 0x1EA1DF;
        public const uint Wormhole2 = 0x1EA1E0;
        public const uint Wormhole3 = 0x1EA1E1;
        public const uint CrystalVoidzonesEntityId = 0x4000EC20u;
        public const uint OrbVoidzonesEntityId = 0x4000EC24u;
        public const uint WormholesEntityId = 0x4000EC28u;

        // P4: eight arena pieces at the centre, the floor and each of Temporal Prison's jails.
        public const uint ArenaFeature = 0x1EA1A1;
    }

    // The arena pieces' layouts, in retail's spawn order. UNVERIFIED: the first seven are the jails
    // NE, E, SE, S, SW, W, NW, the last the whole arena's.
    public static class ArenaFeatureLayoutId
    {
        public const uint NorthEastJail = 0x7C86C6;
        public const uint EastJail = 0x7C86C8;
        public const uint SouthEastJail = 0x7C86C9;
        public const uint SouthJail = 0x7C86CA;
        public const uint SouthWestJail = 0x7C86CB;
        public const uint WestJail = 0x7C86CC;
        public const uint NorthWestJail = 0x7C86CD;
        public const uint WholeArena = 0x7C86CE;
    }

    public static class ActionId
    {
        // P1
        public const uint LivingLiquidAttack = 18808;
        public const uint LiquidHandAttack = 18809;
        public const uint JagdDollAttack = 19278;
        public const uint FluidSwing = 18864;
        public const uint FluidStrike = 18871;
        public const uint Cascade = 18470;
        public const uint HandOfPrayer = 18475;
        public const uint HandOfParting = 18476;
        public const uint HandOfPain = 18477;
        public const uint ProteanWaveTornadoVisible = 18869;
        public const uint ProteanWaveTornadoInvisible = 18870;
        public const uint ProteanWaveBossVisible = 18466;
        public const uint ProteanWaveBossInvisible = 18467;
        public const uint ProteanWaveHelperVisible = 18468;
        public const uint ProteanWaveHelperInvisible = 18469;
        public const uint Exhaust = 18462;
        // What a second Luminous Aetheroplasm sets off around its holder.
        public const uint ExhaustOverload = 18463;
        public const uint ReducibleComplexity = 18464;
        public const uint Pressurize = 18473;
        public const uint Outburst = 18474;
        public const uint Sluice = 18865;
        public const uint Splash = 18866;
        public const uint Drainage = 18471;
        public const uint LiquidGaol = 18472;

        // Limit Cut
        public const uint HawkBlaster = 18480;
        public const uint AlphaSword = 18484;
        public const uint SuperBlasstyCharge = 19279;
        public const uint JKick = 18516;

        // P2
        public const uint CruiseChaserAttack = 18810;
        public const uint BruteJusticeAttack = 18811;
        public const uint Whirlwind = 18882;
        public const uint JudgmentNisi = 18494;
        public const uint LinkUp = 18495;
        public const uint PunishingWave = 18498;
        public const uint PunishingThunder = 18499;
        public const uint EyeOfTheChakram = 18517;
        public const uint OpticalSight = 18479;
        public const uint HawkBlasterP2 = 18481;
        public const uint Photon = 18486;
        public const uint PhotonHit = 18487;
        public const uint SpinCrusher = 19058;
        public const uint CrashingWave = 18496;
        public const uint CrashingThunder = 18497;
        public const uint DrainageP2 = 18500;
        public const uint MissileCommand = 18509;
        public const uint EarthMissile = 18510;
        public const uint EarthMissileIce = 18511;
        public const uint Enumeration = 18512;
        public const uint HiddenMinefield = 18513;
        public const uint HiddenMine = 18514;
        public const uint HiddenMineShrapnel = 18515;
        public const uint Verdict = 18491;
        public const uint LimitCutP2 = 18483;
        public const uint Flarethrower = 18501;
        public const uint FlarethrowerCone = 18502;
        public const uint PropellerWind = 18482;
        public const uint Gavel = 18492;
        public const uint DoubleRocketPunch = 18503;
        public const uint SuperJump = 18505;
        public const uint SuperJumpLanding = 18506;
        public const uint ApocalypticRay = 18507;
        public const uint ApocalypticRayHit = 18508;
        // A nisi colliding with another, or failing Gavel.
        public const uint FinalSentence = 18493;
        public const uint FinalSentenceEnrage = 18518;
        public const uint EternalDarknessP2 = 18490;
        public const uint SevereContamination = 18891;

        // Temporal Stasis
        public const uint TemporalStasis = 18522;
        public const uint AlphaSwordP3 = 18539;
        public const uint FlarethrowerP3 = 18540;
        public const uint PlaintOfSeverity = 18529;
        public const uint PlaintOfSurety = 18531;

        // P3
        public const uint AlexanderPrimeAttack = 18812;
        public const uint ChasteningHeat = 19072;
        public const uint DivineSpear = 19074;
        public const uint InceptionFormation = 18543;
        public const uint InceptionVisual = 18545;
        public const uint JudgmentCrystal = 18523;
        public const uint JudgmentCrystalAoe = 18524;
        public const uint UndyingAffection = 19020;
        public const uint Aetheroplasm = 18546;
        public const uint ElectromagneticBurst = 18548;
        public const uint Tetrashatter = 19080;
        public const uint TetrashatterFail = 18525;
        public const uint TetrashatterVisual = 19023;
        public const uint Inception = 18526;
        public const uint TrueHeartSuccess = 19024;
        public const uint SacramentInception = 18527;
        public const uint PlaintOfSolidarity = 18530;
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
        public const uint MegaHoly = 19075;
        public const uint SummonAlexander = 19029;
        public const uint JStorm = 18550;
        public const uint JWave = 18551;
        public const uint EternalDarknessP3 = 18549;
        public const uint Burst = 18547;
        public const uint DivineJudgment = 18552;
        public const uint DivineJudgmentSuccess = 18553;
        public const uint DivineJudgmentEnrage = 19030;
        public const uint DivineJudgmentRaidwide = 18554;
        public const uint DivineJudgmentCountdown = 19034;

        // P4
        public const uint PerfectAlexanderAttack = 19083;
        public const uint TheFinalWord = 18557;
        public const uint OrdainedMotion = 18558;
        public const uint OrdainedStillness = 18559;
        public const uint OrdainedMotionSuccess = 19039;
        public const uint OrdainedStillnessSuccess = 19040;
        public const uint OrdainedMotionFail = 18560;
        public const uint OrdainedStillnessFail = 18561;
        public const uint DivineRetributionLight = 18562;
        public const uint DivineRetributionDark = 18563;
        public const uint OpticalSightSpread = 18570;
        public const uint OpticalSightStack = 18571;
        public const uint IndividualReprobation = 18572;
        public const uint CollectiveReprobation = 18573;
        public const uint Faithlessness = 19073;
        // Perfect Alexander's leap in place before each projection and before Temporal Interference.
        public const uint FateProjectionLeap = 18581;
        public const uint FateProjectionAlpha = 18555;
        public const uint FateCalibrationAlpha = 18556;
        // The safe clone's two casts, which the future's Ordained Motion/Stillness follow.
        public const uint AlphaFirstOrdainedTell = 18584;
        public const uint AlphaSecondOrdainedTell = 18858;
        public const uint AlphaMotionShown = 19213;
        public const uint AlphaStillnessShown = 19214;
        public const uint AlphaDefamationShown = 18596;
        public const uint AlphaAggravatedShown = 18597;
        // Also Fate Calibration β's.
        public const uint SharedSentenceShown = 18598;
        public const uint AlphaMotionKill = 18585;
        public const uint AlphaStillnessKill = 18586;
        public const uint AlphaSacramentShown = 18591;
        public const uint ResolveOrdainedMotion = 18859;
        public const uint ResolveOrdainedStillness = 18860;
        public const uint Sacrament = 18569;
        public const uint PlaintOfObloquy = 18528;
        public const uint OrdainedCapitalPunishment = 18578;
        public const uint OrdainedCapitalPunishmentHit = 18579;
        public const uint OrdainedPunishment = 18577;
        public const uint FateProjectionBeta = 19219;
        public const uint FateCalibrationBeta = 19220;
        // Right as they are, though often listed the other way round.
        public const uint BetaLightTetheredKill = 18587;
        public const uint BetaDarkTetheredKill = 18588;
        public const uint BetaJumpShown = 18589;
        public const uint BetaDonutShown = 18590;
        public const uint BetaSpreadShown = 18592;
        public const uint BetaStackShown = 18593;
        public const uint BetaSpreadKill = 18594;
        public const uint BetaStackKill = 18595;
        public const uint JJump = 18565;
        public const uint ResolveOpticalSightSpread = 18861;
        public const uint ResolveOpticalSightStack = 18862;
        public const uint RadiantSacrament = 18566;
        public const uint AlmightyJudgment = 18574;
        public const uint AlmightyJudgmentTile = 18575;
        public const uint AlmightyJudgmentBlast = 18576;
        public const uint IrresistibleGrace = 18580;
        public const uint TemporalInterference = 18582;
        public const uint TemporalPrison = 18583;
    }

    public static class StatusId
    {
        public const ushort WaterResistanceDownII = 1025;
        public const ushort LuminousAetheroplasm = 706;
        public const ushort Throttle = 700;
        public const ushort Dropsy = 289;
        public const ushort MagicVulnerabilityUp = 1138;
        public const ushort PhysicalVulnerabilityUp = 695;

        // P2
        public const ushort CompressedWater = 2142;
        public const ushort CompressedLightning = 2143;
        public const ushort WaterResistanceDownIIP2 = 2144;
        public const ushort LightningResistanceDownII = 2145;
        public const ushort FireResistanceDownII = 2098;
        public const ushort FinalJudgmentPenaltyIII = 1035;
        public const ushort Invincibility = 775;
        public const ushort DamageUp = 290;
        public const ushort Frostbite = 285;
        public const ushort Sludge = 287;

        // Temporal Stasis
        public const ushort TemporalDisplacement = 1119;
        public const ushort AggravatedAssault = 1121;
        public const ushort HouseArrest = 1123;
        public const ushort RestrainingOrder = 1124;

        // P3
        public const ushort SharedSentence = 1122;
        public const ushort Bleeding = 320;
        public const ushort Heavy = 1796;
        public const ushort EnigmaCodex = 2146;
        public const ushort EnigmaCodexHeart = 2147;
        public const ushort DamageDown = 1016;
        public const ushort VulnerabilityDown = 406;
        public const ushort VulnerabilityDownAlexanderPrime = 350;
        public const ushort VulnerabilityUp = 444;
        public const ushort DamageUpStacking = 443;
        public const ushort DownForTheCount = 783;

        // P4
        public const ushort FinalWordContactProhibition = 2152;
        public const ushort FinalWordContactRegulation = 2153;
        public const ushort FinalWordEscapeProhibition = 2154;
        public const ushort FinalWordEscapeDetection = 2155;
        public const ushort ContactProhibitionOrdained = 3055;
        public const ushort ContactRegulationOrdained = 3056;
        public const ushort EscapeProhibitionOrdained = 3057;
        public const ushort EscapeDetectionOrdained = 3058;
        // On each projection, its param saying light or dark.
        public const ushort FateCalibrationBeta = 2195;
        // Shows as Temporal Displacement: a jailed player can't move.
        public const ushort TemporalPrison = 2165;

        public static ushort FinalDecreeNisi(NisiLetter letter) => letter switch
        {
            NisiLetter.Alpha => 2222, NisiLetter.Beta => 2223, NisiLetter.Gamma => 2137, _ => 2138,
        };

        public static ushort FinalJudgment(NisiLetter letter) => letter switch
        {
            NisiLetter.Alpha => 2224, NisiLetter.Beta => 2225, NisiLetter.Gamma => 2139, _ => 2140,
        };
    }

    public static class StatusParam
    {
        public const ushort BetaLight = 0x84;
        public const ushort BetaDark = 0x85;
    }

    public static class TetherId
    {
        public const ushort DollFixate = 17;
        public const ushort DollFeed = 41;
        public const ushort Drainage = 3;
        public const ushort HouseArrest = 28;
        public const ushort RestrainingOrder = 29;
        public const ushort Plasmasphere = 12;
        // From each player to their projection.
        public const ushort FateProjection = 98;
    }

    // Limit Cut numbers 1..8.
    public static readonly uint[] LimitCutLockons = [79, 80, 81, 82, 83, 84, 85, 86];

    public static class LockonId
    {
        public const uint Enumeration = 65;
        public const uint EarthMissileIce = 67;
        public const uint CompressedWater = 68;
        public const uint CompressedLightning = 69;
        public const uint JudgmentCrystal = 96;
        public const uint IncineratingHeat = 93;
        public const uint OpticalSightStack = 62;
        public const uint OpticalSightSpread = 139;
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
        public const uint FinalJudgmentProtocol = 24002;
        public const uint GavelFalls = 24003;
        // UNVERIFIED: never seen; shown as the success line is.
        public const uint GavelFails = 24004;
        public const uint AlexanderPrimeArrives = 24005;
        public const uint TimeOfJudgment = 24006;
        public const uint ExecutingJudgment = 24007;
        public const uint FiveSeconds = 24008;
        public const uint FourSeconds = 24009;
        public const uint ThreeSeconds = 24010;
        public const uint TwoSeconds = 24011;
        public const uint OneSecond = 24012;
        public const uint FateProjectionAlpha = 24017;
    }

    public static class Weather
    {
        public const byte FairSkies = 2;
        // Also named Fair Skies; the weather from J Kick on.
        public const byte FairSkiesP2 = 105;
        public const byte BeyondTime = 71;
        // Also named Beyond Time; the frozen-time look.
        public const byte BeyondTimeStopped = 72;
        // Also named Fair Skies; the P4 sky.
        public const byte FairSkiesP4 = 108;
        // While the projections live out the future.
        public const byte FateProjection = 132;
    }

    public static class Bgm
    {
        public const ushort P1 = 706;
        public const ushort P2 = 723;
        // From Temporal Stasis' cast end, while time stands still.
        public const ushort TimeStop = 724;
        public const ushort P3 = 725;
        public const ushort P4Arrival = 713;
        public const ushort P4 = 726;
    }

    // ActorControl 0x52 as the march starts, retail's id for each kind; what it selects is UNVERIFIED.
    public static class MarchControl
    {
        public const uint Category = 0x52;
        public const uint Contact = 6378;
        public const uint Escape = 6379;
    }

    // MapEffect slots 1..7 as every restart sets them.
    public static readonly ushort[] ArenaStates = [1, 4, 1, 4, 4, 4, 4];

    public static class Geometry
    {
        // Crossing it kills; the wall a knockback stops at stands past it, about 23.5y out.
        public const float ArenaRadius = 20f;

        // Enemies close in to this far past touching hitboxes.
        public const float EnemyReach = 2f;

        // Hawk Blaster pairs land this far out.
        public const float BlasterRadius = 14f;

        // A Liquid Rage's water: Dropsy lands this far from its centre.
        public const float PuddleRadius = 7.5f;

        // P2's puddles, from real pulls' positions with and without their statuses.
        public const float MissilePuddleRadius = 5f;
        public const float SmallIceRadius = 6f;
        public const float LargeIceRadius = 9f;

        // BNpcBase scale 1 x skeleton radius 1, what the engine gives an Embolus once it arrives.
        public const float EmbolusRadius = 1f;

        // Scenario-local position at `radius` along compass heading `degrees` (0 = north, clockwise).
        public static Vector3 OnCompass(float degrees, float radius)
        {
            var heading = float.DegreesToRadians(degrees);
            return new Vector3(radius * MathF.Sin(heading), 0f, -radius * MathF.Cos(heading));
        }
    }
}
