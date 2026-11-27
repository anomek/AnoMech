using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Dsr.P3Nidhogg;

public sealed class DsrP3NidhoggAi(DsrP3NidhoggAi.ArrowFacing arrowFacing) : IScenarioAi<DsrP3NidhoggState>
{
    public enum ArrowFacing
    {
        West,
        East,
    }

    public string Name => arrowFacing == ArrowFacing.West
        ? "Westhogg (NAUR): stack N; circle S, up E, down W, face west; towers H1-M1 NW, MT-R1 NE, OT-R2 SW, H2-M2 SE"
        : "Easthogg (LPDU): stack N; circle S, up W, down E, face east; towers H1-M1 NW, MT-R1 NE, OT-R2 SW, H2-M2 SE";

    private const float InRadius = 4f;
    private const float OutRadius = 10.5f;
    private const float WheelEdge = 8f;
    private const float WheelMargin = 0.5f;
    private const float StackRadius = 8.5f;
    private const float JumpRadius = 7.5f;
    private const float SecondJumpRadius = 11f;
    private const float TowerReach = 4f;
    private const float DiveBaitBeyondTower = 1f;
    private const float SecondTowerSoakBeyond = 3f;
    private const float CounterBaitRadius = 16f;
    private const float CounterBaitBeyondTower = 1f;
    private const float TankSpotRadius = 3f;
    private const float IdleRadius = 4f;
    private const float CleaveDodgeRadius = 6f;
    private const float InterceptDistance = 1.5f;
    private const float AfterDivesRadius = 7f;
    private const float SideIdleRadius = 8f;
    private const float FirstSignalRadius = 6f;
    private const float SecondSignalRadius = 8f;
    private const float ThirdSignalRadius = 11f;
    private const float ClaimRadius = 2.5f;
    private const float GeirskogulHalfWidth = 4f;
    private const float GeirskogulLength = 62f;
    private const float GeirskogulDangerSeconds = 4.5f;
    private const float BaiterLeavesWithin = 0.3f;
    private const float LineMargin = 0.5f;
    private const float LineExitMargin = 1.5f;
    private const float DodgeCheckInterval = 0.2f;
    private const float DodgeUntil = 90f;
    private const float ArenaHalfWidth = 20f;

    private static readonly float North = 0f;
    private static readonly float East = MathF.PI / 2f;
    private static readonly float South = MathF.PI;
    private static readonly float West = 3f * MathF.PI / 2f;
    private static readonly float NorthWest = 7f * MathF.PI / 4f;
    private static readonly float NorthEast = MathF.PI / 4f;
    private static readonly float SouthEast = 3f * MathF.PI / 4f;
    private static readonly float SouthWest = 5f * MathF.PI / 4f;

    private static readonly Dictionary<PartyRole, int> HomeCounterTower = new()
    {
        [PartyRole.MainTank] = 0, [PartyRole.PhysRangedDps] = 0,
        [PartyRole.ShieldHealer] = 1, [PartyRole.MeleeDpsB] = 1,
        [PartyRole.OffTank] = 2, [PartyRole.CasterDps] = 2,
        [PartyRole.RegenHealer] = 3, [PartyRole.MeleeDpsA] = 3,
    };

    private DsrP3NidhoggState state = null!;
    private SimWorld world = null!;
    private Dictionary<PartyRole, int> counterTower = new();
    private Dictionary<PartyRole, Vector3> counterBait = new();
    private Dictionary<PartyRole, Vector3> sentTo = new();
    private Dictionary<PartyRole, int> claimedSide = new();
    private bool sidesLocked;
    private Dictionary<PartyRole, Vector3> planned = new();
    private Dictionary<SimEnemy, float> geirskogulLines = new();
    private HashSet<PartyRole> heldOutOfLines = new();

    public void Run(DsrP3NidhoggState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        counterTower = new();
        counterBait = new();
        sentTo = new();
        claimedSide = new();
        sidesLocked = false;
        planned = new();
        geirskogulLines = new();
        heldOutOfLines = new();
        var ai = new AiManager(world);

        ai.Move(0.5f, () => ByRole(Idle));
        ai.Move(19.0f, SignalIntendedSides);
        ai.Move(20.5f, SignalIntendedSides);
        ai.Move(22.0f, SignalIntendedSides);
        ai.Move(23.5f, SignalIntendedSides);
        world.Events.Add(24.5f, LockClaimedSides);
        ai.Move(24.6f, () => ByRole(role => Order(role) == 1 ? JumpSpot(role) : Stack()), arrivalTime: 32.8f);
        world.Events.Add(32.9f, () => FaceArrowJumpers(1));
        ai.Move(33.6f, () => ByRole(role => FirstRoundWheel(role, 0)), arrivalTime: 36.7f);
        ai.Move(37.1f, () => ByRole(role => Order(role) == 3 ? FirstRoundWheel(role, 1) : null), arrivalTime: 39.5f);
        ai.Move(37.1f, () => ByRole(role => Order(role) == 3 ? null : FirstRoundWheel(role, 1)), arrivalTime: 39.7f);
        world.Events.Add(40.0f, DodgeGeirskogulLines);
        world.Events.Add(42.53f, () => StartGeirskogulLines(DiveTowers(1)));
        world.Events.Add(52.54f, () => StartGeirskogulLines(DiveTowers(2)));
        world.Events.Add(63.54f, () => StartGeirskogulLines(DiveTowers(3)));
        world.Events.Add(82.72f, () => StartGeirskogulLines(UntetheredCounterTowers()));
        ai.Move(40.1f, () => ByRole(role => Order(role) == 3 ? FirstRoundBait(role) : null), arrivalTime: 41.63f);
        ai.Move(40.1f, () => ByRole(role => Order(role) switch { 2 => JumpSpot(role), 1 => Stack(), _ => null }), arrivalTime: 42.8f);
        ai.Move(41.8f, () => ByRole(role => Order(role) == 3 ? InsideOwnSide(role) : null));
        world.Events.Add(42.9f, () => FaceArrowJumpers(2));
        ai.Move(43.5f, () => ByRole(SecondRoundSoaks), arrivalTime: 49.6f);
        ai.Move(47.1f, () => ByRole(role => Order(role) == 3 ? JumpSpot(role) : null), arrivalTime: 53.7f);
        ai.Move(52.7f, () => ByRole(role => IsSideFirst(role) ? Stack() : null), arrivalTime: 54.8f);
        world.Events.Add(53.9f, () => FaceArrowJumpers(3));
        ai.Move(55.1f, () => ByRole(role => ThirdRoundWheel(role, 2)), arrivalTime: 58.2f);
        ai.Move(58.6f, () => ByRole(role => ThirdRoundTower(role) is null ? null : ThirdRoundWheel(role, 3)), arrivalTime: 60.5f);
        ai.Move(58.6f, () => ByRole(role => ThirdRoundTower(role) is null ? ThirdRoundWheel(role, 3) : null), arrivalTime: 61.3f);
        ai.Move(61.6f, () => ByRole(ThirdRoundBaits), arrivalTime: 62.64f);
        ai.Move(62.8f, () => ByRole(LeaveLastTower));
        ai.Move(63.7f, () => ByRole(role => role == PartyRole.MainTank && ThirdRoundTower(role) is null ? At(NorthEast, TankSpotRadius) : null), arrivalTime: 65.7f);
        ai.Move(63.7f, () => ByRole(role => role != PartyRole.MainTank && ThirdRoundTower(role) is null ? Stack() : null), arrivalTime: 67.5f);

        ai.Move(70.1f, () => ByRole(role => role == PartyRole.MainTank ? null : BehindNidhogg()), arrivalTime: 73.0f);
        ai.Move(70.2f, () => ByRole(role => role == PartyRole.MainTank ? BehindNidhogg() : null), arrivalTime: 73.0f);
        ai.Move(73.8f, () => ByRole(HomeCounterTowerSpot));
        ai.Move(75.6f, () => ByRole(CounterTowerSoak), arrivalTime: 79.8f);
        ai.Move(80.2f, () => ByRole(CounterBait), arrivalTime: 81.72f);
        ai.Move(80.2f, () => ByRole(TakeTetherOrWait));
        ai.Move(81.4f, () => ByRole(TanksTakeTethers));
        ai.Move(81.8f, () => ByRole(LeaveCounterBait));
        ai.Move(82.6f, () => ByRole(TanksTakeTethers));
        ai.Move(83.8f, () => ByRole(TanksTakeTethers));
        ai.Move(85.0f, () => ByRole(TanksTakeTethers));
        ai.Move(87.4f, () => ByRole(AfterTowers));
    }

    private int Order(PartyRole role) => state.Order[role];

    private static bool IsTank(PartyRole role) => role is PartyRole.MainTank or PartyRole.OffTank;

    private static Vector3 At(float bearing, float radius) => Compass.AtBearing(bearing, radius);

    private static Vector3? Idle(PartyRole role) => role == PartyRole.MainTank ? TankSpot() : At(South, IdleRadius);

    private static Vector3 TankSpot() => At(North, TankSpotRadius);

    private Vector3 BehindNidhogg()
    {
        if (Nidhogg() is not { } boss) return At(South, CleaveDodgeRadius);
        var facing = new Vector3(MathF.Sin(boss.Rotation), 0f, MathF.Cos(boss.Rotation));
        return boss.Position - facing * CleaveDodgeRadius;
    }

    private Vector3? AfterTowers(PartyRole role) => role switch
    {
        PartyRole.MainTank => TankSpot(),
        PartyRole.OffTank => At(South, IdleRadius),
        _ => OwnCounterTowerSide(role),
    };

    // Straight out of the line once the clones have aimed: the MT to whichever tank spot is nearer,
    // the others to SW or SE.
    private Vector3? LeaveLastTower(PartyRole role)
    {
        if (ThirdRoundTower(role) is not { } tower) return null;
        var west = tower.X < -1f;
        return role == PartyRole.MainTank
            ? At(west ? NorthWest : NorthEast, TankSpotRadius)
            : At(west ? SouthWest : SouthEast, AfterDivesRadius);
    }

    private Vector3 OwnCounterTowerSide(PartyRole role)
        => At(counterTower.GetValueOrDefault(role, HomeCounterTower[role]) <= 1 ? East : West, SideIdleRadius);

    private static Vector3 Stack() => At(North, StackRadius);

    private bool OutAt(int wheel) => (wheel % 2 == 0) == state.OutFirst[wheel / 2];

    private SimEnemy? Nidhogg() => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == DsrConstants.BNpcBaseId.NidhoggP3 && !e.Dead);

    private IEnumerable<SimEnemy> Clones() => world.Children.OfType<SimEnemy>().Where(e => e.BNpcBaseId == DsrConstants.BNpcBaseId.NidhoggClone && !e.Dead);

    private Vector3 StackForWheel(int wheel) => At(North, OutAt(wheel) ? OutRadius : InRadius);

    private int Side(PartyRole role)
    {
        var order = Order(role);
        if (state.OrdersWithArrows.Contains(order))
            return state.Jump[role] switch
            {
                JumpKind.High => 1,
                JumpKind.Forward => arrowFacing == ArrowFacing.West ? 2 : 0,
                _ => arrowFacing == ArrowFacing.West ? 0 : 2,
            };
        return claimedSide.TryGetValue(role, out var claimed) ? claimed : state.InOrder(order).IndexOf(role);
    }

    private IAiMove SignalIntendedSides()
    {
        ClaimSides();
        return ByRole(role => SignalSpot(Order(role), claimedSide[role]));
    }

    private void LockClaimedSides()
    {
        ClaimSides();
        sidesLocked = true;
    }

    private static Vector3 SignalSpot(int order, int side) => order switch
    {
        1 => At(SideBearing(side), FirstSignalRadius),
        2 => At(SecondSideBearing(side), SecondSignalRadius),
        _ => At(SideBearing(side), ThirdSignalRadius),
    };

    private bool IsHumanDriven(PartyRole role)
        => world.Party.Get(role) is { } member && member.IsAlive() && !world.Party.IsBotDriven(member);

    private int? SideSignalledBy(PartyRole role, int order, IEnumerable<int> openSides)
    {
        if (world.Party.Get(role) is not { } member) return null;
        var at = new Vector2(member.Position.X, member.Position.Z);
        float DistanceTo(int side) => Vector2.Distance(at, new Vector2(SignalSpot(order, side).X, SignalSpot(order, side).Z));
        return openSides.Where(side => DistanceTo(side) <= ClaimRadius).OrderBy(DistanceTo).Cast<int?>().FirstOrDefault();
    }

    private void ClaimSides()
    {
        if (sidesLocked) return;
        var claims = new Dictionary<PartyRole, int>();
        foreach (var order in new[] { 1, 2, 3 })
        {
            var group = state.InOrder(order);
            var open = Enumerable.Range(0, group.Count).ToList();
            foreach (var human in group.Where(IsHumanDriven))
            {
                if (SideSignalledBy(human, order, open) is not { } side) continue;
                claims[human] = side;
                open.Remove(side);
            }
            var bots = group.Where(role => !IsHumanDriven(role)).ToList();
            foreach (var bot in bots)
            {
                var wanted = claimedSide.TryGetValue(bot, out var previous) ? previous : group.IndexOf(bot);
                if (!open.Contains(wanted)) continue;
                claims[bot] = wanted;
                open.Remove(wanted);
            }
            foreach (var role in bots.Concat(group).Where(role => !claims.ContainsKey(role)).Distinct())
            {
                claims[role] = open[0];
                open.RemoveAt(0);
            }
        }
        claimedSide = claims;
    }

    private static float SideBearing(int side) => side switch { 0 => West, 1 => South, _ => East };

    private static float SecondSideBearing(int side) => side == 0 ? NorthWest : NorthEast;

    private bool IsSideFirst(PartyRole role) => Order(role) == 1 && Side(role) != 1;

    private Vector3 InsideOwnSide(PartyRole role) => At(SideBearing(Side(role)), InRadius);

    private Vector3 JumpSpot(PartyRole role) => Order(role) == 2
        ? At(SecondSideBearing(Side(role)), SecondJumpRadius)
        : At(SideBearing(Side(role)), JumpRadius);

    private Vector3? TowerNear(int order, Vector3 hint)
        => state.DiveTowers.TryGetValue(order, out var towers) && towers.Count > 0
            ? towers.MinBy(t => Vector3.DistanceSquared(t, hint))
            : null;

    private Vector3? FirstRoundTower(PartyRole role) => TowerNear(1, At(SideBearing(Side(role)), JumpRadius));

    private Vector3? SecondRoundTower(PartyRole role) => TowerNear(2, At(Side(role) == 0 ? NorthWest : NorthEast, SecondJumpRadius));

    private Vector3? ThirdRoundTower(PartyRole role) => Order(role) switch
    {
        2 => TowerNear(3, At(Side(role) == 0 ? West : East, JumpRadius)),
        1 when Side(role) == 1 => TowerNear(3, At(South, JumpRadius)),
        _ => null,
    };

    // Coming from the north stack, an out goes no further round than the southern intercardinals: the
    // south tower is soaked on the next wheel.
    private static Vector3 SoakForWheel(Vector3 tower, bool outside, bool fromNorthStack)
    {
        var towerRadius = new Vector2(tower.X, tower.Z).Length();
        var radius = Math.Clamp(outside ? OutRadius : InRadius, towerRadius - TowerReach, towerRadius + TowerReach);
        var safe = outside ? radius > WheelEdge + WheelMargin : radius < WheelEdge - WheelMargin;
        var bearing = outside && fromNorthStack ? Math.Clamp(Compass.BearingOf(tower), -3f * MathF.PI / 4f, 3f * MathF.PI / 4f) : Compass.BearingOf(tower);
        return safe ? At(bearing, radius) : tower;
    }

    private static Vector3 Beyond(Vector3 tower, float distance)
        => At(Compass.BearingOf(tower), new Vector2(tower.X, tower.Z).Length() + distance);

    private Vector3? FirstRoundWheel(PartyRole role, int wheel)
        => Order(role) == 3 && FirstRoundTower(role) is { } tower ? SoakForWheel(tower, OutAt(wheel), wheel == 0) : StackForWheel(wheel);

    private Vector3? FirstRoundBait(PartyRole role)
        => FirstRoundTower(role) is { } tower ? Beyond(tower, DiveBaitBeyondTower) : Stack();

    private Vector3? SecondRoundSoaks(PartyRole role)
    {
        if (IsSideFirst(role)) return SecondRoundTower(role) is { } tower ? Beyond(tower, SecondTowerSoakBeyond) : Stack();
        return Order(role) == 2 ? Stack() : null;
    }

    private Vector3? ThirdRoundWheel(PartyRole role, int wheel)
        => ThirdRoundTower(role) is { } tower ? SoakForWheel(tower, OutAt(wheel), wheel == 2) : StackForWheel(wheel);

    private Vector3? ThirdRoundBaits(PartyRole role)
        => ThirdRoundTower(role) is { } tower ? Beyond(tower, DiveBaitBeyondTower) : Stack();

    private void FaceArrowJumpers(int order)
    {
        foreach (var role in state.InOrder(order))
        {
            if (state.Jump[role] == JumpKind.High) continue;
            if (world.Party.Get(role) is not { } member || !member.IsAlive() || !world.Party.IsBotDriven(member)) continue;
            member.StopMoving();
            member.Face(member.Position + (arrowFacing == ArrowFacing.West ? -Vector3.UnitX : Vector3.UnitX) * 100f);
        }
    }

    private Vector3? CounterTowerSoak(PartyRole role)
    {
        if (counterTower.Count == 0) AssignCounterTowers();
        return DsrP3NidhoggState.CounterTower(counterTower[role]);
    }

    private static Vector3? HomeCounterTowerSpot(PartyRole role) => DsrP3NidhoggState.CounterTower(HomeCounterTower[role]);

    private static int NearestCounterTower(Vector3 position)
        => Enumerable.Range(0, 4).MinBy(i => Vector3.DistanceSquared(DsrP3NidhoggState.CounterTower(i), position));

    private void AssignCounterTowers()
    {
        counterTower = new Dictionary<PartyRole, int>(HomeCounterTower);
        var soakers = state.CounterTowerSoakers;
        var leavers = new List<PartyRole>();
        for (var tower = 0; tower < 4; tower++)
            if (soakers[tower] == 1)
                leavers.Add(HomeCounterTower.First(e => e.Value == tower && (IsTank(e.Key) || IsMelee(e.Key))).Key);
        var openings = Enumerable.Range(0, 4).SelectMany(t => Enumerable.Repeat(t, Math.Max(0, soakers[t] - 2))).ToList();
        foreach (var leaver in leavers)
        {
            var home = HomeCounterTower[leaver];
            var pick = openings.OrderBy(t => FlexPreference((t - home + 4) % 4)).First();
            openings.Remove(pick);
            counterTower[leaver] = pick;
        }
    }

    private static bool IsMelee(PartyRole role) => role is PartyRole.MeleeDpsA or PartyRole.MeleeDpsB;

    private static int FlexPreference(int clockwiseSteps) => clockwiseSteps switch { 1 => 0, 3 => 1, _ => 2 };

    private Vector3? CounterBait(PartyRole role)
    {
        if (IsTank(role)) return null;
        if (counterBait.Count == 0) AssignCounterBaits();
        if (!counterBait.TryGetValue(role, out var bait)) return null;
        sentTo[role] = bait;
        return bait;
    }

    private Vector3? LeaveCounterBait(PartyRole role)
    {
        if (!counterBait.ContainsKey(role)) return null;
        var spot = OwnCounterTowerSide(role);
        sentTo[role] = spot;
        return spot;
    }

    private Vector3? TakeTetherOrWait(PartyRole role)
    {
        if (IsTank(role)) return TakeTether(role);
        if (counterBait.Count == 0) AssignCounterBaits();
        if (counterBait.ContainsKey(role)) return null;
        var spot = OwnCounterTowerSide(role);
        sentTo[role] = spot;
        return spot;
    }

    private void AssignCounterBaits()
    {
        if (counterTower.Count == 0) AssignCounterTowers();
        var tethered = state.TetherTower;
        var spots = Enumerable.Range(0, 4).Where(t => t != tethered)
            .Select(t => At(Compass.BearingOf(DsrP3NidhoggState.CounterTower(t)), DsrP3NidhoggState.CounterTowerRadius + CounterBaitBeyondTower)).ToList();
        var candidates = Enum.GetValues<PartyRole>().Where(r => !IsTank(r) && world.Party.Get(r) is { } m && m.IsAlive()).ToList();
        var pairs = spots.SelectMany(spot => candidates.Select(role => (spot, role, distance: Vector3.Distance(SoakingAt(role), spot))))
            .OrderBy(p => p.distance).ThenBy(p => p.role);
        foreach (var (spot, role, _) in pairs)
            if (!counterBait.ContainsKey(role) && !counterBait.ContainsValue(spot))
                counterBait[role] = spot;
    }

    // A bot's own tower rather than where its jitter left it, so every client picks the same baiters.
    private Vector3 SoakingAt(PartyRole role)
        => IsHumanDriven(role) ? world.Party.Get(role)!.Position : DsrP3NidhoggState.CounterTower(counterTower[role]);

    private Vector3? TanksTakeTethers(PartyRole role) => IsTank(role) ? TakeTether(role) : null;

    private Vector3? TakeTether(PartyRole role)
    {
        var index = role == PartyRole.MainTank ? 0 : 1;
        if (state.SoulTethers.Count <= index) return role == PartyRole.MainTank ? TankSpot() : null;
        var (source, tether) = state.SoulTethers[index];
        var me = world.Party.Get(role);
        if (tether.B is { } holder && ReferenceEquals(holder, me))
            return index == 0 ? TankSpot() : At(Compass.BearingOf(source.Position), CounterBaitRadius);
        var toward = tether.B is { } current ? HeadingFor(current) - source.Position : Vector3.Zero;
        toward.Y = 0f;
        return toward.LengthSquared() < 0.01f ? source.Position : source.Position + Vector3.Normalize(toward) * InterceptDistance;
    }

    private Vector3 HeadingFor(SimCharacter member) =>
        member is ISimPartyMember { Role: var role } && sentTo.TryGetValue(role, out var spot) ? spot : member.Position;

    private IEnumerable<Vector3> DiveTowers(int order) => state.DiveTowers.GetValueOrDefault(order) ?? [];

    private IEnumerable<Vector3> UntetheredCounterTowers()
        => Enumerable.Range(0, 4).Where(t => t != state.TetherTower).Select(DsrP3NidhoggState.CounterTower);

    private void StartGeirskogulLines(IEnumerable<Vector3> towers)
    {
        var now = world.Events.Elapsed;
        foreach (var tower in towers)
            if (Clones().MinBy(c => Vector3.DistanceSquared(c.Position, tower)) is { } clone)
                geirskogulLines[clone] = now;
    }

    private void DodgeGeirskogulLines()
    {
        var now = world.Events.Elapsed;
        var live = geirskogulLines
            .Where(l => !l.Key.Dead && now >= l.Value + BaiterLeavesWithin && now < l.Value + GeirskogulDangerSeconds)
            .Select(l => (l.Key.Position, new Vector3(MathF.Sin(l.Key.Rotation), 0f, MathF.Cos(l.Key.Rotation)))).ToList();

        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (world.Party.Get(role) is not { } bot || !bot.IsAlive() || !world.Party.IsBotDriven(bot)) continue;
            var here = bot.Position;
            var destination = planned.GetValueOrDefault(role, here);
            if (InLine(live, here, LineMargin) is { } standingIn)
            {
                if (InLine(live, destination, LineMargin) is null) ResumeIfHeld(role, bot, destination);
                else HoldAt(role, bot, ClearOfLine(standingIn, here));
            }
            else if (InLine(live, destination, LineMargin) is not null || PathCrossesLine(live, here, destination))
            {
                heldOutOfLines.Add(role);
                bot.StopMoving();
            }
            else ResumeIfHeld(role, bot, destination);
        }

        if (now + DodgeCheckInterval < DodgeUntil) world.Events.Add(DodgeCheckInterval, DodgeGeirskogulLines);
    }

    private void HoldAt(PartyRole role, SimCharacter bot, Vector3 spot)
    {
        heldOutOfLines.Add(role);
        bot.MoveTo(spot, AiManager.SprintSpeed);
    }

    private void ResumeIfHeld(PartyRole role, SimCharacter bot, Vector3 destination)
    {
        if (heldOutOfLines.Remove(role)) bot.MoveTo(destination, AiManager.RunSpeed);
    }

    private static (Vector3 Origin, Vector3 Direction)? InLine(List<(Vector3 Origin, Vector3 Direction)> lines, Vector3 point, float margin)
    {
        foreach (var line in lines)
        {
            var offset = point - line.Origin;
            var along = offset.X * line.Direction.X + offset.Z * line.Direction.Z;
            var across = MathF.Abs(offset.X * line.Direction.Z - offset.Z * line.Direction.X);
            if (along >= -margin && along <= GeirskogulLength && across <= GeirskogulHalfWidth + margin) return line;
        }
        return null;
    }

    private static bool PathCrossesLine(List<(Vector3 Origin, Vector3 Direction)> lines, Vector3 from, Vector3 to)
    {
        var steps = (int)MathF.Ceiling(Vector3.Distance(from, to) / 0.5f);
        for (var i = 1; i <= steps; i++)
            if (InLine(lines, Vector3.Lerp(from, to, i / (float)steps), LineMargin) is not null) return true;
        return false;
    }

    private static Vector3 ClearOfLine((Vector3 Origin, Vector3 Direction) line, Vector3 point)
    {
        var offset = point - line.Origin;
        var along = offset.X * line.Direction.X + offset.Z * line.Direction.Z;
        var side = offset.X * line.Direction.Z - offset.Z * line.Direction.X >= 0f ? 1f : -1f;
        var normal = new Vector3(line.Direction.Z, 0f, -line.Direction.X) * side;
        var spot = line.Origin + line.Direction * along + normal * (GeirskogulHalfWidth + LineExitMargin);
        return new Vector3(Math.Clamp(spot.X, -ArenaHalfWidth, ArenaHalfWidth), 0f, Math.Clamp(spot.Z, -ArenaHalfWidth, ArenaHalfWidth));
    }

    private IAiMove ByRole(Func<PartyRole, Vector3?> at)
    {
        var spots = Enum.GetValues<PartyRole>().Select(role => (role, spot: at(role))).ToList();
        foreach (var (role, spot) in spots)
            if (spot is { } p) planned[role] = p;
        return AiMove.Create(spots.Select(e => e.spot is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
    }
}
