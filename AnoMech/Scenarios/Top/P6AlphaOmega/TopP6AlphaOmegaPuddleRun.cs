using System;
using System.Collections.Generic;
using System.Numerics;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

// Unlimited Wave Cannon's puddle run as a player sees it: every puddle down and every exaflare
// blast has a spot and a moment it goes off. A bot keeps the guide's next spot while its walk
// there stays clear of all of them, else takes the nearest variation that does, late if it must.
public static class TopP6AlphaOmegaPuddleRun
{
    public readonly record struct Hazard(Vector2 Centre, float Radius, float Lands);

    public readonly record struct Walk(Vector2 From, float Departs, Vector2 To, float Speed)
    {
        public Vector2 At(float time)
        {
            var leg = To - From;
            var length = leg.Length();
            if (time <= Departs || length < 1e-4f) return time <= Departs ? From : To;
            return From + leg * MathF.Min(1f, Speed * (time - Departs) / length);
        }

        public float Arrival => Departs + Vector2.Distance(From, To) / Speed;

        public static Walk Standing(Vector2 at, float speed) => new(at, 0f, at, speed);
    }

    public const float PuddleRadius = 6f;
    public const float ExaflareRadius = 8f;
    private const float Margin = 0.5f;
    private const float WallReach = 19f;
    // A blast landing after the next choice is clear if the guide's next walk avoids it, or a
    // walk straight out of it at this share of full speed would.
    private const float EscapeShare = 0.4f;
    private const float LateCost = 4f;
    private const float DelayCost = 2f;
    private static readonly float[] Delays = [0f, 0.25f, 0.5f, 0.75f, 1f, 1.5f];
    private static readonly float[] Steps = [0f, 1.5f, -1.5f, 3f, -3f, 4.5f, -4.5f];
    // Short hops out of where the bot stands, for when nothing near the guide's spot is clear.
    private static readonly float[] Hops = [2f, 3.5f, 5f];
    private const int HopBearings = 16;

    // `current` is the walk the bot is on until it turns, no earlier than `departs`; the next
    // choice turns it again at `nextDeparts`, toward `thenGuide` as far as the guide knows.
    public static (Vector2 Spot, float Delay, bool Safe) Choose(Walk current, float departs, Vector2 guide, float arriveBy, float nextDeparts,
        Vector2 thenGuide, IReadOnlyList<Hazard> hazards)
    {
        var from = current.At(departs);
        var along = guide - from;
        along = along.LengthSquared() > 0.01f ? Vector2.Normalize(along) : guide.LengthSquared() > 0.01f ? Vector2.Normalize(guide) : Vector2.UnitY;
        var side = new Vector2(-along.Y, along.X);
        var spots = new List<Vector2>();
        foreach (var forward in Steps)
        foreach (var sideways in Steps)
            spots.Add(InsideTheWall(guide + along * forward + side * sideways));
        foreach (var hop in Hops)
            for (var k = 0; k < HopBearings; k++)
                spots.Add(InsideTheWall(from + TopCompass.Point(hop, 360f * k / HopBearings)));
        var best = (Spot: guide, Delay: 0f, Safe: false, Slack: float.MinValue, Cost: float.MaxValue);
        foreach (var spot in spots)
        {
            var shift = Vector2.Distance(spot, guide);
            foreach (var delay in Delays)
            {
                var turn = departs + delay;
                var next = new Walk(current.At(turn), turn, spot, current.Speed);
                var then = new Walk(next.At(nextDeparts), nextDeparts, thenGuide, current.Speed);
                var slack = Slack(current, next, then, hazards);
                var cost = shift + DelayCost * delay + LateCost * MathF.Max(0f, next.Arrival - arriveBy);
                var safe = slack >= 0f;
                if (safe && (!best.Safe || cost < best.Cost) || !safe && !best.Safe && slack > best.Slack)
                    best = (spot, delay, safe, slack, cost);
            }
        }
        return (best.Spot, best.Delay, best.Safe);
    }

    // Whether the walk the bot is on, then the guide's walk after the next choice, stays clear.
    public static bool StaysClear(Walk current, float nextDeparts, Vector2 thenGuide, IReadOnlyList<Hazard> hazards)
        => Slack(current, current, new Walk(current.At(nextDeparts), nextDeparts, thenGuide, current.Speed), hazards) >= 0f;

    // The least room kept from any blast as it lands: on the current walk until the turn, then the
    // next, then the guide's walk after it.
    public static float Slack(Walk current, Walk next, Walk then, IReadOnlyList<Hazard> hazards)
    {
        var slack = float.MaxValue;
        foreach (var hazard in hazards)
        {
            var needed = hazard.Radius + Margin;
            var room = Vector2.Distance(At(current, next, then, hazard.Lands), hazard.Centre) - needed;
            if (hazard.Lands > then.Departs)
                room = MathF.Max(room, Vector2.Distance(then.From, hazard.Centre) - needed + EscapeShare * then.Speed * (hazard.Lands - then.Departs));
            slack = MathF.Min(slack, room);
        }
        return slack;
    }

    private static Vector2 At(Walk current, Walk next, Walk then, float time)
        => time < next.Departs ? current.At(time) : time < then.Departs ? next.At(time) : then.At(time);

    private static Vector2 InsideTheWall(Vector2 spot) => spot.Length() > WallReach ? spot * (WallReach / spot.Length()) : spot;
}
