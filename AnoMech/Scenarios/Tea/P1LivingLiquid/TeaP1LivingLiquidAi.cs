using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Scenarios.Tea.P1LivingLiquid;

public sealed class TeaP1LivingLiquidAi : IScenarioAi<TeaP1LivingLiquidState>
{
    public string Name => "Living Liquid APT";

    private const float TornadoBaitDistance = 8.8f;
    private const float TornadoArcDistance = 9.1f;
    private const float SecondsToRegroup = 3.3f;
    private const float BossesApartForPrayer = 21f;
    private const byte HandOfPrayerTell = 19;
    private const byte HandOfPartingTell = 20;
    private const float OrbPathBeforePhaseEnds = 24.5f;
    private const float DollFeedHp = 0.25f;
    private const float SecondsBetweenLooks = 0.3f;
    private const float FeedFromBossCentre = 0.5f;
    private const float FeedOutsideBossFacing = 60f;
    private const float DollTooCloseToBoss = 5.5f;
    private const float HoldClearOfBosses = 9.8f;
    private const float EmbolusLookahead = 6f;
    private const float LineUpAt = 58f;
    private const float LastDollLook = 65f;
    private const float HitboxMargin = 0.5f;
    private const float SpotSpacing = 1f;
    private const float ClearOfTornadoBaits = 9.5f;

    private TeaP1LivingLiquidState state = null!;
    private SimWorld world = null!;
    private AiManager ai = null!;
    private float proteanFacing;

    private float TankRadius => world.Party.Get(MainTank)?.HitboxRadius ?? 0.5f;

    private TornadoT First => state.FirstTornadoes;
    private TornadoT Second => state.SecondTornadoes;

    public void Run(TeaP1LivingLiquidState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        ai = new AiManager(world);

        ai.Move(0f, StandSouthOfCentre);
        ai.Move(3.0f, () => AiMove.Single(MainTank, new Vector2(3.9f, 0f)));
        ai.Move(4.0f, () => AiMove.Single(MainTank, new Vector2(0f, -3.9f)));

        world.Events.Add(20.3f, () =>
        {
            ai.Move(0f, MakeWayForTheBosses, jitter: 0f);
            world.Events.Add(0f, () => RouteAroundBosses(OffTank, null, OffTank, RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps));
        });
        Walk(22.8f, MainTank, OutToTheSouthTornado);
        ai.Move(24.8f, SpreadForFirstTornadoes);
        world.Events.Add(24.8f, () => RouteAroundBosses(OffTank, null, PhysRangedDps, CasterDps, RegenHealer, ShieldHealer));
        world.Events.Add(25.5f, () =>
        {
            ai.Move(0f, OffTankOnTheHandsFarSide, jitter: 0f);
            world.Events.Add(0f, () => RouteAroundBosses(OffTank, null, OffTank));
        });
        ai.Move(26.8f, TakeDollSpotsAndHoldTheHandNorth);
        world.Events.Add(26.8f, TakeTheHandNorthPastTheParty);

        ai.UseAction(29.85f, MainTank, Enmity.ProvokeActionId, () => HandTellsParting() ? state.Objects.LiquidHand : null);
        ai.Move(30.2f, () => AiMove.Single(OffTank, First.Abs(new Vector2(0f, -11f))));
        Walk(31.4f, MainTank, () => [Bait(First, TornadoT.South, -72.5f, TornadoArcDistance), Bait(First, TornadoT.South, -60f)]);
        Walk(32.5f, MainTank, () => [Bait(First, TornadoT.South, -72.5f, TornadoArcDistance), Bait(First, TornadoT.South, -100f)]);
        Walk(32.5f, PhysRangedDps, () => [Bait(First, TornadoT.East, 72.5f, TornadoArcDistance), Bait(First, TornadoT.East, 85f)]);
        Walk(32.5f, CasterDps, () => [Bait(First, TornadoT.West, -72.5f, TornadoArcDistance), Bait(First, TornadoT.West, -85f)]);
        for (var k = 0; k < TeaP1LivingLiquidState.DollBearings.Length; k++)
        {
            var doll = k;
            ai.UseAction(FirstHitOnDoll(DollTaker(doll)), DollTaker(doll), JobActions.AutoAttackActionId, () => state.Objects.Dolls[doll]);
        }
        ai.Move(33.6f, BackBesideTheirDolls);
        world.Events.Add(33.6f, () => RouteAroundBosses(MainTank, null, MeleeDpsA, MeleeDpsB));
        ai.Move(37.6f, () => ByRole((MeleeDpsA, MeleeOnDoll(MeleeDpsA)), (MeleeDpsB, MeleeOnDoll(MeleeDpsB))));

        ai.Move(38.65f, () => ByRole((RegenHealer, First.Abs(new Vector2(-1.5f, -4.5f))), (ShieldHealer, First.Abs(new Vector2(1.5f, -4.5f)))));
        CarryDollsToABoss(MeleeDpsA, 38.65f, () => First.Abs(new Vector2(-3f, -9f)), feedFrom: 38.65f, backFrom: 46.5f, () => First.Abs(new Vector2(-2f, -4f)));
        CarryDollsToABoss(MeleeDpsB, 38.65f, () => First.Abs(new Vector2(3f, -9f)), feedFrom: 38.65f, backFrom: 46.5f, () => First.Abs(new Vector2(2f, -4f)));
        CarryDollsToABoss(PhysRangedDps, 38.65f, null, feedFrom: 49.3f, backFrom: 53.6f, () => First.Abs(new Vector2(2f, -3f)));
        CarryDollsToABoss(CasterDps, 38.65f, null, feedFrom: 49.3f, backFrom: 53.6f, () => First.Abs(new Vector2(-2f, -3f)));
        ai.UseAction(40.5f, OffTank, Enmity.ProvokeActionId, () => state.Objects.LiquidHand);
        ai.Move(40.6f, () => AiMove.Single(OffTank, First.Abs(new Vector2(-1f, -2f))));
        Walk(41.3f, MainTank, () => [Bait(First, TornadoT.South, -72.5f, TornadoArcDistance), Bait(First, TornadoT.South, -60f), WhereLivingLiquidStopsAtCentre()]);

        ai.Move(53.8f, () => ByRole((MainTank, AroundLivingLiquid(First, 135f, 3.9f)), (OffTank, AroundLiquidHand(First, 225f, 2f))));
        ai.Move(LineUpAt, LineUpNorthOfLivingLiquid);
        world.Events.Add(65.85f, NoteLivingLiquidFacing);
        ai.Move(65.9f, DodgeFirstVisibleProteans);
        ai.Move(70.95f, SwapInForSecondWave);

        ai.Move(74.1f, () => InFrame(First,
            (MainTank, 2.8f, 2.8f), (OffTank, -4f, 7f), (MeleeDpsA, -1.5f, -6.5f), (MeleeDpsB, 1.5f, -6.5f),
            (RegenHealer, -1.5f, -8f), (ShieldHealer, 1.5f, -8f), (CasterDps, -3f, -9f), (PhysRangedDps, 3f, -9f)));
        ai.Move(85.8f, () => InFrame(First,
            (OffTank, -3.5f, 2f), (MeleeDpsA, -1.5f, -3f), (MeleeDpsB, 1.5f, -3f), (RegenHealer, -1.5f, -4.5f),
            (ShieldHealer, 1.5f, -4.5f), (CasterDps, -3f, -4f), (PhysRangedDps, 3f, -4f)));

        ai.Move(92.7f, LineUpForSecondProteans);
        ai.UseAction(95.98f, RegenHealer, Esuna.ActionId, CleanseTopDown);
        ai.UseAction(96.08f, ShieldHealer, Esuna.ActionId, CleanseBottomUp);
        ai.UseAction(98.48f, RegenHealer, Esuna.ActionId, CleanseTopDown);
        ai.UseAction(98.58f, ShieldHealer, Esuna.ActionId, CleanseBottomUp);
        ai.UseAction(100.98f, RegenHealer, Esuna.ActionId, CleanseTopDown);
        ai.UseAction(101.08f, ShieldHealer, Esuna.ActionId, CleanseBottomUp);
        world.Events.Add(103.05f, NoteLivingLiquidFacing);
        ai.Move(103.1f, () => ByRole(
            (MainTank, AroundFacing(45f, 3f)), (MeleeDpsB, AroundFacing(90f, 3f)), (OffTank, AroundFacing(260f, 3f)),
            (MeleeDpsA, AroundFacing(225f, 3.5f)), (RegenHealer, AroundFacing(330f, 10f)),
            (CasterDps, Bait(Second, TornadoT.West, -50f)), (ShieldHealer, Bait(Second, TornadoT.South, -50f)),
            (PhysRangedDps, Bait(Second, TornadoT.East, -50f))));
        ai.UseAction(103.48f, RegenHealer, Esuna.ActionId, CleanseTopDown);
        ai.UseAction(103.58f, ShieldHealer, Esuna.ActionId, CleanseBottomUp);
        ai.Move(106.1f, () => AiMove.Single(MeleeDpsA, AroundFacing(180f, 3f)));
        ai.Move(109.0f, () => ByRole(
            (MainTank, AroundFacing(45f, 4.7f)), (MeleeDpsB, AroundFacing(70f, 6f)), (MeleeDpsA, AroundFacing(155f, 5.5f)),
            (OffTank, AroundFacing(235f, 5f)), (PhysRangedDps, AroundFacing(110f, 2.6f)), (ShieldHealer, AroundFacing(195f, 2.6f)),
            (CasterDps, AroundFacing(270f, 2.6f)), (RegenHealer, AroundFacing(320f, 2.6f))));
        ai.Move(111.25f, () => AiMove.Single(MeleeDpsB, Bait(Second, TornadoT.East, 60f)));
        ai.Move(112.05f, () => ByRole((MeleeDpsA, Bait(Second, TornadoT.South, 60f)), (OffTank, Bait(Second, TornadoT.West, 60f))));

        ai.Move(114.3f, () => InFrame(Second,
            (MainTank, -0.6f, -16.8f), (OffTank, -4.5f, 5f), (RegenHealer, 2f, -3f), (ShieldHealer, 0.5f, -1.5f),
            (PhysRangedDps, 1f, -4f), (CasterDps, 3f, -1.5f), (MeleeDpsA, 4f, 0f), (MeleeDpsB, -1f, -3f)));
        ai.Move(118.95f, () => HandTellsPrayer() ? DragBossesApart() : AiMove.Create().NaturalOrder(), jitter: 0f);
        ai.Move(119.2f, () => HandTellsPrayer() ? DragBossesApart() : RegroupClearOfEmboli(), jitter: 0f);
    }

    private IAiMove StandSouthOfCentre() => ByRole(
        (MainTank, new Vector2(0f, 4f)), (OffTank, new Vector2(-1f, 4.5f)), (RegenHealer, new Vector2(-3f, 4.5f)),
        (ShieldHealer, new Vector2(3f, 4.5f)), (MeleeDpsA, new Vector2(-2.5f, 2.5f)), (MeleeDpsB, new Vector2(2.5f, 2.5f)),
        (PhysRangedDps, new Vector2(1.5f, 5f)), (CasterDps, new Vector2(-1.5f, 5f)));

    private IAiMove SpreadForFirstTornadoes() => ByRole(
        (PhysRangedDps, Bait(First, TornadoT.East, 60f)),
        (CasterDps, Bait(First, TornadoT.West, -60f)),
        (RegenHealer, ClearOfTheHandsWalk(RegenHealer, First.Abs(First.Cardinal ? new Vector2(-3f, -8.5f) : new Vector2(3f, -8.5f)))),
        (ShieldHealer, First.Abs(new Vector2(9.8f, 9.45f))));

    private IAiMove TakeDollSpotsAndHoldTheHandNorth() => ByRole(
        (OffTank, First.Abs(new Vector2(0f, -13f))),
        (MeleeDpsA, ClearOfTheHandsWalk(MeleeDpsA, MeleeBesideDoll(MeleeDpsA))),
        (MeleeDpsB, ClearOfTheHandsWalk(MeleeDpsB, MeleeBesideDoll(MeleeDpsB))));

    private IAiMove BackBesideTheirDolls()
        => ByRole(new[] { MeleeDpsA, MeleeDpsB }.Where(r => ClearOfTheHandsWalk(r, MeleeBesideDoll(r)) != MeleeBesideDoll(r)).Select(r => (r, MeleeBesideDoll(r))).ToArray());

    private Vector2 ClearOfTheHandsWalk(PartyRole role, Vector2 spot)
    {
        var handRadius = state.Objects.LiquidHand?.HitboxRadius ?? 1.6f;
        var memberRadius = world.Party.Get(role)?.HitboxRadius ?? 0.5f;
        var rest = First.Abs(new Vector2(0f, -13f + handRadius + memberRadius + TeaConstants.Geometry.EnemyReach));
        var stop = handRadius + TankRadius + TeaConstants.Geometry.EnemyReach;
        var lanes = new[] { Bait(First, TornadoT.South, -85f), Bait(First, TornadoT.South, -60f), Bait(First, TornadoT.South, -100f) }
            .Select(b => (A: rest, B: b + Vector2.Normalize(rest - b) * stop)).ToList();
        var keep = handRadius + memberRadius + HitboxMargin + 0.3f;
        bool Clear(Vector2 p) => lanes.All(l => DistanceToSegment(p, l.A, l.B) >= keep);
        if (Clear(spot)) return spot;
        Vector2? best = null;
        for (var gx = -12; gx <= 12; gx++)
        for (var gz = -12; gz <= 12; gz++)
        {
            var p = spot + new Vector2(gx, gz) * 0.5f;
            if (!Clear(p) || p.Length() > 18f || TornadoSpots(First).Any(t => Vector2.Distance(p, t) < ClearOfTornadoBaits)) continue;
            if (best is null || Vector2.Distance(p, spot) < Vector2.Distance(best.Value, spot)) best = p;
        }
        return best ?? spot;
    }

    private IAiMove MakeWayForTheBosses()
    {
        if (world.Party.Get(MainTank) is not { } tank || state.Objects.LivingLiquid is not { } livingLiquid || state.Objects.LiquidHand is not { } hand)
            return AiMove.Create().NaturalOrder();
        var livingLiquidAt = Flat(livingLiquid.Position);
        var handAt = Flat(hand.Position);
        var bait = Bait(First, TornadoT.South, -60f);
        var lane = new List<(Vector2 A, Vector2 B)> { (livingLiquidAt, bait) };
        var wayOut = WayOutFromUnderLivingLiquid(Flat(tank.Position), bait);
        if (wayOut.Count > 0) lane.Add((wayOut[^1], bait));
        var partySide = wayOut.Count > 0
            ? -MathF.Sign(Cross(bait - livingLiquidAt, wayOut[^1] - livingLiquidAt))
            : MathF.Sign(Cross(bait - livingLiquidAt, PartyCentre() - livingLiquidAt));
        var members = new[] { OffTank, RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps }
            .Select(r => (Role: r, Member: world.Party.Get(r))).Where(m => m.Member.IsAlive()).ToList();
        var taken = members.ToDictionary(m => m.Role, m => Flat(m.Member!.Position));
        var spots = new Vector2?[8];
        foreach (var (role, member) in members)
        {
            var at = taken[role];
            var keep = livingLiquid.HitboxRadius + member!.HitboxRadius + HitboxMargin + 0.3f;
            var handKeep = hand.HitboxRadius + member.HitboxRadius + HitboxMargin + 0.3f;
            var keeps = lane.Select(l => (l.A, l.B, Radius: keep)).ToList();
            var handStop = hand.HitboxRadius + (world.Party.Get(OffTank)?.HitboxRadius ?? 0.5f) + TeaConstants.Geometry.EnemyReach;
            if (role != OffTank && taken.TryGetValue(OffTank, out var offTankAt))
            {
                var stepped = Vector2.Distance(handAt, offTankAt) > handStop ? offTankAt + Vector2.Normalize(handAt - offTankAt) * handStop : handAt;
                keeps.Add((handAt, stepped, handKeep));
                var goal = First.Abs(new Vector2(0f, -13f));
                var leavesFrom = stepped + Vector2.Normalize(goal - stepped) * (handStop - 0.3f);
                if (role is MeleeDpsA or MeleeDpsB) keeps.AddRange(HandSweep(stepped, OffTankRoute(leavesFrom, goal, stepped, hand.HitboxRadius), handStop, handKeep));
            }
            bool Clear(Vector2 p) => keeps.All(k => DistanceToSegment(p, k.A, k.B) >= k.Radius);
            if (Clear(at) && (role != OffTank || Vector2.Distance(at, handAt) <= handStop)) continue;
            var around = new List<(Vector2 A, Vector2 B, float Radius)> { (livingLiquidAt, livingLiquidAt, keep - 0.3f), (handAt, handAt, handKeep - 0.3f) };
            Vector2? best = null;
            foreach (var pass in (int[])[0, 1, 2])
            {
                var candidates = new List<Vector2>();
                for (var gx = -20; gx <= 20; gx++)
                for (var gz = -20; gz <= 20; gz++)
                {
                    var p = new Vector2(gx, gz) * 0.5f;
                    if (!Clear(p) || (pass < 2 && MathF.Sign(Cross(bait - livingLiquidAt, p - livingLiquidAt)) != partySide) || !Dry(p)) continue;
                    if (pass == 0 && role == OffTank && Vector2.Distance(p, handAt) > handStop) continue;
                    if (Vector2.Distance(p, livingLiquidAt) < keep || Vector2.Distance(p, handAt) < handKeep) continue;
                    if (taken.Any(t => t.Key != role && Vector2.Distance(t.Value, p) < SpotSpacing)) continue;
                    candidates.Add(p);
                }
                best = candidates.OrderBy(p => Vector2.Distance(at, p)).Take(40).Cast<Vector2?>().FirstOrDefault(p => Route(at, p!.Value, around, Puddles()) is not null);
                if (best is not null) break;
            }
            if (best is not { } spot) continue;
            taken[role] = spot;
            spots[(int)role] = spot;
        }
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 PartyCentre()
    {
        var others = PerRole.All.Where(r => r != MainTank).Select(world.Party.Get).OfType<SimCharacter>().Where(m => m.IsAlive()).Select(m => Flat(m.Position)).ToList();
        return others.Count == 0 ? Vector2.Zero : others.Aggregate(Vector2.Zero, (a, b) => a + b) / others.Count;
    }

    private List<Vector2> WayOutFromUnderLivingLiquid(Vector2 tankAt, Vector2 bait)
    {
        if (state.Objects.LivingLiquid is not { } livingLiquid) return [];
        var centre = Flat(livingLiquid.Position);
        var ring = livingLiquid.HitboxRadius + TankRadius + HitboxMargin + 0.1f;
        var start = Vector2.Distance(tankAt, centre);
        if (DistanceToSegment(centre, tankAt, bait) >= MathF.Min(start, ring) - 0.05f) return [];
        var toBait = bait - centre;
        var d = Vector2.Normalize(toBait);
        var alpha = MathF.Acos(Math.Clamp(ring / toBait.Length(), -1f, 1f));
        var away = new Vector2(-d.Y, d.X);
        if (Vector2.Dot(PartyCentre() - centre, away) > 0f) away = -away;
        var exit = centre + (d * MathF.Cos(alpha) + away * MathF.Sin(alpha)) * ring;
        if (DistanceToSegment(centre, tankAt, exit) >= start - 0.5f) return [exit];
        var from = start > 0.01f ? Bearing(tankAt - centre) : Bearing(exit - centre);
        var turn = ((Bearing(exit - centre) - from + 540f) % 360f) - 180f;
        if (AngleBetween(Bearing(d), from + turn / 2f) < MathF.Abs(turn) / 2f) turn -= MathF.CopySign(360f, turn);
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(turn) / 30f));
        return [.. Enumerable.Range(0, steps + 1).Select(i => centre + TornadoT.Polar(from + turn * i / steps, ring))];
    }

    private Vector2[] OutToTheSouthTornado()
    {
        Vector2[] baits = [Bait(First, TornadoT.South, -60f), Bait(First, TornadoT.South, -72.5f, TornadoArcDistance), Bait(First, TornadoT.South, -85f)];
        if (world.Party.Get(MainTank) is not { } tank) return baits;
        var at = Flat(tank.Position);
        var path = new List<Vector2>();
        var wayOut = WayOutFromUnderLivingLiquid(at, baits[0]);
        if (wayOut.Count > 0)
        {
            path.AddRange(wayOut);
            at = wayOut[^1];
        }
        var hand = state.Objects.LiquidHand is { IsActive: true } liquidHand
            ? new List<(Vector2 A, Vector2 B, float Radius)> { (Flat(liquidHand.Position), Flat(liquidHand.Position), liquidHand.HitboxRadius + TankRadius + HitboxMargin) }
            : [];
        path.AddRange(Route(at, baits[0], hand, Puddles()) ?? [baits[0]]);
        path.AddRange(baits.Skip(1));
        return [.. path];
    }

    private void TakeTheHandNorthPastTheParty()
    {
        if (state.Objects.LiquidHand is not { IsActive: true } hand || world.Party.Get(OffTank) is not { } offTank) return;
        var handAt = Flat(hand.Position);
        var path = OffTankRoute(Flat(offTank.Position), offTank.MoveDestination is { } goal ? Flat(goal) : First.Abs(new Vector2(0f, -13f)), handAt, hand.HitboxRadius);
        if (offTank.IsAlive() && offTank.MoveDestination is not null && path.Count > 1) WalkThrough(offTank, path);
        var stop = hand.HitboxRadius + offTank.HitboxRadius + TeaConstants.Geometry.EnemyReach;
        RouteAroundBosses(OffTank, m => HandSweep(handAt, path, stop, hand.HitboxRadius + m.HitboxRadius + HitboxMargin), MeleeDpsA, MeleeDpsB);
    }

    private List<Vector2> OffTankRoute(Vector2 from, Vector2 goal, Vector2 handAt, float handRadius)
        => Route(from, goal, [(handAt, handAt, handRadius + TankRadius + HitboxMargin)], Puddles()) ?? [goal];

    private IAiMove OffTankOnTheHandsFarSide()
    {
        if (world.Party.Get(OffTank) is not { } offTank || state.Objects.LiquidHand is not { } hand || state.Objects.LivingLiquid is not { } livingLiquid)
            return AiMove.Create().NaturalOrder();
        var handAt = Flat(hand.Position);
        var goal = First.Abs(new Vector2(0f, -13f));
        var spot = handAt + Vector2.Normalize(goal - handAt) * (hand.HitboxRadius + offTank.HitboxRadius + TeaConstants.Geometry.EnemyReach - 0.3f);
        if (Vector2.Distance(spot, Flat(offTank.Position)) < 0.75f || !Dry(spot) || BossLane(livingLiquid, MainTank, offTank, overshoot: true).Any(k => DistanceToSegment(spot, k.A, k.B) < k.Radius))
            return AiMove.Create().NaturalOrder();
        return AiMove.Single(OffTank, spot);
    }

    private static List<(Vector2 A, Vector2 B, float Radius)> HandSweep(Vector2 handAt, List<Vector2> offTankPath, float stop, float radius)
    {
        var goal = offTankPath[^1];
        var last = offTankPath.Count > 1 ? offTankPath[^2] : handAt;
        var rest = Vector2.Distance(last, goal) > stop ? goal + Vector2.Normalize(last - goal) * stop : last;
        var points = new List<Vector2> { handAt };
        points.AddRange(offTankPath.Take(offTankPath.Count - 1));
        points.Add(rest);
        return [(handAt, handAt, radius), .. points.Zip(points.Skip(1), (a, b) => (a, b, radius))];
    }

    private void RouteAroundBosses(PartyRole handTank, Func<SimCharacter, List<(Vector2 A, Vector2 B, float Radius)>>? handSweep, params PartyRole[] roles)
    {
        foreach (var role in roles)
        {
            if (world.Party.Get(role) is not { } member || !member.IsAlive() || member.MoveDestination is not { } destination) continue;
            var keeps = new List<(Vector2 A, Vector2 B, float Radius)>();
            if (role != MainTank && state.Objects.LivingLiquid is { IsActive: true } livingLiquid) keeps.AddRange(BossLane(livingLiquid, MainTank, member, overshoot: true));
            if (handSweep is not null) keeps.AddRange(handSweep(member));
            else if (state.Objects.LiquidHand is { IsActive: true } hand)
                keeps.AddRange(role == handTank
                    ? [(Flat(hand.Position), Flat(hand.Position), hand.HitboxRadius + member.HitboxRadius + HitboxMargin)]
                    : BossLane(hand, handTank, member, overshoot: false));
            if (Route(Flat(member.Position), Flat(destination), keeps, Puddles()) is { Count: > 1 } path) WalkThrough(member, path);
        }
    }

    private List<(Vector2 A, Vector2 B, float Radius)> BossLane(SimEnemy boss, PartyRole tankRole, SimCharacter member, bool overshoot)
    {
        var at = Flat(boss.Position);
        var tank = world.Party.Get(tankRole);
        var heads = tank?.MoveDestination is { } destination ? Flat(destination) : tank is not null ? Flat(tank.Position) : at;
        var stop = boss.HitboxRadius + (tank?.HitboxRadius ?? 0.5f) + TeaConstants.Geometry.EnemyReach;
        var end = Vector2.Distance(at, heads) <= stop + 0.05f ? at : overshoot ? heads : heads + Vector2.Normalize(at - heads) * stop;
        var radius = boss.HitboxRadius + member.HitboxRadius + HitboxMargin;
        return [(at, at, radius), (at, end, radius)];
    }

    private List<(Vector2 A, Vector2 B, float Radius)> Puddles()
        => TornadoSpots(First).Select(t => (t, t, TeaConstants.Geometry.PuddleRadius + 0.3f)).ToList();

    private static List<Vector2>? Route(Vector2 from, Vector2 to, List<(Vector2 A, Vector2 B, float Radius)> bosses, List<(Vector2 A, Vector2 B, float Radius)> puddles)
    {
        var live = bosses.Where(k => DistanceToSegment(from, k.A, k.B) > k.Radius && DistanceToSegment(to, k.A, k.B) > k.Radius).ToList();
        if (live.All(k => SegmentDistance(from, to, k.A, k.B) >= k.Radius)) return [to];
        var all = live.Concat(puddles).ToList();
        bool Clear(Vector2 a, Vector2 b) => all.All(k => SegmentDistance(a, b, k.A, k.B) >= k.Radius);
        var candidates = live
            .SelectMany(k => new[] { k.A, k.B, (k.A + k.B) / 2f }.Distinct().SelectMany(c => ((float[])[0.7f, 2f]).SelectMany(extra => Enumerable.Range(0, 24).Select(i => c + TornadoT.Polar(i * 15f, k.Radius + extra)))))
            .Where(c => c.Length() <= 18.5f && all.All(k => DistanceToSegment(c, k.A, k.B) >= k.Radius + 0.1f))
            .ToList();
        var single = candidates.Where(c => Clear(from, c) && Clear(c, to)).OrderBy(c => Vector2.Distance(from, c) + Vector2.Distance(c, to)).ToList();
        if (single.Count > 0) return [single[0], to];
        var lasts = candidates.Where(c => Clear(c, to)).ToList();
        (float Cost, Vector2 A, Vector2 B)? best = null;
        foreach (var a in candidates.Where(c => Clear(from, c)))
        foreach (var b in lasts)
        {
            var cost = Vector2.Distance(from, a) + Vector2.Distance(a, b) + Vector2.Distance(b, to);
            if ((best is null || cost < best.Value.Cost) && Clear(a, b)) best = (cost, a, b);
        }
        return best is { } two ? [two.A, two.B, to] : null;
    }

    private IAiMove LineUpNorthOfLivingLiquid() => ByRole(PerRole.All.Select(role => (role, LineUpSpot(role))).ToArray());

    private Vector2 LineUpSpot(PartyRole role) => role switch
    {
        MainTank => AroundLivingLiquid(First, 180f, 3.9f),
        OffTank => AroundLivingLiquid(First, 180f, 5.5f),
        MeleeDpsA => AroundLivingLiquid(First, 0f, 6f),
        MeleeDpsB => AroundLivingLiquid(First, 0f, 7f),
        RegenHealer => AroundLivingLiquid(First, 0f, 8f),
        ShieldHealer => AroundLivingLiquid(First, 0f, 9f),
        PhysRangedDps => AroundLivingLiquid(First, 0f, 10f),
        _ => AroundLivingLiquid(First, 0f, 11f),
    };

    private IAiMove DodgeFirstVisibleProteans() => ByRole(
        (MainTank, AroundFacing(-45f, 3f)), (OffTank, AroundFacing(45f, 3f)), (MeleeDpsA, AroundFacing(90f, 3f)),
        (MeleeDpsB, AroundFacing(-90f, 3f)), (RegenHealer, AroundFacing(150f, 10f)), (ShieldHealer, AroundFacing(-150f, 10f)),
        (CasterDps, AroundFacing(130f, 10f)), (PhysRangedDps, AroundFacing(-130f, 10f)));

    private IAiMove SwapInForSecondWave() => ByRole(
        (MainTank, AroundFacing(-45f, 4.7f)), (OffTank, AroundFacing(45f, 5.5f)), (MeleeDpsA, AroundFacing(50f, 5.5f)),
        (MeleeDpsB, AroundFacing(-50f, 5.5f)), (RegenHealer, AroundFacing(150f, 2f)), (ShieldHealer, AroundFacing(-150f, 2f)),
        (CasterDps, AroundFacing(90f, 2f)), (PhysRangedDps, AroundFacing(-90f, 2f)));

    private IAiMove LineUpForSecondProteans() => ByRole(
        (MainTank, AroundLivingLiquid(Second, 0f, 3.9f)), (OffTank, AroundLivingLiquid(Second, 0f, 5.5f)),
        (RegenHealer, AroundLivingLiquid(Second, 0f, 7f)), (CasterDps, AroundLivingLiquid(Second, 0f, 8.5f)),
        (MeleeDpsA, AroundLivingLiquid(Second, 180f, 3f)), (MeleeDpsB, AroundLivingLiquid(Second, 180f, 4f)),
        (ShieldHealer, AroundLivingLiquid(Second, 180f, 5f)), (PhysRangedDps, AroundLivingLiquid(Second, 180f, 6f)));

    private void NoteLivingLiquidFacing()
    {
        if (state.Objects.LivingLiquid is { } livingLiquid)
            proteanFacing = 180f - float.RadiansToDegrees(livingLiquid.Rotation);
    }

    private SimCharacter? CleanseTopDown() => NextToCleanse(RegenHealer, ShieldHealer, topDown: true);

    private SimCharacter? CleanseBottomUp() => NextToCleanse(ShieldHealer, RegenHealer, topDown: false);

    private SimCharacter? NextToCleanse(PartyRole healer, PartyRole coHealer, bool topDown)
    {
        if (world.Party.Get(healer) is { } self && Throttled(self)) return self;
        var order = topDown ? PerRole.All : PerRole.All.Reverse();
        return order.Where(role => role != healer && role != coHealer).Select(world.Party.Get).FirstOrDefault(Throttled);
    }

    private static bool Throttled(SimCharacter? member) => member.IsAlive() && member!.ShowsStatus(TeaConstants.StatusId.Throttle);

    private bool HandTellsPrayer() => state.Objects.LiquidHand?.ModelState == HandOfPrayerTell;

    private bool HandTellsParting() => state.Objects.LiquidHand?.ModelState == HandOfPartingTell;

    private IAiMove DragBossesApart()
    {
        if (state.Objects.LivingLiquid is not { } livingLiquid || state.Objects.LiquidHand is not { } hand) return AiMove.Create().NaturalOrder();
        var orbs = state.Objects.Emboli.Where(o => o.IsActive).ToList();
        var orbRadius = orbs.Count > 0 ? OrbRadius(orbs[0]) : TeaConstants.Geometry.EmbolusRadius;
        var paths = orbs.Select(o => (From: Flat(o.Position), To: Flat(o.Position) + Forward(o.Rotation) * OrbPathBeforePhaseEnds)).ToList();
        float Clear(Vector2 p) => paths.Count == 0 ? float.MaxValue : paths.Min(l => DistanceToSegment(p, l.From, l.To));
        var puddles = TornadoSpots(Second);
        bool Dry(Vector2 p) => p.Length() <= 18.5f && puddles.All(t => Vector2.Distance(p, t) >= TeaConstants.Geometry.PuddleRadius + 0.8f);
        var livingLiquidStop = livingLiquid.HitboxRadius + TankRadius + TeaConstants.Geometry.EnemyReach;
        var handStop = hand.HitboxRadius + TankRadius + TeaConstants.Geometry.EnemyReach;
        var livingLiquidAt = Flat(livingLiquid.Position);
        var handAt = Flat(hand.Position);
        var mainTankAt = Flat(world.Party.Get(MainTank)?.Position ?? Vector3.Zero);
        var offTankAt = Flat(world.Party.Get(OffTank)?.Position ?? Vector3.Zero);

        (float Cost, Vector2 Tank, Vector2 OffTank)? best = null;
        for (var bearing = 0f; bearing < 360f; bearing += 10f)
        for (var cx = -6f; cx <= 6f; cx += 1.5f)
        for (var cz = -6f; cz <= 6f; cz += 1.5f)
        {
            var axis = TornadoT.Polar(bearing, 1f);
            var centre = new Vector2(cx, cz);
            var livingLiquidEnd = centre + axis * (BossesApartForPrayer / 2f);
            var handEnd = centre - axis * (BossesApartForPrayer / 2f);
            if (livingLiquidEnd.Length() > 15f || handEnd.Length() > 15f) continue;
            if (Clear(livingLiquidEnd) < livingLiquid.HitboxRadius + orbRadius + 1f || Clear(handEnd) < hand.HitboxRadius + orbRadius + 1f) continue;
            var mainTank = livingLiquidEnd + Away(livingLiquidEnd, livingLiquidAt, axis) * livingLiquidStop;
            var offTank = handEnd + Away(handEnd, handAt, -axis) * handStop;
            if (!Dry(mainTank) || !Dry(offTank)) continue;
            var cost = MathF.Max(MathF.Max(Vector2.Distance(mainTank, mainTankAt), Vector2.Distance(offTank, offTankAt)),
                MathF.Max(Vector2.Distance(livingLiquidEnd, livingLiquidAt), Vector2.Distance(handEnd, handAt)))
                + WalkPastPenalty(mainTankAt, mainTank, livingLiquidAt) + WalkPastPenalty(offTankAt, offTank, handAt);
            if (best is null || cost < best.Value.Cost) best = (cost, mainTank, offTank);
        }
        if (best is not { } plan) return AiMove.Create().NaturalOrder();

        Vector2? party = null;
        for (var gx = -8; gx <= 8; gx++)
        for (var gz = -8; gz <= 8; gz++)
        {
            var spot = new Vector2(gx, gz);
            if (spot.Length() > 8f || !Dry(spot)) continue;
            if (party is null || Clear(spot) > Clear(party.Value) + 0.5f || (Clear(spot) >= orbRadius + 3f && spot.Length() < party.Value.Length()))
                party = spot;
        }
        var at = party ?? Vector2.Zero;
        return ByRole(
            (MainTank, plan.Tank), (OffTank, plan.OffTank),
            (RegenHealer, at + new Vector2(-1.2f, 0f)), (ShieldHealer, at + new Vector2(1.2f, 0f)),
            (MeleeDpsA, at + new Vector2(0f, -1.2f)), (MeleeDpsB, at + new Vector2(0f, 1.2f)),
            (PhysRangedDps, at + new Vector2(-0.8f, 0.9f)), (CasterDps, at + new Vector2(0.8f, -0.9f)));
    }

    private static float WalkPastPenalty(Vector2 tankAt, Vector2 tankTo, Vector2 boss)
        => Vector2.Dot(tankTo - boss, tankAt - boss) < 0f ? 8f : 0f;

    private static Vector2 Away(Vector2 end, Vector2 from, Vector2 fallback)
        => Vector2.DistanceSquared(end, from) > 0.01f ? Vector2.Normalize(end - from) : fallback;

    private IAiMove RegroupClearOfEmboli()
    {
        if (state.Objects.LivingLiquid is not { } livingLiquid || state.Objects.LiquidHand is not { } hand) return AiMove.Create().NaturalOrder();
        var orbs = state.Objects.Emboli.Where(o => o.IsActive).ToList();
        var orbRadius = orbs.Count > 0 ? OrbRadius(orbs[0]) : TeaConstants.Geometry.EmbolusRadius;
        var paths = orbs.Select(o => (From: Flat(o.Position), To: Flat(o.Position) + Forward(o.Rotation) * OrbPathBeforePhaseEnds)).ToList();
        var meeting = ((float[])[2f, 1.5f, 1f, 0.5f])
            .Select(margin => MeetingClearOfEmboli(livingLiquid, hand, paths, orbRadius, margin))
            .FirstOrDefault(m => m is not null);
        if (meeting is not { } plan) return AiMove.Create().NaturalOrder();
        return ByRole(
            (MainTank, plan.Tank), (OffTank, plan.OffTank),
            (RegenHealer, plan.Party + new Vector2(-1.2f, 0f)), (ShieldHealer, plan.Party + new Vector2(1.2f, 0f)),
            (MeleeDpsA, plan.Party + new Vector2(0f, -1.2f)), (MeleeDpsB, plan.Party + new Vector2(0f, 1.2f)),
            (PhysRangedDps, plan.Party + new Vector2(-0.8f, 0.9f)), (CasterDps, plan.Party + new Vector2(0.8f, -0.9f)));
    }

    private (Vector2 Tank, Vector2 OffTank, Vector2 Party)? MeetingClearOfEmboli(SimEnemy livingLiquid, SimEnemy hand,
        List<(Vector2 From, Vector2 To)> paths, float orbRadius, float margin)
    {
        var puddles = TornadoSpots(Second);
        var livingLiquidStop = livingLiquid.HitboxRadius + TankRadius + TeaConstants.Geometry.EnemyReach;
        var handStop = hand.HitboxRadius + TankRadius + TeaConstants.Geometry.EnemyReach;
        var livingLiquidAt = Flat(livingLiquid.Position);
        var handAt = Flat(hand.Position);
        var mainTankAt = Flat(world.Party.Get(MainTank)?.Position ?? Vector3.Zero);
        var offTankAt = Flat(world.Party.Get(OffTank)?.Position ?? Vector3.Zero);
        var walkable = AiManager.RunSpeed * SecondsToRegroup;

        float Clear(Vector2 p) => paths.Count == 0 ? float.MaxValue : paths.Min(l => DistanceToSegment(p, l.From, l.To));
        bool Standable(Vector2 p, float radius)
            => p.Length() <= radius && puddles.All(t => Vector2.Distance(p, t) >= TeaConstants.Geometry.PuddleRadius + 0.8f) && Clear(p) >= orbRadius + TankRadius + margin;

        (float Cost, Vector2 Tank, Vector2 OffTank, Vector2 Party)? best = null;
        for (var gx = -14; gx <= 14; gx++)
        for (var gz = -14; gz <= 14; gz++)
        {
            var meet = new Vector2(gx, gz);
            if (meet.Length() > 14f || Vector2.Distance(meet, livingLiquidAt) < 0.5f) continue;
            if (Clear(meet) < livingLiquid.HitboxRadius + orbRadius + margin) continue;
            var mainTank = meet + Vector2.Normalize(meet - livingLiquidAt) * livingLiquidStop;
            if (!Standable(mainTank, 18.5f) || Vector2.Distance(mainTank, mainTankAt) > walkable) continue;
            var handEnd = meet + Vector2.Normalize(handAt - meet);
            if (Clear(handEnd) < hand.HitboxRadius + orbRadius + margin) continue;
            var offTank = handEnd + Vector2.Normalize(handEnd - handAt) * handStop;
            if (!Standable(offTank, 18.5f) || Vector2.Distance(offTank, offTankAt) > walkable) continue;
            var swing = Bearing(mainTank - meet);
            if (OutsideCone(meet, swing, 45f, livingLiquid.HitboxRadius + 8f, offTank) < 1.5f) continue;
            Vector2? party = null;
            var partyClearance = 0f;
            foreach (var distance in (float[])[6f, 7.5f, 5f])
            {
                for (var bearing = 0f; bearing < 360f; bearing += 15f)
                {
                    if (AngleBetween(bearing, swing) < 75f) continue;
                    var spot = meet + TornadoT.Polar(bearing, distance);
                    if (Standable(spot, 15.5f) && (party is null || Clear(spot) > partyClearance))
                        (party, partyClearance) = (spot, Clear(spot));
                }
                if (party is not null) break;
            }
            if (party is not { } partySpot) continue;
            var cost = MathF.Max(Vector2.Distance(mainTank, mainTankAt), Vector2.Distance(offTank, offTankAt))
                       - 0.5f * MathF.Min(Clear(meet) - livingLiquid.HitboxRadius - orbRadius, 3f);
            if (best is null || cost < best.Value.Cost) best = (cost, mainTank, offTank, partySpot);
        }
        return best is { } b ? (b.Tank, b.OffTank, b.Party) : null;
    }

    private IAiMove ByRole(params (PartyRole Role, Vector2 Spot)[] spots)
    {
        var coords = new Vector2?[8];
        foreach (var (role, spot) in spots) coords[(int)role] = spot;
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove InFrame(TornadoT frame, params (PartyRole Role, float X, float Z)[] spots)
        => ByRole(spots.Select(s => (s.Role, frame.Abs(new Vector2(s.X, s.Z)))).ToArray());

    private void Walk(float time, PartyRole role, Func<Vector2[]> waypoints)
        => world.Events.Add(time, () =>
        {
            if (world.Party.Get(role) is { } member && member.IsAlive()) WalkThrough(member, waypoints());
        });

    private void WalkThrough(SimCharacter member, IEnumerable<Vector2> waypoints)
    {
        var from = Flat(member.Position);
        var delay = 0f;
        foreach (var point in waypoints)
        {
            var to = new Vector3(point.X, 0f, point.Y);
            if (delay <= 0f) member.MoveTo(to, speed: AiManager.RunSpeed);
            else world.Events.Add(delay, () => { if (member.IsAlive()) member.MoveTo(to, speed: AiManager.RunSpeed); });
            delay += Vector2.Distance(from, point) / AiManager.RunSpeed;
            from = point;
        }
    }

    private static Vector2 Bait(TornadoT frame, int tornado, float degrees, float distance = TornadoBaitDistance)
    {
        var relativeTornado = tornado switch { TornadoT.West => new Vector2(-TornadoT.Radius, 0f), TornadoT.East => new Vector2(TornadoT.Radius, 0f), _ => new Vector2(0f, TornadoT.Radius) };
        var towardCentre = tornado switch { TornadoT.West => 90f, TornadoT.East => 270f, _ => 0f };
        return frame.Abs(relativeTornado + TornadoT.Polar(towardCentre + degrees, distance));
    }

    private Vector2 MeleeBesideDoll(PartyRole role)
    {
        var doll = DollSpot(role);
        var nearest = TornadoSpots(First).OrderBy(t => Vector2.Distance(t, doll)).First();
        return doll + Vector2.Normalize(doll - nearest) * 1.6f;
    }

    private Vector2 MeleeOnDoll(PartyRole role) => TornadoT.Polar(DollBearing(role), 7.3f);

    private Vector2 DollSpot(PartyRole role) => TornadoT.Polar(DollBearing(role), 7f);

    private float DollBearing(PartyRole role)
    {
        for (var k = 0; k < TeaP1LivingLiquidState.DollBearings.Length; k++)
            if (DollTaker(k) == role) return TeaP1LivingLiquidState.DollBearings[k];
        return 0f;
    }

    private static float FirstHitOnDoll(PartyRole taker) => taker switch
    {
        MeleeDpsA or MeleeDpsB => 35.7f,
        PhysRangedDps => 35.45f,
        _ => 36.74f,
    };

    private void CarryDollsToABoss(PartyRole taker, float holdFrom, Func<Vector2>? holdSpot, float feedFrom, float backFrom, Func<Vector2> backSpot)
    {
        var carried = false;
        var holding = false;
        void Look()
        {
            var now = world.Events.Elapsed;
            if (now > LastDollLook || world.Party.Get(taker) is not { } member || !member.IsAlive()) return;
            var dolls = DollsOn(member);
            if (dolls.Count == 0)
            {
                if (!carried) world.Events.Add(SecondsBetweenLooks, Look);
                else if (now >= LineUpAt) ai.Move(0f, () => AiMove.Single(taker, LineUpSpot(taker)));
                else ai.Move(MathF.Max(backFrom - now, 0f), () => AiMove.Single(taker, backSpot()));
                return;
            }
            carried = true;
            if (now >= feedFrom && dolls.All(d => d.HpFraction < DollFeedHp) && FeedSpot(member) is { } feed)
                ai.Move(0f, () => AiMove.Single(taker, feed), jitter: 0f);
            else if (!holding && holdSpot is not null)
            {
                holding = true;
                var spot = holdSpot();
                var hold = BossClearance(spot) >= HoldClearOfBosses ? spot : HoldSpotClearOfBosses(member) ?? spot;
                ai.Move(0f, () => AiMove.Single(taker, hold));
            }
            else if (dolls.Any(d => BossClearance(Flat(d.Position)) < DollTooCloseToBoss) && HoldSpotClearOfBosses(member) is { } away)
                ai.Move(0f, () => AiMove.Single(taker, away), jitter: 0f);
            world.Events.Add(SecondsBetweenLooks, Look);
        }
        world.Events.Add(holdFrom, Look);
    }

    private List<SimEnemy> DollsOn(SimCharacter member)
        => world.Children.OfType<SimTether>()
            .Where(t => t.IsActive && t.TetherId == TeaConstants.TetherId.DollFixate && t.B == member)
            .Select(t => t.A).OfType<SimEnemy>().Where(d => d.IsActive).ToList();

    private List<SimEnemy> Bosses()
        => new[] { state.Objects.LivingLiquid, state.Objects.LiquidHand }.OfType<SimEnemy>().Where(b => b.IsActive).ToList();

    private float BossClearance(Vector2 p)
    {
        var bosses = Bosses();
        return bosses.Count == 0 ? float.MaxValue : bosses.Min(b => Vector2.Distance(p, Flat(b.Position)));
    }

    private Vector2? FeedSpot(SimCharacter member)
    {
        var at = Flat(member.Position);
        Vector2? best = null;
        foreach (var boss in Bosses())
        {
            var centre = Flat(boss.Position);
            var facing = Forward(boss.Rotation);
            var side = Vector2.DistanceSquared(at, centre) < 0.01f ? -facing : Vector2.Normalize(at - centre);
            var spot = centre + BesideFacing(side, facing) * FeedFromBossCentre;
            if (!Dry(spot) || !PathClearOfEmboli(at, spot, member.HitboxRadius)) continue;
            if (best is null || Vector2.Distance(at, spot) < Vector2.Distance(at, best.Value)) best = spot;
        }
        return best;
    }

    private static Vector2 BesideFacing(Vector2 side, Vector2 facing)
    {
        var turn = ((Bearing(side) - Bearing(facing) + 540f) % 360f) - 180f;
        return MathF.Abs(turn) >= FeedOutsideBossFacing ? side : TornadoT.Polar(Bearing(facing) + MathF.CopySign(FeedOutsideBossFacing, turn), 1f);
    }

    private Vector2? HoldSpotClearOfBosses(SimCharacter member)
    {
        var at = Flat(member.Position);
        var bosses = Bosses().Select(b => Flat(b.Position)).ToList();
        (Vector2 Spot, float Clearance, float Walk)? best = null;
        for (var bearing = 0f; bearing < 360f; bearing += 15f)
        foreach (var radius in (float[])[8f, 10f, 12f])
        {
            var spot = TornadoT.Polar(bearing, radius);
            if (!Dry(spot) || !PathClearOfEmboli(at, spot, member.HitboxRadius)) continue;
            if (bosses.Any(b => DistanceToSegment(b, at, spot) < DollTooCloseToBoss)) continue;
            var clearance = MathF.Min(BossClearance(spot), HoldClearOfBosses);
            var walk = Vector2.Distance(at, spot);
            if (best is null || clearance > best.Value.Clearance || (clearance == best.Value.Clearance && walk < best.Value.Walk))
                best = (spot, clearance, walk);
        }
        return best?.Spot;
    }

    private bool Dry(Vector2 p)
        => p.Length() <= 18.5f && TornadoSpots(First).All(t => Vector2.Distance(p, t) >= TeaConstants.Geometry.PuddleRadius + 0.8f);

    private bool PathClearOfEmboli(Vector2 from, Vector2 to, float radius)
    {
        foreach (var orb in state.Objects.Emboli.Where(o => o.IsActive))
        {
            var at = Flat(orb.Position);
            var ahead = at + Forward(orb.Rotation) * EmbolusLookahead;
            var reach = OrbRadius(orb) + radius + 0.5f;
            var steps = Math.Max(1, (int)(Vector2.Distance(from, to) / 0.5f));
            for (var i = 0; i <= steps; i++)
                if (DistanceToSegment(Vector2.Lerp(from, to, (float)i / steps), at, ahead) < reach) return false;
        }
        return true;
    }

    private PartyRole DollTaker(int k)
    {
        var relative = ((TeaP1LivingLiquidState.DollBearings[k] - First.OpenSide) % 360f + 360f) % 360f;
        return relative switch
        {
            < 90f => PhysRangedDps,
            < 180f => MeleeDpsB,
            < 270f => MeleeDpsA,
            _ => CasterDps,
        };
    }

    private static List<Vector2> TornadoSpots(TornadoT frame) => [frame.Tornado(TornadoT.West), frame.Tornado(TornadoT.East), frame.Tornado(TornadoT.South)];

    private Vector2 WhereLivingLiquidStopsAtCentre()
    {
        if (state.Objects.LivingLiquid is not { } livingLiquid) return Vector2.Zero;
        var stop = livingLiquid.HitboxRadius + TankRadius + TeaConstants.Geometry.EnemyReach;
        var at = Flat(livingLiquid.Position);
        return at.LengthSquared() < 0.01f ? new Vector2(0f, stop) : Vector2.Normalize(-at) * stop;
    }

    private Vector2 AroundLivingLiquid(TornadoT frame, float relativeBearing, float radius)
        => Flat(state.Objects.LivingLiquid?.Position ?? Vector3.Zero) + frame.Abs(TornadoT.Polar(relativeBearing, radius));

    private Vector2 AroundLiquidHand(TornadoT frame, float relativeBearing, float radius)
        => Flat(state.Objects.LiquidHand?.Position ?? Vector3.Zero) + frame.Abs(TornadoT.Polar(relativeBearing, radius));

    private Vector2 AroundFacing(float degreesFromFacing, float radius)
        => Flat(state.Objects.LivingLiquid?.Position ?? Vector3.Zero) + TornadoT.Polar(proteanFacing + degreesFromFacing, radius);

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        if (ab.LengthSquared() < 1e-6f) return Vector2.Distance(p, a);
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (Cross(d - c, a - c) * Cross(d - c, b - c) < 0f && Cross(b - a, c - a) * Cross(b - a, d - a) < 0f) return 0f;
        return MathF.Min(MathF.Min(DistanceToSegment(a, c, d), DistanceToSegment(b, c, d)), MathF.Min(DistanceToSegment(c, a, b), DistanceToSegment(d, a, b)));
    }

    private static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;

    private static float OutsideCone(Vector2 apex, float axisBearing, float halfDegrees, float length, Vector2 p)
    {
        var v = p - apex;
        var d = v.Length();
        if (d < 1e-4f) return -length;
        var off = AngleBetween(Bearing(v), axisBearing);
        if (off <= halfDegrees)
            return d <= length ? -MathF.Min(d * MathF.Sin(float.DegreesToRadians(halfDegrees - off)), length - d) : d - length;
        var along = d * MathF.Cos(float.DegreesToRadians(off - halfDegrees));
        return along < 0f ? d : along > length ? d - length : d * MathF.Sin(float.DegreesToRadians(off - halfDegrees));
    }

    private static float Bearing(Vector2 v) => (float.RadiansToDegrees(MathF.Atan2(v.X, -v.Y)) + 360f) % 360f;

    private static float AngleBetween(float a, float b) => MathF.Abs(((a - b + 540f) % 360f) - 180f);

    private static Vector2 Forward(float rotation) => new(MathF.Sin(rotation), MathF.Cos(rotation));

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static float OrbRadius(SimEnemy orb) => orb.PacketSpawnPending ? TeaConstants.Geometry.EmbolusRadius : orb.HitboxRadius;
}
