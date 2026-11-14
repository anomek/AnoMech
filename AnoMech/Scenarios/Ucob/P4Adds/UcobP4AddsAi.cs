using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Ucob.P4Adds.UcobP4AddsConstants;

namespace AnoMech.Scenarios.Ucob.P4Adds;

public sealed class UcobP4AddsAi : IScenarioAi<UcobP4AddsState>
{
    public string Name => "NAUR";

    private const int Slots = 8;
    private const float TwisterSidestep = 2.6f;
    private const float DynamoHuddleRadius = 3.8f;
    private const float DynamoHuddleArc = 2.44f;
    private const float DiveSpreadRadius = 6.5f;

    private const float TankHitboxRadius = 0.5f;
    private static readonly Vector2 AddsStackSpot = new(13.2f, -7.1f);
    private static readonly Vector2 AlongWallFromAdds = Vector2.Normalize(new Vector2(-AddsStackSpot.Y, AddsStackSpot.X));
    private static readonly Vector2 TwintaniaTankSpot =
        AddsStackSpot + AlongWallFromAdds * (UcobConstants.Geometry.TwintaniaHitboxRadius + TankHitboxRadius);
    private static readonly Vector2 NaelTankPlummetSpot =
        AddsStackSpot - AlongWallFromAdds * (UcobConstants.Geometry.NaelHitboxRadius + TankHitboxRadius);
    private const float TankPairGap = 1f;
    private static readonly Vector2 NaelTankSpot =
        TwintaniaTankSpot - Vector2.Normalize(AddsStackSpot) * TankPairGap;
    private static readonly Vector2 PartyStack = Flat(UcobConstants.AetherWaymarks.First(w => w.Slot == WaymarkSlot.Four).Offset);
    private static readonly Vector2 DiveSpreadCenter = new(3f, -2f);
    private static readonly Vector2 NorthHealerSpot = new(2f, -17f);
    private static readonly Vector2 EastHealerSpot = new(15f, 9f);
    private static readonly Vector2 UntargetedDpsHatchSpot = new(16.5f, 6f);
    private static readonly Vector2 UntargetedRangedHatchSpot = new(0f, 11f);
    private const float NeurolinkWaitOutside = 3.2f;
    private const float LiquidHellKiteRadius = 18f;
    private const float LiquidHellKiteStartBearing = 157f;
    private const float LiquidHellKiteStepDegrees = 22f;
    private const float ChariotClearance = 2.5f;
    private const float SafeArenaReach = 18.5f;
    private const float LooseStackRadius = 3f;
    private const float TightDiveSpreadRadius = 5f;
    private const float QuoteJitter = 0.1f;
    private const float TwisterClearance = Geometry.TwisterTriggerRadius + 1.3f;
    private const float NeurolinkExitDistance = 4.8f;

    private static readonly Vector2[] UptimeSpots =
    [
        Vector2.Zero,
        Vector2.Zero,
        NorthHealerSpot,
        EastHealerSpot,
        new(5.8f, -4.6f),
        new(7f, -2.2f),
        new(-2.5f, 3f),
        new(-4.5f, 0.5f),
    ];


    private static readonly (float From, float Until)[] NaelTankDodgesPlummetWindows = [(0f, 21.14f), (69.7f, 77.84f)];

    private UcobP4AddsState state = null!;
    private SimWorld world = null!;

    public void Run(UcobP4AddsState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);
        state.TankAssignmentsChanged += () => ai.Move(0f, TanksFollowTheSwap, jitter: 0f, sprint: true);

        ai.Move(0.5f, TanksMeetAddsAtSpawn);
        ai.Move(13.2f, UptimeWithNaelTankOutOfPlummet);

        ai.Move(21.2f, () => RangedBaitsLiquidHell(0));
        ai.Move(21.4f, TanksTakeTheirAdds);
        ai.Move(23.99f, () => RangedBaitsLiquidHell(1), jitter: 0f);
        ai.Move(25.14f, () => RangedBaitsLiquidHell(2), jitter: 0f);
        ai.Move(26.30f, () => RangedBaitsLiquidHell(3), jitter: 0f);
        ai.Move(27.50f, () => RangedBaitsLiquidHell(4), jitter: 0f);
        ai.Move(28.68f, () => RangedBaitsLiquidHell(5), jitter: 0f);
        ai.Move(30.3f, () => HatchPositions(0), jitter: 0.2f);
        ai.Move(36.76f, () => SwapNeurolinksAroundTwisters(0, 0), jitter: 0f);
        ai.Move(37.3f, () => SwapNeurolinksAroundTwisters(0, 0), jitter: 0f);

        ai.Move(41.45f, () => AnswerQuote(0, 0));
        ai.Move(43.6f, () => AnswerFirstMoveAgainOnceTwistersExpire(0), sprint: true);
        ai.Move(45.45f, () => AnswerQuote(0, 1), jitter: QuoteJitter, sprint: true);
        ai.Move(48.55f, () => AnswerQuote(0, 2), jitter: QuoteJitter, sprint: true);
        ai.Move(51.9f, Uptime);

        ai.Move(53.91f, () => SidestepTwisters(1), jitter: 0f);
        ai.Move(58.7f, Uptime);
        ai.Move(60.8f, UptimeOnceTwistersExpire);
        ai.Move(78.1f, TanksTakeTheirAdds);

        ai.Move(83.9f, () => RangedBaitsLiquidHell(0));
        ai.Move(86.63f, () => RangedBaitsLiquidHell(1), jitter: 0f);
        ai.Move(87.79f, () => RangedBaitsLiquidHell(2), jitter: 0f);
        ai.Move(88.95f, () => RangedBaitsLiquidHell(3), jitter: 0f);
        ai.Move(90.10f, () => RangedBaitsLiquidHell(4), jitter: 0f);
        ai.Move(91.26f, () => RangedBaitsLiquidHell(5), jitter: 0f);
        ai.Move(92.9f, () => HatchPositions(1), jitter: 0.2f);
        ai.Move(99.39f, () => SwapNeurolinksAroundTwisters(1, 2), jitter: 0f);
        ai.Move(99.95f, () => SwapNeurolinksAroundTwisters(1, 2), jitter: 0f);

        ai.Move(104.05f, () => AnswerQuote(1, 0));
        ai.Move(106.2f, () => AnswerFirstMoveAgainOnceTwistersExpire(1), sprint: true);
        ai.Move(108.03f, () => AnswerQuote(1, 1), jitter: QuoteJitter, sprint: true);
        ai.Move(111.13f, () => AnswerQuote(1, 2), jitter: QuoteJitter, sprint: true);
        ai.Move(114.5f, Uptime);

        ai.Move(116.53f, () => SidestepTwisters(3), jitter: 0f);
        ai.Move(121.3f, Uptime);
    }

    private IAiMove TanksMeetAddsAtSpawn()
    {
        var spots = UptimeSpots.Select(s => (Vector2?)s).ToArray();
        spots[(int)PartyRole.MainTank] = new(-4f, -7f);
        spots[(int)PartyRole.OffTank] = new(4f, -7f);
        return Go(spots);
    }

    private IAiMove Uptime()
    {
        var spots = UptimeSpots.Select(s => (Vector2?)s).ToArray();
        spots[(int)PartyRole.MainTank] = TankSpot(PartyRole.MainTank);
        spots[(int)PartyRole.OffTank] = TankSpot(PartyRole.OffTank);
        return Go(spots);
    }

    private IAiMove UptimeOnceTwistersExpire() => Uptime();

    private IAiMove UptimeWithNaelTankOutOfPlummet()
    {
        var spots = UptimeSpots.Select(s => (Vector2?)s).ToArray();
        spots[(int)PartyRole.MainTank] = TankSpotOutOfPlummet(PartyRole.MainTank);
        spots[(int)PartyRole.OffTank] = TankSpotOutOfPlummet(PartyRole.OffTank);
        return Go(spots);
    }

    private IAiMove TanksFollowTheSwap() =>
        NaelTankDodgesPlummet() ? TanksTakeTheirAddsWithNaelTankOutOfPlummet() : TanksTakeTheirAdds();

    private bool NaelTankDodgesPlummet()
    {
        var now = world.Events.Elapsed;
        return NaelTankDodgesPlummetWindows.Any(w => now >= w.From && now < w.Until);
    }

    private IAiMove TanksTakeTheirAddsWithNaelTankOutOfPlummet()
    {
        var spots = new Vector2?[Slots];
        spots[(int)PartyRole.MainTank] = TankSpotOutOfPlummet(PartyRole.MainTank);
        spots[(int)PartyRole.OffTank] = TankSpotOutOfPlummet(PartyRole.OffTank);
        return Go(spots);
    }

    private IAiMove TanksTakeTheirAdds()
    {
        var spots = new Vector2?[Slots];
        spots[(int)PartyRole.MainTank] = TankSpot(PartyRole.MainTank);
        spots[(int)PartyRole.OffTank] = TankSpot(PartyRole.OffTank);
        return Go(spots);
    }

    private Vector2 TankSpot(PartyRole tank) =>
        state.NaelTank == tank ? NaelTankSpot : TwintaniaTankSpot;

    private Vector2 TankSpotOutOfPlummet(PartyRole tank) =>
        state.NaelTank == tank ? NaelTankPlummetSpot : TwintaniaTankSpot;

    private IAiMove RangedBaitsLiquidHell(int drop)
    {
        var spots = new Vector2?[Slots];
        spots[(int)PartyRole.PhysRangedDps] = ClockwiseFromB(drop);
        return Go(spots);
    }

    private static Vector2 ClockwiseFromB(int drop)
    {
        var bearing = (LiquidHellKiteStartBearing + LiquidHellKiteStepDegrees * drop) * MathF.PI / 180f;
        return new Vector2(MathF.Sin(bearing), -MathF.Cos(bearing)) * LiquidHellKiteRadius;
    }

    private static Vector2 HatchSpot(HatchSet hatch, PartyRole dps) => hatch.NeurolinkFor(dps) switch
    {
        null when dps == PartyRole.PhysRangedDps => UntargetedRangedHatchSpot,
        null => UntargetedDpsHatchSpot,
        1 => WaitBesideNeurolinkTwo(),
        { } link => NeurolinkCenter(link),
    };

    private IAiMove HatchPositions(int set)
    {
        var hatch = state.Hatches[set];
        var spots = new Vector2?[Slots];
        spots[(int)PartyRole.MainTank] = TankSpot(PartyRole.MainTank);
        spots[(int)PartyRole.OffTank] = TankSpot(PartyRole.OffTank);
        spots[(int)PartyRole.RegenHealer] = NorthHealerSpot;
        spots[(int)PartyRole.ShieldHealer] = EastHealerSpot;
        spots[(int)hatch.Untargeted] = UntargetedDpsHatchSpot;
        foreach (var role in hatch.Targets)
            spots[(int)role] = HatchSpot(hatch, role);
        return Go(spots);
    }

    private IAiMove SwapNeurolinksAroundTwisters(int hatchSet, int twisterSet)
    {
        var hatch = state.Hatches[hatchSet];
        var spots = SidestepSpots(twisterSet);
        var waitingForHatch = new HashSet<int>();
        foreach (var role in hatch.Targets)
        {
            if (hatch.NeurolinkFor(role) is not { } link) continue;
            if (link == 1)
                spots[(int)role] = NeurolinkCenter(link);
            else if (AlreadyHatched(role))
                spots[(int)role] = ExitNeurolink(link);
            else
            {
                spots[(int)role] = NeurolinkCenter(link);
                waitingForHatch.Add((int)role);
            }
        }
        return Go(spots, waitingForHatch);
    }

    private bool AlreadyHatched(PartyRole role) =>
        world.Party.Get(role) is { } member && member.HasStatus(UcobConstants.StatusId.ManaHypersensitivity);

    private static Vector2 NeurolinkCenter(int link) => Flat(Geometry.Neurolinks[link]);

    private static Vector2 WaitBesideNeurolinkTwo()
    {
        var link = NeurolinkCenter(1);
        var outward = AwayFromArenaCenter(link);
        var northSide = new Vector2(outward.Y, -outward.X);
        if (northSide.Y > 0) northSide = -northSide;
        return link + northSide * NeurolinkWaitOutside;
    }

    private static Vector2 ExitNeurolink(int link) =>
        NeurolinkCenter(link) + AwayFromArenaCenter(NeurolinkCenter(link)) * NeurolinkExitDistance;

    private static Vector2 AwayFromArenaCenter(Vector2 here) => -TowardArenaCenter(here);

    private IAiMove SidestepTwisters(int set) => Go(SidestepSpots(set));

    private Vector2?[] SidestepSpots(int set)
    {
        var spots = new Vector2?[Slots];
        var snapshot = state.TwisterSnapshots[set];
        var twisterSpots = snapshot.Select(s => Flat(s.Position)).ToList();
        foreach (var (role, position) in snapshot)
        {
            if (!world.Party.Get(role).IsAlive()) continue;
            var twister = Flat(position);
            spots[(int)role] = twister + SidestepDirection(twister, twisterSpots) * TwisterSidestep;
        }
        return spots;
    }

    private static Vector2 SidestepDirection(Vector2 here, List<Vector2> twisterSpots)
    {
        if (StandingInNeurolink(here)) return TowardArenaCenter(here);

        var nearestOther = twisterSpots
            .Where(t => Vector2.Distance(t, here) > 0.01f)
            .OrderBy(t => Vector2.Distance(t, here))
            .Cast<Vector2?>()
            .FirstOrDefault();
        if (nearestOther is { } other && Vector2.Distance(other, here) < 6f)
            return Vector2.Normalize(here - other);
        return TowardArenaCenter(here);
    }

    private static bool StandingInNeurolink(Vector2 here) =>
        Geometry.Neurolinks.Any(link => Vector2.Distance(Flat(link), here) <= Geometry.NeurolinkRadius);

    private static Vector2 TowardArenaCenter(Vector2 here) =>
        here.LengthSquared() < 1f ? new Vector2(1f, 0f) : Vector2.Normalize(-here);

    private IAiMove AnswerFirstMoveAgainOnceTwistersExpire(int quote) => AnswerQuote(quote, 0);

    private IAiMove AnswerQuote(int quote, int step)
    {
        var spots = state.Quotes[quote].Moves[step] switch
        {
            NaelMove.Dynamo => HuddleUnderNael(),
            NaelMove.Dive => SpreadForRavenDive(NextMoveIsStack(quote, step)),
            NaelMove.Chariot => StepOutOfChariot(NextMoveIsStack(quote, step)),
            _ => StackAwayFromNael(),
        };
        return Go(spots);
    }

    private Vector2?[] HuddleUnderNael()
    {
        var nael = Flat(state.Nael?.Position ?? Geometry.NaelSpawn);
        var towardCenter = MathF.Atan2(-nael.X, -nael.Y);
        var spots = new Vector2?[Slots];
        for (var slot = 0; slot < Slots; slot++)
        {
            var angle = towardCenter + DynamoHuddleArc * (slot / (Slots - 1f) - 0.5f);
            spots[slot] = nael + new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * DynamoHuddleRadius;
        }
        return spots;
    }

    private static Vector2?[] SpreadForRavenDive(bool stackIsNext)
    {
        var center = stackIsNext ? PartyStack : DiveSpreadCenter;
        var radius = stackIsNext ? TightDiveSpreadRadius : DiveSpreadRadius;
        var spots = new Vector2?[Slots];
        for (var slot = 0; slot < Slots; slot++)
        {
            var angle = slot * MathF.PI / 4f;
            spots[slot] = center + new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * radius;
        }
        return spots;
    }

    private Vector2?[] StepOutOfChariot(bool stackIsNext)
    {
        var nael = Flat(state.Nael?.Position ?? Geometry.NaelSpawn);
        var safeDistance = Geometry.ChariotRadius + ChariotClearance;
        var spots = new Vector2?[Slots];
        for (var slot = 0; slot < Slots; slot++)
        {
            if (world.Party.Get(slot) is not { } member || !member.IsAlive()) continue;
            var here = Flat(member.Position);
            if (stackIsNext)
            {
                spots[slot] = DriftTowardStack(here);
                continue;
            }
            var fromNael = here - nael;
            if (fromNael.Length() >= safeDistance) continue;
            var away = fromNael.LengthSquared() < 0.01f ? TowardArenaCenter(nael) : Vector2.Normalize(fromNael);
            spots[slot] = NearestInsideArena(nael, away, safeDistance);
        }
        return spots;
    }

    private bool NextMoveIsStack(int quote, int step) =>
        step + 1 < state.Quotes[quote].Moves.Count && state.Quotes[quote].Moves[step + 1] == NaelMove.Beam;

    private static Vector2 DriftTowardStack(Vector2 here)
    {
        var offset = here - PartyStack;
        return offset.Length() <= LooseStackRadius ? here : PartyStack + Vector2.Normalize(offset) * LooseStackRadius;
    }

    private IAiMove Go(Vector2?[] spots, IReadOnlySet<int>? stayingOnTheirTwister = null)
    {
        for (var slot = 0; slot < spots.Length; slot++)
        {
            if (stayingOnTheirTwister?.Contains(slot) == true) continue;
            if (spots[slot] is { } spot)
                spots[slot] = ClearOfLiveTwisters(spot);
            else if (StandingByLiveTwister(slot) is { } here)
                spots[slot] = ClearOfLiveTwisters(here);
        }
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2? StandingByLiveTwister(int slot)
    {
        if (world.Party.Get(slot) is not { } member || !member.IsAlive()) return null;
        var here = Flat(member.Position);
        return state.LiveTwisters.Any(t => Vector2.Distance(Flat(t), here) < TwisterClearance) ? here : null;
    }

    private Vector2 ClearOfLiveTwisters(Vector2 spot)
    {
        for (var pass = 0; pass < 3; pass++)
            foreach (var twister in state.LiveTwisters)
            {
                var center = Flat(twister);
                var offset = spot - center;
                if (offset.Length() >= TwisterClearance) continue;
                var away = offset.LengthSquared() < 0.01f ? TowardArenaCenter(center) : Vector2.Normalize(offset);
                spot = center + away * TwisterClearance;
            }
        return spot;
    }

    private static Vector2 NearestInsideArena(Vector2 origin, Vector2 direction, float distance)
    {
        for (var step = 0; step <= 12; step++)
            foreach (var sign in new[] { 1f, -1f })
            {
                var angle = sign * step * MathF.PI / 12f;
                var turned = new Vector2(
                    direction.X * MathF.Cos(angle) - direction.Y * MathF.Sin(angle),
                    direction.X * MathF.Sin(angle) + direction.Y * MathF.Cos(angle));
                var spot = origin + turned * distance;
                if (spot.Length() <= SafeArenaReach) return spot;
            }
        return origin + TowardArenaCenter(origin) * distance;
    }

    private static Vector2?[] StackAwayFromNael() =>
        Enumerable.Repeat((Vector2?)PartyStack, Slots).ToArray();

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
