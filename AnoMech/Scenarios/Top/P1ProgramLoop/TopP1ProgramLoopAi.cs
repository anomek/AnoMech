using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.Native.Interfaces;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P1ProgramLoop;

public sealed class TopP1ProgramLoopAi(bool automarkers) : IScenarioAi<TopP1ProgramLoopState>
{
    public string Name => automarkers ? "NA (automarkers)" : "NA";

    private const float GrabRadius = 3f;
    private const float HoldRadius = 15f;
    private const float OutsideHitbox = 12.5f;
    private const float ColumnSpacing = 1.2f;
    private const float TankPullRadius = 5.5f;
    private const float TankPullBearing = 315f;
    private const float SoakSideOffset = 1.8f;
    private const float IdleSideOffset = 4f;
    private const float IdleOutset = 1.5f;
    private const float RouteRadius = 13f;
    private const float Precise = 0f;
    private const float Loose = 0.3f;
    private const float LookEvery = 0.25f;
    private const float HandOverPatience = 4f;
    private const float CarryAfterBlaster = 2.9f;
    private const float LeaveForTowerAfterBlaster = 0.3f;
    private const float LeaveTowerAfterBlaster = 1f;
    private const float WalkOutAfterBlaster = 6.2f;
    private const float StopTakingBeforeBlaster = 0.5f;
    private const float SettleBeforeBlaster = 1.5f;
    private const float SettleBeforeTower = 0.3f;
    private const float ClaimBeforeTower = 0.8f;
    private const float BlastReach = 15f;
    private const float BlastMargin = 1.5f;
    private const float TowerRadius = 3f;
    private const float TowerReach = 3.5f;
    private const float TowerEdge = 2.5f;
    private const float TetherClearance = 1f;
    private const float ShortTether = 4.5f;
    private const float BeyondHolder = 2.5f;
    private const float WallReach = 19f;
    private const float MovePrompt = 0.3f;
    private const float TethersAppear = 20.36f;

    private static readonly Vector2 SouthStack = new(0f, 13.5f);
    private static readonly Vector2 Anchor = new(0.02f, 0.27f);
    private static readonly float[] BlasterTimes = [28.56f, 37.52f, 46.49f, 55.44f];
    private static readonly float[] TowerTimes = [28.47f, 37.47f, 46.48f, 55.48f];
    private static readonly float[] LastLooksBeforeBlaster = [1.2f, 0.7f];
    private static readonly int[] HolderNumbers = [3, 4, 1, 2];
    private static readonly float[] StepRings = [1.5f, 3f, 4.5f, 6f];
    private static readonly float[] AsideRings = [0f, 2f, 4f, 6f, 8f];
    private static readonly float[] DodgeRings = [0.5f, 1f, 1.5f, 2f, 2.5f];
    private const int StepBearings = 16;

    private TopP1ProgramLoopState state = null!;
    private SimWorld world = null!;
    private AiManager ai = null!;
    private Dictionary<PartyRole, bool> inLeftGroup = null!;
    private readonly Dictionary<PartyRole, Vector2> planned = new();
    private readonly Dictionary<PartyRole, Vector2> holdSpots = new();
    private readonly Dictionary<PartyRole, int> orders = new();
    private readonly Dictionary<PartyRole, int> carries = new();

    public void Run(TopP1ProgramLoopState s, SimWorld simWorld)
    {
        state = s;
        world = simWorld;
        ai = new AiManager(world);
        inLeftGroup = TopLightParties.SplitByInLine(state.WithNumber);
        planned.Clear();
        holdSpots.Clear();
        orders.Clear();
        carries.Clear();

        ai.Move(MovePrompt, StackSouth, jitter: 1f);
        Go(3.2f, PartyRole.MainTank, TopCompass.Point(TankPullRadius, TankPullBearing), Loose);
        WalkAround(10.32f, PartyRole.MainTank, SouthStack, 15.32f, mustArrive: false, 0);
        PlanColumnBehindThrees(15.32f);
        PlanFirstSet(20.62f, 25.62f);
        for (var set = 0; set < 3; set++)
            PlanHandOver(set, BlasterTimes[set]);
        for (var set = 0; set < BlasterTimes.Length; set++)
        {
            StepOutOfMisplacedBlasts(set);
            ClaimYourTower(set);
        }

        if (!automarkers) return;
        ai.Automarker(15.52f, () => LoopMarkers(3, 1));
        ai.Automarker(28.92f, () => LoopMarkers(4, 2));
        ai.Automarker(37.92f, () => LoopMarkers(1, 3));
        ai.Automarker(46.92f, () => LoopMarkers(2, 4));
        ai.Automarker(55.92f, () => new Dictionary<PartyRole, Sign>());
    }

    private static IAiMove StackSouth() => AiMove.All(SouthStack);

    private void PlanColumnBehindThrees(float time)
    {
        var column = state.WithNumber(3).Concat(PerRole.All.Where(role => state.NumberOf(role) != 3)).ToList();
        for (var i = 0; i < column.Count; i++)
        {
            var spot = new Vector2(0f, i < 2 ? 4.5f + 1.5f * i : OutsideHitbox + ColumnSpacing * (i - 2));
            planned[column[i]] = spot;
            Go(time, column[i], spot, Precise);
        }
    }

    private void PlanFirstSet(float carryTime, float walkOutTime)
    {
        var towers = state.Towers[0];
        foreach (var left in new[] { true, false })
        {
            var holder = Member(3, left);
            var cardinal = DropCardinal(towers, left);
            TakeYourTether(TethersAppear, holder, 3, carryTime, cardinal, walkOutTime, viaSouth: true, BlasterTimes[0] - StopTakingBeforeBlaster);
        }
        foreach (var left in new[] { true, false })
        {
            WalkAround(carryTime, Member(1, left), SoakSpot(towers, left), TowerTimes[0] - SettleBeforeTower, mustArrive: true, 0);
            WalkAround(carryTime, Member(2, left), BesideTower(towers, TowerCardinal(towers, left), IdleOutset), BlasterTimes[0] - SettleBeforeBlaster,
                mustArrive: false, 0);
            WalkAround(carryTime, Member(4, left), TakerWaitSpot(towers, left), BlasterTimes[0] - SettleBeforeBlaster, mustArrive: false, 0);
        }
        foreach (var left in new[] { true, false })
            planned[Member(3, left)] = holdSpots[Member(3, left)] = HoldSpot(DropCardinal(towers, left));
    }

    private void PlanHandOver(int set, float blaster)
    {
        var next = state.Towers[set + 1];
        var oldHolder = HolderNumbers[set];
        var newHolder = HolderNumbers[set + 1];
        var justSoaked = set + 1;
        var nextSoaker = set + 2;
        var lastHandOver = set == 2;
        var settle = BlasterTimes[set + 1] - SettleBeforeBlaster;

        foreach (var left in new[] { true, false })
            TakeYourTether(blaster, Member(newHolder, left), newHolder, blaster + CarryAfterBlaster, DropCardinal(next, left), blaster + WalkOutAfterBlaster,
                viaSouth: false, BlasterTimes[set + 1] - StopTakingBeforeBlaster);

        var passedTowers = NearestTowers(next, Member(oldHolder, true), Member(oldHolder, false));
        var soakedTowers = NearestTowers(next, Member(justSoaked, true), Member(justSoaked, false));
        foreach (var left in new[] { true, false })
        {
            var passed = Member(oldHolder, left);
            LeaveOnceTaken(blaster + CarryAfterBlaster, passed, BesideTower(next, left ? passedTowers.Left : passedTowers.Right, IdleOutset), settle, set + 1);

            WalkAround(blaster + LeaveForTowerAfterBlaster, Member(nextSoaker, left), SoakSpot(next, left), TowerTimes[set + 1] - SettleBeforeTower,
                mustArrive: true, set + 1);

            var soakedSpot = lastHandOver
                ? BesideTower(next, left ? soakedTowers.Left : soakedTowers.Right, 0f)
                : TakerWaitSpot(next, left);
            WalkAround(blaster + LeaveTowerAfterBlaster, Member(justSoaked, left), soakedSpot, settle, mustArrive: false, set + 1);
        }

        foreach (var left in new[] { true, false })
        {
            var taker = Member(newHolder, left);
            planned[taker] = holdSpots[taker] = HoldSpot(DropCardinal(next, left));
        }
    }

    private void TakeYourTether(float from, PartyRole taker, int takerNumber, float carryAt, int cardinal, float walkOutAt, bool viaSouth, float until)
    {
        var carrying = false;
        void Look()
        {
            if (!Drives(taker) || world.Party.Get(taker) is not { } member || !member.IsAlive() || world.Events.Elapsed > until) return;
            if (member.HasTetherInSlot0(TetherId.PassableTether))
            {
                if (!carrying) CarryTetherOut(taker, carryAt, cardinal, walkOutAt, viaSouth);
                carrying = true;
            }
            else
            {
                carrying = false;
                if (TetherToTake(taker, takerNumber) is { } holder) MoveNow(taker, GrabPoint(holder.Position), Precise);
            }
            world.Events.Add(LookEvery, Look);
        }
        At(from, Look);
    }

    private SimCharacter? TetherToTake(PartyRole taker, int takerNumber)
    {
        var holders = TetherHolders().Where(holder => holder.Role is not { } role || state.NumberOf(role) != takerNumber).ToList();
        if (holders.Count == 0) return null;
        var partner = state.WithNumber(takerNumber).First(role => role != taker);
        var partnerHolds = world.Party.Get(partner) is { } p && p.IsAlive() && p.HasTetherInSlot0(TetherId.PassableTether);
        if (partnerHolds || world.Party.Get(partner) is not { } partnerMember || !partnerMember.IsAlive() || holders.Count == 1)
            return holders.MinBy(holder => Cost(taker, holder)).Member;
        var (a, b) = (holders[0], holders[1]);
        return Cost(taker, a) + Cost(partner, b) <= Cost(taker, b) + Cost(partner, a) ? a.Member : b.Member;
    }

    private float Cost(PartyRole taker, (SimCharacter Member, PartyRole? Role) holder)
    {
        var sameGroup = holder.Role is { } role && inLeftGroup.TryGetValue(role, out var left) && left == inLeftGroup[taker];
        return (sameGroup ? 0f : 100f) + Vector2.Distance(Live(taker), GrabPoint(holder.Member.Position));
    }

    private IEnumerable<(SimCharacter Member, PartyRole? Role)> TetherHolders()
    {
        foreach (var role in PerRole.All)
            if (world.Party.Get(role) is { } member && member.IsAlive() && member.HasTetherInSlot0(TetherId.PassableTether))
                yield return (member, role);
    }

    private static Vector2 GrabPoint(Vector3 holder)
    {
        var away = new Vector2(holder.X, holder.Z) - Anchor;
        return away.LengthSquared() < 1e-4f ? Anchor : Anchor + Vector2.Normalize(away) * GrabRadius;
    }

    private void CarryTetherOut(PartyRole holder, float carryAt, int cardinal, float walkOutAt, bool viaSouth)
    {
        var carry = carries[holder] = carries.GetValueOrDefault(holder) + 1;
        bool StillCarrying() => carries[holder] == carry && world.Party.Get(holder) is { } member && member.IsAlive()
            && member.HasTetherInSlot0(TetherId.PassableTether);
        world.Events.Add(MathF.Max(0f, carryAt - world.Events.Elapsed), () =>
        {
            if (!Drives(holder) || !StillCarrying()) return;
            var target = TopCompass.Point(GrabRadius, cardinal * 90f);
            var from = Live(holder);
            List<Vector2> path = viaSouth
                ? [TopCompass.Point(GrabRadius, 180f), .. Arc(TopCompass.Point(GrabRadius, 180f), target, GrabRadius)]
                : Arc(from, target, GrabRadius);
            var arrives = WalkPath(holder, from, path, Precise);
            world.Events.Add(MathF.Max(arrives, walkOutAt - world.Events.Elapsed), () =>
            {
                if (StillCarrying()) MoveNow(holder, HoldSpot(cardinal), Precise);
            });
        });
    }

    private void LeaveOnceTaken(float from, PartyRole passed, Vector2 destination, float deadline, int towerSet)
    {
        planned[passed] = destination;
        var giveUp = from + HandOverPatience;
        void Look()
        {
            if (!Drives(passed) || world.Party.Get(passed) is not { } member || !member.IsAlive()) return;
            if (member.HasTetherInSlot0(TetherId.PassableTether))
            {
                if (world.Events.Elapsed < giveUp) world.Events.Add(LookEvery, Look);
                return;
            }
            WalkAroundTethers(passed, destination, deadline, mustArrive: false, towerSet)();
        }
        At(from, Look);
    }

    private void WalkAround(float time, PartyRole role, Vector2 destination, float deadline, bool mustArrive, int towerSet)
    {
        planned[role] = destination;
        At(time, WalkAroundTethers(role, destination, deadline, mustArrive, towerSet));
    }

    private Action WalkAroundTethers(PartyRole role, Vector2 destination, float deadline, bool mustArrive, int towerSet)
    {
        Vector2? aside = null;
        void Look()
        {
            if (!Drives(role) || world.Party.Get(role) is not { } member || !member.IsAlive()) return;
            var now = world.Events.Elapsed;
            var here = Live(role);
            var lines = LongTethers(member);
            var route = AroundCenter(here, destination);
            if (FirstCrossed(here, route, lines) is not { } blocker)
            {
                WalkPath(role, here, route, Loose);
                return;
            }
            var beyond = blocker * ((blocker.Length() + BeyondHolder) / blocker.Length());
            if (beyond.Length() <= WallReach)
            {
                List<Vector2> detour = [.. AroundCenter(here, beyond), .. AroundCenter(beyond, destination)];
                if (FirstCrossed(here, detour, lines) == null && now + MovePrompt + PathLength(here, detour) / AiManager.RunSpeed <= deadline)
                {
                    WalkPath(role, here, detour, Loose);
                    return;
                }
            }
            if (mustArrive && now + LookEvery + MovePrompt + PathLength(here, route) / AiManager.RunSpeed > deadline)
            {
                WalkPath(role, here, route, Loose);
                return;
            }
            if (now >= deadline) return;
            if (!mustArrive && SafeAside(here, destination, lines, towerSet) is { } spot && (aside is not { } previous || Vector2.Distance(spot, previous) > 1f))
            {
                aside = spot;
                WalkPath(role, here, AroundCenter(here, spot), Precise);
            }
            world.Events.Add(LookEvery, Look);
        }
        return Look;
    }

    private List<Vector2> LongTethers(SimCharacter walker)
        => TetherHolders().Where(holder => !ReferenceEquals(holder.Member, walker))
            .Select(holder => Live(holder.Member)).Where(holder => Vector2.Distance(holder, Anchor) > ShortTether).ToList();

    private static Vector2? FirstCrossed(Vector2 from, IEnumerable<Vector2> waypoints, IReadOnlyList<Vector2> lines)
    {
        foreach (var waypoint in waypoints)
        {
            foreach (var holder in lines)
                if (SegmentsCloserThan(from, waypoint, Anchor, holder, TetherClearance))
                    return holder;
            from = waypoint;
        }
        return null;
    }

    private static float PathLength(Vector2 from, IEnumerable<Vector2> waypoints)
    {
        var length = 0f;
        foreach (var waypoint in waypoints)
        {
            length += Vector2.Distance(from, waypoint);
            from = waypoint;
        }
        return length;
    }

    private Vector2? SafeAside(Vector2 here, Vector2 destination, IReadOnlyList<Vector2> lines, int towerSet)
    {
        var blasts = PredictedBlasts();
        var towers = TowerSpots(towerSet);
        Vector2? best = null;
        var bestCost = float.MaxValue;
        foreach (var centre in new[] { destination, here })
            foreach (var ring in AsideRings)
                for (var k = 0; k < (ring > 0f ? StepBearings : 1); k++)
                {
                    var spot = centre + TopCompass.Point(ring, 360f * k / StepBearings);
                    if (!StandsClear(spot, blasts, towers)) continue;
                    var route = AroundCenter(here, spot);
                    if (FirstCrossed(here, route, lines) != null) continue;
                    var cost = Vector2.Distance(spot, destination) + 0.5f * PathLength(here, route);
                    if (cost >= bestCost) continue;
                    bestCost = cost;
                    best = spot;
                }
        return best;
    }

    private static bool StandsClear(Vector2 spot, IReadOnlyList<Vector2> blasts, IReadOnlyList<Vector2> towers)
        => spot.Length() >= OutsideHitbox && spot.Length() <= WallReach
            && towers.All(tower => Vector2.Distance(spot, tower) >= TowerReach)
            && blasts.All(blast => Vector2.Distance(spot, blast) >= BlastReach + BlastMargin);

    private List<Vector2> PredictedBlasts()
        => TetherHolders().Select(holder =>
        {
            if (holder.Role is { } role && Drives(role) && holdSpots.TryGetValue(role, out var spot)) return spot;
            var at = Live(holder.Member);
            return at.Length() < HoldRadius ? TopCompass.Point(HoldRadius, TopCompass.Bearing(at)) : at;
        }).ToList();

    private List<Vector2> TowerSpots(int set) => state.Towers[set].Cardinals.Select(cardinal => TowerSpot(state.Towers[set], cardinal)).ToList();

    private void StepOutOfMisplacedBlasts(int set)
    {
        foreach (var look in LastLooksBeforeBlaster)
            At(BlasterTimes[set] - look, () =>
            {
                var blasts = PredictedBlasts();
                var towers = TowerSpots(set);
                var lines = TetherHolders().Select(holder => Live(holder.Member)).ToList();
                foreach (var role in PerRole.All)
                {
                    if (!Drives(role) || world.Party.Get(role) is not { } member || !member.IsAlive() || member.HasTetherInSlot0(TetherId.PassableTether)) continue;
                    if (state.NumberOf(role) == set + 1)
                    {
                        DodgeInsideYourTower(role, set, blasts);
                        continue;
                    }
                    var here = Live(role);
                    if (blasts.All(blast => Vector2.Distance(here, blast) >= BlastReach + BlastMargin)) continue;
                    if (SafeStepFrom(here, blasts, towers, lines) is { } step) MoveNow(role, step, Precise);
                }
            });
    }

    private Vector2? SafeStepFrom(Vector2 here, IReadOnlyList<Vector2> blasts, IReadOnlyList<Vector2> towers, IReadOnlyList<Vector2> lines)
    {
        Vector2? best = null;
        var bestRoom = float.MinValue;
        foreach (var ring in StepRings)
            for (var k = 0; k < StepBearings; k++)
            {
                var spot = here + TopCompass.Point(ring, 360f * k / StepBearings);
                if (spot.Length() < OutsideHitbox || spot.Length() > WallReach) continue;
                if (towers.Any(tower => Vector2.Distance(spot, tower) < TowerReach)) continue;
                if (SegmentsCloserThan(here, spot, Anchor, Anchor, MathF.Min(OutsideHitbox, Vector2.Distance(here, Anchor)))) continue;
                if (lines.Any(holder => SegmentsCloserThan(here, spot, Anchor, holder, TetherClearance))) continue;
                var room = blasts.Min(blast => Vector2.Distance(spot, blast)) - BlastReach - BlastMargin;
                if (room >= 0f) return spot;
                if (room <= bestRoom) continue;
                bestRoom = room;
                best = spot;
            }
        return best;
    }

    private void DodgeInsideYourTower(PartyRole soaker, int set, IReadOnlyList<Vector2> blasts)
    {
        var here = Live(soaker);
        var room = blasts.Count == 0 ? float.MaxValue : blasts.Min(blast => Vector2.Distance(here, blast)) - BlastReach - BlastMargin;
        if (room >= 0f) return;
        var tower = TowerSpot(state.Towers[set], TowerCardinal(state.Towers[set], inLeftGroup[soaker]));
        Vector2? best = null;
        foreach (var ring in DodgeRings.Where(ring => ring <= TowerEdge))
            for (var k = 0; k < StepBearings; k++)
            {
                var spot = tower + TopCompass.Point(ring, 360f * k / StepBearings);
                if (spot.Length() < OutsideHitbox) continue;
                var spotRoom = blasts.Min(blast => Vector2.Distance(spot, blast)) - BlastReach - BlastMargin;
                if (spotRoom <= room) continue;
                room = spotRoom;
                best = spot;
            }
        if (best is { } dodge) MoveNow(soaker, dodge, Precise);
    }

    private void ClaimYourTower(int set)
        => At(TowerTimes[set] - ClaimBeforeTower, () =>
        {
            foreach (var left in new[] { true, false })
            {
                var soaker = Member(set + 1, left);
                if (!Drives(soaker) || world.Party.Get(soaker) is not { } member || !member.IsAlive()) continue;
                var tower = TowerSpot(state.Towers[set], TowerCardinal(state.Towers[set], left));
                var mine = Vector2.Distance(Live(soaker), tower);
                if (mine <= TowerRadius) continue;
                MoveNow(soaker, SoakSpot(state.Towers[set], left), Precise);
            }
        });

    private static bool SegmentsCloserThan(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float distance)
    {
        for (var i = 0; i <= 10; i++)
        {
            var p = Vector2.Lerp(a, b, i / 10f);
            var segment = d - c;
            var t = Math.Clamp(Vector2.Dot(p - c, segment) / MathF.Max(segment.LengthSquared(), 1e-6f), 0f, 1f);
            if (Vector2.Distance(p, c + segment * t) < distance) return true;
        }
        return false;
    }

    private PartyRole Member(int number, bool left) => state.WithNumber(number).First(role => inLeftGroup[role] == left);

    private static int TowerCardinal(LoopTowerSet towers, bool left) => left ? towers.Cardinals[1] : towers.Cardinals[0];

    private static int DropCardinal(LoopTowerSet towers, bool left) => left ? towers.FreeCardinals[1] : towers.FreeCardinals[0];

    private static Vector2 HoldSpot(int cardinal) => TopCompass.Point(HoldRadius, cardinal * 90f);

    private static Vector2 TowerSpot(LoopTowerSet towers, int cardinal)
    {
        var position = towers.Position(cardinal);
        return new Vector2(position.X, position.Z);
    }

    private static Vector2 AwayFromNearestHolder(LoopTowerSet towers, Vector2 tower)
    {
        var nearestHolder = towers.FreeCardinals.Select(HoldSpot).MinBy(holder => Vector2.Distance(holder, tower));
        var side = Vector2.Normalize(new Vector2(-tower.Y, tower.X));
        return Vector2.Dot(side, tower - nearestHolder) < 0f ? -side : side;
    }

    private static Vector2 SoakSpot(LoopTowerSet towers, bool left)
    {
        var tower = TowerSpot(towers, TowerCardinal(towers, left));
        return tower + AwayFromNearestHolder(towers, tower) * SoakSideOffset;
    }

    private static Vector2 BesideTower(LoopTowerSet towers, int cardinal, float outset)
    {
        var tower = TowerSpot(towers, cardinal);
        return tower + AwayFromNearestHolder(towers, tower) * IdleSideOffset + Vector2.Normalize(tower) * outset;
    }

    private static Vector2 TakerWaitSpot(LoopTowerSet towers, bool left)
    {
        var holder = HoldSpot(DropCardinal(towers, left));
        var tower = towers.Cardinals.MinBy(cardinal => Vector2.Distance(TowerSpot(towers, cardinal), holder));
        return BesideTower(towers, tower, 0f);
    }

    private (int Left, int Right) NearestTowers(LoopTowerSet towers, PartyRole left, PartyRole right)
    {
        var (a, b) = (towers.Cardinals[0], towers.Cardinals[1]);
        float Walk(PartyRole role, int cardinal) => Vector2.Distance(planned[role], TowerSpot(towers, cardinal));
        return Walk(left, a) + Walk(right, b) <= Walk(left, b) + Walk(right, a) ? (a, b) : (b, a);
    }

    private bool Drives(PartyRole role) => world.Party.Get(role) is { } member && world.Party.IsBotDriven(member);

    private Vector2 Live(PartyRole role) => world.Party.Get(role) is { } member ? Live(member) : Vector2.Zero;

    private static Vector2 Live(SimCharacter member) => new(member.Position.X, member.Position.Z);

    private void At(float time, Action action) => world.Events.Add(time, action);

    private int Claim(PartyRole role) => orders[role] = orders.GetValueOrDefault(role) + 1;

    private void Go(float time, PartyRole role, Vector2 destination, float jitter)
        => At(time, () => MoveNow(role, destination, jitter));

    private void MoveNow(PartyRole role, Vector2 destination, float jitter)
    {
        var order = Claim(role);
        world.Events.Add(0f, () =>
        {
            if (orders[role] == order) ai.Move(MovePrompt, () => AiMove.Single(role, destination), jitter: jitter);
        });
    }

    private float WalkPath(PartyRole role, Vector2 from, IEnumerable<Vector2> waypoints, float jitter)
    {
        var order = Claim(role);
        var after = 0f;
        foreach (var waypoint in waypoints)
        {
            var spot = waypoint;
            world.Events.Add(after, () =>
            {
                if (orders[role] == order) ai.Move(MovePrompt, () => AiMove.Single(role, spot), jitter: jitter);
            });
            after += Vector2.Distance(from, waypoint) / AiManager.RunSpeed + 0.05f;
            from = waypoint;
        }
        return after + MovePrompt;
    }

    private static List<Vector2> AroundCenter(Vector2 from, Vector2 to)
    {
        var waypoints = new List<Vector2>();
        var start = from;
        if (from.Length() < OutsideHitbox)
        {
            start = TopCompass.Point(RouteRadius, TopCompass.Bearing(from));
            waypoints.Add(start);
        }
        var end = to.Length() < OutsideHitbox ? TopCompass.Point(RouteRadius, TopCompass.Bearing(to)) : to;
        waypoints.AddRange(Detour(start, end));
        if (end != to) waypoints.Add(to);
        return waypoints;
    }

    private static List<Vector2> Detour(Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var lengthSq = segment.LengthSquared();
        if (lengthSq < 1e-6f) return [to];
        var t = Math.Clamp(-Vector2.Dot(from, segment) / lengthSq, 0f, 1f);
        var clearance = MathF.Min(OutsideHitbox, MathF.Min(from.Length(), to.Length()));
        if ((from + segment * t).Length() >= clearance - 1e-3f) return [to];
        var turn = TopCompass.Turn(TopCompass.Bearing(from), TopCompass.Bearing(to));
        var middle = TopCompass.Point(RouteRadius, TopCompass.Bearing(from) + (MathF.Abs(turn) <= 170f ? turn / 2f : 90f));
        return [.. Detour(from, middle), .. Detour(middle, to)];
    }

    private static List<Vector2> Arc(Vector2 from, Vector2 to, float radius)
    {
        var start = TopCompass.Bearing(from);
        var turn = TopCompass.Turn(start, TopCompass.Bearing(to));
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(turn) / 60f));
        return Enumerable.Range(1, steps).Select(i => TopCompass.Point(radius, start + turn * i / steps)).ToList();
    }

    private Dictionary<PartyRole, Sign> LoopMarkers(int tetherNumber, int towerNumber)
    {
        var tethers = MarkerOrder(tetherNumber);
        var towers = MarkerOrder(towerNumber);
        return new Dictionary<PartyRole, Sign>
        {
            [tethers[0]] = Sign.Ignore1,
            [tethers[1]] = Sign.Ignore2,
            [towers[0]] = Sign.Attack1,
            [towers[1]] = Sign.Attack2,
        };
    }

    private List<PartyRole> MarkerOrder(int number)
        => state.WithNumber(number).OrderBy(MarkerRank).ThenBy(role => (int)role).ToList();

    private static int MarkerRank(PartyRole role) => role switch
    {
        PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => 0,
        PartyRole.MainTank or PartyRole.OffTank => 1,
        PartyRole.PhysRangedDps => 2,
        PartyRole.CasterDps => 3,
        _ => 4,
    };
}
