using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Uwu;

public class UwuConstants
{
    public const byte Level = 70;
    public const ushort ItemLevel = 375;

    public static IReadOnlyList<Waymark> NaurWaymarks =>
    [
        new(WaymarkSlot.A, new Vector3(0, 0, -6.7f)),
        new(WaymarkSlot.B, new Vector3(6.7f, 0, 0)),
        new(WaymarkSlot.C, new Vector3(0, 0, 6.7f)),
        new(WaymarkSlot.D, new Vector3(-6.7f, 0, 0)),
        new(WaymarkSlot.One, new Vector3(7.3f, 0, 7.3f)),
        new(WaymarkSlot.Two, new Vector3(6, 0, -19)),
        new(WaymarkSlot.Three, new Vector3(0, 0, 0)),
        new(WaymarkSlot.Four, new Vector3(-13, 0, -13)),
    ];

    public static IReadOnlyList<Waymark> StandardWaymarks =>
    [
        new(WaymarkSlot.A, new Vector3(0, 0, -6.7f)),
        new(WaymarkSlot.B, new Vector3(6.699f, 0, 0)),
        new(WaymarkSlot.C, new Vector3(0, 0, 6.699f)),
        new(WaymarkSlot.D, new Vector3(-6.7f, 0, 0)),
        new(WaymarkSlot.One, new Vector3(7.3f, 0, 7.3f)),
        new(WaymarkSlot.Two, new Vector3(0, 0, -19)),
        new(WaymarkSlot.Three, new Vector3(0, 0, 0)),
        new(WaymarkSlot.Four, new Vector3(-13, 0, -13)),
    ];

    public class BNpcBaseId
    {
        public const uint Garuda = 8722;
        public const uint SuparnaChirada = 8723;
        public const uint RazorPlume = 8724;
        public const uint SatinPlume = 8725;
        public const uint SpinyPlume = 8726;
        public const uint Titan = 8727;
        public const uint BombBoulder = 8728;
        public const uint GraniteGaol = 8729;
        public const uint Ifrit = 8730;
        public const uint InfernalNail = 8731;
        public const uint UltimaWeapon = 8734;
        public const uint Aetheroplasm = 8735;
        public const uint Dummy = 9020;
    }

    public class BNpcNameId
    {
        public const uint Dummy = 108;
        public const uint BombBoulder = 1803;
        public const uint Ifrit = 1185;
        public const uint InfernalNail = 1186;
        public const uint Garuda = 1644;
        public const uint Suparna = 1645;
        public const uint Chirada = 1646;
        public const uint RazorPlume = 1647;
        public const uint SatinPlume = 1648;
        public const uint SpinyPlume = 2091;
        public const uint Titan = 1801;
        public const uint GraniteGaol = 1804;
        public const uint UltimaWeapon = 2137;
        public const uint Aetheroplasm = 2138;
    }

    public class ActionId
    {
        public const uint MistralSongBoss = 11074;
        public const uint Featherlance = 11075;
        public const uint Cyclone = 11077;
        public const uint Gigastorm = 11078;
        public const uint SuperCyclone = 11079;
        public const uint Friction = 11080;
        public const uint GreatWhirlwind = 11073;
        public const uint Mesohigh = 11081;
        public const uint MistralSongSuparnaChirada = 11083;
        public const uint WickedWheel = 11084;
        public const uint FeatherRain = 11085;
        public const uint WickedWheelAwaken = 11086;
        public const uint WickedTornado = 11087;
        public const uint Downburst = 11088;
        public const uint EyeOfTheStorm = 11090;
        public const uint Slipstream = 11091;
        public const uint MistralShriek = 11092;
        public const uint AerialBlast = 11093;
        public const uint SuperCycloneAwaken = 11189;
        public const uint SuperCycloneOverload = 11190;
        public const uint Incinerate = 11094;
        public const uint VulcanBurst = 11095;
        public const uint InfernalSurge = 11096;
        public const uint EruptionIfrit = 11097;
        public const uint EruptionPuddle = 11098;
        public const uint InfernoHowl = 11099;
        public const uint SearingWind = 11100;
        public const uint FlamingCrush = 11101;
        public const uint Hellfire = 11102;
        public const uint CrimsonCyclone = 11103;
        public const uint CrimsonCycloneAwaken = 11104;
        public const uint RadiantPlumePuddle = 11105;
        public const uint RockBuster = 11106;
        public const uint MountainBuster = 11107;
        public const uint WeightOfTheLandTitan = 11108;
        public const uint WeightOfTheLand = 11109;
        public const uint GeocrushJump = 11110;
        public const uint Upheaval = 11111;
        public const uint BoulderTitan = 11112;
        public const uint Bury = 11113;
        public const uint Burst = 11114;
        public const uint RockThrow = 11115;
        public const uint RockThrowHelper = 11116;
        public const uint GraniteImpactGaols = 11117;
        public const uint Freefire = 11118;
        public const uint LandslideTitanNormal = 11119;
        public const uint LandslideLine = 11120;
        public const uint LandslideTitan = 11121;
        public const uint UltimatePredation = 11126;
        public const uint ViscousAetheroplasmUltima = 11129;
        public const uint ViscousAetheroplasmEffect = 11130;
        public const uint HomingLasers = 11131;
        public const uint CeruleumVent = 11132;
        public const uint RadiantPlumeUltima = 11133;
        public const uint LandslideUltima = 11134;
        public const uint LandslideLineUltima = 11135;
        public const uint Aetheroplasm = 11137;
        public const uint LightPillarUltima = 11138;
        public const uint LightPillarCircle = 11139;
        public const uint AetherochemicalLaserCenter = 11140;
        public const uint AetherochemicalLaserRight = 11141;
        public const uint AetherochemicalLaserLeft = 11142;
        public const uint TankPurge = 11143;
        public const uint EarthenFury = 11152;
        public const uint MistralSong = 11150;
        public const uint Tumult = 11288;
        public const uint InfernalFetters = 11289;
        public const uint LandslideAwaken = 11298;
        public const uint GraniteImpact = 11448;
        public const uint PostUltimatePredation1 = 11475;
        public const uint PostUltimatePredation2 = 11476;
        public const uint PostUltimatePredation3 = 11477;
        public const uint UltimateAnnihilation = 11596;
        public const uint UltimateSuppression = 11597;
        public const uint GeocrushLanding = 11517;
    }

    public class ActionTimelineId
    {
        public const ushort RazorPlume = 1412;
        public const ushort WarpStart = 7737;
        public const ushort WarpStart2 = 7738;
        public const ushort WarpEnd = 7747;
        public const ushort SistersArrive = 7748;
    }

    public class StatusId
    {
        public const ushort VulnerabilityUp = 202;
        public const ushort Fetters = 292;
        public const ushort InfernalFetters = 377;
        public const ushort FireResistanceDownII = 1255;
        public const ushort ThermalLow = 1525;
        public const ushort ThermalHigh = 1526;
        public const ushort AccursedFlame = 1527;
        public const ushort AetheriallyCharged = 1528;
        public const ushort Woken = 1529;
        public const ushort SearingWind = 1578;
    }

    public class TetherId
    {
        public const ushort Mesohigh = 4;
        public const ushort InfernalFetters = 9;
        public const ushort SpinyFixate = 17;
    }

    public class EObjId
    {
        public const uint Arena = 2007457;
        public const uint ArenaLayoutId = 7538913;
    }

    public class LockonId
    {
        public const ushort MistralSong = 16;
        public const ushort FlamingCrush = 117;
    }

    public class Duration
    {
        public const float RazorPlumeRotation = 17.95f;
        public const float RazorPlumeBack = 1f;
    }

    public class Geometry
    {
        public const float ArenaRadius = 19.4f;
        public const float WickedTornadoOuterRadius = 20; // 20 is the EffectRange in the Action sheet

        public static readonly float RazorPlumeRotation = float.DegreesToRadians(-450);
        public static readonly float RazorPlumeBackDistance = 3.6f;
        public static readonly FrozenDictionary<DirectionEnum, float> LookAtCenterRotation;
        public static readonly FrozenDictionary<DirectionEnum, Placement> UltimaPlacements;
        public static readonly FrozenDictionary<DirectionEnum, Placement> GarudaPlacements;
        public static readonly FrozenDictionary<DirectionEnum, Placement> IfritPlacements;
        public static readonly FrozenDictionary<DirectionEnum, Placement> TitanPlacements;
        public static ReadOnlySpan<Placement> CrimsonCycloneAwakenPlacements => CrimsonCycloneAwakenPlacementsROM.Span;
        public static ReadOnlySpan<float> TitanLandslideOffsets => TitanLandslideOffsetsROM.Span;
        public static ReadOnlySpan<float> TitanLandslideAwakenOffsets => TitanLandslideAwakenOffsetsROM.Span;
        public static ReadOnlySpan<Vector3> TitanBoulderPositions => TitanBoulderPositionsROM.Span;
        public static ReadOnlySpan<Vector3> UltimaRadiantPlumePositions => UltimaRadiantPlumePositionsROM.Span;
        public static ReadOnlySpan<float> UltimaLandslideOffsets => UltimaLandslideOffsetsROM.Span;

        private static readonly ReadOnlyMemory<Placement> CrimsonCycloneAwakenPlacementsROM;
        private static readonly ReadOnlyMemory<float> TitanLandslideOffsetsROM;
        private static readonly ReadOnlyMemory<float> TitanLandslideAwakenOffsetsROM;
        private static readonly ReadOnlyMemory<Vector3> TitanBoulderPositionsROM;
        private static readonly ReadOnlyMemory<Vector3> UltimaRadiantPlumePositionsROM;
        private static readonly ReadOnlyMemory<float> UltimaLandslideOffsetsROM;

        static Geometry()
        {
            LookAtCenterRotation = new Dictionary<DirectionEnum, float>
            {
                { DirectionEnum.N, 0 },
                { DirectionEnum.NE, float.DegreesToRadians(-45) },
                { DirectionEnum.E, float.DegreesToRadians(-90) },
                { DirectionEnum.SE, float.DegreesToRadians(-135) },
                { DirectionEnum.S, float.DegreesToRadians(180) },
                { DirectionEnum.SW, float.DegreesToRadians(135) },
                { DirectionEnum.W, float.DegreesToRadians(90) },
                { DirectionEnum.NW, float.DegreesToRadians(45) }
            }.ToFrozenDictionary();

            UltimaPlacements = GetIntercardinalPlacementDictionary(12);
            GarudaPlacements = GetIntercardinalPlacementDictionary(3);
            IfritPlacements = GetIntercardinalPlacementDictionary(13.7f);
            TitanPlacements = new Dictionary<DirectionEnum, Placement>
            {
                { DirectionEnum.N, new(new(0, 0, -17), LookAtCenterRotation[DirectionEnum.N]) },
                { DirectionEnum.E, new(new(17, 0, 0), LookAtCenterRotation[DirectionEnum.E]) },
                { DirectionEnum.S, new(new(0, 0, 17), LookAtCenterRotation[DirectionEnum.S]) },
                { DirectionEnum.W, new(new(-17, 0, 0), LookAtCenterRotation[DirectionEnum.W]) }
            }.ToFrozenDictionary();

            CrimsonCycloneAwakenPlacementsROM = new Placement[]
            {
                new(new(19.374725f, 0, 0), float.DegreesToRadians(-90)),
                new(new(0, 0, -19.374725f), float.DegreesToRadians(0))
            };

            TitanLandslideOffsetsROM = new float[]
            {
                float.DegreesToRadians(-135),
                float.DegreesToRadians(135),
                float.DegreesToRadians(-45),
                float.DegreesToRadians(45),
                float.DegreesToRadians(0)
            };

            TitanLandslideAwakenOffsetsROM = new float[]
            {
                float.DegreesToRadians(-338),
                float.DegreesToRadians(-22),
                float.DegreesToRadians(-270),
                float.DegreesToRadians(-90),
                float.DegreesToRadians(-180)
            };

            TitanBoulderPositionsROM = new Vector3[]
            {
                new(-3, 0, -5), // NW
                new(3, 0, -5), // NE
                new(6, 0, 0), // E
                new(3, 0, 5), // SE
                new(-3, 0, 5), // SW
                new(-6, 0, 0) // W
            };

            UltimaRadiantPlumePositionsROM = new Vector3[]
            {
                new(0, 0, 18),
                new(11, 0, 11),
                new(-11, 0, 11),
                new(18, 0, 0),
                new(-18, 0, 0),
                new(11, 0, -11),
                new(-11, 0, -11),
                new(0, 0, -18)
            };

            UltimaLandslideOffsetsROM = new float[]
            {
                float.DegreesToRadians(0),
                float.DegreesToRadians(45),
                float.DegreesToRadians(315)
            };
        }

        private static FrozenDictionary<DirectionEnum, Placement> GetIntercardinalPlacementDictionary(float offset)
        {
            return new Dictionary<DirectionEnum, Placement>
            {
                { DirectionEnum.NW, new(new(-offset, 0, -offset), LookAtCenterRotation[DirectionEnum.NW]) },
                { DirectionEnum.NE, new(new(offset, 0, -offset), LookAtCenterRotation[DirectionEnum.NE]) },
                { DirectionEnum.SE, new(new(offset, 0, offset), LookAtCenterRotation[DirectionEnum.SE]) },
                { DirectionEnum.SW, new(new(-offset, 0, offset), LookAtCenterRotation[DirectionEnum.SW]) }
            }.ToFrozenDictionary();
        }
    }

    public enum DirectionEnum
    {
        N,
        NE,
        E,
        SE,
        S,
        SW,
        W,
        NW
    }
}
