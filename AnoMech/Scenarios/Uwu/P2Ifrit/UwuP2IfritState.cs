using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

using AnoMech.Scenarios;

namespace AnoMech.Scenarios.Uwu.P2Ifrit;

// AI positions are authored with C1 south and rotated by FromReference.
public sealed class UwuP2IfritState
{
    public const float EdgeRadius = 19.5f;
    public const float OpenerSafeSpotRadius = 17f;
    private const float ReferenceNailBearing = 180f;

    public const float CrimsonCycloneHalfWidth = 9f;
    public const float AwakenedCrossHalfWidth = 5f;
    public static readonly float[] FinalDashHits = [126.41f, 127.83f, 129.22f, 130.64f];
    private const float AwakenedCrossAfterDash = 2.15f;

    public static readonly Vector2 IfritAtNailsReference = new(-6f, -3f);
    public static readonly Vector2 IfritAtCornerReference = new(-8.5f, -8.5f);

    private static readonly PartyRole[] Healers = [PartyRole.RegenHealer, PartyRole.ShieldHealer];
    private static readonly PartyRole[] Dps = [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];

    public readonly record struct Lane(float Bearing, float HalfWidth, float At);

    public float OpenerBearing { get; }
    public IReadOnlyList<Vector3> RadiantPlumes { get; }
    public Vector3 OpenerSafeSpot { get; }
    public float NailBearing { get; }
    public IReadOnlyList<float> NailKillBearings { get; }
    public PartyRole FettersDps { get; }
    public PartyRole HowlFirst { get; }
    public PartyRole HowlSecond { get; }
    public PartyRole HowlThird { get; }
    public IReadOnlyList<PartyRole> FlamingCrushTargets { get; }
    public int AwakenedDash { get; }
    public IReadOnlyList<Lane> AwakenedCrossLanes { get; }
    public IReadOnlyList<Lane> FinalLanes { get; }

    public UwuP2IfritState(Rng rng, SimParty party, UwuP2IfritStateOverrides overrides)
    {
        OpenerBearing = 90f * rng.Next(4);
        var northSouthPlume = rng.Next(2) == 0 ? 0f : 180f;
        var eastWestPlume = rng.Next(2) == 0 ? 90f : 270f;
        var plumes = new List<Vector3>();
        for (var bearing = 45f; bearing < 360f; bearing += 90f) plumes.Add(AtBearing(bearing, 11f * MathF.Sqrt(2f)));
        for (var bearing = 0f; bearing < 360f; bearing += 90f) plumes.Add(AtBearing(bearing, 7f));
        plumes.Add(AtBearing(northSouthPlume, 18f));
        plumes.Add(AtBearing(eastWestPlume, 18f));
        RadiantPlumes = plumes;
        var besideDash = OpenerBearing % 180f == 0f ? eastWestPlume : northSouthPlume;
        OpenerSafeSpot = AtBearing(besideDash + 180f, OpenerSafeSpotRadius);

        NailBearing = 90f * rng.Next(4);
        NailKillBearings = [Normalize(NailBearing + 225f), NailBearing, Normalize(NailBearing + 135f), Normalize(NailBearing + 90f)];

        FettersDps = Dps[rng.Next(Dps.Length)];
        var playerRole = party.PlayerRole;
        var playerIsHealer = Healers.Contains(playerRole);
        HowlFirst = Healers[rng.Next(Healers.Length)];
        HowlSecond = Healers[rng.Next(Healers.Length)];
        if (playerIsHealer && overrides.SearingWindOnPlayer == SearingWindChoice.FirstAndThirdHowl)
        {
            HowlFirst = playerRole;
            HowlSecond = OtherHealer(playerRole);
        }
        if (playerIsHealer && overrides.SearingWindOnPlayer == SearingWindChoice.SecondHowl) HowlSecond = playerRole;
        HowlThird = OtherHealer(HowlSecond);
        FlamingCrushTargets = [Dps[rng.Next(Dps.Length)], Dps[rng.Next(Dps.Length)]];

        AwakenedDash = rng.Next(4);
        var lanes = NailKillBearings.Select((bearing, i) => new Lane(bearing, CrimsonCycloneHalfWidth, FinalDashHits[i])).ToList();
        var awakenedBearing = NailKillBearings[AwakenedDash];
        var crossAt = FinalDashHits[AwakenedDash] + AwakenedCrossAfterDash;
        AwakenedCrossLanes =
        [
            new Lane(Normalize(awakenedBearing - 45f), AwakenedCrossHalfWidth, crossAt),
            new Lane(Normalize(awakenedBearing + 45f), AwakenedCrossHalfWidth, crossAt),
        ];
        lanes.AddRange(AwakenedCrossLanes);
        FinalLanes = lanes.OrderBy(l => l.At).ToList();
    }

    private static PartyRole OtherHealer(PartyRole healer) =>
        healer == PartyRole.RegenHealer ? PartyRole.ShieldHealer : PartyRole.RegenHealer;

    public Vector2 FromReference(Vector2 reference)
    {
        var rad = (NailBearing - ReferenceNailBearing) * MathF.PI / 180f;
        var (sin, cos) = MathF.SinCos(rad);
        return new Vector2(reference.X * cos - reference.Y * sin, reference.X * sin + reference.Y * cos);
    }

    public Vector3 FromReference(Vector3 reference)
    {
        var flat = FromReference(new Vector2(reference.X, reference.Z));
        return new Vector3(flat.X, reference.Y, flat.Y);
    }

    public static Vector3 NailSpot(float bearing) => AtBearing(bearing, bearing % 90f == 0f ? 10f : 7f * MathF.Sqrt(2f));

    public static Vector3 EdgeSpot(float bearing) => AtBearing(bearing, EdgeRadius);

    // Compass degrees: 0 = north (-Z), 90 = east (+X).
    public static Vector3 AtBearing(float bearingDegrees, float radius)
    {
        var rad = bearingDegrees * MathF.PI / 180f;
        return new Vector3(radius * MathF.Sin(rad), 0f, -radius * MathF.Cos(rad));
    }

    public static float FacingCentre(Vector3 from) => MathF.Atan2(-from.X, -from.Z);

    public static float Facing(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z);

    public static float Normalize(float bearing) => (bearing % 360f + 360f) % 360f;
}
