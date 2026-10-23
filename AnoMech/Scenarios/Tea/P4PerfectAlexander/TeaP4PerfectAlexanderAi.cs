using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P4PerfectAlexander;

public sealed class TeaP4PerfectAlexanderAi : IScenarioAi<TeaP4PerfectAlexanderState>
{
    public string Name => "NAUR";

    private enum Kind { Tank, Healer, Melee, Ranged }

    private const float Jitter = 0.25f;
    private const float ShuffleSpeed = 2f;
    private const float ShuffleLength = 0.6f;
    private const float StillnessWatched = 2.4f;
    private const float ArenaEdge = 19.2f;
    private const float SprintSeconds = 10f;
    private const float LookEvery = 0.2f;
    private const float MiddleClear = 3.3f;
    private const float ArcStep = MathF.PI / 6f;
    private const float TrailClear = 5f;
    private const float PauseClear = 9f;
    private const float MeleeHold = 10.5f;
    private const float ArrivedWithin = 0.25f;

    private const float AlphaProjectionsSetOff = 69.8f;
    private const float AlphaFatesShown = 85f;
    private const float AlphaArriveStill = 94.9f;
    private const float AlphaArriveMoving = 96.5f;
    private const float AlphaUntargetable = 93.6f;
    private const float AlphaFirstCheck = 97.97f;
    private const float AlphaSecondCheck = 102f;
    private const float AlphaRegroup = 103.95f;

    private const float BetaProjectionsSetOff = 144.75f;
    private const float BetaColours = 153.6f;
    private const float BetaLightBeaconLatest = 180.4f;
    private const float BetaAfterTheMarch = 189.25f;
    private const float BetaSpreadFrom = 192.2f;
    private const float BetaSettled = 194.3f;
    private const float BetaSpreadDecided = 197.45f;
    private const float BetaReprobation = 197.9f;
    private const float BetaDonut = 202.82f;
    private const float BetaRegroup = 203.8f;
    private const float SpreadClear = 7f;
    private const float SpreadRoom = 7.6f;
    private const float SpreadEdge = 18.5f;

    private static readonly Vector2 HolderSpot = new(0.4f, -8.4f);

    private static readonly Vector2 LightBeaconSpot = new(0.1f, -14.4f);
    private static readonly Vector2 DarkBeaconSpot = new(0f, 14f);
    private static readonly Vector2[] LightFollowerSpots = [new(-0.5f, 10.8f), new(0.5f, 10.8f), new(0f, 10.2f)];
    private static readonly Vector2[] DarkFollowerSpots = [new(-0.3f, 12f), new(0.3f, 12f), new(0f, 12.6f)];

    private static readonly Vector2 WestStack = new(-7.5f, -1.5f);
    private static readonly Vector2 EastStack = new(7.5f, -1.5f);

    private static readonly Vector2 DefamationSpot = new(-6.7f, -16.3f);
    private static readonly Vector2 SharedSpot = new(4.3f, 17.1f);
    private static readonly Vector2[] NothingSpots = [new(3.6f, 16.3f), new(5.1f, 16.5f), new(4.1f, 17.9f)];
    private static readonly Vector2[] AggravatedSpots = [new(9.2f, 15.9f), new(8.6f, 15.1f), new(9.8f, 15.2f)];
    private static readonly Vector2 ThirdSafeDefamationSpot = new(7.0f, -16.1f);
    private static readonly Vector2 ThirdSafeSharedSpot = new(-9.2f, 15.4f);
    private static readonly Vector2[] ThirdSafeNothingSpots = [new(-8.5f, 14.6f), new(-10.0f, 14.8f), new(-9.4f, 16.2f)];
    private static readonly Vector2[] ThirdSafeAggravatedSpots = [new(-4.3f, 17.9f), new(-3.7f, 17.0f), new(-4.9f, 17.1f)];

    private static readonly Vector2 LightBetaBeaconSpot = new(-7f, -16.4f);
    private static readonly Vector2 LightBetaBeaconWait = new(-6.3f, -9.6f);
    private static readonly Vector2 DarkBetaBeaconSpot = new(13.8f, -0.1f);
    private static readonly Vector2 NorthStack = new(-3.4f, -14.9f);
    private static readonly Vector2 LightStack = new(-1.7f, -12.7f);
    private static readonly Vector2 DarkStack = new(0f, 0.8f);

    private static readonly Vector2 EastBait = new(18.4f, 0f);
    private static readonly Vector2 SouthBait = new(-0.2f, 18.2f);
    private static readonly Vector2 WestBait = new(-18.3f, 0.2f);

    private static readonly Vector2 SpreadNorthWest = new(-11.8f, -9.9f);
    private static readonly Vector2 SpreadNorth = new(0f, -15.5f);
    private static readonly Vector2 SpreadNorthEast = new(10.5f, -10.3f);
    private static readonly Vector2 SpreadEast = new(13.9f, 5.4f);
    private static readonly Vector2 SpreadSouth = new(-0.1f, 14.4f);
    private static readonly Vector2 SpreadWest = new(-13.1f, 6f);
    private static readonly Vector2 SpreadMiddle = new(3.2f, -1f);
    private static readonly Vector2 SpreadInnerNorth = new(-3.4f, -7.3f);
    private static readonly Vector2[] SpreadSpots =
    [
        SpreadNorthWest, SpreadNorth, SpreadNorthEast, SpreadEast, SpreadSouth, SpreadWest, SpreadMiddle, SpreadInnerNorth,
        new(15.6f, -3.3f), new(9.2f, 13.1f), new(-8.5f, 13.6f), new(-15.8f, -2.2f),
    ];
    private static readonly Vector2[] SpreadGrid =
    [
        .. from radius in new[] { 4.5f, 6f, 9f, 12f, 15f, 17.5f }
        from step in Enumerable.Range(0, 18)
        select Flat(Geometry.OnCompass(20f * step + radius, radius)),
    ];
    private static readonly float[] AroundSteps = [2f, 3.5f, 5f, 6.5f, 8f, 9.5f, 11f];
    private static readonly Vector2[] AroundDirections =
        [.. Enumerable.Range(0, 16).Select(k => new Vector2(MathF.Cos(MathF.Tau * k / 16f), MathF.Sin(MathF.Tau * k / 16f)))];

    private static readonly Vector2 Centre = new(0f, 0f);
    private static readonly Vector2 WaymarkOne = new(8f, 0f);
    private static readonly Vector2 WaymarkThree = new(0f, 8f);
    private static readonly Vector2 WaymarkFour = new(8f, 8f);

    private static readonly Vector2[] JailSpots =
        Enumerable.Range(0, 7).Select(k => Flat(Geometry.OnCompass(45f + 45f * k, 12f))).ToArray();
    private static readonly Vector2 PrisonWait = new(0f, 2.5f);
    internal static readonly PartyRole[] Prisoners = [RegenHealer, ShieldHealer, MainTank, OffTank, PhysRangedDps, CasterDps, MeleeDpsA];

    private TeaP4PerfectAlexanderState state = null!;
    private SimWorld world = null!;
    private float practiceStart;
    private readonly Dictionary<PartyRole, (float Time, Vector2 Spot)> startSpots = new();
    private readonly Dictionary<PartyRole, List<(float At, Vector2 Spot)>> sightings = new();
    private readonly Dictionary<PartyRole, List<(float At, Vector2 Spot)>> tracks = new();
    private readonly Dictionary<PartyRole, Vector2> alphaHomes = new();
    private readonly Dictionary<PartyRole, Vector2> spreadLayout = new();
    private readonly Dictionary<PartyRole, Vector2> spreadTargets = new();
    private readonly Dictionary<PartyRole, float> sprintUntil = new();
    private readonly Dictionary<PartyRole, int> walkOrders = new();
    private readonly Dictionary<PartyRole, (Vector2 Spot, bool Around)> walkTargets = new();
    private readonly Dictionary<PartyRole, Rng> noises = new();

    public void Run(TeaP4PerfectAlexanderState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        practiceStart = TeaP4PerfectAlexanderPractice.Of(state.Practice).Start;
        startSpots.Clear();
        sightings.Clear();
        tracks.Clear();
        alphaHomes.Clear();
        spreadLayout.Clear();
        spreadTargets.Clear();
        sprintUntil.Clear();
        walkOrders.Clear();
        walkTargets.Clear();
        noises.Clear();
        if (practiceStart > 0f) world.Events.Add(practiceStart, PlaceBotsForPractice);

        RegroupUnder(0.5f, MainTank);
        TakeTheFinalWord();
        TakeOpticalSights();
        TakeFateCalibrationAlpha();
        TakeCapitalPunishment(113.21f, MainTank, shared: true);
        TakeFateCalibrationBeta();
        TakeCapitalPunishment(213.15f, OffTank, shared: false);
        TakeAlmightyJudgment(232.49f, 0, MainTank);
        TakeCapitalPunishment(258.82f, MainTank, shared: false);
        TakeAlmightyJudgment(277.94f, 1, OffTank);
        TakeTemporalPrison(TeaP4PerfectAlexanderState.TemporalPrisonCast);
    }

    private Vector2 UptimeSpot(PartyRole role, PartyRole holder)
        => role == holder ? HolderSpot : KindOf(role) == Kind.Tank ? SpareTankSpot(role) : PartySpot(role);

    private static Vector2 PartySpot(PartyRole role) => role switch
    {
        RegenHealer => new(2.2f, 5.1f),
        ShieldHealer => new(0.6f, 7.3f),
        MeleeDpsA => new(-2f, 7.3f),
        MeleeDpsB => new(-6.6f, 4.9f),
        PhysRangedDps => new(-4.1f, 3.2f),
        _ => new(-0.9f, 8.7f),
    };

    private Vector2 SpareTankSpot(PartyRole tank)
    {
        var dice = Dice(tank, "uptime");
        var spot = HolderSpot;
        for (var tries = 0; tries < 20; tries++)
        {
            spot = Flat(Geometry.OnCompass(360f * dice.NextSingle(), Between(dice, 5f, 10.8f)));
            if (Vector2.Distance(spot, HolderSpot) >= 2f && PerRole.All.Where(r => KindOf(r) != Kind.Tank).All(r => Vector2.Distance(spot, PartySpot(r)) >= 2f))
                break;
        }
        return spot;
    }

    private static bool SpareTank(PartyRole role, PartyRole holder) => KindOf(role) == Kind.Tank && role != holder;

    private void RegroupUnder(float time, PartyRole holder)
    {
        foreach (var role in PerRole.All) Go(time, role, UptimeSpot(role, holder), around: KindOf(role) == Kind.Tank);
    }

    private void TakeTheFinalWord()
    {
        At(16.95f, () => WalkToFinalWordSpots(firstStillness: !state.FinalWordMotionFirst));
        if (state.FinalWordMotionFirst) ShuffleEveryoneThrough(23.21f, FinalWordSpot);
        else At(23.4f, () => { foreach (var role in PerRole.All) Walk(role, FinalWordSpot(role), AiManager.RunSpeed, jitter: false, SpareTank(role, MainTank)); });
        if (!state.FinalWordMotionFirst) ShuffleEveryoneThrough(34.32f, null);
        RegroupUnder(34.6f, MainTank);
    }

    private FinalWordDebuff? ShownFinalWord(PartyRole role)
    {
        if (Member(role) is not { } member) return null;
        if (member.ShowsStatus(StatusId.FinalWordContactRegulation)) return FinalWordDebuff.LightBeacon;
        if (member.ShowsStatus(StatusId.FinalWordContactProhibition)) return FinalWordDebuff.LightFollower;
        if (member.ShowsStatus(StatusId.FinalWordEscapeDetection)) return FinalWordDebuff.DarkBeacon;
        if (member.ShowsStatus(StatusId.FinalWordEscapeProhibition)) return FinalWordDebuff.DarkFollower;
        return null;
    }

    private Vector2 FinalWordSpot(PartyRole role)
    {
        var debuff = ShownFinalWord(role) ?? state.FinalWordOf(role);
        var same = PerRole.All.Where(r => (ShownFinalWord(r) ?? state.FinalWordOf(r)) == debuff).ToList();
        var index = Math.Max(0, same.IndexOf(role));
        return debuff switch
        {
            FinalWordDebuff.LightBeacon => LightBeaconSpot,
            FinalWordDebuff.DarkBeacon => DarkBeaconSpot,
            FinalWordDebuff.LightFollower => LightFollowerSpots[index % LightFollowerSpots.Length],
            _ => DarkFollowerSpots[index % DarkFollowerSpots.Length],
        };
    }

    private void WalkToFinalWordSpots(bool firstStillness)
    {
        var stillFrom = 23.21f - StillnessWatched - 0.2f;
        foreach (var role in PerRole.All)
        {
            if (Member(role) is not { } member) continue;
            var spot = FinalWordSpot(role);
            var leave = Reaction(role, 0.25f, 0.6f);
            var around = SpareTank(role, MainTank);
            var arrive = world.Events.Elapsed + leave + (around ? AroundLength(Flat(member.Position), spot) : Vector2.Distance(Flat(member.Position), spot)) / AiManager.RunSpeed;
            var r = role;
            world.Events.Add(leave, () => Walk(r, spot, AiManager.RunSpeed, jitter: false, around));
            if (firstStillness && arrive > stillFrom)
                world.Events.Add(stillFrom - world.Events.Elapsed, () => Halt(r));
        }
    }

    private void ShuffleEveryoneThrough(float check, Func<PartyRole, Vector2>? home)
    {
        At(check - ShuffleLength / ShuffleSpeed - 0.1f, () =>
        {
            foreach (var role in PerRole.All)
                if (Member(role) is { } member)
                    Shuffle(role, home?.Invoke(role) ?? Flat(member.Position), new Vector2((int)role % 2 == 0 ? 1f : -1f, 0f), check);
        });
    }

    private void Shuffle(PartyRole role, Vector2 home, Vector2 direction, float check)
    {
        var leg = ShuffleLength / ShuffleSpeed;
        var now = world.Events.Elapsed;
        world.Events.Add(MathF.Max(0f, check - leg - 0.05f - now), () => Walk(role, home + direction * ShuffleLength, ShuffleSpeed, jitter: false));
        world.Events.Add(MathF.Max(0f, check - 0.05f - now), () => Walk(role, home, ShuffleSpeed, jitter: false));
    }

    private void TakeOpticalSights()
    {
        At(41.6f, () => TakeOpticalSight(state.OpticalSpreadFirst));
        At(47.7f, () => TakeOpticalSight(!state.OpticalSpreadFirst));
        RegroupUnder(52.85f, MainTank);
    }

    private void TakeOpticalSight(bool spread)
    {
        foreach (var role in PerRole.All)
        {
            var r = role;
            world.Events.Add(Reaction(r, 0.2f, 0.6f), () => Walk(r, spread ? SpreadSpot(r) : StackSpot(r), AiManager.RunSpeed, jitter: true, SpareTank(r, MainTank)));
        }
    }

    private static Vector2 SpreadSpot(PartyRole role) => role switch
    {
        MainTank => new(0f, -12f),
        OffTank => new(9.4f, -7.5f),
        MeleeDpsB => new(11.7f, 2.7f),
        CasterDps => new(5.2f, 10.8f),
        PhysRangedDps => new(-5.2f, 10.8f),
        MeleeDpsA => new(-11.7f, 2.7f),
        ShieldHealer => new(-9.4f, -7.5f),
        _ => Centre,
    };

    private static bool InWestParty(PartyRole role) => role is MainTank or RegenHealer or MeleeDpsA or PhysRangedDps;

    private static PartyRole Counterpart(PartyRole role) => role switch
    {
        MainTank => OffTank,
        OffTank => MainTank,
        RegenHealer => ShieldHealer,
        ShieldHealer => RegenHealer,
        MeleeDpsA => MeleeDpsB,
        MeleeDpsB => MeleeDpsA,
        PhysRangedDps => CasterDps,
        _ => PhysRangedDps,
    };

    private Vector2 StackSpot(PartyRole role)
    {
        var marked = state.OpticalStackTargets;
        var swapped = marked.Count == 2 && InWestParty(marked[0]) == InWestParty(marked[1]) ? marked.Max() : (PartyRole?)null;
        var west = InWestParty(role);
        if (swapped is { } mover && (role == mover || role == Counterpart(mover))) west = !west;
        return (west ? WestStack : EastStack) + RingOffset(role, 0.7f);
    }

    private void TakeFateCalibrationAlpha()
    {
        TrailProjections(AlphaProjectionsSetOff, AlphaFatesShown, "alpha-trail", (0.1f, 0.8f), (4f, 7f), (5.4f, 11.8f));
        At(AlphaFatesShown + 0.05f, LeaveForAlphaSpots);
        At(AlphaFatesShown + 0.05f, StrafeThroughTheChecks);
        foreach (var role in PerRole.All) Go(AlphaRegroup + Between(Dice(role, "alpha-regroup"), 0f, 1.3f), role, UptimeSpot(role, MainTank), around: true);
    }

    private bool ThirdCloneSafe => state.AlphaSafeClone == 2;

    private Vector2 AlphaSpot(PartyRole role) => state.AlphaSentenceOf(role) switch
    {
        AlphaSentence.Defamation => ThirdCloneSafe ? ThirdSafeDefamationSpot : DefamationSpot,
        AlphaSentence.SharedSentence => ThirdCloneSafe ? ThirdSafeSharedSpot : SharedSpot,
        AlphaSentence.AggravatedAssault => (ThirdCloneSafe ? ThirdSafeAggravatedSpots : AggravatedSpots)[Rank(role, AlphaSentence.AggravatedAssault)],
        _ => (ThirdCloneSafe ? ThirdSafeNothingSpots : NothingSpots)[Rank(role, AlphaSentence.Nothing)],
    };

    private int Rank(PartyRole role, AlphaSentence sentence)
        => PerRole.All.Where(r => state.AlphaSentenceOf(r) == sentence).ToList().IndexOf(role) % 3;

    private IEnumerable<SimCharacter> AlphaHolders(AlphaSentence sentence)
        => PerRole.All.Where(r => state.AlphaSentenceOf(r) == sentence).Select(Member).OfType<SimCharacter>();

    private void LeaveForAlphaSpots()
    {
        alphaHomes.Clear();
        foreach (var role in PerRole.All)
        {
            if (Member(role) is not { } member) continue;
            var dice = Dice(role, "alpha-leave");
            var kind = KindOf(role);
            var home = AlphaSpot(role) + Offset(dice, Jitter);
            alphaHomes[role] = home;
            var legs = kind switch
            {
                Kind.Healer => 2 + dice.Next(2),
                Kind.Melee => 1,
                _ => dice.NextSingle() < 0.25f ? 2 : 1,
            };
            var pauses = Enumerable.Range(0, legs - 1).Select(_ => Between(dice, 0.5f, 1.4f)).ToList();
            var needs = AroundLength(Flat(member.Position), home) / AiManager.RunSpeed + pauses.Sum();
            var leave = kind switch
            {
                Kind.Melee => 89.5f + 4.5f * Middling(dice),
                Kind.Tank => 87f + 4f * Middling(dice),
                _ => 86f + 4f * Middling(dice),
            };
            leave = MathF.Min(leave, (state.AlphaFirstMotion ? AlphaArriveMoving : AlphaArriveStill) - needs);
            if (kind == Kind.Melee) leave = MathF.Min(leave, AlphaUntargetable);
            WalkInLegs(role, home, leave, pauses, dice);
        }
    }

    private void StrafeThroughTheChecks()
    {
        if (!state.AlphaFirstMotion && !state.AlphaSecondMotion) return;
        foreach (var role in PerRole.All)
        {
            var dice = Dice(role, "alpha-strafe");
            var from = state.AlphaFirstMotion ? AlphaFirstCheck - Between(dice, 2.2f, 3f) : AlphaFirstCheck + Between(dice, 0.6f, 1.3f);
            var until = state.AlphaSecondMotion ? AlphaSecondCheck + Between(dice, 0.4f, 1.6f) : AlphaFirstCheck + Between(dice, 0.25f, 0.7f);
            var reach = Between(dice, 0.55f, 0.8f);
            var speed = Between(dice, 1.7f, 2.3f);
            var r = role;
            world.Events.Add(from - world.Events.Elapsed, () => Strafe(r, until, reach, speed));
        }
    }

    private void Strafe(PartyRole role, float until, float reach, float speed)
    {
        if (Member(role) is not { } member || !alphaHomes.TryGetValue(role, out var home)) return;
        var homeward = walkTargets.TryGetValue(role, out var last) && last.Spot == home;
        if (world.Events.Elapsed >= until)
        {
            if (homeward && member.MoveDestination is not null) Halt(role);
            return;
        }
        if (Vector2.Distance(Flat(member.Position), home) > 1.5f)
        {
            if (homeward && member.MoveDestination is null)
                Walk(role, home, AiManager.RunSpeed, jitter: false, around: true);
            world.Events.Add(0.15f, () => Strafe(role, until, reach, speed));
            return;
        }
        var next = 0;
        void Step()
        {
            if (Member(role) is not { } walker) return;
            var now = world.Events.Elapsed;
            if (now >= until)
            {
                Halt(role);
                return;
            }
            var stops = StrafeStops(role, home, reach);
            var to = stops[next++ % stops.Length];
            Walk(role, to, speed, jitter: false);
            world.Events.Add(MathF.Min(until - now, MathF.Max(0.1f, 0.85f * Vector2.Distance(Flat(walker.Position), to) / speed)), Step);
        }
        Step();
    }

    private Vector2[] StrafeStops(PartyRole role, Vector2 home, float reach)
    {
        if (state.AlphaSentenceOf(role) == AlphaSentence.Defamation)
        {
            var side = 1.4f * reach;
            return [home + new Vector2(side, side), home + new Vector2(side, -side), home + new Vector2(-side, -side), home + new Vector2(-side, side)];
        }
        return [home + StrafeAway(role, home) * reach, home];
    }

    private Vector2 StrafeAway(PartyRole role, Vector2 home)
    {
        var shared = AlphaHolders(AlphaSentence.SharedSentence).Select(m => Flat(m.Position)).ToList();
        var aggravated = AlphaHolders(AlphaSentence.AggravatedAssault).Select(m => Flat(m.Position)).ToList();
        var away = state.AlphaSentenceOf(role) == AlphaSentence.AggravatedAssault
            ? shared.Count > 0 ? home - shared[0] : -home
            : aggravated.Count > 0 ? (shared.Count > 0 ? shared[0] : home) - Centroid(aggravated) : -home;
        return away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : Vector2.Normalize(-home);
    }

    private void TrailProjections(float from, float until, string what, (float From, float To) lags, (float From, float To) gaps, (float From, float To) bands)
    {
        At(from, () =>
        {
            sightings.Clear();
            LookAtProjections(until);
            foreach (var role in PerRole.All) Trail(role, until, Dice(role, what), lags, gaps, bands);
        });
    }

    private void LookAtProjections(float until)
    {
        var now = world.Events.Elapsed;
        foreach (var role in PerRole.All)
        {
            if (Member(role) is not { } member || OwnProjection(member) is not { } projection) continue;
            if (!sightings.TryGetValue(role, out var seen)) sightings[role] = seen = [];
            seen.Add((now, Flat(projection.Position)));
        }
        if (now + LookEvery < until) world.Events.Add(LookEvery, () => LookAtProjections(until));
    }

    private SimEnemy? OwnProjection(SimCharacter member)
        => world.Children.OfType<SimTether>()
            .Where(t => t.IsActive && t.TetherId == TetherId.FateProjection && t.A == member)
            .Select(t => t.B).OfType<SimEnemy>()
            .FirstOrDefault(p => p.IsAlive() && state.Objects.Projections.Contains(p));

    private Vector2? SeenAt(PartyRole role, float time)
    {
        if (!sightings.TryGetValue(role, out var seen)) return null;
        Vector2? last = null;
        foreach (var (at, spot) in seen)
        {
            if (at > time) break;
            last = spot;
        }
        return last;
    }

    private void Trail(PartyRole role, float until, Rng dice, (float From, float To) lags, (float From, float To) gaps, (float From, float To) bands)
    {
        var drifts = dice.NextSingle() < DriftChance(role);
        var lag = Between(dice, lags.From, lags.To);
        var gap = drifts ? Between(dice, 6.5f, 10f) : Between(dice, gaps.From, gaps.To);
        var reach = !drifts ? 12f : role == ShieldHealer ? 7.5f : 11f;
        var band = Math.Clamp(Between(dice, bands.From, bands.To), TrailClear, reach);
        void Aim()
        {
            var now = world.Events.Elapsed;
            if (now >= until) return;
            if (Member(role) is { } member && SeenAt(role, now - lag) is { } seen)
            {
                var at = Flat(member.Position);
                var current = SeenAt(role, now) ?? seen;
                var resting = SeenAt(role, now - 0.4f) is { } before && Vector2.Distance(before, current) < 0.3f;
                var crowding = resting && Vector2.Distance(current, at) < 1.6f;
                var followed = crowding ? current : seen;
                var distance = Vector2.Distance(followed, at);
                var spot = OnBand(at, Vector2.Distance(current, followed) > 0.5f ? current : at, followed, crowding ? MathF.Min(gap, 3f) : gap, band);
                var off = Vector2.Distance(spot, at);
                if ((crowding || (drifts ? distance > gap + 0.5f : off > 1f)) && off > 0.5f)
                    Walk(role, spot, AiManager.RunSpeed, jitter: false, around: true);
            }
            world.Events.Add(Between(dice, 0.35f, 0.8f), Aim);
        }
        world.Events.Add(Between(dice, 0.2f, 0.7f), Aim);
    }

    private static Vector2 OnBand(Vector2 at, Vector2 near, Vector2 followed, float gap, float band)
    {
        var radius = followed.Length();
        if (radius < 0.5f || radius + band < gap) return Outward(at) * band;
        var toward = followed / radius;
        var along = (band * band - gap * gap + radius * radius) / (2f * radius);
        var across = band * band - along * along;
        if (across <= 0f) return toward * band;
        var side = new Vector2(-toward.Y, toward.X) * MathF.Sqrt(across);
        var left = toward * along + side;
        var right = toward * along - side;
        return Vector2.DistanceSquared(left, near) <= Vector2.DistanceSquared(right, near) ? left : right;
    }

    private static float DriftChance(PartyRole role) => role switch
    {
        ShieldHealer => 0.7f,
        MeleeDpsA or MeleeDpsB => 0.5f,
        _ => 0.15f,
    };

    private void TakeCapitalPunishment(float cast, PartyRole holder, bool shared)
    {
        var other = holder == MainTank ? OffTank : MainTank;
        var dice = Dice(other, "buster");
        var joins = shared && dice.NextSingle() < 0.9f ? cast - Between(dice, 4.3f, 7f) : cast + 0.3f;
        Go(cast + 0.3f, holder, shared ? new Vector2(0f, -9.3f) : holder == MainTank ? new Vector2(-0.1f, -9f) : new Vector2(0f, -9.2f));
        Go(joins, other, shared ? new Vector2(0f, -9.2f) : holder == MainTank ? new Vector2(10.4f, -4.8f) : new Vector2(11f, -2.3f), around: true);
        Go(cast + 8.4f, holder, shared ? new Vector2(-11.1f, -4.5f) : holder == MainTank ? new Vector2(-9.8f, -5.8f) : new Vector2(-6.7f, -4.9f));
        if (shared) Go(cast + 8.4f, other, new Vector2(5.4f, -8.2f));
        At(cast + (shared ? 9.38f : holder == MainTank ? 10.41f : 8.91f), () => Provoke(other));
        RegroupUnder(cast + 12.5f, other);
    }

    private void Provoke(PartyRole tank)
    {
        if (Member(tank) is ISimPartyMember member && state.Objects.PerfectAlexander is { } boss) member.UseAction(Enmity.ProvokeActionId, boss);
    }

    private void TakeFateCalibrationBeta()
    {
        TrailProjections(BetaProjectionsSetOff, BetaColours, "beta-trail", (0.1f, 0.6f), (2.5f, 5f), (6.6f, 13.2f));
        At(BetaColours, LeaveForPreMarchSpots);
        At(BetaColours, LightBeaconTakesItsDot);
        At(BetaAfterTheMarch, () => WalkToBetaSpots(AfterTheMarch));
        At(BetaAfterTheMarch, () => PlanSprints([BetaRole.DarkBeacon, BetaRole.DarkFar, BetaRole.DarkUntethered, BetaRole.DarkNear], 0.28f, 191.5f, 195.5f));
        At(BetaSpreadFrom, () =>
        {
            if (state.BetaOpticalStack) WalkToBetaSpots(ForTheStacks);
            else TakeSpreadSpots();
        });
        At(BetaSpreadFrom, () => PlanSprints([BetaRole.LightBeacon, BetaRole.LightFar, BetaRole.LightNear, BetaRole.LightShared], 0.45f, 196.5f, 198.5f));
        At(BetaReprobation + 0.05f, RunUnderTheDonut);
        foreach (var role in PerRole.All) Go(BetaRegroup + Between(Dice(role, "beta-regroup"), 0f, 2.3f), role, UptimeSpot(role, OffTank), around: true);
    }

    private void LeaveForPreMarchSpots()
    {
        foreach (var role in PerRole.All)
        {
            if (Member(role) is not { } member) continue;
            var dice = Dice(role, "beta-premarch");
            var part = state.BetaRoleOf(role);
            var spot = part == BetaRole.LightBeacon ? LightBetaBeaconWait : BeforeTheMarch(part);
            var leave = BetaColours + Between(dice, 0.1f, 3.7f);
            var arrive = KindOf(role) == Kind.Melee ? Between(dice, 162f, 169f) : Between(dice, 159f, 163.5f);
            if (part == BetaRole.LightBeacon) arrive = MathF.Min(arrive, LightBeaconOnItsDot(role) - 1.6f);
            if (KindOf(role) == Kind.Melee)
            {
                HoldThenGo(role, spot, leave, arrive);
                continue;
            }
            var legs = 1 + dice.Next(3);
            var slack = MathF.Max(0f, arrive - leave - AroundLength(Flat(member.Position), spot) / AiManager.RunSpeed);
            var weights = Enumerable.Range(0, legs - 1).Select(_ => Between(dice, 0.3f, 1f)).ToList();
            var pauses = weights.Select(w => slack * w / weights.Sum()).ToList();
            WalkInLegs(role, spot, leave, pauses, dice);
        }
    }

    private void HoldThenGo(PartyRole role, Vector2 spot, float leave, float arrive)
    {
        var hold = spot.Length() > MeleeHold ? Outward(spot) * MeleeHold : spot;
        world.Events.Add(leave - world.Events.Elapsed, () =>
        {
            if (Member(role) is not { } member) return;
            var at = Flat(member.Position);
            if (Vector2.Distance(at, hold) > 1f) Walk(role, hold, AiManager.RunSpeed, jitter: false, around: true);
            var go = MathF.Max(arrive - AroundLength(hold, spot) / AiManager.RunSpeed - world.Events.Elapsed, AroundLength(at, hold) / AiManager.RunSpeed);
            world.Events.Add(go, () => Walk(role, spot, AiManager.RunSpeed, jitter: false, around: true));
        });
    }

    private float LightBeaconOnItsDot(PartyRole beacon)
    {
        var on = KindOf(beacon) switch
        {
            Kind.Ranged => 165.7f,
            Kind.Healer => 169.8f,
            Kind.Tank => 175.3f,
            _ => 176.7f,
        };
        return MathF.Min(BetaLightBeaconLatest, on + Between(Dice(beacon, "beta-dot"), -2f, 2f));
    }

    private void LightBeaconTakesItsDot()
    {
        var beacon = state.BetaHolder(BetaRole.LightBeacon);
        var leave = LightBeaconOnItsDot(beacon) - Vector2.Distance(LightBetaBeaconWait, LightBetaBeaconSpot) / AiManager.RunSpeed;
        world.Events.Add(leave - world.Events.Elapsed, () => Walk(beacon, LightBetaBeaconSpot, AiManager.RunSpeed, jitter: false, around: true));
    }

    private void WalkToBetaSpots(Func<BetaRole, Vector2> spot)
    {
        foreach (var role in PerRole.All)
        {
            var r = role;
            world.Events.Add(Reaction(r, 0.1f, 0.4f), () => Walk(r, spot(state.BetaRoleOf(r)), SpeedOf(r), jitter: false, around: true));
        }
    }

    private static Vector2 BeforeTheMarch(BetaRole role) => role switch
    {
        BetaRole.LightBeacon => LightBetaBeaconSpot,
        BetaRole.DarkBeacon => DarkBetaBeaconSpot,
        BetaRole.LightFar => new(12.5f, -1.3f),
        BetaRole.LightNear => new(12.4f, -1.8f),
        BetaRole.LightShared => new(12.2f, -1.3f),
        BetaRole.DarkNear => new(12.2f, -1.6f),
        BetaRole.DarkUntethered => new(11.5f, 0f),
        _ => new(12.7f, 1f),
    };

    private static Vector2 AfterTheMarch(BetaRole role) => role switch
    {
        BetaRole.DarkUntethered => WestBait,
        BetaRole.DarkFar => SouthBait,
        BetaRole.DarkBeacon => EastBait,
        BetaRole.LightBeacon => NorthStack,
        BetaRole.LightFar => new(-3.5f, -14.1f),
        BetaRole.LightNear => new(-4f, -15f),
        BetaRole.LightShared => new(-3f, -14.8f),
        _ => new(-3.7f, -15.3f),
    };

    private static Vector2 ForTheStacks(BetaRole role) => role switch
    {
        BetaRole.DarkBeacon => DarkStack + new Vector2(0.6f, -0.2f),
        BetaRole.DarkUntethered => DarkStack + new Vector2(-1f, 0.2f),
        BetaRole.DarkFar => DarkStack + new Vector2(-0.2f, 1.1f),
        BetaRole.DarkNear => DarkStack + new Vector2(0.2f, -0.8f),
        BetaRole.LightBeacon => LightStack + new Vector2(-0.3f, -0.5f),
        BetaRole.LightFar => LightStack + new Vector2(-0.4f, 0.4f),
        BetaRole.LightNear => LightStack + new Vector2(0.5f, -0.1f),
        _ => LightStack + new Vector2(0.1f, 0.6f),
    };

    private void TakeSpreadSpots()
    {
        spreadLayout.Clear();
        spreadTargets.Clear();
        tracks.Clear();
        foreach (var (role, spot) in SpreadLayout()) spreadLayout[role] = spot;
        WatchTheParty(BetaSpreadDecided);
        foreach (var role in PerRole.All)
        {
            var dice = Dice(role, "beta-spread");
            var r = role;
            world.Events.Add(Between(dice, 0.1f, 0.9f), () =>
            {
                spreadTargets[r] = spreadLayout[r];
                Walk(r, spreadLayout[r], SpeedOf(r), jitter: false, around: true);
                Reconsider(r, dice);
            });
        }
    }

    private Dictionary<PartyRole, Vector2> SpreadLayout()
    {
        var layout = new Dictionary<PartyRole, Vector2>
        {
            [state.BetaHolder(BetaRole.DarkBeacon)] = SpreadEast,
            [state.BetaHolder(BetaRole.DarkFar)] = SpreadSouth,
            [state.BetaHolder(BetaRole.DarkUntethered)] = SpreadWest,
            [state.BetaHolder(BetaRole.LightBeacon)] = SpreadNorthWest,
        };
        var free = new List<Vector2> { SpreadMiddle, SpreadInnerNorth, SpreadNorth, SpreadNorthEast };
        var north = new[] { BetaRole.LightFar, BetaRole.LightNear, BetaRole.LightShared, BetaRole.DarkNear }.Select(state.BetaHolder)
            .OrderBy(r => KindOf(r) == Kind.Healer ? 0 : 1).ThenBy(r => (int)r);
        foreach (var role in north)
        {
            var spot = SpreadPreferences(KindOf(role)).First(free.Contains);
            layout[role] = spot;
            free.Remove(spot);
        }
        return layout;
    }

    private static Vector2[] SpreadPreferences(Kind kind) => kind switch
    {
        Kind.Healer => [SpreadMiddle, SpreadInnerNorth, SpreadNorth, SpreadNorthEast],
        Kind.Tank => [SpreadNorthEast, SpreadNorth, SpreadInnerNorth, SpreadMiddle],
        Kind.Melee => [SpreadNorth, SpreadNorthEast, SpreadInnerNorth, SpreadMiddle],
        _ => [SpreadNorthEast, SpreadInnerNorth, SpreadNorth, SpreadMiddle],
    };

    private void WatchTheParty(float until)
    {
        var now = world.Events.Elapsed;
        foreach (var role in PerRole.All)
        {
            if (Member(role) is not { } member) continue;
            if (!tracks.TryGetValue(role, out var track)) tracks[role] = track = [];
            track.Add((now, Flat(member.Position)));
        }
        if (now < until) world.Events.Add(LookEvery, () => WatchTheParty(until));
    }

    private Vector2 Heading(PartyRole role, Vector2 at)
    {
        if (!tracks.TryGetValue(role, out var track)) return Vector2.Zero;
        var now = world.Events.Elapsed;
        foreach (var (seenAt, seen) in Enumerable.Reverse(track))
            if (now - seenAt >= 0.15f) return (at - seen) / (now - seenAt);
        return Vector2.Zero;
    }

    private bool Settled(PartyRole role, Vector2 at, Vector2 heading)
        => heading.Length() < 1f
           && (world.Events.Elapsed >= BetaSettled || tracks.TryGetValue(role, out var track) && Vector2.Distance(track[0].Spot, at) > 2f);

    private Vector2 Bound(PartyRole role, Vector2 at, Vector2 heading)
    {
        var planned = spreadLayout.GetValueOrDefault(role, at);
        if (heading.Length() < 1f) return planned;
        var ahead = planned - at;
        if (ahead.Length() < 1.5f || Vector2.Dot(Vector2.Normalize(heading), Vector2.Normalize(ahead)) > 0.9f) return planned;
        return at + heading * 0.6f;
    }

    private IEnumerable<(Vector2 At, Vector2 Heading, bool Settled, Vector2 Claim)> Others(PartyRole role)
    {
        foreach (var other in PerRole.All)
        {
            if (other == role || Member(other) is not { } member) continue;
            var at = Flat(member.Position);
            var heading = Heading(other, at);
            var settled = Settled(other, at, heading);
            yield return (at, heading, settled, settled ? at : Bound(other, at, heading));
        }
    }

    private bool Crowded(PartyRole role, Vector2 me, Vector2 target)
        => Others(role).Any(o => o.Settled
            ? Vector2.Distance(o.At, target) < SpreadClear
            : o.Heading.Length() >= 1f && Vector2.Distance(o.Claim, target) < SpreadClear
              && Vector2.Distance(o.At, target) < Vector2.Distance(me, target) - 0.5f);

    private Vector2 FreeSpreadSpot(PartyRole role, Vector2 me, Vector2 current)
    {
        var claims = Others(role).Select(o => o.Claim).ToList();
        var left = BetaReprobation - 0.3f - world.Events.Elapsed;
        var own = spreadLayout.GetValueOrDefault(role, current);
        var speed = SpeedOf(role);
        Vector2? best = null;
        var bestCost = float.MaxValue;
        var roomiest = current;
        var mostRoom = Room(current);
        foreach (var spot in SpreadSpots) Consider(spot, 0f);
        foreach (var spot in SpreadGrid) Consider(spot, 3f);
        foreach (var step in AroundSteps)
        foreach (var direction in AroundDirections)
            Consider(me + direction * step, 3f);
        return best ?? roomiest;

        float Room(Vector2 spot)
        {
            var room = float.MaxValue;
            foreach (var claim in claims) room = MathF.Min(room, Vector2.Distance(claim, spot));
            return room;
        }

        void Consider(Vector2 spot, float unnamed)
        {
            if (spot.Length() > SpreadEdge || spot.Length() < MiddleClear) return;
            var travel = AroundLength(me, spot);
            if (travel > left * AiManager.SprintSpeed) return;
            var room = Room(spot);
            if (room > mostRoom)
            {
                mostRoom = room;
                roomiest = spot;
            }
            if (room < SpreadRoom) return;
            var cost = travel + unnamed + 0.25f * Vector2.Distance(spot, own) + (travel > left * speed ? 4f : 0f);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = spot;
            }
        }
    }

    private void Reconsider(PartyRole role, Rng dice)
    {
        if (world.Events.Elapsed > BetaSpreadDecided) return;
        if (Member(role) is { } member && spreadTargets.TryGetValue(role, out var target))
        {
            var me = Flat(member.Position);
            if (Crowded(role, me, target) && FreeSpreadSpot(role, me, target) is var free && free != target)
            {
                spreadTargets[role] = free;
                if (AroundLength(me, free) > (BetaReprobation - 0.3f - world.Events.Elapsed) * SpeedOf(role)) PressSprint(role);
                Walk(role, free, SpeedOf(role), jitter: false, around: true);
            }
        }
        world.Events.Add(Between(dice, 0.25f, 0.4f), () => Reconsider(role, dice));
    }

    private void PlanSprints(BetaRole[] parts, float chance, float from, float until)
    {
        foreach (var part in parts)
        {
            var role = state.BetaHolder(part);
            var dice = Dice(role, "beta-sprint");
            if (dice.NextSingle() >= chance) continue;
            world.Events.Add(Between(dice, from, until) - world.Events.Elapsed, () => PressSprint(role));
        }
    }

    private void PressSprint(PartyRole role)
    {
        var now = world.Events.Elapsed;
        if (Member(role) is not ISimPartyMember member || now < sprintUntil.GetValueOrDefault(role)) return;
        member.UseSprint(SprintSeconds);
        sprintUntil[role] = now + SprintSeconds;
        if (Member(role)?.MoveDestination is not null && walkTargets.TryGetValue(role, out var walking))
            Walk(role, walking.Spot, AiManager.SprintSpeed, jitter: false, walking.Around);
    }

    private float SpeedOf(PartyRole role)
        => world.Events.Elapsed < sprintUntil.GetValueOrDefault(role) ? AiManager.SprintSpeed : AiManager.RunSpeed;

    private void RunUnderTheDonut()
    {
        var clone = state.Objects.Clones[state.BetaDonutClone];
        var landing = clone is { IsActive: true } ? Flat(clone.Position)
            : state.BetaDonutClone switch { 0 => EastBait, 3 => WestBait, _ => SouthBait };
        var inward = landing.LengthSquared() > 0.01f ? -Vector2.Normalize(landing) : Vector2.UnitY;
        var across = new Vector2(-inward.Y, inward.X);
        foreach (var role in PerRole.All)
        {
            var dice = Dice(role, "beta-donut");
            var spot = landing + inward * Between(dice, 0.8f, 3f) + across * Between(dice, -2.5f, 2.5f);
            var r = role;
            world.Events.Add(Between(dice, 0.05f, 0.8f), () => RunTo(r, spot));
        }
    }

    private void RunTo(PartyRole role, Vector2 spot)
    {
        if (Member(role) is not { } member) return;
        if (AroundLength(Flat(member.Position), spot) > AiManager.RunSpeed * (BetaDonut - 0.4f - world.Events.Elapsed)) PressSprint(role);
        Walk(role, spot, SpeedOf(role), jitter: false, around: true);
    }

    private void TakeAlmightyJudgment(float cast, int judgment, PartyRole holder)
    {
        foreach (var role in PerRole.All.Where(r => r != holder)) Go(cast + 0.3f, role, WaymarkThree + RingOffset(role, 0.8f));
        At(cast + 8.3f, () =>
        {
            var start = StartingPointBesideTheFirstSet(state.JudgmentOrders[judgment][2], state.JudgmentOrders[judgment][0]);
            foreach (var role in PerRole.All)
            {
                var r = role;
                world.Events.Add(Reaction(r, 0.1f, 0.5f), () => Walk(r, start + RingOffset(r, 0.8f), AiManager.RunSpeed, jitter: false));
            }
        });
        At(cast + 14.14f, () =>
        {
            var first = FirstSetPointNextTo(state.JudgmentOrders[judgment][0], StartingPointBesideTheFirstSet(state.JudgmentOrders[judgment][2], state.JudgmentOrders[judgment][0]));
            foreach (var role in PerRole.All)
            {
                var r = role;
                world.Events.Add(Reaction(r, 0.2f, 0.35f), () => Walk(r, first + RingOffset(r, 0.8f), AiManager.RunSpeed, jitter: false));
            }
        });
        RegroupUnder(cast + 19.5f, holder);
    }

    private static Vector2 StartingPointBesideTheFirstSet(int lastSet, int firstSet) => lastSet switch
    {
        2 => WaymarkThree,
        1 => WaymarkFour,
        _ => firstSet == 2 ? Centre : WaymarkOne,
    };

    private static Vector2 FirstSetPointNextTo(int firstSet, Vector2 from) => firstSet switch
    {
        2 => WaymarkThree,
        1 => WaymarkFour,
        _ => from == WaymarkFour ? WaymarkOne : Centre,
    };

    private void TakeTemporalPrison(float prison)
    {
        foreach (var role in PerRole.All.Where(r => r != OffTank)) Go(prison - 7f, role, PrisonWait + RingOffset(role, 1.5f));
        for (var k = 0; k < Prisoners.Length; k++)
            Go(prison + TeaP4PerfectAlexanderState.FirstJail + TeaP4PerfectAlexanderState.JailEvery * k - 4f, Prisoners[k], JailSpots[k]);
    }

    private void At(float time, Action action, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (TeaP4PerfectAlexanderPractice.Covers(state.Practice, time)) world.Events.Add(time, action, file, line);
    }

    private void Go(float time, PartyRole role, Vector2 spot, bool around = false, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (TeaP4PerfectAlexanderPractice.Covers(state.Practice, time))
            world.Events.Add(time + Reaction(role, 0.15f, 0.55f), () => Walk(role, spot, AiManager.RunSpeed, jitter: true, around), file, line);
        else if (time < practiceStart && (!startSpots.TryGetValue(role, out var known) || time >= known.Time))
            startSpots[role] = (time, spot);
    }

    private void PlaceBotsForPractice()
    {
        foreach (var (role, (_, spot)) in startSpots)
            if (Member(role) is { } bot && world.Party.IsBotDriven(bot))
                bot.SetPosition(new Placement(new Vector3(spot.X, 0f, spot.Y), bot.Rotation));
    }

    private void WalkInLegs(PartyRole role, Vector2 to, float leave, IReadOnlyList<float> pauses, Rng dice)
    {
        var fractions = pauses.Select((_, k) => (k + 1f) / (pauses.Count + 1) + Between(dice, -0.1f, 0.1f)).ToList();
        world.Events.Add(leave - world.Events.Elapsed, () =>
        {
            if (Member(role) is not { } member) return;
            var from = Flat(member.Position);
            var route = AroundTheMiddle(from, to);
            var length = RouteLength(from, route);
            var stops = new List<(float Along, float Pause)>();
            for (var k = 0; k < fractions.Count; k++)
                if (OffTheMiddle(from, route, length, length * fractions[k]) is { } along && (stops.Count == 0 || along > stops[^1].Along + 1f))
                    stops.Add((along, pauses[k]));
            stops.Add((length, 0f));
            var after = 0f;
            var reached = 0f;
            foreach (var (along, pause) in stops)
            {
                var stop = along >= length ? to : PointAlong(from, route, along);
                world.Events.Add(after, () => Walk(role, stop, AiManager.RunSpeed, jitter: false, around: true));
                after += (along - reached) / AiManager.RunSpeed + pause;
                reached = along;
            }
        });
    }

    private static float? OffTheMiddle(Vector2 from, IReadOnlyList<Vector2> route, float length, float along)
    {
        for (var shift = 0f; shift <= length; shift += 0.5f)
        {
            if (along - shift > 1f && along - shift < length - 1f && PointAlong(from, route, along - shift).Length() >= PauseClear) return along - shift;
            if (along + shift > 1f && along + shift < length - 1f && PointAlong(from, route, along + shift).Length() >= PauseClear) return along + shift;
        }
        return null;
    }

    private void Walk(PartyRole role, Vector2 spot, float speed, bool jitter, bool around = false)
    {
        if (Member(role) is not { } member) return;
        var at = jitter ? spot + Offset(Noise(role), Jitter) : spot;
        if (at.Length() > ArenaEdge) at = Vector2.Normalize(at) * ArenaEdge;
        var order = walkOrders[role] = walkOrders.GetValueOrDefault(role) + 1;
        walkTargets[role] = (at, around);
        if (around) FollowRoute(role, AroundTheMiddle(Flat(member.Position), at), 0, speed, order);
        else member.MoveTo(new Vector3(at.X, 0f, at.Y), speed);
    }

    private void FollowRoute(PartyRole role, IReadOnlyList<Vector2> route, int leg, float speed, int order)
    {
        if (walkOrders.GetValueOrDefault(role) != order || Member(role) is not { } member) return;
        var distance = Vector2.Distance(Flat(member.Position), route[leg]);
        member.MoveTo(new Vector3(route[leg].X, 0f, route[leg].Y), speed);
        if (leg + 1 == route.Count) world.Events.Add(distance / speed + 0.25f, () => Arrive(role, route[leg], speed, order));
        else world.Events.Add(MathF.Max(0f, distance - 0.3f) / speed, () => FollowRoute(role, route, leg + 1, speed, order));
    }

    private void Arrive(PartyRole role, Vector2 to, float speed, int order)
    {
        if (walkOrders.GetValueOrDefault(role) != order || Member(role) is not { } member) return;
        var distance = Vector2.Distance(Flat(member.Position), to);
        var stopped = member.MoveDestination is null && !member.MovementLocked;
        if (stopped && distance <= ArrivedWithin) return;
        if (stopped) member.MoveTo(new Vector3(to.X, 0f, to.Y), speed);
        world.Events.Add(distance / speed + 0.25f, () => Arrive(role, to, speed, order));
    }

    private void Halt(PartyRole role)
    {
        walkOrders[role] = walkOrders.GetValueOrDefault(role) + 1;
        Member(role)?.StopMoving();
    }

    private static List<Vector2> AroundTheMiddle(Vector2 from, Vector2 to)
    {
        var route = new List<Vector2>();
        var wide = MiddleClear / MathF.Cos(ArcStep / 2f);
        if (from.Length() < MiddleClear)
        {
            if (to.Length() < MiddleClear || Nearest(from, to) >= from.Length() - 0.01f) return [to];
            from = Outward(from) * wide;
            route.Add(from);
        }
        var through = to.Length() < MiddleClear && Nearest(from, to) < to.Length() - 0.01f ? Outward(to) * wide : to;
        if (from.Length() >= MiddleClear && through.Length() >= MiddleClear && Nearest(from, through) < MiddleClear)
            route.AddRange(ArcBetween(from, through, wide));
        if (through != to) route.Add(through);
        route.Add(to);
        return route;
    }

    private static IEnumerable<Vector2> ArcBetween(Vector2 from, Vector2 to, float wide)
    {
        var (a, b) = (MathF.Atan2(from.Y, from.X), MathF.Atan2(to.Y, to.X));
        var (ta, tb) = (MathF.Acos(MiddleClear / from.Length()), MathF.Acos(MiddleClear / to.Length()));
        var anticlockwise = Turn(b - tb - (a + ta));
        var clockwise = Turn(a - ta - (b + tb));
        var (start, sweep, sign) = anticlockwise <= clockwise ? (a + ta, anticlockwise, 1f) : (a - ta, clockwise, -1f);
        var steps = Math.Max(1, (int)MathF.Ceiling(sweep / ArcStep));
        for (var k = 0; k <= steps; k++)
        {
            var angle = start + sign * sweep * k / steps;
            yield return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * wide;
        }
    }

    private static float Turn(float angle) => (angle % MathF.Tau + MathF.Tau) % MathF.Tau;

    private static float Nearest(Vector2 from, Vector2 to)
    {
        var along = to - from;
        var t = along.LengthSquared() < 1e-6f ? 0f : Math.Clamp(-Vector2.Dot(from, along) / along.LengthSquared(), 0f, 1f);
        return (from + along * t).Length();
    }

    private static Vector2 Outward(Vector2 spot) => spot.LengthSquared() > 1e-4f ? Vector2.Normalize(spot) : Vector2.UnitY;

    private static float AroundLength(Vector2 from, Vector2 to)
    {
        var (a, b) = (from.Length(), to.Length());
        if (a < MiddleClear || b < MiddleClear) return RouteLength(from, AroundTheMiddle(from, to));
        if (Nearest(from, to) >= MiddleClear) return Vector2.Distance(from, to);
        var turn = MathF.Acos(Math.Clamp(Vector2.Dot(from, to) / (a * b), -1f, 1f)) - MathF.Acos(MiddleClear / a) - MathF.Acos(MiddleClear / b);
        return MathF.Sqrt(a * a - MiddleClear * MiddleClear) + MathF.Sqrt(b * b - MiddleClear * MiddleClear) + MiddleClear * MathF.Max(0f, turn);
    }

    private static float RouteLength(Vector2 from, IReadOnlyList<Vector2> route)
    {
        var length = 0f;
        foreach (var next in route)
        {
            length += Vector2.Distance(from, next);
            from = next;
        }
        return length;
    }

    private static Vector2 PointAlong(Vector2 from, IReadOnlyList<Vector2> route, float distance)
    {
        foreach (var next in route)
        {
            var leg = Vector2.Distance(from, next);
            if (distance <= leg) return leg < 1e-4f ? next : Vector2.Lerp(from, next, distance / leg);
            distance -= leg;
            from = next;
        }
        return from;
    }

    private float Reaction(PartyRole role, float from, float to) => Between(Noise(role), from, to);

    private Rng Noise(PartyRole role) => noises.TryGetValue(role, out var noise) ? noise : noises[role] = Dice(role, "noise");

    private Rng Dice(PartyRole role, string what) => new Rng(state.BotSeed).Fork(what + role);

    private static float Between(Rng dice, float from, float to) => from + (to - from) * dice.NextSingle();

    private static float Middling(Rng dice) => (dice.NextSingle() + dice.NextSingle()) / 2f;

    private static Vector2 Offset(Rng dice, float radius)
    {
        var angle = MathF.Tau * dice.NextSingle();
        var length = radius * MathF.Sqrt(dice.NextSingle());
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length;
    }

    private static Vector2 Centroid(IReadOnlyList<Vector2> points) => points.Aggregate(Vector2.Zero, (sum, p) => sum + p) / points.Count;

    private static Kind KindOf(PartyRole role) => role switch
    {
        MainTank or OffTank => Kind.Tank,
        RegenHealer or ShieldHealer => Kind.Healer,
        MeleeDpsA or MeleeDpsB => Kind.Melee,
        _ => Kind.Ranged,
    };

    private static Vector2 RingOffset(PartyRole role, float radius) => Flat(Geometry.OnCompass(45f * (int)role, radius));

    private SimCharacter? Member(PartyRole role) => world.Party.Get(role) is { } member && member.IsAlive() ? member : null;

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
