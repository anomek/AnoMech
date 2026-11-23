using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

using AnoMech.Scenarios;

namespace AnoMech.Scenarios.Uwu.P3TitanGaols;

// First-jump positions are authored with Titan east and rotated by FromJumpFrame.
public sealed class UwuP3TitanGaolsState
{
    public const float JumpRadius = 14f;
    public const float FirstShrinkRadius = 16f;

    public float FirstJumpBearing { get; }
    public int SafeSide { get; }
    public IReadOnlyList<PartyRole> GaolTargets { get; }
    public PartyRole FirstLandslideBait { get; }
    public PartyRole SecondLandslideBait { get; }

    public HashSet<PartyRole> Jailed { get; } = [];

    public UwuP3TitanGaolsState(Rng rng, UwuP3TitanGaolsStateOverrides? overrides = null, PartyRole? player = null)
    {
        var rolledJumpBearing = 90f * rng.Next(4);
        FirstJumpBearing = overrides?.FirstJump is { } jump ? jump.Index() * 45f : rolledJumpBearing;
        var rolledSafeSide = rng.Next(2) == 0 ? 1 : -1;
        SafeSide = overrides?.SafeSide ?? rolledSafeSide;
        var everyone = Enum.GetValues<PartyRole>();
        var gaolable = everyone.Where(r => r != PartyRole.MainTank).ToArray();
        var gaolTargets = rng.Shuffle(gaolable).Take(3).ToList();
        if (overrides?.PlayerAlwaysInFirstGaols == true && player is { } me && me != PartyRole.MainTank && !gaolTargets.Contains(me))
            gaolTargets[rng.Next(3)] = me;
        GaolTargets = (overrides?.GaolTargets ?? gaolTargets).OrderBy(r => (int)r).ToList();
        var landslideBaits = everyone.Except(GaolTargets).ToArray();
        var rolledFirstBait = landslideBaits[rng.Next(landslideBaits.Length)];
        var rolledSecondBait = landslideBaits[rng.Next(landslideBaits.Length)];
        FirstLandslideBait = overrides?.FirstLandslideBait ?? rolledFirstBait;
        SecondLandslideBait = overrides?.SecondLandslideBait ?? rolledSecondBait;
    }

    public IReadOnlyList<Vector3> UpheavalBombs =>
    [
        FromJumpFrame(new Vector3(-11f, 0f, 11f)),
        FromJumpFrame(new Vector3(-11f, 0f, -11f)),
        FromJumpFrame(new Vector3(-5f, 0f, 5f)),
        FromJumpFrame(new Vector3(-5f, 0f, -5f)),
        FromJumpFrame(new Vector3(-12f, 0f, -5f * SafeSide)),
    ];

    // Gaol line on the waymarks along Titan's axis, numbered from Titan's side.
    public Vector2 GaolSpot(int order) => FromJumpFrame(order switch
    {
        0 => new Vector2(5.4f, 0.5f * SafeSide),
        1 => new Vector2(0f, 1.0f * SafeSide),
        _ => new Vector2(-6.7f, 1.2f * SafeSide),
    });

    public Vector3 SixthBomb => FromJumpFrame(new Vector3(-12f, 0f, 5f * SafeSide));

    public Vector2 FromJumpFrame(Vector2 eastFrame)
    {
        var rad = (FirstJumpBearing - 90f) * MathF.PI / 180f;
        var (sin, cos) = MathF.SinCos(rad);
        return new Vector2(eastFrame.X * cos - eastFrame.Y * sin, eastFrame.X * sin + eastFrame.Y * cos);
    }

    public Vector3 FromJumpFrame(Vector3 eastFrame)
    {
        var flat = FromJumpFrame(new Vector2(eastFrame.X, eastFrame.Z));
        return new Vector3(flat.X, eastFrame.Y, flat.Y);
    }

    // Compass degrees: 0 = north (-Z), 90 = east (+X).
    public static Vector3 AtBearing(float bearingDegrees, float radius)
    {
        var rad = bearingDegrees * MathF.PI / 180f;
        return new Vector3(radius * MathF.Sin(rad), 0f, -radius * MathF.Cos(rad));
    }

    public static float FacingCentre(Vector3 from) => MathF.Atan2(-from.X, -from.Z);

    public static float Facing(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z);
}
