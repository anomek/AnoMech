using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P3Apocalypse;

// Dark Water at three durations on two players each (two get none), the Apocalypse lights rolling
// around the ring from a random spoke, Spirit Taker on a random player, and Darkest Dance on the
// farthest.
public sealed class FruP3ApocalypseState
{
    public const float RingRadius = 14f;
    public const float WaterAppliedAt = 12.8f;
    // Darkest Dance lands this short of its target.
    private const float DanceLandingShort = 6.3f;
    public static float WaterAt(int duration) => duration switch { 1 => 22.8f, 2 => 41.8f, _ => 50.8f };
    public static float WaterMarkerAt(int duration) => duration switch { 1 => 17.8f, 2 => 35.8f, _ => 45.8f };
    public static float WaveAt(int wave) => 33.3f + 2f * wave;
    public const int WaveCount = 6;
    public static float LightBeatAt(int beat) => 22.1f + 2f * beat;
    public const int LightBeatCount = 5;

    // Eighths of a turn from north where the first lights go up.
    public int Rotation { get; }
    public bool Clockwise { get; }
    // Indexed by PartyRole: 0 = no Dark Water, else its duration slot 1-3.
    public IReadOnlyList<int> WaterDurations { get; }
    public PartyRole SpiritTakerTarget { get; }

    public FruP3ApocalypseState(Rng rng, PartyRole playerRole, FruP3ApocalypseStateOverrides overrides)
    {
        Rotation = overrides.Rotation ?? rng.Next(4);
        Clockwise = overrides.Clockwise ?? rng.NextBool();
        if (overrides.WaterDurations is { } pinned) WaterDurations = pinned;
        else
        {
            var waters = rng.Shuffle(WaterDeal).ToArray();
            foreach (var (role, water) in ForcedDeal.Swap(rng, waters, overrides.DarkWater.Resolve(playerRole), (wanted, duration) => duration == (int)wanted))
                DiagnosticLog.Warn($"[FruP3Apocalypse] {role} asked for {FruP3ApocalypseStateOverrides.Label(water)}, already held by "
                    + $"{FruP3ApocalypseStateOverrides.SeatsPerWater} seats -- leaving {role} to the roll.");
            WaterDurations = waters;
        }
        SpiritTakerTarget = overrides.SpiritTakerTarget ?? rng.NextRole();
    }

    // A peer's shadow of the host's roll (see IMultiplayerReplayable): only what the Ai reads.
    private FruP3ApocalypseState(IReadOnlyList<int> waterDurations, int rotation, bool clockwise)
    {
        WaterDurations = waterDurations;
        Rotation = rotation;
        Clockwise = clockwise;
    }

    private static readonly int[] WaterDeal = [0, 0, 1, 1, 2, 2, 3, 3];

    public static FruP3ApocalypseState? FromNetworkReplay(IReadOnlyList<int> waterDurations, int rotation, bool clockwise)
        => waterDurations.Order().SequenceEqual(WaterDeal) && rotation is >= 0 and < 4
            ? new(waterDurations.ToList(), rotation, clockwise)
            : null;

    public int WaterDuration(PartyRole role) => WaterDurations[(int)role];

    public IEnumerable<PartyRole> WaterHolders(int duration)
        => Enum.GetValues<PartyRole>().Where(role => WaterDuration(role) == duration);

    // The center goes off in the first two waves, twice each, then each step's pair of opposite
    // spokes, three steps at a time.
    public IEnumerable<Vector3> Explosions(int wave)
    {
        if (wave < 2)
        {
            yield return Vector3.Zero;
            yield return Vector3.Zero;
        }
        for (var step = Math.Max(0, wave - 2); step <= wave; step++)
        {
            yield return SpokePosition(Spoke(step));
            yield return SpokePosition(Spoke(step) + 4);
        }
    }

    // Eighths of a turn clockwise from north.
    public int Spoke(int step) => ((Rotation + (Clockwise ? step : -step)) % 8 + 8) % 8;

    public static Vector3 SpokePosition(int spoke)
    {
        var angle = spoke * MathF.PI / 4;
        return new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle)) * RingRadius;
    }

    public static Vector3 DanceLanding(Vector3 oracle, Vector3 target)
    {
        var offset = target - oracle;
        return offset.Length() > DanceLandingShort ? oracle + Vector3.Normalize(offset) * (offset.Length() - DanceLandingShort) : oracle;
    }

    // The light beats run ahead of the explosions; the last wave's spokes never light up.
    public static IEnumerable<int> LitSteps(int beat)
        => new[] { beat - 2, beat - 1, beat - 1, beat }.Where(step => step >= 0);
}
