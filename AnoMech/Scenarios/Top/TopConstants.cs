using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

public sealed record OmegaAttack(byte AttributeFlags, uint ActionId)
{
    public static readonly OmegaAttack Legs   = new(49, TopConstants.ActionId.SuperliminalSteel);
    public static readonly OmegaAttack Staff  = new(16, TopConstants.ActionId.OptimizedBlizzardIII);
    public static readonly OmegaAttack Sword  = new(16, TopConstants.ActionId.EfficientBladework);
    public static readonly OmegaAttack Shield = new(0,  TopConstants.ActionId.BeyondStrength);
}

public sealed record GlitchType(ushort StatusId, Predicate<SimTether> Condition)
{
    public static readonly GlitchType Mid = new(TopConstants.StatusId.MidGlitch,
                                                tether => tether.StretchGt(Geometry.MidGlitchMaxDistance) ||
                                                          tether.StretchLt(Geometry.MidGlitchMinDistance));

    public static readonly GlitchType Far = new(TopConstants.StatusId.FarGlitch,
                                                tether => tether.StretchLt(Geometry.FarGlitchMinDistance));
}


// All IDs and tunables for The Omega Protocol (Ultimate), in one flat class.
// One name per value. Values in decimal.
public static class TopConstants
{
    public const byte Level = 90;

    public static class BNpcBaseId
    {
        public const uint OmegaMDynamis = 15720;             // boss-rank Omega-M (4000A63C)
        public const uint OmegaFDynamis = 15722;             // boss-rank Omega-F
        public const uint OmegaM_3D69 = 15721;
        public const uint OmegaM = 15712;         // P2 boss-rank Omega
        public const uint Omega_3D61 = 15713;         // P2 variant
        public const uint OmegaM_3D62 = 15714;        // P2 Omega-M
        public const uint OmegaF_3D63 = 15715;        // P2 Omega-F
        public const uint OmegaHelper = 9020;         // 0x233C — generic invisible helper used by all phases
        public const uint BeetleHelper = 15724;       // 0x3D6C — beetle / sigma helper visual
        public const uint FinalHelper = 14669;        // 0x394D — wave cannon spinner / ultimate visual
        public const uint RightArmUnit = 15719;       // 0x3D67 — Hyper Pulse caster
        public const uint LeftArmUnit = 15718;        // 0x3D66
        public const uint RearPowerUnit = 15723;
        public const uint OpticalUnit = 15716;        // 0x3D64 — invisible marker for the eye
        public const uint RocketPunchYellow = 15709;  // 0x3D5D — RocketPunch1 (color 0)
        public const uint RocketPunchBlue = 15710;    // 0x3D5E — RocketPunch2 (color 1)
        public const uint AlphaShield = 1026757;
    }

    public static class BNpcNameId
    {
        public const uint OmegaMDynamis = 12257;
        public const uint OmegaFDynamis = 12258;
        public const uint OmegaM = 7633;         // P2 Omega-M
        public const uint OmegaF = 7634;         // P2 Omega-F
        public const uint OmegaM_1DD3 = 7635;          // P2 Omega
        public const uint OmegaBeetle = 7695;
        public const uint OmegaFinal = 7636;
        public const uint RightArmUnit = 7638;        // 0x1DD6 — log name for BNpc 0x3D67
        public const uint LeftArmUnit = 7637;
        public const uint OpticalUnit = 7640;
        public const uint RocketPunch = 7696;
        public const uint RearPowerUnit = 7639;
    }

    public static class EObjId
    {
        public const uint EventObj1EA1A1 = 2007457;
        public const uint TowerTimer = 2013244;
        public const uint TowerSolo = 2013245;
        public const uint TowerPair = 2013246;
    }

    public static class ActionId
    {
        // -- Hello World family --
        public const uint HelloNearWorld = 31625;
        public const uint HelloNearWorldJump = 31626;
        public const uint HelloDistantWorld = 33040;
        public const uint HelloDistantWorldJump = 33041;
        public const uint HelloWorldFail = 31627;                   // Helper->self, range-100 wipe fired on any Hello World fail

        // -- Sword/shield/leg/staff body forms --
        public const uint BeyondStrength = 31525;
        public const uint EfficientBladework = 31526;
        public const uint BeyondDefense = 31527;
        public const uint BeyondDefenseAOE = 31528;                 // OmegaMHelper->player, no cast, range 5 circle
        public const uint PilePitch = 31529;
        public const uint SuperliminalSteel = 31530;
        public const uint SuperliminalSteelOmenL = 31531;
        public const uint SuperliminalSteelOmenR = 31532;
        public const uint OptimizedBlizzardIII = 31533;
        public const uint Discharger = 31534;
        public const uint OptimizedFireIII = 31535;

        // -- Wave Cannon / Oversampled / Diffuse --
        public const uint WaveCannon = 31603;                  // Sigma canonical wave cannon
        public const uint WaveCannonAoe = 31604;
        public const uint WaveCannon_7B80 = 31616;                  // Omega canonical wave cannon
        public const uint WaveCannonKyrios = 31505;
        public const uint OversampledWaveCannonAoe = 31597;         // Helper->players, no cast, range 7 circle spread
        public const uint OversampledWaveCannonLeft = 31639;        // arm unit cone variant — left side
        public const uint OversampledWaveCannonRight = 31638;       // arm unit cone variant — right side
        public const uint OmegaDiffuseWaveCannonFront = 31643;          // FinalHelper->self, 8.0s cast, visual (first set of cones, front/back)
        public const uint OmegaDiffuseWaveCannonSides = 31644;          // FinalHelper->self, 8.0s cast, visual (first set of cones, left/right)
        public const uint OmegaDiffuseWaveCannonRepeatFront = 31607;    // FinalHelper->self, no cast, visual (second set of cones, front/back)
        public const uint OmegaDiffuseWaveCannonRepeatSides = 31608;    // FinalHelper->self, no cast, visual (second set of cones, left/right)
        public const uint OmegaDiffuseWaveCannonAOE = 31609;            // Helper->self, 1.0s cast, range 100 120-degree cone

        // -- Storage Violation --
        public const uint StorageViolationFail = 31492;            // Omega canonical / Sigma variant
        public const uint StorageViolation = 31493;            // Sigma canonical / Omega variant

        // -- Run :() versions --
        public const uint RunMiDeltaVersion = 31624;
        public const uint RunMiSigmaVersion = 32788;
        public const uint RunMiOmegaVersion = 32789;

        // -- Hyper Pulse --
        public const uint HyperPulseDeltaCharging = 31600;                       // arm unit 2.5s cast, range 100 width 8 rect, baited on closest
        public const uint HyperPulseDeltaShoot = 31601;                  // arm unit no-cast follow-up
        public const uint HyperPulseSigma = 31602;

        // -- Rear Lasers --
        public const uint RearLasersCharging = 31631;
        public const uint RearLasersShoot = 31632;

        // -- Sniper Cannon --
        public const uint SniperCannon = 31571;
        public const uint HighPoweredSniperCannon = 31572;

        // -- Solar Ray --
        public const uint SolarRay = 33196;
        public const uint SolarRay_7B01 = 31489;
        public const uint SolarRay_7B02 = 31490;
        public const uint SolarRay_7E6A = 32362;
        public const uint SolarRay_7E6B = 32363;
        public const uint SolarRay_81AD = 33197;

        // -- Critical bugs --
        public const uint CriticalOverflowBug = 31575;
        public const uint CriticalSynchronizationBug = 31574;

        // -- Misc --
        public const uint BallisticImpact = 31500;
        public const uint Blaster = 32374;
        public const uint Blaster_7B0A = 31498;
        public const uint BlasterEffect = 31641;
        public const uint BlasterAoe = 32373;
        public const uint FlameThrower = 32368;
        public const uint FlameThrower_7B0D = 31501;
        public const uint SubjectSimulationFDynamis = 32559;
        public const uint SubjectSimulationF = 31515;
        public const uint ProgramLoop = 31640;
        public const uint Teleport7b42 = 31554;                     // Omega-M teleport
        public const uint Teleport7b43 = 31555;
        public const uint Unknown7b14 = 31508;
        public const uint Unknown7b15 = 31509;
        public const uint Unknown7b16 = 31510;
        public const uint Unknown7b17 = 31511;
        public const uint Unknown7b1d = 31517;
        public const uint Unknown7b1e = 31518;
        public const uint Unknown7b1f = 31519;
        public const uint Unknown7b20 = 31520;
        public const uint Unknown7b85 = 31621;
        public const uint Unknown7bfe = 31742;
        public const uint Unknown7bff = 31743;
        public const uint Unknown7c01 = 31745;
        public const uint Unknown7c02 = 31746;
        public const uint Unknown7f30 = 32560;

        // -- Delta-specific --
        public const uint PeripheralSynthesis = 31628;              // BeetleHelper visual, no cast
        public const uint DeltaExplosion = 31482;                   // RocketPunch->location, 3s cast
        public const uint DeltaUnmitigatedExplosion = 31483;        // RocketPunch->location, 3s cast — raidwide wipe when overlap check fails
        public const uint OpticalLaser = 31521;                     // OpticalUnit line AOE through arena
        public const uint ArchivePeripheral = 32630;                // FinalHelper->self, no cast — spawns the rotating ring
        public const uint SwivelCannonR = 31636;                    // BeetleHelper->self, 10s cast, range 60 210° cone
        public const uint SwivelCannonL = 31637;
        public const uint HwTetherBreak = 31587;                    // Helper->self, no cast, range 100 circle — raidwide hit on tether break
        public const uint HwTetherFail = 32505;                     // Helper->self, no cast, range 100 circle — wipe when a tether expires unbroken

        // -- P2 Party Synergy --
        public const uint Firewall = 31552;
        public const uint Firewall_7B41 = 31553;
        public const uint PartySynergy = 31550;
        public const uint PartySynergy_7B3F = 31551;
        public const uint Spotlight = 31536;
        public const uint SubjectSimulationM = 31516;
        public const uint CondensedWaveCannonKyrios = 31503;
        public const uint DiffuseWaveCannonKyrios = 31504;
        public const uint GuidedMissileKyrios = 31502;
    }

    public static class StatusId
    {
        public const ushort QuickeningDynamis = 3444;               // TOP-wide stack buff
        public const ushort VulnerabilityUp = 3366;                 // generic damage-taken-up
        public const ushort MagicVulnerabilityUp = 2941;
        public const ushort MagicVulnerabilityUpMini = 3516;        // stackable, needs 2 stacks to be lethal
        public const ushort TwiceComeRuin = 2534;                   // second tick lethal
        public const ushort TriceComeRuin = 2530;
        public const ushort HelloNearWorld = 3442;
        public const ushort HelloDistantWorld = 3443;
        public const ushort HWPrepLocalTether = 3503;               // 'local code smell'
        public const ushort HWPrepRemoteTether = 3441;              // 'remote code smell'
        public const ushort HWLocalTether = 3529;                   // 'local regression'
        public const ushort HWRemoteTether = 3530;                  // 'remote regression'
        public const ushort PlayerMonitorRight = 3452;
        public const ushort PlayerMonitorLeft = 3453;
        public const ushort Looper = 3456;
        public const ushort MidGlitch = 3427;
        public const ushort FarGlitch = 3428;
        public const ushort OmegaF = 1675;
        public const ushort OmegaM = 1674;
        public const ushort OmegaM_D7E = 3454;                      // P2 Omega-M form status
        public const ushort Superfluid = 1676;
        public const ushort FirstInLine = 3004;
        public const ushort SecondInLine = 3005;
        public const ushort HPPenalty = 3401;
    }

    public static class TetherId
    {
        public const ushort HWPrepLocal = 200;
        public const ushort HWPrepRemote = 201;
        public const ushort HWLocal = 224;
        public const ushort HWRemote = 225;
        public const ushort AutoTarget = 17;
        public const ushort Glitch = 222;
        public const ushort PassableTether = 89;
    }

    public static class TimelineId
    {
        public const ushort Spawn = 7747;                           // warp/warp_end
        public const ushort WarpOut = 7737;                         // warp/warp_start
        public const ushort RocketPunchSpawn = 1340;
    }

    public static class LockonId
    {
        public const uint RotateCw = 156;                           // vfx/lockon/eff/m0515_turning_right01c.avfx
        public const uint RotateCcw = 157;                          // vfx/lockon/eff/m0515_turning_left01c.avfx
        public const uint X_157 = 343;
        public const uint Stack = 100;
        public const uint PlaystationX = 419;
        public const uint PlaystationSq = 418;
        public const uint PlaystationO = 416;
        public const uint PlaystationTr = 417;
        public const uint WaveCannon = 244;

        public static readonly IReadOnlyList<uint> Playstation = [PlaystationX, PlaystationSq, PlaystationO, PlaystationTr];
    }

    public static class KnockbackId
    {
        public const uint Discharger = 72;
    }

    public static class Geometry
    {
        public const float ArenaRadius = 20f;                       // TOP arena ring
        public const float SuperliminalSteelSafeHalfWidth = 4f;
        public const float OptimizedBlizzardArmHalfWidth = 4f;
        public const float OmegaFAttackHalfLength = 50f;
        public const float PunchBackDistance = 2f;
        public const float HyperPulseStep = MathF.PI / 9f;          // 20° in radians
        public const float HwTetherBreakDistance = 10f;             // remote (short) breaks above; local (long) breaks below
        public const float RocketPunchAoeRadius = 3f;
        public const float BeyondDefenseAoeRadius = 5f;
        public const float BeyondStrengthSafeRadius = 10f;          // donut inner safe radius (ffxiv_bossmod: range 10-40)
        public const float OversampledWaveCannonAoeRadius = 7f;
        public const float PilePitchAoeRadius = 6f;
        public const float HelloWorldInitialAoeRadius = 8f;
        public const float HelloWorldJumpAoeRadius = 4f;
        public const float SwivelCannonRange = 60f;
        public const float SwivelCannonHalfAngle = MathF.PI * 7f / 12f;
        public const float HyperPulseHalfWidth = 4f;
        public const float HyperPulseLength = 100f;
        public const float OpticalLaserHalfWidth = 5f;
        public const float OpticalLaserLength = 100f;
        public const float MidGlitchMinDistance = 21f;
        public const float MidGlitchMaxDistance = 26f;
        public const float FarGlitchMinDistance = 34f;
        public const float TowerRadius = 3f;
        
        public static readonly Placement SuperliminalSteelOmenPlacement =  new(new Vector3(0, 0.000f, 9.9f), MathF.PI);
        public static readonly Vector3 SuperliminalSteelOmenTargetR = new(21.21f, 0f, 49.50f);        
        public static readonly Vector3 SuperliminalSteelOmenTargetL = new(-21.21f, 0, 49.50f);
        public static readonly Placement[] ArmUnitPlacements =
        [
            new(new Vector3(-17.3205f, 0f, -10f), MathF.PI / 3f),      // NW
            new(new Vector3(-17.3205f, 0f,  10f), MathF.PI * 2f / 3f), // SW
            new(new Vector3(      0f, 0f, -20f), 0f),                  // N
            new(new Vector3(      0f, 0f,  20f), MathF.PI),            // S
            new(new Vector3( 17.3205f, 0f, -10f), MathF.PI * 5f / 3f), // NE
            new(new Vector3( 17.3205f, 0f,  10f), MathF.PI * 4f / 3f), // SE
        ];
    }

    public static class BgmId
    {
        public const ushort TopP5 = 964; 
        public const ushort TopP2 = 587;
    }

    public static class Duration
    {
        public const float OmegaAttackOmenDelay = 0.6f;
        public const float HelloWorldDebuff = 44f;
        public const float MonitorHelperLifetime = 5f;
        public const float HwTetherBreakStack = 0.96f;              // Trice Come Ruin / Magic Vuln Up applied per HW break hit
    }
}
