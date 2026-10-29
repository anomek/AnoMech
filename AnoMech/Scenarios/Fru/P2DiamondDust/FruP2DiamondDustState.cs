using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P2DiamondDust;

// Axe or Scythe Kick, four Frigid Stone markers on the supports or the DPS, icicles walking in
// from a random intercard, the Frozen Mirror on a random spoke, the gaze from another, and Twin
// Stillness or Silence.
public sealed class FruP2DiamondDustState
{
    public static float HolyAt(int wave) => 31.7f + wave * (4.7f / 3f);
    public const int HolyWaves = 4;
    public const float PuddleShowsAfterHoly = 0.9f;
    public const float PuddleFadesAfterShowing = 10.86f;
    public static float PuddleFadesAt(int wave) => HolyAt(wave) + PuddleShowsAfterHoly + PuddleFadesAfterShowing;
    public const float IceStart = 40.17f;
    public const float IceEnd = 52.45f;

    public int FirstIcicle { get; }
    public bool SupportsMarked { get; }
    public bool Axe { get; }
    public int MirrorOctant { get; }
    public bool Stillness { get; }
    public int GazeOctant { get; }

    public FruP2DiamondDustState(Rng rng, FruP2DiamondDustStateOverrides overrides)
    {
        FirstIcicle = overrides.FirstIcicle ?? rng.Next(4);
        SupportsMarked = overrides.SupportsMarked ?? rng.NextBool();
        Axe = overrides.Axe ?? rng.NextBool();
        MirrorOctant = overrides.MirrorOctant ?? rng.Next(8);
        Stillness = overrides.Stillness ?? rng.NextBool();
        GazeOctant = overrides.GazeOctant ?? rng.Next(8);
    }

    // A peer's shadow of the host's roll (see IMultiplayerReplayable): nothing rolled.
    private FruP2DiamondDustState(int firstIcicle, bool supportsMarked, bool axe, int mirrorOctant, bool stillness, int gazeOctant)
    {
        FirstIcicle = firstIcicle;
        SupportsMarked = supportsMarked;
        Axe = axe;
        MirrorOctant = mirrorOctant;
        Stillness = stillness;
        GazeOctant = gazeOctant;
    }

    public static FruP2DiamondDustState? FromNetworkReplay(int firstIcicle, bool supportsMarked, bool axe, int mirrorOctant, bool stillness, int gazeOctant)
        => firstIcicle is >= 0 and < 4 && mirrorOctant is >= 0 and < 8 && gazeOctant is >= 0 and < 8
            ? new(firstIcicle, supportsMarked, axe, mirrorOctant, stillness, gazeOctant)
            : null;

    // Octants clockwise from north.
    public static Vector3 Point(float octant, float radius)
        => new(MathF.Sin(octant * MathF.PI / 4) * radius, 0, -MathF.Cos(octant * MathF.PI / 4) * radius);

    public Vector3 MirrorPosition => Point(MirrorOctant, 10);
    public Vector3 GazePosition => Point(GazeOctant, 20);

    public bool Marked(PartyRole role) => ((int)role < 4) == SupportsMarked;

    public IEnumerable<Vector3> Icicles(int wave)
    {
        int[] offsets = wave == 0 ? [0, 4] : wave == 1 ? [1, 3, 5, 7] : [2, 6];
        return offsets.Select(offset => Point(FirstIcicle + offset, 16));
    }
}
