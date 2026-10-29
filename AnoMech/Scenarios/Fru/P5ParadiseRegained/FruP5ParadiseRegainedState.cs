using System;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Fru.P5ParadiseRegained;

// Three two-person towers 3.5s apart, the first two resolving with a wing: a cleave on the boss's
// target and a buster on the nearest (dark) or farthest (light) player. Polarizing Strikes
// follows on the same clock: four rounds of a light and a dark line stack, one on each of the
// two players nearest Pandora, split by which of them stands further to each side.
public sealed class FruP5ParadiseRegainedState
{
    public const float TowerRadius = 3f;
    public static float TowerShowAt(int tower) => 6f + 3.5f * tower;
    public static float TowerHitAt(int tower) => 15.7f + 3.5f * tower;

    public static float LinesAt(int round) => 36.9f + 4.6f * round;
    public static float PathsCastAt(int round) => LinesAt(round) + 2.05f;
    public static float EchoAt(int round) => LinesAt(round) + 2.6f;

    // Which third the towers turn to, whether the east tower is second, and the wing order.
    public int Rotation { get; }
    public bool EastSecond { get; }
    public bool DarkFirst { get; }

    public FruP5ParadiseRegainedState(Rng rng, FruP5ParadiseRegainedStateOverrides overrides)
    {
        Rotation = overrides.Rotation ?? rng.Next(3);
        EastSecond = overrides.EastSecond ?? rng.NextBool();
        DarkFirst = overrides.DarkFirst ?? rng.NextBool();
    }

    // A peer's shadow of the host's roll (see IMultiplayerReplayable): only what the Ai reads.
    private FruP5ParadiseRegainedState(int rotation, bool darkFirst)
    {
        Rotation = rotation;
        DarkFirst = darkFirst;
    }

    public static FruP5ParadiseRegainedState? FromNetworkReplay(int rotation, bool darkFirst)
        => rotation is >= 0 and < 3 ? new(rotation, darkFirst) : null;

    public bool IsDark(int wing) => wing == 0 ? DarkFirst : !DarkFirst;

    private int TowerIndex(int tower)
    {
        var index = tower == 0 ? 0 : (tower == 1) == EastSecond ? 2 : 1;
        return (index + Rotation) % 3;
    }

    public byte TowerSlot(int tower) => (byte)(FruConstants.MapEffect.ParadiseFirstTower + TowerIndex(tower) switch { 1 => 0, 2 => 1, _ => 2 });

    public Vector3 TowerPosition(int tower) => TowerIndex(tower) switch
    {
        1 => new(-6.0622f, 0f, -3.5f),
        2 => new(6.0622f, 0f, -3.5f),
        _ => new(0f, 0f, 7f),
    };

    public Vector3 Rotate(Vector3 position)
    {
        var angle = -Rotation * 2f * MathF.PI / 3f;
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        return new(position.X * cos + position.Z * sin, position.Y, position.Z * cos - position.X * sin);
    }
}
