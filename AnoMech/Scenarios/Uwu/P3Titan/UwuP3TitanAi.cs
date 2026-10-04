using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Uwu.P3Titan;

public sealed class UwuP3TitanAi : IScenarioAi<UwuP3TitanState>
{
    public string Name => "Zeal / The Balance";

    private const float RunSpeed = 6f;
    private const float FastestRun = 6.5f;
    private const float LateFrame = 1f / 60f;
    private const float Margin = 1f;
    private const float GroupSpread = 0.5f;
    private const float SearchStep = 0.6f;
    private const float LandslideLength = 40f;
    private const float UpheavalStandOff = 3.8f;
    private const float TankStandOff = 2.5f;
    private const float PlanStep = 0.3f;
    private const float TightSpread = 0.3f;
    private const float ReactionDelay = 0f;
    private const float TimeToDodgeLater = 1.3f;
    private const float TooCloseToPutOff = 2.5f;
    private const float StackedMargin = 0.3f;
    private const float StackedSpread = 0.1f;
    private const float WedgeRunMargin = 0.3f;

    private static readonly Vector2 TitansLeftSide = new(12f, -5.8f);
    private static readonly Vector2 TitansRightSide = new(12f, 5.8f);

    private UwuP3TitanState state = null!;
    private Vector2? groupTarget;
    private float groupTargetChosenAt;
    private float groupSpread = GroupSpread;
    private (float SecondHitAt, Vector2?[]? Spots, bool[] DodgesSecondHitLater)? wedgePlan;
    private SimWorld world = null!;
    private readonly Vector2?[] headingFor = new Vector2?[8];
    private readonly float[] headingSince = new float[8];

    public void Run(UwuP3TitanState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        groupTarget = null;
        wedgePlan = null;
        world = worldParam;
        ForgetWhereBotsAreHeading();
        var ai = new AiManager(world);

        ai.Move(0.5f, () => Group(new Vector2(0f, 16.5f)));
        ai.Move(6.0f, () => TankAndParty(new Vector2(0f, 5.5f), new Vector2(0f, -5f)));
        ai.Move(24.5f, () => TankAndParty(new Vector2(-7f, 0f), new Vector2(7f, 0f)));
        ai.Move(27.5f, () => TankAndParty(new Vector2(0f, 5.5f), new Vector2(0f, -5f)));

        ai.Move(30.8f, () => Group(OppositeFirstJump(13.5f)));
        ai.Move(36.0f, () => Group(state.FromJumpFrame(new Vector2(14f - UpheavalStandOff, 0f)), 0.1f), jitter: 0f);
        ai.Move(40.1f, () => Group(UpheavalStandingSpot(), 0.1f), jitter: 0f);
        ai.Automarker(46.31f, MarkTheGaolsInOrder);
        ai.Automarker(58.0f, () => []);
        ai.Automarker(92.45f, () => new() { [state.JailedHealer] = Sign.Attack1 });
        ai.Automarker(103.5f, () => []);
        ai.Move(46.8f, JailedBesideTheirGaolSpotsOutOfTheLandslide, jitter: 0f);
        ai.Move(46.8f, BaitTheGaolWindowLandslideThroughTheMiddle, jitter: 0f);
        ai.Move(48.6f, () => PartyTo(new Vector2(-0.8f, -6.1f), withGaolTargets: false), jitter: 0f);
        ai.Move(48.6f, () => MainTankTo(new Vector2(-11f, -5f * state.SafeSide)));
        ai.Move(50.70f, JailedIntoTheChain, jitter: 0f, sprint: true);
        ai.Move(50.75f, () => PartyTo(new Vector2(9.5f, -10.2f), withGaolTargets: false), jitter: 0f);
        ai.Move(50.8f, () => MainTankTo(new Vector2(-11f, 0f)));
        ai.Move(53.05f, () => MainTankTo(new Vector2(-8f, -6f * state.SafeSide)));
        ai.Move(57.0f, () => MainTankTo(new Vector2(12.5f, 0f)), jitter: 0f);
        ai.Move(66.6f, () => MainTankTo(new Vector2(8f, 0f)), jitter: 0f);
        ai.Move(57.8f, () => PartyTo(TitansLeftSide, withGaolTargets: true), jitter: 0f);

        ai.Move(70.3f, () => PartyTo(TitansRightSide, withGaolTargets: true), jitter: 0f);
        ai.Move(70.3f, () => MainTankTo(new Vector2(1f, 0f)), jitter: 0f);
        ai.Move(73.3f, () => PartyTo(new Vector2(4f, 8.5f), withGaolTargets: true), jitter: 0f);
        ai.Move(73.3f, () => MainTankTo(new Vector2(8f, 0f)), jitter: 0f);
        PlanEvery(ai, 76.1f, 80.3f, _ => []);

        ai.Move(84.9f, () => Group(OppositeSecondJump(10f)));
        ai.Move(90.1f, () => Group(OppositeSecondJump(8.5f)));
        ai.Move(92.5f, PartyInFrontHolderBehindTitan, jitter: 0f);
        PlanEvery(ai, 104.9f, 109.4f, _ => []);
        ai.Move(109.5f, PartyInFrontHolderBehindTitan, jitter: 0f);
        ai.Move(116.0f, OffTankBehindTitanForTheBuster, jitter: 0f);
        ai.Move(126.0f, PartyBehindTitanRangedInFront, jitter: 0f);
        PlanEvery(ai, 128.3f, 141.0f, _ => [], holderJoinsTheGroup: true);
        PlanEvery(ai, 141.0f, 147.9f, _ => [(int)PartyRole.MainTank]);
        ai.Move(141.0f, () => TankOppositeTheParty(PartyRole.MainTank));
        ai.Move(148.1f, () => Group(Vector2.Zero));
    }

    private IAiMove PartyInFrontHolderBehindTitan()
    {
        groupTarget = null;
        var spots = new Vector2?[8];
        var front = FromSecondJumpFrame(new Vector2(-4f, 0f));
        for (var slot = 0; slot < 8; slot++) spots[slot] = front + SpreadOffset(slot, TightSpread);
        spots[(int)PartyRole.CasterDps] = FromSecondJumpFrame(new Vector2(-10f, 0f));
        spots[(int)PartyRole.MainTank] = FromSecondJumpFrame(new Vector2(5f, 0f));
        foreach (var jailed in state.Jailed) spots[(int)jailed] = null;
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove OffTankBehindTitanForTheBuster()
    {
        var spots = new Vector2?[8];
        spots[(int)PartyRole.OffTank] = FromSecondJumpFrame(new Vector2(5.5f, 0f));
        spots[(int)PartyRole.MainTank] = FromSecondJumpFrame(new Vector2(-3f, 0f));
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove PartyBehindTitanRangedInFront()
    {
        groupTarget = null;
        var spots = new Vector2?[8];
        var behind = FromSecondJumpFrame(new Vector2(10f, 0f));
        for (var slot = 0; slot < 8; slot++) spots[slot] = behind + SpreadOffset(slot, TightSpread);
        spots[(int)PartyRole.CasterDps] = FromSecondJumpFrame(new Vector2(-10f, 0f));
        spots[(int)PartyRole.RegenHealer] = FromSecondJumpFrame(new Vector2(-10f, 1f));
        return AiMove.Create(spots).NaturalOrder();
    }

    private void PlanEvery(AiManager ai, float from, float to, Func<float, int[]> excluded, bool holderJoinsTheGroup = false)
    {
        for (var t = from; t < to; t += PlanStep)
        {
            var at = t;
            var first = t == from;
            ai.Move(at, () =>
            {
                if (first) ForgetWhereBotsAreHeading();
                return GroupDodgesKnownHazards(at, excluded(at), holderJoinsTheGroup);
            }, jitter: 0f);
        }
    }

    private static IAiMove Nobody() => AiMove.Create(new Vector2?[8]).NaturalOrder();

    private IAiMove Group(Vector2 anchor, float spread = GroupSpread)
    {
        groupTarget = null;
        var spots = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++) spots[slot] = anchor + SpreadOffset(slot, spread);
        return AiMove.Create(spots).NaturalOrder();
    }

    private static Vector2 SpreadOffset(int slot, float spread)
    {
        var angle = slot * MathF.PI / 4f;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spread;
    }

    private static IAiMove Only(PartyRole role, Vector2 spot)
    {
        var spots = new Vector2?[8];
        spots[(int)role] = spot;
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove TankAndParty(Vector2 tank, Vector2 party)
    {
        var spots = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++) spots[slot] = party + SpreadOffset(slot, GroupSpread);
        spots[(int)PartyRole.MainTank] = tank;
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 OppositeFirstJump(float radius) => Flat(UwuP3TitanState.AtBearing(state.FirstJumpBearing + 180f, radius));

    private Vector2 OppositeSecondJump(float radius) => Flat(UwuP3TitanState.AtBearing(state.SecondJumpBearing + 180f, radius));

    private Vector2 UpheavalStandingSpot()
    {
        var titan = new Vector2(14f, 0f);
        var landing = new Vector2(-13.5f, 4f * state.SafeSide);
        return state.FromJumpFrame(titan + Vector2.Normalize(landing - titan) * UpheavalStandOff);
    }

    private IAiMove PartyTo(Vector2 spot, bool withGaolTargets, bool jumpFrame = true)
    {
        groupTarget = null;
        var anchor = jumpFrame ? state.FromJumpFrame(spot) : spot;
        var spots = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++)
        {
            var role = (PartyRole)slot;
            if (role == PartyRole.MainTank) continue;
            if (!withGaolTargets && state.GaolTargets.Contains(role)) continue;
            spots[slot] = anchor + SpreadOffset(slot, TightSpread);
        }
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 FromSecondJumpFrame(Vector2 eastFrame)
    {
        var (sin, cos) = MathF.SinCos((state.SecondJumpBearing - 90f) * MathF.PI / 180f);
        return new Vector2(eastFrame.X * cos - eastFrame.Y * sin, eastFrame.X * sin + eastFrame.Y * cos);
    }

    private Dictionary<PartyRole, Sign> MarkTheGaolsInOrder() =>
        state.GaolTargets.Select((role, order) => (role, order)).ToDictionary(x => x.role, x => Sign.Attack1 + x.order);

    private IAiMove MainTankTo(Vector2 jumpFrameSpot) => Only(PartyRole.MainTank, state.FromJumpFrame(jumpFrameSpot));

    private IAiMove BaitTheGaolWindowLandslideThroughTheMiddle()
    {
        var spots = new Vector2?[8];
        var onTheAxis = state.FromJumpFrame(new Vector2(-11f, 0f));
        for (var slot = 0; slot < 8; slot++)
            if (!state.GaolTargets.Contains((PartyRole)slot)) spots[slot] = onTheAxis + SpreadOffset(slot, TightSpread);
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 BesideGaolSpot(int order) => state.FromJumpFrame(order switch
    {
        0 => new Vector2(5.5f, 3.5f * state.SafeSide),
        1 => new Vector2(0f, 3.8f * state.SafeSide),
        _ => new Vector2(-6.7f, 3.8f * state.SafeSide),
    });

    private IAiMove JailedBesideTheirGaolSpotsOutOfTheLandslide()
    {
        var spots = new Vector2?[8];
        for (var i = 0; i < state.GaolTargets.Count; i++) spots[(int)state.GaolTargets[i]] = BesideGaolSpot(i);
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove JailedIntoTheChain()
    {
        var spots = new Vector2?[8];
        for (var i = 0; i < state.GaolTargets.Count; i++) spots[(int)state.GaolTargets[i]] = state.GaolSpot(i);
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove TankOppositeTheParty(PartyRole tank)
    {
        var others = Enumerable.Range(0, 8).Where(slot => slot != (int)tank).Select(world.Party.Get).OfType<SimCharacter>().Where(m => m.IsAlive()).ToList();
        if (others.Count == 0) return Nobody();
        var titan = Flat(state.TitanPosition);
        var party = others.Aggregate(Vector2.Zero, (sum, m) => sum + Flat(m.Position)) / others.Count;
        var away = titan - party;
        var direction = away.Length() < 0.1f ? new Vector2(0f, -1f) : Vector2.Normalize(away);
        return Only(tank, ClampToArena(titan + direction * TankStandOff, UwuP3TitanState.SecondShrinkRadius - 1f));
    }

    private IAiMove GroupDodgesKnownHazards(float now, int[] excluded, bool holderJoinsTheGroup)
    {
        if (state.AwakenedLandslide is { } landslide && now < landslide.SecondHitAt
            && WedgePlanDecidedOncePerCast(landslide, now, excluded) is { } wedge)
        {
            groupTarget = null;
            for (var slot = 0; slot < 8; slot++)
                if (wedge[slot] is { } spot) HeadFor(slot, spot, now);
            return AiMove.Create((Vector2?[])wedge.Clone()).NaturalOrder();
        }
        var holder = (int)PartyRole.MainTank;
        var members = Enumerable.Range(0, 8)
            .Where(slot => (holderJoinsTheGroup || slot != holder) && !excluded.Contains(slot) && !state.Jailed.Contains((PartyRole)slot))
            .Select(slot => (slot, member: world.Party.Get(slot)))
            .Where(x => x.member is { } m && m.IsAlive())
            .ToList();
        var spots = new Vector2?[8];
        if (members.Count == 0) return AiMove.Create(spots).NaturalOrder();
        var anchor = CentreOfTheBots(members.Select(x => x.member!).ToList());
        var upcoming = state.Hazards.Where(h => h.At > now).OrderBy(h => h.At).ToList();
        var stillReachable = groupTarget is { } kept && kept.Length() <= ArenaRadiusAt(now) - 1f - GroupSpread
            && ClearOfHazardsOnTheWay(anchor, kept, now, Imminent(upcoming, now), GroupSpread + 0.2f,
                reaction: MathF.Max(0f, groupTargetChosenAt + ReactionDelay - now));
        if (!stillReachable)
        {
            groupTarget = NearestSpotClearOfUpcomingHazards(anchor, now, Margin + GroupSpread, out var stacked);
            groupTargetChosenAt = now;
            groupSpread = stacked ? StackedSpread : GroupSpread;
        }
        var target = groupTarget!.Value;
        var imminent = Imminent(upcoming, now);
        foreach (var (slot, member) in members)
        {
            var at = Flat(member!.Position);
            var spot = target + SpreadOffset(slot, groupSpread);
            if (!ReachesSafely(slot, at, spot, now, imminent, WedgeRunMargin))
                spot = headingFor[slot] is { } heading && ReachesSafely(slot, at, heading, now, imminent, 0f)
                    ? heading
                    : NearestSpotClearOfUpcomingHazards(WhereItTurns(slot, at), now, Margin);
            spots[slot] = HeadFor(slot, spot, now);
        }
        if (!holderJoinsTheGroup && !excluded.Contains(holder) && world.Party.Get(holder) is { } tank && tank.IsAlive())
            spots[holder] = state.Hazards.Any(h => h.At > now)
                ? NearestSpotClearOfUpcomingHazards(Flat(tank.Position), now, Margin)
                : BesideTitanAwayFrom(target, now);
        return AiMove.Create(spots).NaturalOrder();
    }

    private List<(int Slot, SimCharacter Member)> WedgeMembers(int[] excluded) =>
        Enumerable.Range(0, 8)
            .Where(slot => !excluded.Contains(slot) && !state.Jailed.Contains((PartyRole)slot))
            .Select(slot => (slot, member: world.Party.Get(slot)))
            .Where(x => x.member is { } m && m.IsAlive())
            .Select(x => (x.slot, x.member!))
            .ToList();

    private Vector2?[]? WedgePlanDecidedOncePerCast(UwuP3TitanState.AwakenedLandslideCast landslide, float now, int[] excluded)
    {
        if (wedgePlan is { } decided && decided.SecondHitAt == landslide.SecondHitAt)
        {
            if (decided.Spots != null && !state.Hazards.Any(h => h.At > now && h.At < landslide.SecondHitAt))
                ReplanThoseDodgingTheSecondHitLater(decided.Spots, decided.DodgesSecondHitLater, now);
            return decided.Spots;
        }
        var members = WedgeMembers(excluded);
        Vector2?[]? spots = null;
        var dodgesLater = new bool[8];
        if (members.Count > 0)
        {
            var anchor = CentreOfTheBots(members.Select(x => x.Member).ToList());
            if (NearestSpotInAWedgeBothHitsMiss(landslide, anchor, now) is { } wedge)
            {
                spots = new Vector2?[8];
                var upcoming = state.Hazards.Where(h => h.At > now).ToList();
                foreach (var (slot, member) in members)
                {
                    var at = Flat(member.Position);
                    var spot = wedge + SpreadOffset(slot, TightSpread);
                    if (ClearOfHazardsOnTheWay(at, spot, now, upcoming, WedgeRunMargin))
                        spots[slot] = spot;
                    else
                    {
                        spots[slot] = NearestSpotClearOfUpcomingHazards(at, now, Margin);
                        dodgesLater[slot] = true;
                    }
                }
            }
        }
        wedgePlan = (landslide.SecondHitAt, spots, dodgesLater);
        return spots;
    }

    private void ReplanThoseDodgingTheSecondHitLater(Vector2?[] spots, bool[] dodgesLater, float now)
    {
        for (var slot = 0; slot < 8; slot++)
        {
            if (!dodgesLater[slot]) continue;
            dodgesLater[slot] = false;
            if (world.Party.Get(slot) is { } member && member.IsAlive())
                spots[slot] = NearestSpotClearOfUpcomingHazards(Flat(member.Position), now, Margin);
        }
    }

    private Vector2? NearestSpotInAWedgeBothHitsMiss(UwuP3TitanState.AwakenedLandslideCast landslide, Vector2 from, float now)
    {
        var upcoming = state.Hazards.Where(h => h.At > now).ToList();
        var reach = ArenaRadiusAt(now) - 1f - TightSpread;
        Vector2? best = null;
        var bestDistance = float.MaxValue;
        foreach (var degrees in new[] { 67.5f, -67.5f, 112.5f, -112.5f })
        {
            var rotation = landslide.Rotation + degrees * MathF.PI / 180f;
            var direction = new Vector2(MathF.Sin(rotation), MathF.Cos(rotation));
            for (var radius = 8.5f; radius <= 14f; radius += 0.5f)
            {
                var spot = landslide.Origin + direction * radius;
                if (spot.Length() > reach) break;
                var distance = Vector2.Distance(from, spot);
                if (distance >= bestDistance || !ClearOfHazardsOnTheWay(from, spot, now, upcoming, TightSpread + 0.3f)) continue;
                best = spot;
                bestDistance = distance;
                break;
            }
        }
        return best;
    }

    private void ForgetWhereBotsAreHeading() => Array.Clear(headingFor);

    private Vector2 HeadFor(int slot, Vector2 spot, float now)
    {
        if (headingFor[slot] is not { } heading || Vector2.Distance(heading, spot) >= 0.1f)
        {
            headingFor[slot] = spot;
            headingSince[slot] = now;
        }
        return spot;
    }

    private Vector2 WhereItTurns(int slot, Vector2 at)
    {
        if (headingFor[slot] is not { } heading) return at;
        var toHeading = heading - at;
        var distance = toHeading.Length();
        return distance > 0.01f ? at + toHeading / distance * MathF.Min(distance, FastestRun * ReactionDelay) : at;
    }

    private bool ReachesSafely(int slot, Vector2 at, Vector2 spot, float now, List<UwuP3TitanState.Hazard> hazards, float margin)
    {
        var slowest = margin == 0f ? FastestRun : RunSpeed;
        if (headingFor[slot] is not { } heading)
            return ClearOfHazardsOnTheWay(at, spot, now, hazards, margin, slowest: slowest);
        if (Vector2.Distance(heading, spot) < 0.1f)
            return ClearOfHazardsOnTheWay(at, spot, now, hazards, margin, reaction: MathF.Max(0f, headingSince[slot] + ReactionDelay - now), slowest: slowest);
        return ClearOfHazardsOnTheWay(WhereItTurns(slot, at), spot, now + ReactionDelay, hazards, margin, reaction: 0f, slowest: slowest)
            && ClearOfHazardsOnTheWay(at, spot, now, hazards, margin, reaction: 0f, slowest: slowest);
    }

    private static Vector2 CentreOfTheBots(List<SimCharacter> members)
    {
        var bots = members.Where(m => m is not SimPlayer).ToList();
        var counted = bots.Count > 0 ? bots : members;
        return counted.Aggregate(Vector2.Zero, (sum, m) => sum + Flat(m.Position)) / counted.Count;
    }

    private Vector2 BesideTitanAwayFrom(Vector2 party, float now)
    {
        var titan = Flat(state.TitanPosition);
        var away = titan - party;
        var direction = away.Length() < 0.1f ? new Vector2(0f, -1f) : Vector2.Normalize(away);
        return ClampToArena(titan + direction * TankStandOff, ArenaRadiusAt(now) - 1f);
    }

    private Vector2 NearestSpotClearOfUpcomingHazards(Vector2 from, float now, float margin) =>
        NearestSpotClearOfUpcomingHazards(from, now, margin, out _);

    private Vector2 NearestSpotClearOfUpcomingHazards(Vector2 from, float now, float margin, out bool stacked)
    {
        var upcoming = state.Hazards.Where(h => h.At > now).OrderBy(h => h.At).ToList();
        var reach = ArenaRadiusAt(now) - 1f - GroupSpread;
        var tightest = MathF.Min(margin, GroupSpread + 0.25f);
        var dodgeable = new[] { upcoming.Count }.Concat(CutsLeavingTimeToDodgeTheRest(upcoming, now)).ToList();
        var tiers = dodgeable.SelectMany(count => new[] { (count, margin, margin), (count, tightest, tightest) })
            .Concat(dodgeable.Select(count => (count, StackedMargin, tightest)))
            .Concat(dodgeable.Select(count => (count, 0f, 0f)))
            .Concat(Enumerable.Range(0, upcoming.Count).Reverse().SelectMany(count => new[] { (count, margin, margin), (count, tightest, tightest) }));
        foreach (var (count, circleMargin, laneMargin) in tiers)
            if (NearestClearSpot(from, now, upcoming.Take(count).ToList(), circleMargin, laneMargin, reach,
                    circleMargin == 0f ? FastestRun : RunSpeed) is { } found)
            {
                stacked = circleMargin < tightest;
                return found;
            }
        stacked = false;
        return from;
    }

    private static List<UwuP3TitanState.Hazard> Imminent(List<UwuP3TitanState.Hazard> byTime, float now)
    {
        var cut = CutsLeavingTimeToDodgeTheRest(byTime, now).DefaultIfEmpty(byTime.Count).Min();
        return byTime.Take(cut).ToList();
    }

    private static IEnumerable<int> CutsLeavingTimeToDodgeTheRest(List<UwuP3TitanState.Hazard> byTime, float now)
    {
        for (var count = byTime.Count - 1; count >= 1; count--)
            if (byTime[count].At - byTime[count - 1].At >= TimeToDodgeLater && byTime[count].At >= now + TooCloseToPutOff)
                yield return count;
    }

    private static Vector2? NearestClearSpot(Vector2 from, float now, List<UwuP3TitanState.Hazard> hazards, float margin, float laneMargin, float reach,
        float slowest = RunSpeed)
    {
        Vector2? best = null;
        var bestDistance = float.MaxValue;
        for (var x = -reach; x <= reach; x += SearchStep)
            for (var z = -reach; z <= reach; z += SearchStep)
            {
                var spot = new Vector2(x, z);
                if (spot.Length() > reach) continue;
                var distance = Vector2.Distance(from, spot);
                if (distance >= bestDistance || !ClearOfHazardsOnTheWay(from, spot, now, hazards, margin, laneMargin, slowest: slowest)) continue;
                best = spot;
                bestDistance = distance;
            }
        return best;
    }

    private static bool ClearOfHazardsOnTheWay(Vector2 from, Vector2 to, float now, IEnumerable<UwuP3TitanState.Hazard> hazards, float margin,
        float? laneMargin = null, float reaction = ReactionDelay, float slowest = RunSpeed)
    {
        var distance = Vector2.Distance(from, to);
        var direction = distance > 0.01f ? (to - from) / distance : Vector2.Zero;
        foreach (var hazard in hazards)
        {
            var running = MathF.Max(0f, hazard.At - now - reaction);
            var nearest = MathF.Min(distance, slowest * running);
            var farthest = hazard.At > now ? MathF.Min(distance, FastestRun * (hazard.At - now + LateFrame)) : nearest;
            var closest = Math.Clamp(Vector2.Dot(hazard.Origin - from, direction), nearest, farthest);
            var hazardMargin = hazard.IsLane ? laneMargin ?? margin : margin;
            if (IsInside(from + direction * nearest, hazard, hazardMargin)
                || IsInside(from + direction * farthest, hazard, hazardMargin)
                || !hazard.IsLane && IsInside(from + direction * closest, hazard, hazardMargin)) return false;
        }
        return true;
    }

    private static bool IsInside(Vector2 point, UwuP3TitanState.Hazard hazard, float margin)
    {
        if (!hazard.IsLane) return Vector2.Distance(point, hazard.Origin) <= hazard.Size + margin;
        var rad = hazard.Bearing * MathF.PI / 180f;
        var forward = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));
        var offset = point - hazard.Origin;
        var along = Vector2.Dot(offset, forward);
        if (along < -margin || along > LandslideLength) return false;
        return MathF.Abs(offset.X * forward.Y - offset.Y * forward.X) <= hazard.Size + margin;
    }

    private static float ArenaRadiusAt(float now) =>
        now < 32.6f ? 19.4f : now < 87f ? UwuP3TitanState.FirstShrinkRadius : UwuP3TitanState.SecondShrinkRadius;

    private static Vector2 ClampToArena(Vector2 spot, float limit)
    {
        return spot.Length() > limit ? Vector2.Normalize(spot) * limit : spot;
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
}
