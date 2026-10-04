using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

// Travels in TopP5OmegaAiReplayStateMessage, so it stays a plain value: Action is derived, not stored.
public sealed record OmegaAttack(byte AttributeFlags, uint ActionId)
{
    public static readonly OmegaAttack Legs   = new(49, TopConstants.ActionId.SuperliminalSteel);
    public static readonly OmegaAttack Staff  = new(16, TopConstants.ActionId.OptimizedBlizzardIII);
    public static readonly OmegaAttack Sword  = new(16, TopConstants.ActionId.EfficientBladework);
    public static readonly OmegaAttack Shield = new(0,  TopConstants.ActionId.BeyondStrength);

    [JsonIgnore]
    public EnemyAction Action => ActionId switch
    {
        TopConstants.ActionId.SuperliminalSteel => TopActions.SuperliminalSteel,
        TopConstants.ActionId.OptimizedBlizzardIII => TopActions.OptimizedBlizzardIII,
        TopConstants.ActionId.EfficientBladework => TopActions.EfficientBladework,
        TopConstants.ActionId.BeyondStrength => TopActions.BeyondStrength,
        _ => throw new InvalidOperationException($"No EnemyAction for omega attack {ActionId}"),
    };
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
        public const uint OmegaF = 15713;         // P2 variant
        public const uint OmegaMClone = 15714;        // P2 Omega-M
        public const uint OmegaFClone = 15715;        // P2 Omega-F
        public const uint OmegaHelper = 9020;         // 0x233C — generic invisible helper used by all phases
        public const uint BeetleHelper = 15724;       // 0x3D6C — beetle / sigma helper visual
        public const uint FinalHelper = 14669;        // 0x394D — wave cannon spinner / ultimate visual
        public const uint RightArmUnit = 15719;       // 0x3D67 — Hyper Pulse caster
        public const uint LeftArmUnit = 15718;        // 0x3D66
        public const uint RearPowerUnit = 15723;
        public const uint OpticalUnit = 15716;        // 0x3D64 — invisible marker for the eye
        public const uint RocketPunchYellow = 15709;  // 0x3D5D — RocketPunch1 (color 0)
        public const uint RocketPunchBlue = 15710;    // 0x3D5E — RocketPunch2 (color 1)
        public const uint AlphaOmega = 15725;         // 0x3D6D — P6 Alpha Omega boss
        public const uint OmegaBeetle = 15708;        // 0x3D5C — P1 boss
        public const uint OmegaFinal = 15717;         // 0x3D65 — P3 boss
        public const uint CosmoMeteor = 15726;        // 0x3D6E — P6 big meteor
        public const uint CosmoComet = 15727;         // 0x3D6F — P6 small comet
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
        public const uint RocketPunchYellow = 7696;
        public const uint RocketPunchBlue = 7697;
        public const uint RearPowerUnit = 7639;
        public const uint OmegaIntermission = 7666;     // speaks the merge's lines
        public const uint AlphaOmega = 12256;
        public const uint CosmoMeteor = 12259;
        public const uint CosmoComet = 12260;
    }

    // InstanceContentTextData rows the bosses say through MapController.BattleTalk; each lands
    // 0.09s before the cast it announces, except the pull's opening line.
    public static class BattleTalkId
    {
        public const uint InitiatingDirectAnalysis = 35800;
        public const uint PartyMemberGeneration = 35803;
        public const uint ImitationOfMortalForm = 35805;
        public const uint ReconfigurationSequence = 35806;
        public const uint BlipBloopBleep = 35807;
        public const uint ExperimentConcluded = 35808;
        public const uint HelloWorld = 35809;
        public const uint CriticalDamageDetected = 35810;
        public const uint InitiatingSystemReboot = 35811;
        public const uint DefeatUnacceptable = 35812;
        public const uint StructuralLimitationsExceeded = 35813;
        public const uint StructuralFailureImminent = 35814;
        public const uint MustEvolve = 35815;
        public const uint UnknownAugmentation = 35817;
        public const uint AmplificationInconsistent = 35818;
        public const uint BeginHypothesis = 35819;
        public const uint EvaluationFailed = 35820;
        public const uint IAmTheOmega = 35821;
        public const uint PathThroughTheExpanse = 35828;
        public const uint TenMillionSuns = 35829;
        public const uint VelocityOfAWyrm = 35831;
        public const uint FlamesOfTheDragonstar = 35830;
        public const uint TestNearsItsConclusion = 35832;
        public const uint ImpactInvitingExtinction = 35833;
        public const uint MostFeebleOfSpecies = 35834;
        public const uint IncomprehensibleStrength = 35835;
        public const uint UnmeasurableMight = 35836;
        public const uint ClaimingThisVictory = 35837;
    }

    public static class EObjId
    {
        public const uint EventObj1EA1A1 = 2007457;
        public const uint TowerTimer = 2013244;
        public const uint TowerSolo = 2013245;
        public const uint TowerPair = 2013246;
        public const uint IntermissionVoidzone = 2013217;           // 0x1EB821
        // The instance director's event, which Program Loop's towers and the intermission void
        // zone are bound to.
        public const uint DirectorEventId = 0x800375ACU;
        // Program Loop's eight tower spots by cardinal (N, E, S, W): the clockwise-shifted spot,
        // then the counter-clockwise one. A tower and its timer share their spot's id.
        public static readonly uint[][] LoopTowerLayoutIds =
        [
            [0x91D93CU, 0x91D93BU],
            [0x91D93EU, 0x91D93DU],
            [0x91D941U, 0x91D940U],
            [0x91D943U, 0x91D942U],
        ];
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
        public const uint WaveCannonKyrios = 31505;
        public const uint OversampledWaveCannonAoe = 31597;         // Helper->players, no cast, range 7 circle spread
        public const uint OversampledWaveCannonRight = 31595;       // P3 boss monitor, cleaves its right side
        public const uint OversampledWaveCannonLeft = 31596;        // P3 boss monitor, cleaves its left side
        public const uint DeltaOversampledWaveCannonLeft = 31639;        // arm unit cone variant — left side
        public const uint DeltaOversampledWaveCannonRight = 31638;       // arm unit cone variant — right side
        public const uint OmegaDiffuseWaveCannonFront = 31643;          // FinalHelper->self, 8.0s cast, visual (first set of cones, front/back)
        public const uint OmegaDiffuseWaveCannonSides = 31644;          // FinalHelper->self, 8.0s cast, visual (first set of cones, left/right)
        public const uint OmegaDiffuseWaveCannonRepeatFront = 31607;    // FinalHelper->self, no cast, visual (second set of cones, front/back)
        public const uint OmegaDiffuseWaveCannonRepeatSides = 31608;    // FinalHelper->self, no cast, visual (second set of cones, left/right)
        public const uint OmegaDiffuseWaveCannonAOE = 31609;            // Helper->self, 1.0s cast, range 100 120-degree cone

        // -- Storage Violation --
        public const uint StorageViolationSolo = 31492;
        public const uint StorageViolationPair = 31493;
        // Unfilled-tower raidwide. UNVERIFIED: picked from the Action sheet, never seen cast.
        public const uint StorageViolationObliteration = 31494;

        // -- Run :() versions --
        public const uint RunMiDeltaVersion = 31624;
        public const uint RunMiSigmaVersion = 32788;
        public const uint RunMiOmegaVersion = 32789;

        // -- Blind Faith, P5's end --
        public const uint BlindFaith = 31623;
        public const uint BlindFaithSuccess = 32626;                // Helper->self, the raidwide knockback that ends P5

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

        // -- P1 Program Loop --
        public const uint ProgramLoop = 31491;
        public const uint Blaster = 31495;
        public const uint BlasterRepeat = 31496;
        public const uint BlasterLast = 31497;
        public const uint BlasterAoe = 31498;                        // Helper->players, range 15 circle around a tether holder

        // -- P3 Hello World / Monitors --
        public const uint HelloWorld = 31573;
        public const uint LatentDefect = 31599;
        public const uint CriticalSynchronizationBug = 31574;       // 2-man stack
        public const uint CriticalOverflowBug = 31575;              // defamation
        public const uint LatentSynchronizationDefect = 31576;      // stack debuff expired without a partner
        public const uint LatentDefectExpired = 31577;              // defamation debuff expired
        public const uint CriticalUnderflowBug = 31578;             // red rot expiry
        public const uint CriticalPerformanceBug = 31579;           // blue rot expiry
        public const uint CascadingLatentDefectExpired = 31581;     // red tower debuff ran out
        public const uint LatentPerformanceDefectExpired = 31582;   // blue tower debuff ran out
        public const uint CascadingLatentDefect = 31583;            // red tower
        public const uint LatentPerformanceDefect = 31584;          // blue tower
        public const uint CascadingLatentDefectUnsoaked = 31585;
        public const uint LatentPerformanceDefectUnsoaked = 31586;
        public const uint CriticalError = 31588;
        public const uint TeleportP3 = 31558;
        public const uint P3End = 31559;                            // 0x7B47, the boss's own exit as P3 ends
        public const uint IonEfflux = 31560;
        public const uint AutoAttackP1 = 31741;                     // 0x7BFD
        public const uint AutoAttackP3 = 31744;                     // 0x7C00

        // -- P4 Blue Screen --
        public const uint P4Begin = 31610;                          // 0x7B7A
        public const uint BlueScreen = 31611;                       // 0x7B7B
        public const uint BlueScreenAoe = 31612;                    // 0x7B7C
        public const uint P4WaveCannonProtean = 31614;              // 0x7B7E
        public const uint P4WaveCannonStack = 31615;                // 0x7B7F, line stack from the centre
        public const uint P4WaveCannonProteanAoe = 31616;           // 0x7B80, the protean's lingering line
        public const uint P4WaveCannonVisualStart = 31617;          // 0x7B81
        public const uint P4WaveCannonVisual1 = 31618;              // 0x7B82
        public const uint P4WaveCannonVisual3 = 31619;              // 0x7B83
        public const uint P4WaveCannonVisual4 = 31620;              // 0x7B84
        public const uint P4WaveCannonVisual2 = 32534;              // 0x7F16
        public const uint P4WaveCannonStackTarget = 22393;          // 0x5779, the line stack's marker
        public const uint WaveRepeater1 = 31567;                    // 0x7B4F, range 6 circle
        public const uint WaveRepeater2 = 31568;                    // 0x7B50, 6-12 donut
        public const uint WaveRepeater3 = 31569;                    // 0x7B51, 12-18 donut
        public const uint WaveRepeater4 = 31570;                    // 0x7B52, 18-24 donut

        // -- P3 intermission --
        public const uint LaserShower = 31557;                      // 0x7B45, P2's enrage
        public const uint DieF = 31507;                             // 0x7B13, the P2 Omega's kill
        public const uint IntermissionTeleportM = 31561;            // 0x7B49
        public const uint IntermissionTeleportF = 31562;            // 0x7B4A
        public const uint IntermissionMergeStart = 31563;           // 0x7B4B
        public const uint IntermissionMergeEnd = 31564;             // 0x7B4C, Final Omega appears
        public const uint ColossalBlow = 31566;                     // 0x7B4E, range 11 circle

        // -- Misc --
        public const uint BallisticImpact = 31500;
        public const uint OmegaBlaster = 32374;
        public const uint BlasterEffect = 31641;
        public const uint OmegaBlasterAoe = 32373;
        public const uint SubjectSimulationFDynamis = 32559;
        public const uint SubjectSimulationF = 31515;
        public const uint SigmaProgramLoop = 31640;
        public const uint Teleport7b42 = 31554;                     // Omega-M teleport
        public const uint Teleport7b43 = 31555;
        public const uint SuperfluidAnimationM = 31508;
        public const uint SuperfluidAnimationF = 31509;
        public const uint SubjectSimulationFWarpDown = 31510;
        public const uint Unknown7b17 = 31511;
        public const uint Unknown7b1d = 31517;
        public const uint SubjectSimulationFWarpUp = 31518;
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
        public const uint PartySynergyM = 31550;
        public const uint PartySynergyF = 31551;
        public const uint Spotlight = 31536;
        public const uint SubjectSimulationM = 31516;
        public const uint CondensedWaveCannonKyrios = 31503;
        public const uint DiffuseWaveCannonKyrios = 31504;
        public const uint GuidedMissileKyrios = 31502;

        // -- P6 Wave Cannon 2 / Cosmo (Alpha Omega) --
        public const uint CosmoMemory = 31649;                      // 0x7BA1
        public const uint CosmoArrow = 31650;                       // 0x7BA2
        public const uint CosmoArrowOmen = 31651;                   // 0x7BA3
        public const uint CosmoArrowDamage = 31652;                 // 0x7BA4
        public const uint CosmoDive = 31654;                        // 0x7BA6
        public const uint CosmoDive_7BA7 = 31655;                   // 0x7BA7
        public const uint CosmoDive_7BA8 = 31656;                   // 0x7BA8
        public const uint WaveCannon_7BA9 = 31657;                  // 0x7BA9 — P6 Alpha Omega wave cannon
        public const uint WaveCannonWildCharge = 31658;            // 0x7BAA
        public const uint WaveCannonProtean = 31659;               // 0x7BAB
        public const uint UnlimitedWaveCannon = 31660;             // 0x7BAC
        public const uint WaveCannon_7BAD = 31661;                  // 0x7BAD
        public const uint WaveCannon_7BAE = 31662;                  // 0x7BAE
        public const uint WaveCannon_7BAF = 31663;                  // 0x7BAF
        public const uint AlphaOmegaAutoAttack = 31747;            // 0x7C03
        public const uint Unknown7ddf = 32223;                      // 0x7DDF
        public const uint Inhale = 32337;                           // 0x7E51
        public const uint RunMi = 31648;                            // 0x7BA0 — P6 enrage
        public const uint FlashGale = 32223;                        // 0x7DDF — P6 auto on the main target and the farthest player
        public const uint CosmoMeteor = 31664;                      // 0x7BB0
        public const uint CosmoMeteorEnd = 31665;                   // 0x7BB1
        public const uint CosmoMeteorPuddle = 31666;                // 0x7BB2
        public const uint CosmoMeteorStack = 31667;                 // 0x7BB3
        public const uint CosmoMeteorFlare = 31668;                 // 0x7BB4
        public const uint CosmoMeteorEnrage = 31669;                // 0x7BB5, cast by any comet or meteor left standing
        public const uint CosmoMeteorSpread = 32699;                // 0x7FBB
        public const uint MagicNumber = 31670;                      // 0x7BB6
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
        public const ushort DeltaPrepLocalTether = 3440;            // 'local code smell'
        public const ushort DeltaPrepRemoteTether = 3504;           // 'remote code smell'
        public const ushort DeltaLocalTether = 1672;                // 'local regression'
        public const ushort DeltaRemoteTether = 1673;               // 'remote regression'
        public const ushort PlayerMonitorRight = 3452;
        public const ushort PlayerMonitorLeft = 3453;
        public const ushort Looper = 3456;
        public const ushort MidGlitch = 3427;
        public const ushort FarGlitch = 3428;
        public const ushort OmegaF = 1675;
        public const ushort OmegaM = 1674;
        public const ushort OmegaM_D7E = 3454;                      // P2 Omega-M form status
        public const ushort Superfluid = 1676;
        public const ushort PacketFilterM = 3499;
        public const ushort PacketFilterF = 3500;
        public const ushort FirstInLine = 3004;
        public const ushort SecondInLine = 3005;
        public const ushort ThirdInLine = 3006;
        public const ushort FourthInLine = 3451;
        public const ushort HPPenalty = 3401;
        public const ushort CodeMi = 3447;                          // 0xD77 — P6 Alpha Omega 'Code M/i' form
        public const ushort BrilliantDynamis = 3446;
        public const ushort SparkOfDynamis = 3448;
        public const ushort MagicNumber = 3532;
        // Unnamed; Alpha Omega holds it (param 229) through Cosmo Meteor.
        public const ushort AlphaOmegaCosmoMeteor = 2056;
        public const ushort Doom = 2519;
        public const ushort DamageDown = 2911;
        public const ushort MemoryLoss = 1626;
        public const ushort InfiniteLimit = 3450;
        public const ushort SniperCannonFodder = 3425;
        public const ushort HighPoweredSniperCannonFodder = 3426;
        public const ushort DownForTheCount = 2408;

        // P3 Hello World
        public const ushort HWPrepStack = 3436;                     // 'synchronization code smell'
        public const ushort HWPrepDefamation = 3437;                // 'overflow code smell'
        public const ushort HWPrepRedRot = 3438;                    // 'underflow code smell'
        public const ushort HWPrepBlueRot = 3439;                   // 'performance code smell'
        public const ushort HWStack = 3524;                         // 'critical synchronization bug'
        public const ushort HWDefamation = 3525;                    // 'critical overflow bug'
        public const ushort HWRedRot = 3526;                        // 'critical underflow bug'
        public const ushort HWBlueRot = 3429;                       // 'critical performance bug'
        public const ushort HWNeedDefamation = 3527;                // 'latent defect'
        public const ushort HWNeedStack = 3434;                     // 'latent synchronization bug'
        public const ushort HWRedTower = 3528;                      // 'cascading latent defect'
        public const ushort HWBlueTower = 3435;                     // 'latent performance defect'
        public const ushort HWImmuneStack = 3430;                   // 'synchronization debugger'
        public const ushort HWImmuneDefamation = 3431;              // 'overflow debugger'
        public const ushort HWImmuneRedRot = 3432;                  // 'underflow debugger'
        public const ushort HWImmuneBlueRot = 3433;                 // 'performance debugger'
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
        public const ushort Spawn2 = 7748;                          // warp/warp_end2, a left arm unit's
        public const ushort WarpOut = 7737;                         // warp/warp_start
        public const ushort WarpOut2 = 7738;                        // warp/warp_start2, a left arm unit's
        public const ushort RocketPunchSpawn = 1340;
    }

    public static class LockonId
    {
        public const uint RotateCw = 156;                           // vfx/lockon/eff/m0515_turning_right01c.avfx
        public const uint RotateCcw = 157;                          // vfx/lockon/eff/m0515_turning_left01c.avfx
        public const uint SolarRay = 343;
        public const uint Stack = 100;
        public const uint PlaystationX = 419;
        public const uint PlaystationSq = 418;
        public const uint PlaystationO = 416;
        public const uint PlaystationTr = 417;
        public const uint WaveCannon = 244;
        public const uint OptimizedMeteor = 346;                    // P6 Cosmo Meteor flare

        public static readonly IReadOnlyList<uint> Playstation = [PlaystationX, PlaystationSq, PlaystationO, PlaystationTr];
    }

    public static class KnockbackId
    {
        public const uint Discharger = 72;
        public const uint BlindFaith = 79;
    }

    public static class VfxPath
    {
        // A rocket punch's spawn burst (P5 Delta).
        public const string RocketPunchSpawn = "vfx/monster/m0114/eff/m0114cbbm_sp_pop_c0i.avfx";
    }

    public static class Geometry
    {
        public const float ArenaRadius = 20f;                       // TOP arena ring
        public const float PunchBackDistance = 2f;
        public const float HyperPulseStep = MathF.PI / 9f;          // 20° in radians
        public const float HwTetherBreakDistance = 9f;              // remote (short) breaks above; local (long) breaks below
        public const float HwRotPassRadius = 1.5f;                  // UNVERIFIED: how close a rot has to be to pass
        public const float RocketPunchAoeRadius = 3f;
        public const float BeyondDefenseAoeRadius = 5f;
        // Omega-M closes to this far from a Beyond Defense target that stands farther out.
        public const float BeyondDefenseReach = 5.5f;
        public const float BeyondStrengthSafeRadius = 10f;          // donut inner safe radius (ffxiv_bossmod: range 10-40)
        public const float OversampledWaveCannonAoeRadius = 7f;
        public const float PilePitchAoeRadius = 6f;
        public const float HelloWorldInitialAoeRadius = 8f;
        public const float HelloWorldJumpAoeRadius = 4f;
        public const float SwivelCannonRange = 60f;
        public const float SwivelCannonHalfAngle = MathF.PI * 7f / 12f;
        public const float HyperPulseHalfWidth = 4f;
        public const float HyperPulseLength = 100f;
        // 20, not 21: BossMod's P2PartySynergy DistanceRange is (20, 26) for Mid glitch. The
        // AI's Mid-glitch stacks sit 90 degrees apart at radius 15 = 21.21y, which is inside the
        // real window with 1.2y to spare but was only 0.21y above the old 21 -- AiManager's 0.3y
        // placement jitter then pushed pairs under it at random, the tether's conditional
        // Vulnerability Up came on, and Spotlight (magic) killed them.
        public const float MidGlitchMinDistance = 20f;
        public const float MidGlitchMaxDistance = 26f;
        public const float FarGlitchMinDistance = 34f;
        public const float TowerRadius = 3f;
        
        public static readonly Placement SuperliminalSteelOmenPlacement =  new(new Vector3(0, 0.000f, 9.9f), MathF.PI);
        public static readonly Vector3 SuperliminalSteelOmenTargetR = new(21.21f, 0f, 49.50f);        
        public static readonly Vector3 SuperliminalSteelOmenTargetL = new(-21.21f, 0, 49.50f);
        // The legs side rects (80 x 36) are cast from the F's own spot and start 40 behind it,
        // 22 to either side, which leaves the 8-wide corridor along its axis.
        public static readonly Placement LegsSideHelperPlacement = new(new Vector3(0f, 0f, -10f), 0f);
        public static readonly Vector3 LegsSideTargetL = new(22f, 0f, -50f);
        public static readonly Vector3 LegsSideTargetR = new(-22f, 0f, -50f);
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

    // Each job's LB3, how a DPS one plays (it lands before its bar fills), and the party gauge:
    // a bar is 10000 units, and it fills 220 every 3s from 2.98s after a limit break lands.
    public static class LimitBreak
    {
        public static readonly IReadOnlyDictionary<byte, uint> ByJob = new Dictionary<byte, uint>
        {
            [19] = 199, [21] = 4240, [32] = 4241, [37] = 17105,
            [24] = 208, [28] = 4247, [33] = 4248, [40] = 24859,
            [25] = 205, [27] = 4246, [35] = 7862, [42] = 34867,
            [23] = 4244, [31] = 4245, [38] = 17106,
            [20] = 202, [22] = 4242, [30] = 4243, [34] = 7861, [39] = 24858, [41] = 34866,
        };
        // The same ids flat, for the multiplayer allowlist (see SimAssets).
        public static readonly uint[] ActionIds = ByJob.Values.ToArray();
        private static readonly HashSet<uint> TankIds = [199, 4240, 4241, 17105];
        private static readonly HashSet<uint> CasterAndHealerIds = [208, 4247, 4248, 24859, 205, 4246, 7862, 34867];
        public static float AnimationLockOf(uint actionId) =>
            TankIds.Contains(actionId) ? 3.86f : CasterAndHealerIds.Contains(actionId) ? 8.1f : 3.7f;
        public const float DpsBar = 4.5f;
        public const float DpsLands = 3.97f;
        public const float GaugeBar = 10_000f;
        public const float GaugeFull = 3 * GaugeBar;
        public const float GaugeTick = 220f;
        public const float GaugeTickSeconds = 3f;
        public const float GaugeFirstTick = 2.98f;

        // What a press short of a full gauge fires instead, by role: tank, healer, melee, physical
        // ranged, caster. It spends only its own bars.
        public static readonly uint[] LimitBreakOneActionIds = [197, 206, 200, 4238, 203];
        public static readonly uint[] LimitBreakTwoActionIds = [198, 207, 201, 4239, 204];

        public static int LevelOf(uint actionId)
            => LimitBreakOneActionIds.Contains(actionId) ? 1 : LimitBreakTwoActionIds.Contains(actionId) ? 2 : 3;
    }

    public static class BgmId
    {
        public const ushort TopP1 = 962;
        public const ushort TopP2 = 963;
        public const ushort TopP3 = 950;
        public const ushort TopP5 = 964; 
        public const ushort TopP6 = 951;
    }

    public static class Duration
    {
        public const float MonitorHelperLifetime = 5f;
        public const float OmegaAttackOmenDelay = 0.5f;
        public const float HelloWorldDebuff = 44f;
        public const float HwTetherBreakStack = 0.96f;              // Trice Come Ruin / Magic Vuln Up applied per HW break hit
        public const float Doom = 2.96f;
    }
}
