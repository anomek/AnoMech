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
    private const float DiveBaitBeyondTower = 4.5f;
    private const float SecondTowerSoakBeyond = 3f;
    private const float CounterBaitRadius = 16f;
    private const float TankSpotRadius = 3f;
    private const float IdleRadius = 4f;
    private const float CleaveDodgeRadius = 6f;
    private const float InterceptDistance = 1.5f;
    private const float WestIdleRadius = 7f;
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
    private Dictionary<PartyRole, int> claimedSide = new();
    private bool sidesLocked;
    private readonly bool?[] seenOutFirst = new bool?[2];
    private Dictionary<PartyRole, Vector3> planned = new();
    private Dictionary<SimEnemy, (float Start, Vector3 Origin, Vector3 Direction)> geirskogulLines = new();
    private HashSet<PartyRole> heldOutOfLines = new();

    public void Run(DsrP3NidhoggState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        counterTower = new();
        counterBait = new();
        claimedSide = new();
        sidesLocked = false;
        seenOutFirst[0] = seenOutFirst[1] = null;
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
        ai.Move(24.6f, () => ByRole(role => Order(role) == 1 ? JumpSpot(role) : Stack()), arrivalTime: 32.8f, leaveNow: true);
        world.Events.Add(25.8f, () => ReadWheelOrder(0));
        world.Events.Add(32.9f, () => FaceArrowJumpers(1));
        ai.Move(33.6f, () => ByRole(role => FirstRoundWheel(role, 0)), arrivalTime: 36.7f, leaveNow: true);
        ai.Move(37.1f, () => ByRole(role => FirstRoundWheel(role, 1)), arrivalTime: 39.7f, leaveNow: true);
        world.Events.Add(40.0f, DodgeGeirskogulLines);
        ai.Move(40.1f, () => ByRole(FirstRoundBaitsAndSecondJumpers), arrivalTime: 42.3f, leaveNow: true);
        ai.Move(42.7f, () => ByRole(role => Order(role) == 3 ? InsideOwnSide(role) : null));
        world.Events.Add(42.9f, () => FaceArrowJumpers(2));
        ai.Move(43.5f, () => ByRole(SecondRoundSoaks), arrivalTime: 49.6f, leaveNow: true);
        ai.Move(44.0f, () => ByRole(role => Order(role) == 3 ? Stack() : null));
        ai.Move(47.1f, () => ByRole(role => Order(role) == 3 ? JumpSpot(role) : null));
        world.Events.Add(47.3f, () => ReadWheelOrder(1));
        ai.Move(52.7f, () => ByRole(role => IsSideFirst(role) ? Stack() : null), arrivalTime: 54.8f, leaveNow: true);
        world.Events.Add(53.9f, () => FaceArrowJumpers(3));
        ai.Move(55.1f, () => ByRole(role => ThirdRoundWheel(role, 2)), arrivalTime: 58.2f, leaveNow: true);
        ai.Move(58.6f, () => ByRole(role => ThirdRoundWheel(role, 3)), arrivalTime: 61.3f, leaveNow: true);
        ai.Move(61.6f, () => ByRole(ThirdRoundBaits), arrivalTime: 63.3f, leaveNow: true);
        ai.Move(63.7f, () => ByRole(role => role == PartyRole.MainTank ? TankSpot() : At(NorthWest, WestIdleRadius)), arrivalTime: 65.5f, leaveNow: true);

        ai.Move(68.2f, () => ByRole(role => role == PartyRole.MainTank ? null : AcrossFromMainTank()));
        ai.Move(70.2f, () => ByRole(role => role == PartyRole.MainTank ? null : BehindNidhogg()), sprint: true);
        ai.Move(70.3f, () => ByRole(role => role == PartyRole.MainTank ? BehindNidhogg() : null), sprint: true);
        ai.Move(73.8f, () => ByRole(HomeCounterTowerSpot), arrivalTime: 76.5f, leaveNow: true);
        ai.Move(75.6f, () => ByRole(CounterTowerSoak), arrivalTime: 79.8f, leaveNow: true);
        ai.Move(80.2f, () => ByRole(TakeTetherOrBaitOrWait), arrivalTime: 82.5f, leaveNow: true);
        ai.Move(81.4f, () => ByRole(TanksTakeTethers));
        ai.Move(82.6f, () => ByRole(TanksTakeTethers));
        ai.Move(83.8f, () => ByRole(TanksTakeTethers));
        ai.Move(85.0f, () => ByRole(TanksTakeTethers));
        ai.Move(87.4f, () => ByRole(Idle));

        ai.Move(106.8f, () => ByRole(role => role == PartyRole.MainTank ? null : At(North, CleaveDodgeRadius)));
        ai.Move(108.2f, () => ByRole(role => role == PartyRole.MainTank ? null : BehindNidhogg()), sprint: true);
        ai.Move(108.3f, () => ByRole(role => role == PartyRole.MainTank ? BehindNidhogg() : null), sprint: true);
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

    private Vector3 AcrossFromMainTank()
    {
        var boss = Nidhogg()?.Position ?? Vector3.Zero;
        if (world.Party.Get(PartyRole.MainTank) is not { } tank || !tank.IsAlive()) return BehindNidhogg();
        var toTank = tank.Position - boss;
        toTank.Y = 0f;
        return toTank.LengthSquared() < 0.01f ? BehindNidhogg() : boss - Vector3.Normalize(toTank) * CleaveDodgeRadius;
    }

    private static Vector3 Stack() => At(North, StackRadius);

    private bool OutAt(int wheel) => (wheel % 2 == 0) == (seenOutFirst[wheel / 2] ?? true);

    private SimEnemy? Nidhogg() => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == DsrConstants.BNpcBaseId.NidhoggP3 && !e.Dead);

    private IEnumerable<SimEnemy> Clones() => world.Children.OfType<SimEnemy>().Where(e => e.BNpcBaseId == DsrConstants.BNpcBaseId.NidhoggClone && !e.Dead);

    private void ReadWheelOrder(int cast)
    {
        if (Nidhogg() is { } boss && boss.CastActionId is DsrConstants.ActionId.GnashAndLash or DsrConstants.ActionId.LashAndGnash)
            seenOutFirst[cast] = boss.CastActionId == DsrConstants.ActionId.GnashAndLash;
    }

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

    private static Vector3 SoakForWheel(Vector3 tower, bool outside)
    {
        var towerRadius = new Vector2(tower.X, tower.Z).Length();
        var radius = Math.Clamp(outside ? OutRadius : InRadius, towerRadius - TowerReach, towerRadius + TowerReach);
        var safe = outside ? radius > WheelEdge + WheelMargin : radius < WheelEdge - WheelMargin;
        return safe ? At(Compass.BearingOf(tower), radius) : tower;
    }

    private static Vector3 Beyond(Vector3 tower, float distance)
        => At(Compass.BearingOf(tower), new Vector2(tower.X, tower.Z).Length() + distance);

    private Vector3? FirstRoundWheel(PartyRole role, int wheel)
        => Order(role) == 3 && FirstRoundTower(role) is { } tower ? SoakForWheel(tower, OutAt(wheel)) : StackForWheel(wheel);

    private Vector3? FirstRoundBaitsAndSecondJumpers(PartyRole role) => Order(role) switch
    {
        3 => FirstRoundTower(role) is { } tower ? Beyond(tower, DiveBaitBeyondTower) : Stack(),
        2 => JumpSpot(role),
        _ => Stack(),
    };

    private Vector3? SecondRoundSoaks(PartyRole role)
    {
        if (IsSideFirst(role)) return SecondRoundTower(role) is { } tower ? Beyond(tower, SecondTowerSoakBeyond) : Stack();
        return Order(role) == 2 ? Stack() : null;
    }

    private Vector3? ThirdRoundWheel(PartyRole role, int wheel)
        => ThirdRoundTower(role) is { } tower ? SoakForWheel(tower, OutAt(wheel)) : StackForWheel(wheel);

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

    private int[] SoakersShownOnCounterTowers()
    {
        var soakers = new[] { 2, 2, 2, 2 };
        foreach (var clone in Clones())
        {
            var shown = Array.IndexOf(DsrConstants.ActionId.DarkdragonDiveForSoakers, clone.CastActionId);
            if (shown >= 0) soakers[NearestCounterTower(clone.Position)] = shown + 1;
        }
        return soakers;
    }

    private int? TetheredCounterTower()
    {
        var boss = Nidhogg();
        return state.SoulTethers.Where(t => !ReferenceEquals(t.Source, boss)).Select(t => (int?)NearestCounterTower(t.Source.Position)).FirstOrDefault();
    }

    private void AssignCounterTowers()
    {
        counterTower = new Dictionary<PartyRole, int>(HomeCounterTower);
        var soakers = SoakersShownOnCounterTowers();
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

    private Vector3? TakeTetherOrBaitOrWait(PartyRole role)
    {
        if (IsTank(role)) return TakeTether(role);
        if (counterBait.Count == 0) AssignCounterBaits();
        return counterBait.TryGetValue(role, out var bait) ? bait : BehindNidhogg();
    }

    private void AssignCounterBaits()
    {
        if (counterTower.Count == 0) AssignCounterTowers();
        var tethered = TetheredCounterTower();
        var spots = Enumerable.Range(0, 4).Where(t => t != tethered)
            .Select(t => At(Compass.BearingOf(DsrP3NidhoggState.CounterTower(t)), CounterBaitRadius)).ToList();
        var candidates = Enum.GetValues<PartyRole>().Where(r => !IsTank(r) && world.Party.Get(r) is { } m && m.IsAlive()).ToList();
        var pairs = spots.SelectMany(spot => candidates.Select(role => (spot, role, distance: Vector3.Distance(world.Party.Get(role)!.Position, spot))))
            .OrderBy(p => p.distance).ThenBy(p => p.role);
        foreach (var (spot, role, _) in pairs)
            if (!counterBait.ContainsKey(role) && !counterBait.ContainsValue(spot))
                counterBait[role] = spot;
    }

    private Vector3? TanksTakeTethers(PartyRole role) => IsTank(role) ? TakeTether(role) : null;

    private Vector3? TakeTether(PartyRole role)
    {
        var index = role == PartyRole.MainTank ? 0 : 1;
        if (state.SoulTethers.Count <= index) return role == PartyRole.MainTank ? TankSpot() : null;
        var (source, tether) = state.SoulTethers[index];
        var me = world.Party.Get(role);
        if (tether.B is { } holder && ReferenceEquals(holder, me))
            return index == 0 ? TankSpot() : At(Compass.BearingOf(source.Position), CounterBaitRadius);
        var toward = tether.B is { } current ? current.Position - source.Position : Vector3.Zero;
        toward.Y = 0f;
        return toward.LengthSquared() < 0.01f ? source.Position : source.Position + Vector3.Normalize(toward) * InterceptDistance;
    }

    private void DodgeGeirskogulLines()
    {
        var now = world.Events.Elapsed;
        foreach (var clone in Clones())
            if (clone.CastActionId == DsrConstants.ActionId.Geirskogul && !geirskogulLines.ContainsKey(clone))
                geirskogulLines[clone] = (now, clone.Position, new Vector3(MathF.Sin(clone.Rotation), 0f, MathF.Cos(clone.Rotation)));
        var live = geirskogulLines.Values
            .Where(l => now >= l.Start + BaiterLeavesWithin && now < l.Start + GeirskogulDangerSeconds)
            .Select(l => (l.Origin, l.Direction)).ToList();

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
        if (heldOutOfLines.Remove(role)) bot.MoveTo(destination, AiManager.SprintSpeed);
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
