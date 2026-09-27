using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Ucob.P5Exaflares;

// One rolling eruption: where it lands, when it lands, and when its lane arrow went up
// (KnownAt): bots may only dodge what has already been telegraphed.
public sealed record ExaflareHit(Vector3 Position, float Time, float KnownAt);

// One lane of fire: six eruptions marching Advance yalms along Rotation, starting at Start.
public sealed record ExaflareLine(Vector3 Start, float Rotation, float TelegraphAt, IReadOnlyList<ExaflareHit> Hits);

// Per-run randomization plus the pattern it resolves to.
//
// Six parallel lanes cross the arena, all travelling the same way, spaced 8y apart at
// perpendicular offsets -20/-12/-4/+4/+12/+20. Each lane erupts six times, 8y apart, from 20y
// behind the arena to 20y past it. They fire as three pairs 3s apart, and which two lanes make
// up a pair is randomized per set — so the dodge is "stand on a lane that has already burned,
// or on one that is not in this pair", never "find the gap" (adjacent lanes are 8y apart and
// each blast is 6y, so the lanes overlap).
//
// Every number here is measured from a clear log (fflogs r7bCpTZQJ2AdzVKH, fight 3, all four
// Exaflare sets agree): lane offsets and spacing, the 20y start-back, 6 eruptions, 8y per step,
// 1.47s between steps, 3.0s between pairs, the first pair's arrow going up 2.01s after the boss
// starts its Exaflare cast, and the first eruption 3.97s after its own arrow.
public sealed class UcobP5ExaflaresState
{
    public const float HitRadius = 6f;              // ExaflareFirst/Rest EffectRange
    public const float Advance = 8f;
    public const float RollInterval = 1.47f;
    public const int HitsPerLine = 6;
    public const float StartDistance = 20f;
    public const float TelegraphSeconds = 3.97f;    // arrow omen up to its own release (the first eruption)

    public const int PairCount = 3;
    public const float PairInterval = 3f;
    public const float BossCastAt = 3f;
    public const float BossCastSeconds = 3.7f;
    public const float FirstTelegraphAt = BossCastAt + 2.01f;

    // Perpendicular offsets of the six lanes, in firing-agnostic order.
    public static readonly IReadOnlyList<float> LaneOffsets = [-20f, -12f, -4f, 4f, 12f, 20f];

    public Direction Direction { get; }

    // The lanes in firing order, two per pair.
    public IReadOnlyList<float> LaneOrder { get; }

    // Set axes: Travel is where the lanes roll, Lateral the axis the lane offsets sit on.
    public Vector3 Travel { get; }
    public Vector3 Lateral { get; }
    public IReadOnlyList<ExaflareLine> Lines { get; }
    public IReadOnlyList<ExaflareHit> Hits { get; }
    public float LastHitAt { get; }

    // The scenario's unscaled clock. Bots schedule their dodges on it rather than on the
    // EventTimeScale-driven AiManager, so they stay locked to the fire.
    public EventScheduler Timeline { get; }

    private readonly Rng rng = new();

    public UcobP5ExaflaresState(UcobP5ExaflaresStateOverrides overrides, EventScheduler timeline)
    {
        Timeline = timeline;
        Direction = overrides.Direction ?? rng.NextDirection();

        var theta = Direction.RadiansFromNorth;
        var travel = new Vector3(MathF.Sin(theta), 0f, -MathF.Cos(theta));
        var lateral = new Vector3(MathF.Cos(theta), 0f, MathF.Sin(theta));
        Travel = travel;
        Lateral = lateral;
        // Placement rotation faces +Z at 0, so a compass bearing is its mirror.
        var rotation = MathF.PI - theta;

        var order = rng.Shuffle(LaneOffsets.ToArray()).ToList();
        LaneOrder = order;
        var lines = new List<ExaflareLine>(LaneOffsets.Count);
        var hits = new List<ExaflareHit>(LaneOffsets.Count * HitsPerLine);
        for (var i = 0; i < order.Count; i++)
        {
            var telegraphAt = FirstTelegraphAt + i / 2 * PairInterval;
            var line = BuildLine(travel, lateral, rotation, order[i], telegraphAt);
            lines.Add(line);
            hits.AddRange(line.Hits);
        }

        Lines = lines;
        Hits = hits;
        LastHitAt = FirstTelegraphAt + (PairCount - 1) * PairInterval
                    + TelegraphSeconds + (HitsPerLine - 1) * RollInterval;
    }

    private static ExaflareLine BuildLine(Vector3 travel, Vector3 lateral, float rotation, float offset, float telegraphAt)
    {
        var start = travel * -StartDistance + lateral * offset;
        var hits = new List<ExaflareHit>(HitsPerLine);
        for (var i = 0; i < HitsPerLine; i++)
            hits.Add(new ExaflareHit(
                start + travel * (Advance * i),
                telegraphAt + TelegraphSeconds + i * RollInterval,
                telegraphAt));

        return new ExaflareLine(start, rotation, telegraphAt, hits);
    }
}
