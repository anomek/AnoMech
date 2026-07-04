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

    public class BNpcBaseId
    {
        public const uint Garuda = 8722;
        public const uint SuparnaChirada = 8723;
        public const uint Titan = 8727;
        public const uint BombBoulder = 8728;
        public const uint Ifrit = 8730;
        public const uint UltimaWeapon = 8734;
        public const uint Dummy = 9020;
    }

    public class BNpcNameId
    {
        public const uint Dummy = 108;
        public const uint BombBoulder = 1803;
        public const uint Ifrit = 1185;
        public const uint Garuda = 1644;
        public const uint Suparna = 1645;
        public const uint Chirada = 1646;
        public const uint Titan = 1801;
        public const uint UltimaWeapon = 2137;
    }

    public class ActionId
    {
        public const uint WickedWheel = 11084;
        public const uint FeatherRain = 11085;
        public const uint WickedWheelAwaken = 11086;
        public const uint WickedTornado = 11087;
        public const uint MistralShriek = 11092;
        public const uint EruptionIfrit = 11097;
        public const uint EruptionPuddle = 11098;
        public const uint CrimsonCyclone = 11103;
        public const uint CrimsonCycloneAwaken = 11104;
        public const uint RadiantPlumePuddle = 11105;
        public const uint BoulderTitan = 11112;
        public const uint Bury = 11113;
        public const uint Burst = 11114;
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
        public const uint Tumult = 11288;
        public const uint InfernalFetters = 11289;
        public const uint LandslideAwaken = 11298;
        public const uint PostUltimatePredation1 = 11475;
        public const uint PostUltimatePredation2 = 11476;
        public const uint PostUltimatePredation3 = 11477;
        public const uint UltimateAnnihilation = 11596;
    }

    public class ActionTimelineId
    {
        public const ushort WarpStart = 7737;
        public const ushort WarpStart2 = 7738;
        public const ushort WarpEnd = 7747;
    }

    public class StatusId
    {
        public const ushort InfernalFetters = 377;
        public const ushort Woken = 1529;
    }

    public class TetherId
    {
        public const ushort InfernalFetters = 9;
    }

    public class Geometry
    {
        public const float ArenaRadius = 19.4f;
        public const float WickedTornadoOuterRadius = 20; // 20 is the EffectRange in the Action sheet

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
