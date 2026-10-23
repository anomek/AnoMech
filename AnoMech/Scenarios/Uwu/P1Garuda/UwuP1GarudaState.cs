using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

using AnoMech.Scenarios;

namespace AnoMech.Scenarios.Uwu.P1Garuda;

// The opening Mistral Song always targets a healer.
public sealed class UwuP1GarudaState
{
    public static readonly Vector3 SpinyPlumeSpawn = new(-10f, 0f, 0f);
    public static readonly Vector3 GigastormSpot = new(-6.8f, 0f, 6.2f);
    public const float SongSpotRadius = 19.5f;
    public static readonly Vector3 SuparnaTetherSpot = new(-15f, 0f, 0f);
    public static readonly Vector3 ChiradaTetherSpot = new(15f, 0f, 0f);

    public const float BubbleRadius = 6f;

    private static readonly Vector3[] SatinPlumeSpots =
    [
        new(-4f, 0f, -16f), new(4f, 0f, -16f), new(12f, 0f, -12f), new(16f, 0f, -4f),
        new(16f, 0f, 4f), new(12f, 0f, 12f), new(4f, 0f, 16f), new(-4f, 0f, 16f),
        new(-12f, 0f, 12f), new(-16f, 0f, 4f), new(-16f, 0f, -4f), new(-12f, 0f, -12f),
    ];

    private static readonly PartyRole[] Healers = [PartyRole.RegenHealer, PartyRole.ShieldHealer];

    private static readonly PartyRole[] NonTanks =
    [
        PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB,
        PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    public Vector3 SuparnaSongSpot { get; }
    public Vector3 ChiradaSongSpot { get; }
    public PartyRole MistralSongTarget { get; }
    public IReadOnlyList<PartyRole> SistersSongTargets { get; }
    public IReadOnlyList<PartyRole> FrictionTargets { get; }
    public IReadOnlyList<Vector3> SatinPlumesFirst { get; }
    public IReadOnlyList<Vector3> SatinPlumesSecond { get; }
    public IReadOnlyList<PartyRole> MesohighTargets { get; }

    public Vector3 SuparnaSongIntercept { get; set; }
    public Vector3 ChiradaSongIntercept { get; set; }
    public SimTether? SuparnaMesohigh { get; set; }
    public SimTether? ChiradaMesohigh { get; set; }

    public UwuP1GarudaState(Rng rng)
    {
        var cardinals = rng.Shuffle(0f, 90f, 180f, 270f).Take(2).ToArray();
        SuparnaSongSpot = AtBearing(cardinals[0], SongSpotRadius);
        ChiradaSongSpot = AtBearing(cardinals[1], SongSpotRadius);
        MistralSongTarget = Healers[rng.Next(Healers.Length)];
        SistersSongTargets = rng.Shuffle(NonTanks).Take(2).ToList();
        FrictionTargets = [NonTanks[rng.Next(NonTanks.Length)], NonTanks[rng.Next(NonTanks.Length)]];
        SatinPlumesFirst = rng.Shuffle(SatinPlumeSpots).Take(4).ToList();
        SatinPlumesSecond = rng.Shuffle(SatinPlumeSpots).Take(4).ToList();
        MesohighTargets = rng.Shuffle(Enum.GetValues<PartyRole>()).Take(2).ToList();
    }

    // Compass degrees: 0 = north (-Z), 90 = east (+X).
    public static Vector3 AtBearing(float bearingDegrees, float radius)
    {
        var rad = bearingDegrees * MathF.PI / 180f;
        return new Vector3(radius * MathF.Sin(rad), 0f, -radius * MathF.Cos(rad));
    }

    public static float BearingOf(Vector3 point) => (MathF.Atan2(point.X, -point.Z) * 180f / MathF.PI + 360f) % 360f;
}
