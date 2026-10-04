using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

public sealed class TopP3HelloWorldAi : IScenarioAi<TopP3HelloWorldState>
{
    public string Name => "NA";

    private static readonly float[] Resolves = [32.12f, 53.19f, 74.26f, 95.34f];
    private static readonly float[] TowerCasts = [22.16f, 43.21f, 64.26f, 85.30f];
    private static readonly float[] TethersLive = [30.99f, 52.00f, 73.00f, 93.99f];
    private const float LookEvery = 0.6f;
    private const float MeetEvery = 0.3f;
    private const float PickUpEvery = 0.2f;
    private const float SwitchMargin = 2f;
    private const float MeetFrom = 2.6f;
    private const float LeanAfterTowers = 1.5f;
    private const float RemoteWait = 3f;
    private const float LocalWait = 5.5f;
    private const float MeetUntil = 0.25f;
    private const float PickUpAfter = 1.0f;
    private const float WalkUpAfter = 0.05f;
    private const float Standoff = 3f;
    private const float PickUpPatience = 5f;
    private const float StackReach = 4.5f;
    private const float StackClear = 6.5f;
    private const float StackLead = 2.5f;
    private const float DefamationReach = 18.5f;
    private const float DefamationClear = 21.5f;
    private const float TowerClear = 6.8f;
    private const float RotClear = 6.5f;
    private const float RemoteTetherReach = 8f;
    private const float LocalTetherClear = 10f;
    private const float FlankAngle = 32f;
    private const float MaxMelee = 15f;
    private const float CloseIn = 7f;
    private const float PromptMoveDelay = 0.3f;
    private const float Settle = 0.1f;
    private const float RotPathClear = 2f;
    private const float ShareWhen = 0.5f;
    private static readonly float[] ShareRings = [4f, 5.5f, 7f, 8.5f, 10f, 11.5f, 13f, 14.5f, 16f];
    private const float PassClear = 2.5f;
    private const float PassAround = 3f;
    private const float Grace = 0.05f;
    private const float ArenaEdge = 19f;
    private const float LookAhead = 7f;
    private const float SteerEvery = 0.2f;
    private const float NoticeMoved = 0.5f;
    private const float Arrived = 0.3f;
    private static readonly RotColor[] RotColors = [RotColor.Red, RotColor.Blue];

    private static readonly Dictionary<PartyRole, float> ClockSpots = new()
    {
        [PartyRole.MainTank] = 0f, [PartyRole.PhysRangedDps] = 45f, [PartyRole.ShieldHealer] = 90f, [PartyRole.MeleeDpsB] = 135f,
        [PartyRole.OffTank] = 180f, [PartyRole.MeleeDpsA] = 225f, [PartyRole.RegenHealer] = 270f, [PartyRole.CasterDps] = 315f,
    };

    private const float DefamationRing = 15.8f;
    private const float DefamationSpot = 15.5f;
    private const float StackRing = 12.5f;

    private TopP3HelloWorldState state = null!;
    private SimWorld world = null!;
    private Rng noise = null!;
    private AiManager ai = null!;
    private readonly Dictionary<PartyRole, int> routes = new();
    private readonly Dictionary<PartyRole, PartyRole>[] meetings = [new(), new(), new(), new()];
    private readonly Dictionary<PartyRole, Walk> walks = new();
    private bool steering;

    private sealed record Walk(int Route, List<Vector2> Intended, List<Vector2> Path, List<int> Legs, List<float> Fires,
                               PartyRole? Meeting, float Jitter, Dictionary<PartyRole, Vector2> Seen);

    public void Run(TopP3HelloWorldState s, SimWorld w)
    {
        state = s;
        world = w;
        noise = w.Stream("top-p3-hello-world-ai");
        ai = new AiManager(world);
        routes.Clear();
        foreach (var meeting in meetings) meeting.Clear();
        walks.Clear();
        steering = false;

        foreach (var (role, bearing) in ClockSpots)
            ai.Move(1f + PromptMoveDelay, () => AiMove.Single(role, TopCompass.Point(10f, bearing)), jitter: 0.3f);
        WaitUnderOmega(0, TowerCasts[0] + 0.1f);
        for (var patch = 0; patch < 4; patch++)
            PlanPatch(patch);
        var slot = 0;
        foreach (var role in PerRole.All)
        {
            var spot = TopCompass.Point(3f, 180f + 45f * slot++);
            ai.Move(106f + PromptMoveDelay, () => AiMove.Single(role, spot), jitter: 0.5f);
        }
    }

    private void PlanPatch(int patch)
    {
        var defamationSide = DefamationBearing(patch);
        var stackSide = defamationSide + 180f;
        var towersUp = TowerCasts[patch] + 0.1f;
        var resolve = Resolves[patch];
        var defamationSpots = Either(defamationSide, 45f);
        var stackSpots = Either(stackSide, 25f);

        PairUp(towersUp, HelloWorldRole.Defamation, patch, () => defamationSpots, (from, bearing) => AlongRing(from, DefamationRing, DefamationSpot, bearing), 0.2f, 6f);
        PairUp(towersUp, HelloWorldRole.Stack, patch, () => stackSpots, (from, bearing) => AlongRing(from, StackRing, StackRing, bearing), 0.2f, 6f);
        LeanUnderOmega(patch, HelloWorldRole.Remote, HelloWorldRole.Stack, RemoteWait);
        LeanUnderOmega(patch, HelloWorldRole.Local, patch == 3 ? HelloWorldRole.Stack : HelloWorldRole.Defamation, patch == 3 ? RemoteWait : LocalWait);
        Meet(resolve - MeetFrom, resolve - MeetUntil, HelloWorldRole.Remote, HelloWorldRole.Stack, patch, BesideStack);

        if (patch == 3)
        {
            Meet(resolve - MeetFrom, resolve - MeetUntil, HelloWorldRole.Local, HelloWorldRole.Stack, patch, BetweenTheStacks);
            PairUp(resolve + 1.2f, HelloWorldRole.Remote, patch, () => Either(stackSide, 70f), (_, bearing) => [TopCompass.Point(8f, bearing)], 0f);
            PairUp(resolve + 1.2f, HelloWorldRole.Local, patch, () => Either(stackSide, 20f), (_, bearing) => [TopCompass.Point(3f, bearing)], 0.2f);
            return;
        }

        Meet(resolve - MeetFrom, resolve - MeetUntil, HelloWorldRole.Local, HelloWorldRole.Defamation, patch, BesideDefamation);
        TakeTheRots(float.MaxValue, resolve + PickUpAfter, patch, HelloWorldRole.Remote, HelloWorldRole.Stack, state.StackColor,
            remote => StepClearOfTheRots(remote, patch, Either(stackSide, 50f)));
        TakeTheRots(resolve + WalkUpAfter, resolve + PickUpAfter, patch, HelloWorldRole.Local, HelloWorldRole.Defamation, state.DefamationColor,
            local => ComeTogetherOnceTheBlueTetherBreaks(local, patch, Either(defamationSide, 15f)));
        WaitUnderOmega(patch + 1, resolve + 9.3f);
    }

    private void LeanUnderOmega(int patch, HelloWorldRole tetherRole, HelloWorldRole holderRole, float radius)
    {
        var tethers = Members(tetherRole, patch);
        var holders = Members(holderRole, patch);
        var until = Resolves[patch] - MeetFrom;
        void Look()
        {
            if (world.Events.Elapsed > until) return;
            AssignPartners(patch, tethers, holders);
            foreach (var tether in tethers.Where(Drives).Where(IsAlive))
                if (meetings[patch].TryGetValue(tether, out var holder))
                    GoNow(tether, [TopCompass.Point(radius, TopCompass.Bearing(Live(holder)))], 0f);
            world.Events.Add(MeetEvery, Look);
        }
        world.Events.Add(TowerCasts[patch] + LeanAfterTowers, Look);
    }

    private void Meet(float from, float until, HelloWorldRole tetherRole, HelloWorldRole holderRole, int patch,
        Func<PartyRole, PartyRole, int, Vector2?> spotFor)
    {
        var tethers = Members(tetherRole, patch);
        var holders = Members(holderRole, patch);
        void Look()
        {
            if (world.Events.Elapsed > until) return;
            AssignPartners(patch, tethers, holders);
            foreach (var tether in tethers.Where(Drives).Where(IsAlive))
                if (meetings[patch].TryGetValue(tether, out var holder) && spotFor(tether, holder, patch) is { } spot)
                    GoNow(tether, [spot], 0f);
            world.Events.Add(MeetEvery, Look);
        }
        world.Events.Add(from, Look);
    }

    private void AssignPartners(int patch, IReadOnlyList<PartyRole> tethers, IReadOnlyList<PartyRole> holders)
    {
        var meeting = meetings[patch];
        var liveHolders = holders.Where(IsAlive).ToList();
        if (tethers.Count != 2 || liveHolders.Count == 0) return;
        if (liveHolders.Count == 1)
        {
            foreach (var tether in tethers) meeting[tether] = liveHolders[0];
            return;
        }
        var crossed = CrossedIsClearlyShorter(tethers, (tether, k) => Vector2.Distance(Live(tether), Live(liveHolders[k])));
        meeting[tethers[0]] = liveHolders[crossed ? 1 : 0];
        meeting[tethers[1]] = liveHolders[crossed ? 0 : 1];
    }

    private static bool CrossedIsClearlyShorter(IReadOnlyList<PartyRole> inRoleOrder, Func<PartyRole, int, float> walk)
    {
        var (straightLonger, straightShorter) = LongerAndShorter(walk(inRoleOrder[0], 0), walk(inRoleOrder[1], 1));
        var (crossedLonger, crossedShorter) = LongerAndShorter(walk(inRoleOrder[0], 1), walk(inRoleOrder[1], 0));
        if (MathF.Abs(straightLonger - crossedLonger) > SwitchMargin) return crossedLonger < straightLonger;
        return crossedShorter + SwitchMargin < straightShorter;
    }

    private static (float Longer, float Shorter) LongerAndShorter(float a, float b) => (MathF.Max(a, b), MathF.Min(a, b));

    private Vector2? BesideStack(PartyRole remote, PartyRole stack, int patch)
    {
        var holder = Live(stack);
        var other = Members(HelloWorldRole.Stack, patch).Where(role => role != stack).Select(Live).FirstOrDefault(holder);
        var partner = Members(HelloWorldRole.Remote, patch).FirstOrDefault(role => role != remote);
        var partnerAt = Live(partner);
        var lean = other == holder ? Vector2.Zero : Vector2.Normalize(other - holder);
        var guide = holder + lean * StackLead;
        var defamations = Members(HelloWorldRole.Defamation, patch).Where(IsAlive).Select(Live).ToList();
        var towers = Towers(patch);
        return Best(Around(holder, [2.5f, 3f, 3.5f, 4f]), guide, spot =>
            Over(Vector2.Distance(spot, holder), StackReach)
            + Under(Vector2.Distance(spot, other), other == holder ? 0f : StackClear)
            + towers.Sum(tower => Under(Vector2.Distance(spot, tower), TowerClear))
            + defamations.Sum(defamation => Under(Vector2.Distance(spot, defamation), DefamationClear))
            + Over(Vector2.Distance(spot, partnerAt), RemoteTetherReach));
    }

    private Vector2? BesideDefamation(PartyRole local, PartyRole defamation, int patch)
    {
        var holder = Live(defamation);
        var otherAt = Members(HelloWorldRole.Defamation, patch).Where(role => role != defamation && IsAlive(role)).Select(role => (Vector2?)Live(role)).FirstOrDefault();
        var partner = Members(HelloWorldRole.Local, patch).FirstOrDefault(role => role != local);
        var partnerAt = Live(partner);
        var towers = Towers(patch);
        var tower = towers.MinBy(spot => Vector2.Distance(spot, holder));
        var towerBearing = TopCompass.Bearing(tower);
        var away = otherAt is { } awayFrom ? MathF.Sign(TopCompass.Turn(TopCompass.Bearing(awayFrom), towerBearing)) : 1f;
        if (away == 0f) away = 1f;
        var guide = TopCompass.Point(MaxMelee, towerBearing + away * FlankAngle);
        var stacks = Members(HelloWorldRole.Stack, patch).Where(IsAlive).Select(Live).ToList();
        var rotHolders = Members(HelloWorldRole.Stack, patch).Concat(Members(HelloWorldRole.Defamation, patch))
                                                              .Where(role => role != local && IsAlive(role)).Select(Live).ToList();
        var here = Live(local);
        var reach = AiManager.RunSpeed * MathF.Max(0f, Resolves[patch] - Settle - world.Events.Elapsed - PromptMoveDelay);

        float Shortfall(Vector2 spot, Vector2 taken, Vector2? avoided)
        {
            var travel = Vector2.Distance(here, spot);
            var at = travel <= reach ? spot : here + (spot - here) / travel * reach;
            return Over(Vector2.Distance(at, taken), DefamationReach)
                   + (avoided is { } avoid ? Under(Vector2.Distance(at, avoid), DefamationClear) : 0f)
                   + towers.Sum(t => Under(Vector2.Distance(at, t), TowerClear))
                   + stacks.Sum(stack => Under(Vector2.Distance(at, stack), StackClear))
                   + Under(Vector2.Distance(at, partnerAt), LocalTetherClear)
                   + rotHolders.Sum(rot => Under(DistanceToSegment(rot, here, spot), RotPathClear));
        }

        var flank = new List<Vector2>();
        for (var radius = 12.5f; radius <= 18f; radius += 1f)
            for (var angle = 16f; angle <= 56f; angle += 4f)
                flank.Add(TopCompass.Point(radius, towerBearing + away * angle));
        var (spot, shortfall) = BestScored(flank, guide, candidate => Shortfall(candidate, holder, otherAt));
        if (shortfall <= ShareWhen || otherAt is not { } shared) return spot;

        var sharing = ShareRings.SelectMany(radius => Enumerable.Range(0, 36).Select(k => TopCompass.Point(radius, 10f * k)));
        var (shareSpot, shareShortfall) = BestScored(sharing, here, candidate => Shortfall(candidate, shared, holder));
        return shareShortfall + ShareWhen <= shortfall ? shareSpot : spot;
    }

    private Vector2? BetweenTheStacks(PartyRole local, PartyRole stack, int patch)
    {
        var stacks = Members(HelloWorldRole.Stack, patch).Where(IsAlive).Select(Live).ToList();
        if (stacks.Count == 0) return null;
        var middle = stacks.Aggregate(Vector2.Zero, (sum, at) => sum + at) / stacks.Count;
        var guide = middle * 0.85f;
        var defamations = Members(HelloWorldRole.Defamation, patch).Where(IsAlive).Select(Live).ToList();
        var towers = Towers(patch);
        return Best(Around(guide, [0f, 1f, 2f, 3f]), guide, spot =>
            towers.Sum(tower => Under(Vector2.Distance(spot, tower), TowerClear))
            + defamations.Sum(defamation => Under(Vector2.Distance(spot, defamation), DefamationClear)));
    }

    private static Vector2? Best(IEnumerable<Vector2> candidates, Vector2 guide, Func<Vector2, float> shortfall)
        => BestScored(candidates, guide, shortfall).Spot;

    private static (Vector2? Spot, float Shortfall) BestScored(IEnumerable<Vector2> candidates, Vector2 guide, Func<Vector2, float> shortfall)
    {
        Vector2? best = null;
        var bestKey = (float.MaxValue, float.MaxValue);
        foreach (var spot in candidates)
        {
            var key = (MathF.Round(shortfall(spot), 2), Vector2.Distance(spot, guide));
            if (key.CompareTo(bestKey) >= 0) continue;
            bestKey = key;
            best = spot;
        }
        return (best, bestKey.Item1);
    }

    private static IEnumerable<Vector2> Around(Vector2 centre, float[] rings)
    {
        foreach (var ring in rings)
            for (var k = 0; k < (ring > 0f ? 16 : 1); k++)
                yield return centre + TopCompass.Point(ring, 22.5f * k);
    }

    private static float Over(float value, float limit) => MathF.Max(0f, value - limit);

    private static float Under(float value, float limit) => MathF.Max(0f, limit - value);

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var along = to - from;
        var lengthSquared = along.LengthSquared();
        var t = lengthSquared < 1e-9f ? 0f : Math.Clamp(Vector2.Dot(point - from, along) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, from + along * t);
    }

    private void TakeTheRots(float walkUpFrom, float stepOnFrom, int patch, HelloWorldRole tetherRole, HelloWorldRole holderRole, RotColor color,
                             Action<PartyRole> onceTaken)
    {
        var tethers = Members(tetherRole, patch);
        var holders = Members(holderRole, patch);
        foreach (var tether in tethers)
        {
            var stepOn = stepOnFrom + Hesitation();
            var giveUp = stepOnFrom + PickUpPatience;
            void Look()
            {
                if (!Drives(tether) || !IsAlive(tether) || world.Events.Elapsed > giveUp) return;
                if (world.Party.Get(tether) is { } member && member.HasStatus(color.RotStatusId))
                {
                    onceTaken(tether);
                    return;
                }
                AssignPartners(patch, tethers, holders.Where(holder => world.Party.Get(holder) is { } h && h.HasStatus(color.RotStatusId)).ToList());
                if (meetings[patch].TryGetValue(tether, out var holder))
                {
                    var there = Live(holder);
                    var here = Live(tether);
                    if (world.Events.Elapsed >= stepOn) GoNow(tether, [there], 0f, holder);
                    else if (Vector2.Distance(here, there) > Standoff + 0.5f) GoNow(tether, [there + Vector2.Normalize(here - there) * Standoff], 0f);
                }
                world.Events.Add(PickUpEvery, Look);
            }
            world.Events.Add(MathF.Min(walkUpFrom, stepOn), Look);
        }
    }

    private void StepClearOfTheRots(PartyRole remote, int patch, (float First, float Second) markers)
    {
        var oldRots = Members(HelloWorldRole.Stack, patch).Concat(Members(HelloWorldRole.Defamation, patch)).Where(IsAlive).Select(Live).ToList();
        var here = Live(remote);
        var spots = new[] { TopCompass.Point(DefamationSpot, markers.First), TopCompass.Point(DefamationSpot, markers.Second) };
        var spot = OwnSpotOfTheTwo(remote, Members(HelloWorldRole.Remote, patch), spots)
                   ?? spots.OrderBy(candidate => oldRots.Sum(rot => Under(Vector2.Distance(candidate, rot), RotClear)))
                       .ThenBy(candidate => Vector2.Distance(candidate, here)).First();
        GoNow(remote, [spot], 0.2f);
    }

    private Vector2? OwnSpotOfTheTwo(PartyRole role, IReadOnlyList<PartyRole> pair, Vector2[] spots)
    {
        if (pair.Where(other => other != role && IsAlive(other)).Select(other => (PartyRole?)other).FirstOrDefault() is not { } partner)
            return null;
        PartyRole[] inRoleOrder = role < partner ? [role, partner] : [partner, role];
        var crossed = CrossedIsClearlyShorter(inRoleOrder, (person, k) => Vector2.Distance(Live(person), spots[k]));
        return (role == inRoleOrder[0]) != crossed ? spots[0] : spots[1];
    }

    private void ComeTogetherOnceTheBlueTetherBreaks(PartyRole local, int patch, (float First, float Second) spots)
    {
        var remotes = Members(HelloWorldRole.Remote, patch);
        var partner = Members(HelloWorldRole.Local, patch).FirstOrDefault(role => role != local);
        var oldRots = Members(HelloWorldRole.Stack, patch).Concat(Members(HelloWorldRole.Defamation, patch)).ToList();
        var clearOnce = false;
        Vector2? heading = null;
        void Look()
        {
            if (!Drives(local) || !IsAlive(local)) return;
            var blueUp = remotes.Any(remote => world.Party.Get(remote) is { } member && member.IsAlive() && member.HasStatus(StatusId.HWRemoteTether));
            if (world.Events.Elapsed < TethersLive[patch] + 0.5f || blueUp || AnyoneVulnerable())
            {
                clearOnce = false;
                world.Events.Add(PickUpEvery, Look);
                return;
            }
            if (!clearOnce)
            {
                clearOnce = true;
                world.Events.Add(PickUpEvery, Look);
                return;
            }
            var here = Live(local);
            if (heading is not { } target)
            {
                var candidates = new[] { TopCompass.Point(DefamationSpot, spots.First), TopCompass.Point(DefamationSpot, spots.Second) };
                heading = OwnSpotOfTheTwo(local, Members(HelloWorldRole.Local, patch), candidates)
                          ?? candidates.MinBy(candidate => Vector2.Distance(candidate, here));
                GoNow(local, [heading.Value], 0f);
            }
            else if (world.Party.Get(local) is not { } self || !self.HasStatus(StatusId.HWLocalTether))
                return;
            else if (Vector2.Distance(here, target) < 0.5f && Vector2.Distance(here, Live(partner)) > Geometry.HwTetherBreakDistance - 0.5f
                     && CloserToThePartner(here, Live(partner), StillTicking(oldRots)) is { } closer)
            {
                heading = closer;
                GoNow(local, [closer], 0f);
            }
            world.Events.Add(PickUpEvery, Look);
        }
        world.Events.Add(0f, Look);
    }

    private bool AnyoneVulnerable()
        => PerRole.All.Any(role => world.Party.Get(role) is { } member && member.IsAlive()
                                   && (member.HasStatus(StatusId.MagicVulnerabilityUp) || member.HasStatus(StatusId.MagicVulnerabilityUpMini)));

    private List<Vector2> StillTicking(IEnumerable<PartyRole> oldRots)
        => oldRots.Where(role => world.Party.Get(role) is { } member && member.IsAlive()
                                 && (member.HasStatus(RotColor.Red.RotStatusId) || member.HasStatus(RotColor.Blue.RotStatusId)))
                  .Select(Live).ToList();

    private static Vector2? CloserToThePartner(Vector2 here, Vector2 partner, List<Vector2> ticking)
    {
        var towards = TopCompass.Bearing(here - partner);
        foreach (var turn in new[] { 0f, -20f, 20f, -40f, 40f, -60f, 60f, -80f, 80f })
        {
            var spot = partner + TopCompass.Point(CloseIn, towards + turn);
            if (ticking.All(rot => Vector2.Distance(spot, rot) >= RotClear && DistanceToSegment(rot, here, spot) >= RotClear)) return spot;
        }
        return null;
    }

    private void WaitUnderOmega(int patch, float time)
    {
        var remotes = Members(HelloWorldRole.Remote, patch);
        PairUp(time, HelloWorldRole.Remote, patch, () => Either(Middle(remotes), 15f), (from, bearing) => AroundUnderOmega(from, 3f, bearing), 0.2f);

        var locals = Members(HelloWorldRole.Local, patch);
        var together = patch == 3;
        var radius = together ? 3f : 5.5f;
        PairUp(time, HelloWorldRole.Local, patch, () => Either(Middle(locals), together ? 15f : 90f), (from, bearing) => AroundUnderOmega(from, radius, bearing), 0.2f);
    }

    private void PairUp(float time, HelloWorldRole kind, int patch, Func<(float First, float Second)> spots, Func<Vector2, float, List<Vector2>> path,
                        float jitter, float watch = 2.4f)
    {
        var pair = Members(kind, patch);
        var bearings = new float[2];
        var going = new Dictionary<PartyRole, float>();
        void Look()
        {
            if (pair.Count != 2) return;
            var crossed = CrossedIsClearlyShorter(pair, (role, k) => WalkLength(Live(role), path(Live(role), bearings[k])));
            for (var i = 0; i < 2; i++)
            {
                var role = pair[i];
                var bearing = bearings[crossed ? 1 - i : i];
                if (!Drives(role) || (going.TryGetValue(role, out var current) && current == bearing)) continue;
                going[role] = bearing;
                GoNow(role, path(Live(role), bearing), jitter);
            }
        }
        world.Events.Add(time, () =>
        {
            (bearings[0], bearings[1]) = spots();
            Look();
        });
        for (var look = LookEvery; look <= watch + 0.01f; look += LookEvery)
            world.Events.Add(time + look, Look);
    }

    private static float WalkLength(Vector2 from, IEnumerable<Vector2> waypoints)
    {
        var length = 0f;
        foreach (var waypoint in waypoints)
        {
            length += Vector2.Distance(from, waypoint);
            from = waypoint;
        }
        return length;
    }

    private void GoNow(PartyRole role, IEnumerable<Vector2> waypoints, float jitter, PartyRole? meeting = null)
    {
        var route = routes[role] = routes.GetValueOrDefault(role) + 1;
        var from = Live(role);
        var intended = waypoints.ToList();
        var (path, legs) = Drives(role) ? RouteAroundTheRots(role, from, intended, meeting) : (intended, Enumerable.Range(0, intended.Count).ToList());
        var fires = new List<float>();
        var delay = 0f;
        foreach (var waypoint in path)
        {
            fires.Add(world.Events.Elapsed + delay);
            world.Events.Add(delay, () =>
            {
                if (routes[role] == route) ai.Move(PromptMoveDelay, () => AiMove.Single(role, waypoint), jitter: jitter);
            });
            delay += Vector2.Distance(from, waypoint) / AiManager.RunSpeed + 0.05f;
            from = waypoint;
        }
        if (!Drives(role) || path.Count == 0) return;
        walks[role] = new Walk(route, intended, path, legs, fires, meeting, jitter, PerRole.All.ToDictionary(other => other, Live));
        if (steering) return;
        steering = true;
        world.Events.Add(SteerEvery, KeepOutOfEachOthersWay);
    }

    private void KeepOutOfEachOthersWay()
    {
        foreach (var (role, walk) in walks.ToList())
        {
            if (routes.GetValueOrDefault(role) != walk.Route || !Drives(role) || !IsAlive(role))
            {
                walks.Remove(role);
                continue;
            }
            var step = Math.Max(0, walk.Fires.FindLastIndex(at => at <= world.Events.Elapsed));
            if (step == walk.Path.Count - 1 && Vector2.Distance(Live(role), walk.Path[^1]) < Arrived)
            {
                walks.Remove(role);
                continue;
            }
            if (PerRole.All.Any(other => other != role && Vector2.Distance(Live(other), walk.Seen[other]) > NoticeMoved) && StillInTheWay(role, walk, step))
                GoNow(role, StillAhead(Live(role), walk.Intended.Skip(walk.Legs[step])), walk.Jitter, walk.Meeting);
        }
        steering = walks.Count > 0;
        if (steering) world.Events.Add(SteerEvery, KeepOutOfEachOthersWay);
    }

    private (List<Vector2> Path, List<int> Legs) RouteAroundTheRots(PartyRole role, Vector2 from, List<Vector2> waypoints, PartyRole? meeting)
    {
        var people = ARotWouldPassBetween(role, meeting);
        var outward = HoldsARot(role);
        var path = new List<Vector2>();
        var legs = new List<int>();
        var at = from;
        var walked = 0f;
        for (var leg = 0; leg < waypoints.Count; leg++)
        {
            var waypoint = waypoints[leg];
            if (walked < LookAhead)
            {
                waypoint = ClearOf(waypoint, people, outward);
                foreach (var bend in Sidestep(at, waypoint, people, outward, 3))
                {
                    path.Add(bend);
                    legs.Add(leg);
                }
            }
            path.Add(waypoint);
            legs.Add(leg);
            walked += Vector2.Distance(at, waypoint);
            at = waypoint;
        }
        return (path, legs);
    }

    private bool StillInTheWay(PartyRole role, Walk walk, int step)
    {
        var people = ARotWouldPassBetween(role, walk.Meeting);
        var at = Live(role);
        var walked = 0f;
        foreach (var waypoint in walk.Path.Skip(step))
        {
            if (walked >= LookAhead) break;
            if (people.Any(person => Vector2.Distance(waypoint, person) < PassClear - Grace) || FirstInTheWay(at, waypoint, people) is not null) return true;
            walked += Vector2.Distance(at, waypoint);
            at = waypoint;
        }
        return false;
    }

    private static List<Vector2> StillAhead(Vector2 here, IEnumerable<Vector2> waypoints)
    {
        var left = waypoints.ToList();
        while (left.Count >= 2 && Vector2.Distance(here, left[1]) <= Vector2.Distance(left[0], left[1])) left.RemoveAt(0);
        return left;
    }

    private List<Vector2> ARotWouldPassBetween(PartyRole role, PartyRole? meeting)
        => PerRole.All.Where(other => other != role && other != meeting && IsAlive(other) && ARotWouldPass(role, other)).Select(Live).ToList();

    private bool ARotWouldPass(PartyRole one, PartyRole other)
        => world.Party.Get(one) is { } a && world.Party.Get(other) is { } b && RotColors.Any(color => Catches(a, b, color) || Catches(b, a, color));

    private static bool Catches(SimCharacter holder, SimCharacter other, RotColor color)
        => holder.HasStatus(color.RotStatusId) && !other.HasStatus(color.RotStatusId) && !other.HasStatus(color.DebuggerStatusId);

    private bool HoldsARot(PartyRole role)
        => world.Party.Get(role) is { } member && RotColors.Any(color => member.HasStatus(color.RotStatusId));

    private static Vector2? FirstInTheWay(Vector2 from, Vector2 to, IEnumerable<Vector2> people)
    {
        Vector2? first = null;
        var firstReached = float.MaxValue;
        var along = to - from;
        var length = along.Length();
        foreach (var person in people)
        {
            var gap = DistanceToSegment(person, from, to);
            if (gap >= PassClear - Grace || gap >= Vector2.Distance(person, from) - Grace) continue;
            var reached = length < 1e-6f ? 0f : Math.Clamp(Vector2.Dot(person - from, along) / length, 0f, length);
            if (reached >= firstReached) continue;
            firstReached = reached;
            first = person;
        }
        return first;
    }

    private static Vector2 ClearOf(Vector2 spot, List<Vector2> people, bool outward)
    {
        bool Clear(Vector2 at) => people.All(person => Vector2.Distance(at, person) >= PassClear - Grace);
        if (Clear(spot)) return spot;
        var radius = spot.Length();
        var bearing = TopCompass.Bearing(spot);
        for (var step = 0.5f; step <= 4f; step += 0.5f)
            foreach (var moved in outward ? new[] { radius + step, radius - step } : new[] { radius - step, radius + step })
                if (moved is >= 0f and <= ArenaEdge && Clear(TopCompass.Point(moved, bearing)))
                    return TopCompass.Point(moved, bearing);
        return spot;
    }

    private static List<Vector2> Sidestep(Vector2 from, Vector2 to, List<Vector2> people, bool outward, int depth)
    {
        if (depth == 0 || FirstInTheWay(from, to, people) is not { } person) return [];
        var others = people.Where(other => other != person).ToList();
        List<Vector2>? best = null;
        var bestKey = (int.MaxValue, float.MaxValue);
        foreach (var turning in new[] { 1, -1 })
        {
            var arc = ArcAround(from, to, person, turning);
            if (arc.Count == 0) return [];
            var stops = arc.Prepend(from).Append(to).ToList();
            var legs = stops.Zip(stops.Skip(1)).ToList();
            var blocked = arc.Count(at => at.Length() > ArenaEdge + Grace) + legs.Count(leg => FirstInTheWay(leg.First, leg.Second, others) is not null);
            var key = (blocked, outward ? -arc[arc.Count / 2].Length() : legs.Sum(leg => Vector2.Distance(leg.First, leg.Second)));
            if (key.CompareTo(bestKey) >= 0) continue;
            bestKey = key;
            best = arc;
        }
        return [.. Sidestep(from, best![0], others, outward, depth - 1), .. best, .. Sidestep(best[^1], to, others, outward, depth - 1)];
    }

    private static List<Vector2> ArcAround(Vector2 from, Vector2 to, Vector2 person, int turning)
    {
        if (Vector2.Distance(from, person) <= PassAround && Vector2.Distance(to, person) <= PassAround) return [];
        float Tangent(Vector2 at, int side)
        {
            var offset = at - person;
            var angle = MathF.Atan2(offset.Y, offset.X);
            return offset.Length() <= PassAround ? angle : angle + side * MathF.Acos(PassAround / offset.Length());
        }
        var start = Tangent(from, turning);
        var end = Tangent(to, -turning);
        var sweep = ((end - start) * turning % MathF.Tau + MathF.Tau) % MathF.Tau;
        if (sweep > 1.5f * MathF.PI) sweep = 0f;
        var steps = Math.Max(1, (int)MathF.Ceiling(sweep / (MathF.PI / 6f)));
        var reach = PassAround / MathF.Cos(sweep / steps / 2f);
        return Enumerable.Range(0, steps + 1)
                         .Select(k => start + turning * sweep * k / steps)
                         .Select(angle => person + reach * new Vector2(MathF.Cos(angle), MathF.Sin(angle)))
                         .ToList();
    }

    private float Hesitation() => noise.NextFloat(0f, 0.25f);

    private bool Drives(PartyRole role)
        => world.Party.Get(role) is { } member && world.Party.IsBotDriven(member);

    private bool IsAlive(PartyRole role) => world.Party.Get(role) is { } member && member.IsAlive();

    private Vector2 Live(PartyRole role)
        => world.Party.Get(role) is { } member ? new Vector2(member.Position.X, member.Position.Z) : TopCompass.Point(10f, ClockSpots[role]);

    private List<Vector2> Towers(int patch)
    {
        var towers = state.Towers[patch];
        return Enumerable.Range(0, 4).Select(i => new Vector2(towers.Position(i).X, towers.Position(i).Z)).ToList();
    }

    private float DefamationBearing(int patch)
    {
        var towers = state.Towers[patch];
        var bearings = Enumerable.Range(0, 4)
                                 .Where(i => towers.ColorOf(i) == state.DefamationColor)
                                 .Select(i => TopCompass.Bearing(new Vector2(towers.Position(i).X, towers.Position(i).Z)))
                                 .ToList();
        return bearings[0] + TopCompass.Turn(bearings[0], bearings[1]) / 2f;
    }

    private List<PartyRole> Members(HelloWorldRole role, int patch) => state.WithRole(role, patch).ToList();

    private float Middle(IReadOnlyList<PartyRole> pair)
    {
        var first = TopCompass.Bearing(Live(pair[0]));
        return first + TopCompass.Turn(first, TopCompass.Bearing(Live(pair[1]))) / 2f;
    }

    private static (float First, float Second) Either(float axis, float offset) => (axis - offset, axis + offset);

    private static List<Vector2> AroundUnderOmega(Vector2 from, float radius, float bearing)
        => [TopCompass.Point(radius, TopCompass.Bearing(from)), TopCompass.Point(radius, bearing)];

    private static List<Vector2> AlongRing(Vector2 from, float ring, float spot, float bearing)
    {
        var start = TopCompass.Bearing(from);
        var turn = TopCompass.Turn(start, bearing);
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(turn) / 30f));
        return [TopCompass.Point(ring, start), .. Enumerable.Range(1, steps).Select(i => TopCompass.Point(ring, start + turn * i / steps)), TopCompass.Point(spot, bearing)];
    }
}
