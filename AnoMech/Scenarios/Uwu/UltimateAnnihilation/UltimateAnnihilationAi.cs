using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Uwu.UltimateAnnihilation;

public class UltimateAnnihilationAi : IScenarioAi<UltimateAnnihilationState>
{
    public string Name => "NAUR";

    private const float RunSpeed = 6.5f;
    private const float PlanSpeed = 6f;
    private const float ReactionDelay = 0.3f;
    private const float SearchStep = 0.5f;
    private const int MaxShiftSteps = 16;
    private const int ReplanSteps = 24;
    private const float Margin = 1f;
    private const float TightMargin = 0.5f;
    private const float PlayerDrift = 2f;
    private const float FeatherRainRadius = 3f;
    private const float OrbTouchRadius = 1.5f;
    private const float OrbBlastRadius = 6f;
    private const float OrbBlastClearance = 6.5f;
    private const float MesohighRadius = 3f;
    private const float FirstMesohighAt = 22.50f;
    private const float InsideTheEye = 11f;
    private const float ArenaReach = 17.9f;

    private static readonly Vector2 Orb = new(2f, -4f);
    private static readonly Vector2 WestStack = new(-6f, -4.5f);
    private static readonly Vector2 EastOfStack = new(2.5f, -3.7f);
    private static readonly Vector2 NorthWestOfStack = new(-6.5f, -12.5f);
    private static readonly Vector2 NorthEdge = new(-0.8f, -17.5f);
    private static readonly Vector2 NorthWestOutOfLines = new(-9.5f, -12.5f);
    private static readonly Vector2 NorthWestStack = new(-6.5f, -7.5f);
    private static readonly Vector2 InwardOfNorthWestStack = new(-4.5f, -4f);
    private static readonly Vector2 HealersWest = new(-11f, 3f);
    private static readonly Vector2 OutOfTheRadiantPlumes = new(-10.5f, -10.5f);
    private static readonly Vector2 WestOfTheDiffractiveLaser = new(-7.5f, -3.5f);
    private static readonly Vector2 MainTankOrb = new(1.6f, -3.4f);
    private static readonly Vector2 OffTankOrb = new(2.6f, -4.4f);

    private readonly record struct Hazard(Vector2 Center, float Radius, float? LandsAt);

    private UltimateAnnihilationState state = null!;
    private SimWorld world = null!;
    private readonly Vector2?[] planned = new Vector2?[8];
    private List<Hazard> expected = [];
    private readonly HashSet<int> unresolved = [];

    public void Run(UltimateAnnihilationState state, SimWorld world)
    {
        this.state = state;
        this.world = world;
        Array.Clear(planned);
        expected = [];

        var ai = new AiManager(world);

        ai.Move(4f, () => Plan(Everyone(WestStack)));
        ai.Move(15f, () => Plan(Dodging(15f, Everyone(EastOfStack))));
        ai.Move(17.6f, () => Plan(Dodging(17.6f, Everyone(WestStack), InsideTheEye)));
        ai.Move(20.6f, () => ClearOfTheFirstMesohigh(20.6f,
            BothHealersWest(Group(NorthWestOfStack, MainTankOrb, OffTankOrb, searingWind: HealersWest, firstMesohigh: new(-1f, 6.5f)))));
        ai.Move(23f, () => Plan(Dodging(23f, Only(state.FirstMesohighTaker, new(12.5f, 1f)))));
        foreach (var at in new[] { 23.1f, 23.6f, 24.1f, 24.6f, 25.1f, 25.6f })
            ai.Move(at, () => TanksPopTheOrbOnceThePartyIsClear(at));
        ai.Move(24.5f, () => Plan(Dodging(24.5f, Only(state.SearingWindTarget, new(-3f, 14f)))));
        ai.Move(24.5f, () => Plan(Dodging(24.5f, Only(OtherHealer, NorthEdge))));
        ai.Move(25.65f, () => AheadOfFeatherRain(25.65f, 26.81f,
            Group(NorthEdge, new(4.5f, -8f), new(6f, -7f), searingWind: new(0f, 17.5f), firstMesohigh: new(11.5f, -11f))));
        ai.Move(26f, () => OutOfThePuddles(26f));
        ai.Move(27.3f, () => Plan(Group(NorthEdge, MainTankOrb, OffTankOrb, searingWind: new(0f, 17.5f), firstMesohigh: new(11.5f, -11f))));
        ai.Move(28.8f, () => Plan(Group(null, new(-1.5f, -15.5f), new(1f, -16.5f))));
        ai.Move(29.2f, () => Plan(Only(state.FirstMesohighTaker, NorthEdge)));
        ai.Move(31.8f, () => Plan(SearingWindHoldsStill(Group(NorthWestOutOfLines, new(-8.5f, -10f), new(7.5f, -11f)))));
        ai.Move(32.2f, () => Plan(Only(state.SearingWindTarget, new(9f, 11f))));
        ai.Move(35.3f, () => Plan(Group(NorthWestStack, NorthWestStack, OffTankOrb, searingWind: new(0f, 10.5f))));
        ai.Move(40.3f, () => Plan(Only(PartyRole.MainTank, new(-2.5f, -6f))));
        ai.Move(43.02f, () => AheadOfFeatherRain(43.02f, 44.22f,
            Group(InwardOfNorthWestStack, new(-0.5f, -14.5f), new(6f, -8f), searingWind: new(3.5f, 9.5f))));
        ai.Move(43.4f, () => OutOfThePuddles(43.4f));
        ai.Move(46f, () => Plan(Group(NorthWestStack, new(0f, -7.7f), new(7.3f, -3.7f), searingWind: new(0.5f, 11f))));
        ai.Move(54.6f, () => Plan(Only(state.SearingWindTarget, NorthWestStack)));
        if (!state.UltimaAboveHalf) return;
        ai.Move(66.4f, () => Plan(Group(OutOfTheRadiantPlumes, new(0f, -16f), new(10.5f, -10.5f))));
        ai.Move(69.4f, () => Plan(Group(WestOfTheDiffractiveLaser, new(0.5f, -8.3f), new(6.5f, -4.5f))));
    }

    private IAiMove AheadOfFeatherRain(float now, float landsAt, Vector2?[] spots)
    {
        var result = new Vector2?[8];
        foreach (var drift in new[] { PlayerDrift, 0f })
        {
            expected = Enumerable.Range(0, 8)
                .Where(slot => world.Party.Get(slot) != null)
                .Select(slot => new Hazard(Projected(slot, now + ReactionDelay, now), FeatherRainRadius + (IsLivePlayer(slot) ? drift : 0f), landsAt))
                .ToList();
            unresolved.Clear();
            var dodged = Dodging(now, spots);
            for (var slot = 0; slot < 8; slot++)
                if (result[slot] == null && !unresolved.Contains(slot)) result[slot] = dodged[slot];
        }
        expected = [];
        for (var slot = 0; slot < 8; slot++) result[slot] ??= spots[slot];
        return Plan(result);
    }

    private IAiMove OutOfThePuddles(float now)
    {
        var spots = new Vector2?[8];
        var depart = now + ReactionDelay;
        for (var slot = 0; slot < 8; slot++)
        {
            if (!IsBot(slot) || planned[slot] is not { } heading) continue;
            if (ClearOnTheWay(Flat(world.Party.Get(slot)!.Position), heading, now, now, TightMargin)) continue;
            var from = Projected(slot, depart, now);
            Vector2? best = null;
            var bestDistance = float.MaxValue;
            for (var i = -ReplanSteps; i <= ReplanSteps; i++)
                for (var j = -ReplanSteps; j <= ReplanSteps; j++)
                {
                    var spot = from + new Vector2(i, j) * SearchStep;
                    var distance = Vector2.Distance(spot, heading);
                    if (spot.Length() > ArenaReach || distance >= bestDistance || !ClearOnTheWay(from, spot, now, depart, TightMargin)) continue;
                    best = spot;
                    bestDistance = distance;
                }
            spots[slot] = best;
        }
        return Plan(spots);
    }

    private IAiMove ClearOfTheFirstMesohigh(float now, Vector2?[] spots)
    {
        var taker = (int)state.FirstMesohighTaker;
        var takerOnly = new Vector2?[8];
        takerOnly[taker] = spots[taker];
        var takerSpot = Dodging(now, takerOnly)[taker];
        var rest = (Vector2?[])spots.Clone();
        rest[taker] = null;
        var saved = expected;
        if (IsBot(taker) && takerSpot is { } to)
        {
            var from = Projected(taker, now + ReactionDelay, now);
            var distance = Vector2.Distance(from, to);
            var at = distance < 0.01f ? from : from + (to - from) / distance * MathF.Min(distance, RunSpeed * MathF.Max(0f, FirstMesohighAt - now - ReactionDelay));
            expected = [.. saved, new Hazard(at, MesohighRadius, FirstMesohighAt)];
        }
        var result = Dodging(now, rest);
        expected = saved;
        result[taker] = takerSpot;
        return Plan(result);
    }

    private IAiMove TanksPopTheOrbOnceThePartyIsClear(float now)
    {
        var spots = new Vector2?[8];
        var tanks = new[] { PartyRole.MainTank, PartyRole.OffTank }
            .Select(role => world.Party.Get(role))
            .Where(tank => tank != null && tank.IsAlive())
            .ToList();
        if (state.UnpoppedOrbs == 0 || tanks.Count == 0) return Plan(spots);
        var arrival = now + ReactionDelay + tanks.Min(tank => MathF.Max(0f, Vector2.Distance(Flat(tank!.Position), Orb) - OrbTouchRadius)) / RunSpeed;
        var lastChance = now >= 25.5f;
        var partyInTheBlast = Enumerable.Range(0, 8)
            .Any(slot => IsBot(slot) && !IsTank(slot) && Vector2.Distance(Projected(slot, arrival, now), Orb) <= OrbBlastClearance);
        if (partyInTheBlast && !lastChance) return Plan(spots);
        spots[(int)PartyRole.MainTank] = MainTankOrb;
        spots[(int)PartyRole.OffTank] = OffTankOrb;
        return Plan(spots);
    }

    private Vector2?[] Dodging(float now, Vector2?[] spots, float reach = ArenaReach)
    {
        var depart = now + ReactionDelay;
        var result = (Vector2?[])spots.Clone();
        var groups = Enumerable.Range(0, 8)
            .Where(slot => spots[slot] != null && IsBot(slot))
            .GroupBy(slot => (Spot: spots[slot]!.Value, Tank: IsTank(slot)));
        foreach (var group in groups)
        {
            var saved = expected;
            if (state.UnpoppedOrbs > 0 && !group.Key.Tank) expected = [.. saved, new Hazard(Orb, OrbBlastRadius, null)];
            var found = NearestClear(group.Key.Spot, group.Select(slot => Projected(slot, depart, now)).ToList(), now, depart, reach);
            expected = saved;
            if (found == null) unresolved.UnionWith(group);
            foreach (var slot in group) result[slot] = found ?? group.Key.Spot;
        }
        return result;
    }

    private Vector2? NearestClear(Vector2 preferred, List<Vector2> froms, float now, float depart, float reach)
    {
        foreach (var margin in new[] { Margin, TightMargin })
        {
            Vector2? best = null;
            var bestShift = float.MaxValue;
            for (var i = -MaxShiftSteps; i <= MaxShiftSteps; i++)
                for (var j = -MaxShiftSteps; j <= MaxShiftSteps; j++)
                {
                    var offset = new Vector2(i, j) * SearchStep;
                    var shift = offset.Length();
                    var spot = preferred + offset;
                    if (shift > MaxShiftSteps * SearchStep || shift >= bestShift || spot.Length() > reach) continue;
                    if (!froms.All(from => ClearOnTheWay(from, spot, now, depart, margin))) continue;
                    best = spot;
                    bestShift = shift;
                }
            if (best != null) return best;
        }
        return null;
    }

    private bool ClearOnTheWay(Vector2 from, Vector2 to, float now, float depart, float margin)
    {
        var distance = Vector2.Distance(from, to);
        var direction = distance > 0.01f ? (to - from) / distance : Vector2.Zero;
        foreach (var hazard in UpcomingHazards(now))
        {
            var along = hazard.LandsAt is { } landsAt
                ? MathF.Min(distance, PlanSpeed * MathF.Max(0f, landsAt - depart))
                : Math.Clamp(Vector2.Dot(hazard.Center - from, direction), 0f, distance);
            if (Vector2.Distance(from + direction * along, hazard.Center) <= hazard.Radius + margin) return false;
        }
        return true;
    }

    private IEnumerable<Hazard> UpcomingHazards(float now) =>
        state.Puddles
            .Where(puddle => puddle.LandsAt > now)
            .Select(puddle => new Hazard(puddle.Center, puddle.Radius, puddle.LandsAt))
            .Concat(expected);

    private Vector2 Projected(int slot, float at, float now)
    {
        var position = Flat(world.Party.Get(slot)!.Position);
        if (!IsBot(slot) || planned[slot] is not { } heading) return position;
        var distance = Vector2.Distance(position, heading);
        return distance < 0.01f ? position : position + (heading - position) / distance * MathF.Min(distance, RunSpeed * MathF.Max(0f, at - now));
    }

    private bool IsBot(int slot) => world.Party.Get(slot) is { } member && world.Party.IsBotDriven(member) && member.IsAlive();

    private bool IsLivePlayer(int slot) => world.Party.Get(slot) is { } member && !world.Party.IsBotDriven(member) && member.IsAlive();

    private static bool IsTank(int slot) => slot is (int)PartyRole.MainTank or (int)PartyRole.OffTank;

    private IAiMove Plan(Vector2?[] spots)
    {
        for (var slot = 0; slot < 8; slot++)
            if (spots[slot] is { } spot) planned[slot] = spot;
        return AiMove.Create(spots).NaturalOrder();
    }

    private PartyRole OtherHealer =>
        state.SearingWindTarget == PartyRole.RegenHealer ? PartyRole.ShieldHealer : PartyRole.RegenHealer;

    private Vector2?[] SearingWindHoldsStill(Vector2?[] spots)
    {
        spots[(int)state.SearingWindTarget] = null;
        return spots;
    }

    private Vector2?[] BothHealersWest(Vector2?[] spots)
    {
        spots[(int)OtherHealer] = HealersWest;
        return spots;
    }

    private static Vector2?[] Everyone(Vector2 spot) => Enumerable.Repeat<Vector2?>(spot, 8).ToArray();

    private static Vector2?[] Only(PartyRole role, Vector2 spot)
    {
        var spots = new Vector2?[8];
        spots[(int)role] = spot;
        return spots;
    }

    private Vector2?[] Group(Vector2? group, Vector2? mainTank, Vector2? offTank, Vector2? searingWind = null, Vector2? firstMesohigh = null)
    {
        var spots = new Vector2?[8];
        for (var i = 0; i < spots.Length; i++)
            spots[i] = group;
        spots[(int)PartyRole.MainTank] = mainTank;
        spots[(int)PartyRole.OffTank] = offTank;
        spots[(int)state.SearingWindTarget] = searingWind ?? group;
        spots[(int)state.FirstMesohighTaker] = firstMesohigh ?? group;
        return spots;
    }

    private static Vector2 Flat(Vector3 position) => new(position.X, position.Z);
}
