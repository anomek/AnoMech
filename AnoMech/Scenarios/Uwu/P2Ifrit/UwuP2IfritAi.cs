using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Uwu.P2Ifrit;

public sealed class UwuP2IfritAi : IScenarioAi<UwuP2IfritState>
{
    public string Name => "Zeal / The Balance";

    private const float RunSpeed = 6f;
    private const float LaneMargin = 1.2f;
    private const float DodgeReach = 17.5f;
    private const float DodgeStep = 0.75f;
    private const float SearingWindClearance = 15f;
    private const float PartyCornerRadius = 15.8f;
    private const float MainTankCornerRadius = 17.5f;
    private const float RimRadius = 16.5f;
    private const float OppositeTheClosePairOfNails = 112.5f;

    private static readonly Vector2 FinalMainTankSpot = new(-12.5f, 0f);
    private static readonly Vector2 FinalPartySpot = new(-4f, 5f);

    private UwuP2IfritState state = null!;
    private SimWorld world = null!;

    public void Run(UwuP2IfritState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);

        ai.Move(0.5f, () => AiMove.Create(SpreadAround(Vector2.Zero, 1.2f)).NaturalOrder());
        ai.Move(5.3f, RunToOpenerSafeSpot);
        ai.Move(10.3f, TankIfritSouthPartyNorth);

        ai.Move(40.9f, SurroundNailsWithIfritPulledBetweenTheClosePair);
        ai.Move(48.3f, () => BaitersSplitTo(RimReference(60f), RimReference(160f)));
        ai.Move(48.5f, () => Only(state.HowlFirst, RimReference(OppositeTheClosePairOfNails)));
        ai.Move(51.2f, () => BaitersSplitTo(RimReference(26.5f), RimReference(193.5f)));
        ai.Move(53.2f, () => BaitersSplitTo(RimReference(-7f), RimReference(227f)));
        ai.Move(55.2f, () => BaitersSplitTo(RimReference(-40.5f), RimReference(260.5f)));
        ai.Move(58.3f, () => BaitersTo(NailPhasePartySpot()));
        ai.Move(67.0f, () => Only(state.HowlFirst, NailPhasePartySpot()));

        ai.Move(72.0f, () => AiMove.Create(SpreadAround(Vector2.Zero, 1.2f)).NaturalOrder());
        ai.Move(79.6f, GatherInLastNailCorner);
        ai.Move(82.0f, () => BaitersTo(RimReference(100f, 17f)));
        ai.Move(84.8f, () => Only(state.HowlSecond, RimReference(225f)));
        ai.Move(89.9f, () => BaitersTo(RimReference(66.5f, 17f)));
        ai.Move(91.9f, () => BaitersTo(RimReference(33f, 17f)));
        ai.Move(93.9f, () => BaitersTo(RimReference(-0.5f, 17f)));
        ai.Move(95.9f, () => BaitersTo(CornerReference(PartyCornerRadius)));
        ai.Move(104.85f, () => Only(state.HowlThird, RimReference(45f)));
        ai.Move(117.7f, () => Only(state.HowlSecond, CornerReference(PartyCornerRadius)));
        ai.Move(119.5f, () => Only(state.HowlThird, RimReference(135f)));

        for (var t = 123.0f; t < 133.3f; t += 0.3f)
        {
            var at = t;
            ai.Move(at, () => DodgeCrimsonCyclones(at), jitter: 0f);
        }

        ai.Move(134.9f, TankIfritWestPartyEastOfHim);
        ai.Move(149.5f, () => BaitersTo(Rim(150f)));
        ai.Move(152.3f, () => BaitersTo(Rim(116.5f)));
        ai.Move(154.3f, () => BaitersTo(Rim(83f)));
        ai.Move(156.3f, () => BaitersTo(Rim(49.5f)));
        ai.Move(158.3f, () => BaitersTo(FinalPartySpot));
    }

    private IAiMove RunToOpenerSafeSpot()
    {
        var safe = Flat(state.OpenerSafeSpot);
        var inward = -Vector2.Normalize(safe);
        var along = new Vector2(-inward.Y, inward.X);
        var spots = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++)
            spots[slot] = safe + along * ((slot % 4) - 1.5f) + inward * (slot < 4 ? 0f : 0.8f);
        return AiMove.Create(spots).NaturalOrder();
    }

    private static IAiMove TankIfritSouthPartyNorth() => AiMove.Create(
        new(0f, 6.5f),
        new(0f, -3.5f),
        new(-2f, -5.5f),
        new(2f, -5.5f),
        new(-1f, -4.5f),
        new(1f, -4.5f),
        new(-3f, -6.5f),
        new(3f, -6.5f)
    ).NaturalOrder();

    private IAiMove SurroundNailsWithIfritPulledBetweenTheClosePair()
    {
        var spots = SpreadAround(NailPhasePartySpot(), 1.2f);
        spots[(int)PartyRole.MainTank] = Reference(-9.5f, -3.5f);
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 NailPhasePartySpot() => Reference(1.5f, -1.5f);

    private IAiMove GatherInLastNailCorner()
    {
        var spots = SpreadAround(CornerReference(PartyCornerRadius), 1.2f);
        spots[(int)PartyRole.MainTank] = CornerReference(MainTankCornerRadius);
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 CornerReference(float radius) => RimReference(315f, radius);

    private static IAiMove TankIfritWestPartyEastOfHim()
    {
        var spots = SpreadAround(FinalPartySpot, 1.2f);
        spots[(int)PartyRole.MainTank] = FinalMainTankSpot;
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove DodgeCrimsonCyclones(float now)
    {
        var spots = new Vector2?[8];
        var party = new List<Vector2>();
        for (var slot = 0; slot < 8; slot++)
        {
            if (slot == (int)state.HowlThird || world.Party.Get(slot) is not { } member || !member.IsAlive()) continue;
            var spot = NearestSpotClearOfUpcomingLanes(Flat(member.Position), now, []);
            spots[slot] = spot;
            party.Add(spot);
            party.Add(Flat(member.Position));
        }
        if (world.Party.Get(state.HowlThird) is { } holder && holder.IsAlive())
            spots[(int)state.HowlThird] = NearestSpotClearOfUpcomingLanes(Flat(holder.Position), now, party);
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 NearestSpotClearOfUpcomingLanes(Vector2 from, float now, IReadOnlyList<Vector2> keepClearOf)
    {
        var upcoming = state.FinalLanes.Where(l => l.At > now).ToList();
        for (var count = upcoming.Count; count > 0; count--)
        {
            var lanes = upcoming.Take(count).ToList();
            Vector2? best = null;
            var bestDistance = float.MaxValue;
            for (var x = -DodgeReach; x <= DodgeReach; x += DodgeStep)
                for (var z = -DodgeReach; z <= DodgeReach; z += DodgeStep)
                {
                    var spot = new Vector2(x, z);
                    if (spot.Length() > DodgeReach || keepClearOf.Any(p => Vector2.Distance(p, spot) < SearingWindClearance)) continue;
                    var distance = Vector2.Distance(from, spot);
                    if (distance >= bestDistance || !ClearOfLanesOnTheWay(from, spot, now, lanes)) continue;
                    best = spot;
                    bestDistance = distance;
                }
            if (best is { } found) return found;
        }
        return from;
    }

    private static bool ClearOfLanesOnTheWay(Vector2 from, Vector2 to, float now, IEnumerable<UwuP2IfritState.Lane> lanes)
    {
        var distance = Vector2.Distance(from, to);
        var direction = distance > 0.01f ? (to - from) / distance : Vector2.Zero;
        foreach (var lane in lanes)
        {
            var whenItHits = from + direction * MathF.Min(distance, RunSpeed * (lane.At - now));
            if (DistanceFromLaneAxis(whenItHits, lane.Bearing) <= lane.HalfWidth + LaneMargin) return false;
        }
        return true;
    }

    private static float DistanceFromLaneAxis(Vector2 point, float bearing)
    {
        var rad = bearing * MathF.PI / 180f;
        var axis = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));
        return MathF.Abs(point.X * axis.Y - point.Y * axis.X);
    }

    private static IAiMove BaitersTo(Vector2 spot)
    {
        var spots = new Vector2?[8];
        spots[(int)PartyRole.PhysRangedDps] = spot + new Vector2(-0.4f, 0f);
        spots[(int)PartyRole.CasterDps] = spot + new Vector2(0.4f, 0f);
        return AiMove.Create(spots).NaturalOrder();
    }

    private static IAiMove BaitersSplitTo(Vector2 physRanged, Vector2 caster)
    {
        var spots = new Vector2?[8];
        spots[(int)PartyRole.PhysRangedDps] = physRanged;
        spots[(int)PartyRole.CasterDps] = caster;
        return AiMove.Create(spots).NaturalOrder();
    }

    private static IAiMove Only(PartyRole role, Vector2 spot)
    {
        var spots = new Vector2?[8];
        spots[(int)role] = spot;
        return AiMove.Create(spots).NaturalOrder();
    }

    private static Vector2?[] SpreadAround(Vector2 centre, float spread)
    {
        var spots = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++)
        {
            var angle = slot * MathF.PI / 4f;
            spots[slot] = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spread;
        }
        return spots;
    }

    private Vector2 Reference(float x, float z) => state.FromReference(new Vector2(x, z));

    private Vector2 RimReference(float bearing, float radius = RimRadius) => state.FromReference(Rim(bearing, radius));

    private static Vector2 Rim(float bearing, float radius = RimRadius) => Flat(UwuP2IfritState.AtBearing(bearing, radius));

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
}
