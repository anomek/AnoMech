using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P2BjCc;

public sealed class TeaP2BjCcAi(bool lightningThreeToOffTank) : IScenarioAi<TeaP2BjCcState>
{
    public string Name => lightningThreeToOffTank ? "NAUR (third lightning on OT)" : "NAUR";

    private const float LookEvery = 0.1f;
    private const float LastLook = 172f;
    private const float Clearance = 1.9f;
    private const float MovingClearance = 2.7f;
    private const float NearestAllowed = 1.7f;
    private const float MovingNearest = 2.5f;
    private const float FidgetClearance = 2.3f;
    private const float DodgeWithin = 1.35f;
    private const float WaryDodgeWithin = 1.8f;
    private const float DodgeAhead = 0.4f;
    private const float DodgeStep = 2.4f;
    private const float DodgeFrom = 4f;
    private const float DodgedClear = 3f;
    private const float ComingWithin = 5f;
    private const float GoneBeyond = 6f;
    private const float NoticeChance = 0.05f;
    private const float TouchNoticeChance = 0.25f;
    private const float TakenWithin = 1.5f;
    private const float SpotSeenFrom = 5f;
    private const float Foresight = 0.8f;
    private const float MotionCarriesOn = 0.35f;
    private const float UrgentNearest = 1.3f;
    private const float UrgentMovingNearest = 1.7f;
    private const float ContactMargin = 1.2f;
    private const float PassingGap = 1.7f;
    private const float NisiLasts = 36f;
    private const float UrgentCarry = 0.15f;
    private const float UrgentWaryCarry = 0.4f;
    private const float LaggedStep = 1.2f;
    private const float LagRemembered = 6f;
    private const float OffTheSpot = 0.3f;
    private const float SteerAgain = 0.5f;
    private const int MostRewalks = 3;
    private const float Reaction = 0.2f;
    private const float LateJudged = 1f;
    private const float SteadyStep = 0.5f;
    private const float StillStep = 0.02f;
    private const float PoseStep = 0.9f;
    private const float JustStopped = 0.5f;
    private const float SteppingAgain = 0.8f;
    private const float StandAfterRetry = 0.6f;
    private const float PressedYield = 0.3f;
    private const float CollisionAhead = 1f;
    private const float WalkingPace = 3f;
    private const float Settled = 1f;
    private const float LeftBehind = 3f;
    private const int ShieldBreakers = 3;
    private const float Circling = 2.5f;
    private const int MostLetGo = 5;
    private const float Settling = 1.5f;
    private const float EarlyDodgeReturns = 3.5f;
    private const int MostDodgeHalts = 3;
    private const int MostYields = 3;
    private const float ReAim = 0.35f;
    private const float CloseEnough = 0.35f;
    private const float EdgeRadius = 18.2f;
    private const float MeleeEdgeRadius = 17.4f;
    private const float ClearOfDash = 7.5f;
    private const float EdgeGap = 3f;
    private const float EdgeSearch = 20f;
    private const float PuddleMargin = 1f;
    private const float PassSide = 2.6f;
    private const float RobotReach = 1f;
    private const float FlarethrowerAimNorthOfTornado = 10f;
    private const float FlameHalfWidth = 45f;
    private const float FlameMargin = 12f;
    private const float ShieldAhead = 4.26f;
    private const float ShieldFacing = 40f;
    private const float ShieldFront = 32f;
    private const float ShieldReach = 22f;
    private const float LineupStaging = 3.4f;
    private const float ArenaEdge = 18.8f;
    private const float TornadoClearance = 11f;
    private const float BlasterReach = 11f;
    private const float CruiseChaserDrift = 1.8f;
    private const float MineTrigger = 2f;
    private const float MineBlast = 9f;
    private const float Receiving = 1.5f;
    private const float ExpiryMargin = 1.5f;
    private const float ShapeSlack = 0.6f;
    private const float LetGoWithin = 4.5f;
    private const float LetGoTrip = 1.5f;
    private const float MinefieldReach = 6f;
    private const float EnumerationReach = 5f;
    private const float Cell = 0.5f;
    private const int MostExpanded = 2500;

    private const float JKick = 2.0f;
    private const float RobotsTargetable = 5.07f;
    private const float ChakramsShow = 25.17f;
    private const float OpticalSnapshot = 31.31f;
    private const float ChakramsDash = 32.22f;
    private const float BlastersLand = 36.5f;
    private const float BlastersResolve = 36.21f;
    private const float PhotonHits = 38.79f;
    private const float SpinCrusherCast = 45.61f;
    private const float SpinCrusherHits = 48.61f;
    private const float FirstStacks = 53.2f;
    private const float EarthMissiles = 61.68f;
    private const float Markers = 63.57f;
    private const float Minefields = 63.67f;
    private const float MinefieldsLand = 66.67f;
    private const float Enumerations = 68.72f;
    private const float SecondStacks = 82.9f;
    private const float FlarethrowerCast = 87.83f;
    private const float Flarethrower = 92.01f;
    private const float ShieldGivesOut = 99.4f;
    private const float SecondWhirlwind = 103.95f;
    private const float ThirdStacks = 112.65f;
    private const float PropellerWind = 125.12f;
    private const float PropellerWindLands = 130.82f;
    private const float StatusesClear = 135.18f;
    private const float SuperJumpCast = 154.5f;
    private const float SuperJumpLands = 158.81f;
    private const float LastRayHit = 166.07f;

    private const float FirstPassGivesUp = 45f;
    private const float FirstPassSupportsLeave = 44.5f;
    private const float SecondPassGivesUp = 76f;
    private const float SecondPassSupportsLeave = 75.5f;
    private const float LastSecondPass = 78.5f;
    private const float ChaseLead = 0.8f;
    private const float ThirdPassGivesUp = 106.5f;
    private const float ThirdPassSupportsLeave = 106f;
    private const float LineupGivesUp = 132.8f;

    private static readonly Vector2 D = new(-13.75f, 0f);
    private static readonly Vector2 B = new(13.75f, -8.25f);
    private static readonly (float From, float Until, Vector2 Spot)[] SettlingTornadoes = [(53.2f, 58.0f, D), (82.9f, 87.7f, B), (112.65f, 117.45f, B)];
    private static readonly Vector2 LightningPartnerSpot = new(0f, -17f);
    private static readonly Vector2 EastOfTheMainTank = new(3.8f, 1.2f);
    private static readonly Vector2 WestOfTheMainTank = new(-3.8f, 1.0f);
    private static readonly Vector2 BesideTheLightningMainTank = new(4.0f, 0.9f);
    private static readonly Vector2 BesideTheSecondWater = new(-3f, 2.4f);
    private static readonly Vector2 BesideTheWaitingOffTank = new(2.8f, 1.1f);
    private static readonly Vector2 SpinCrusherBaitSpot = new(-2f, 17.5f);
    private static readonly Vector2 OffTankMissileSpot = new(-2.6f, 18f);
    private static readonly Vector2 RangedMissileSpot = new(1.6f, 18.3f);
    private static readonly Vector2 OffTankMinefieldSpot = new(-2.8f, 12.2f);
    private static readonly Vector2 RangedMinefieldSpot = new(1.4f, 12.4f);
    private static readonly Vector2 OffTankIceSpot = new(-12.6f, 9.1f);
    private static readonly Vector2 MainTankIceSpot = new(-11.5f, -11.7f);
    private static readonly Vector2 OutOfOffTankMinefield = new(-9f, 12.2f);
    private static readonly Vector2 OffTankAfterMines = new(1.8f, 11.1f);
    private static readonly Vector2 FirstMeleeSpot = new(1.8f, -4.7f);
    private static readonly Vector2 SecondMeleeSpot = new(-2.2f, -5.1f);
    private static readonly Vector2 FirstMeleeEastStep = new(6f, -6f);
    private static readonly Vector2 BehindCruiseChaser = new(1.4f, 3f);
    private static readonly Vector2 WestThirdSpot = new(0.3f, -5.2f);
    private static readonly Vector2 WestHealerSpot = new(-3f, -1.9f);
    private static readonly Vector2 EastCasterSpot = new(7.3f, -3.6f);
    private static readonly Vector2 EastRangedSpot = new(7.9f, -0.4f);
    private static readonly Vector2 EastHealerSpot = new(5.6f, -1.2f);
    private static readonly Vector2 EastHealerCrossing = new(-2.5f, 2.2f);
    private static readonly Vector2 EastFlexWay = new(9.5f, -6.5f);
    private static readonly Vector2 EastFlexSpot = new(9.6f, -2f);
    private static readonly Vector2 PhysRangedFirstStackSpot = new(-13.6f, 3.4f);
    private static readonly Vector2 CasterFirstStackSpot = new(-10.5f, 4f);
    private static readonly Vector2 NorthOfTheFirstWater = new(-12.5f, -4.5f);
    private static readonly Vector2 EastOfTheFirstWater = new(-9f, -1f);
    private static readonly Vector2[] PhysRangedLightningLoop = [new(-6f, 9f), new(-17.8f, 5f), new(-17.8f, -5.5f), new(-9f, -12.5f), new(3.8f, -12.5f)];
    private static readonly Vector2[] PhysRangedLightningDown = [new(9.5f, -11f), new(9.5f, 9f)];
    private static readonly Vector2 CasterLightningTurn = new(5.5f, -6f);
    private static readonly Vector2 CasterLightningExit = new(8.5f, -8f);
    private static readonly Vector2 CasterOutOfTheWater = new(1.5f, 4.5f);
    private static readonly Vector2 CasterSouthOfB = new(10.4f, -0.6f);
    private static readonly Vector2[] RangedAlongTheWall = [new(14.6f, -1.5f), new(13.2f, 1.5f)];
    private static readonly Vector2 SecondStackHealerSpot = new(10f, -5.6f);
    private static readonly Vector2 SecondStackCasterSpot = new(12.4f, -3.4f);
    private static readonly Vector2 SecondStackRangedSpot = new(14.8f, -4.8f);
    private static readonly Vector2 FirstMeleeWestOfBruteJustice = new(-5f, -7f);
    private static readonly Vector2 SecondMeleeWestOfBruteJustice = new(-2.4f, -5.1f);
    private static readonly Vector2 FirstMeleeEastOfTheIce = new(-2f, -4.2f);
    private static readonly Vector2 SecondMeleeEastOfTheIce = new(0.3f, -5.6f);
    private static readonly Vector2 FirstMeleeWestStep = new(-7.8f, 1.3f);
    private static readonly Vector2 FirstMeleeBackFromTheWest = new(-5.5f, -7.5f);
    private static readonly Vector2 LightningHealerLineSpot = new(1.8f, -2f);
    private static readonly Vector2 OtherHealerLineSpot = new(0.5f, 1.5f);
    private static readonly Vector2 MainTankLineSpot = new(0f, -14.8f);
    private static readonly Vector2 FirstMeleeThirdWater = new(7.5f, -11.5f);
    private static readonly Vector2 SecondMeleeThirdWater = new(7f, -7.7f);
    private static readonly Vector2 RangedThirdWater = new(14.9f, -4.6f);
    private static readonly Vector2 CasterThirdWater = new(11.8f, -3f);
    private static readonly Vector2 HealerThirdWater = new(8.8f, -4.9f);
    private static readonly Vector2 SecondWaterAlone = new(9f, 6.2f);
    private static readonly Vector2 AroundTheThirdWater = new(4.6f, -3.2f);
    private static readonly Vector2 OffTankWaitsForLightning = new(-13f, 8.5f);
    private static readonly Vector2 PhysRangedSuperJumpBait = new(17.5f, 0f);
    private static readonly Vector2 PhysRangedOutOfTheRay = new(17.4f, -4.6f);

    private static readonly PartyRole[] Dps = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
    private static readonly PartyRole[] Supports = [MainTank, OffTank, RegenHealer, ShieldHealer];
    private static readonly PartyRole[] Everyone = PerRole.All;
    private static readonly PartyRole[] AllButTanks = [RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];

    private static readonly float[][] EdgeBearings =
    [
        [46.5f, 219.7f, 270f, 90f, 58.6f, 35.4f, 230.4f, 133.3f],
        [350f, 171f, 286f, 108f, 8f, 333f, 244f, 138f],
        [0.3f, 180.6f, 321.3f, 122.8f, 17.5f, 346.8f, 222f, 148.7f],
        [12f, 195f, 266f, 77f, 28f, 359f, 225f, 133f],
    ];

    private static readonly (float From, float Until, PartyRole[] Roles, bool UntilPassed)[] HoldStill =
    [
        (12f, 24.8f, [OffTank, RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps], false),
        (29.6f, 32.6f, Everyone, false),
        (38.6f, 45f, Supports, true),
        (51.4f, 53.4f, Everyone, false),
        (57f, 64.6f, [OffTank, PhysRangedDps], false),
        (63.4f, 68.9f, AllButTanks, false),
        (67.5f, 68.9f, [MainTank, OffTank], false),
        (68.7f, 75.6f, Supports, true),
        (80.6f, 83.1f, Everyone, false),
        (84f, 97f, [OffTank], false),
        (90.6f, 92.3f, Everyone, false),
        (93f, 96.8f, [RegenHealer, ShieldHealer, PhysRangedDps, CasterDps], false),
        (94f, 104f, Supports, true),
        (110.4f, 112.9f, Everyone, false),
        (124f, 135.4f, Everyone, false),
        (145f, 152f, [MainTank, OffTank], false),
        (150f, 166.3f, [PhysRangedDps], false),
    ];

    private TeaP2BjCcState state = null!;
    private SimWorld world = null!;
    private readonly Seat[] seats = Enumerable.Range(0, 8).Select(_ => new Seat()).ToArray();
    private readonly Queue<(float At, Vector2 Where)>[] sightings = Enumerable.Range(0, 8).Select(_ => new Queue<(float At, Vector2 Where)>()).ToArray();
    private readonly List<Watcher> watches = [];
    private readonly Dictionary<PartyRole, Vector2> edgeSpots = [];
    private readonly List<Vector2> mineSpots = [];
    private readonly List<bool> minesPopped = [];
    private float? minesArmedAt;
    private bool shieldSeenUp;
    private float? shieldDownAt;
    private readonly NisiLetter?[] nisi = new NisiLetter?[8];
    private readonly NisiLetter?[] judgment = new NisiLetter?[8];
    private readonly float[] nisiSince = new float[8];
    private readonly float[] jerkySeen = new float[8];
    private readonly float[] steppedAt = new float[8];
    private readonly (Vector2 Velocity, float At)[] lastMotion = new (Vector2 Velocity, float At)[8];
    private readonly NisiLetter?[] receiving = new NisiLetter?[8];
    private float lettersReadAt = float.NaN;
    private float judgmentNisiSeenAt = float.NegativeInfinity;
    private bool lettersHandedOut;
    private PartyRole? firstLightningCarrier;
    private Grid? grid;
    private PartyRole? dodgingFrom;

    public void Run(TeaP2BjCcState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        foreach (var seat in seats) seat.Reset();
        foreach (var seen in sightings) seen.Clear();
        watches.Clear();
        edgeSpots.Clear();
        mineSpots.Clear();
        minesPopped.Clear();
        minesArmedAt = null;
        shieldSeenUp = false;
        shieldDownAt = null;
        lettersReadAt = float.NaN;
        judgmentNisiSeenAt = float.NegativeInfinity;
        firstLightningCarrier = null;
        dodgingFrom = null;
        Array.Clear(nisi);
        Array.Clear(judgment);
        Array.Clear(nisiSince);
        Array.Fill(jerkySeen, float.NegativeInfinity);
        Array.Fill(steppedAt, float.NegativeInfinity);
        Array.Fill(lastMotion, (Vector2.Zero, float.NegativeInfinity));
        Array.Clear(receiving);

        world.Events.Add(0f, Look);
        PlanTheOpening();
        PlanTheChakrams();
        PlanTheFirstPass();
        PlanTheFirstStacksBreakingUp();
        PlanTheMissilesAndMinefields();
        PlanTheMinesAndTheSecondPass();
        PlanTheShieldPhase();
        PlanTheThirdPass();
        PlanTheLineup();
        PlanAfterGavel();
        PlanSuperJump();
    }

    private void PlanTheOpening()
    {
        foreach (var (role, at) in Departures("clock", (OffTank, 2.6f, 3.8f, 5f), (RegenHealer, 2.6f, 4.2f, 6.5f), (ShieldHealer, 3f, 5.2f, 7.5f),
                     (MeleeDpsA, 2.8f, 4.8f, 7f), (MeleeDpsB, 2.4f, 3.6f, 5.5f), (PhysRangedDps, 2.4f, 3.8f, 6f), (CasterDps, 3.2f, 5.6f, 8.5f)))
            When(at, () => TakeTheSpot(role, Stroll with { Settle = 0.3f }, ClockSpot));
        When(JKick + React(MainTank, "toward-bj", 0.3f, 0.7f, 1.2f), () => Walk(MainTank, Stroll, new Vector2(-3.8f, 0.3f)));
        When(RobotsTargetable + React(MainTank, "lure", 0.3f, 0.6f, 1f), () => LureToTheCentre(MainTank, state.Objects.BruteJustice, new Vector2(-0.5f, -0.3f)));
        When(RobotsTargetable + React(OffTank, "lure", 0.8f, 1.3f, 1.9f), () => LureToTheCentre(OffTank, state.Objects.CruiseChaser, new Vector2(0.8f, 0.4f)));
    }

    private void LureToTheCentre(PartyRole tank, SimEnemy? boss, Vector2 bossTo)
    {
        var job = Job(tank);
        Walk(tank, Stroll, () => Later(tank, job, React(tank, "lured", 0.3f, 0.8f, 1.4f), () => TakeTheSpot(tank, Stroll with { Hops = 2 }, ClockSpot)),
            LureSpot(boss, tank, bossTo));
    }

    private static Vector2 ClockSpot(PartyRole role) => role switch
    {
        MainTank => new(0f, -3.2f),
        OffTank => new(0f, 3.4f),
        RegenHealer => new(-3f, 0f),
        ShieldHealer => new(3f, 0f),
        MeleeDpsA => new(2.3f, -2f),
        MeleeDpsB => new(-2.5f, -2.5f),
        PhysRangedDps => new(-2.1f, 2.4f),
        _ => new(2.3f, 2.4f),
    };

    private static Vector2 PhotonSpot(PartyRole role) => role == MainTank ? new Vector2(0f, -2.5f) : ClockSpot(role);

    private void TakeTheSpot(PartyRole role, Gait gait, Func<PartyRole, Vector2> spotOf)
    {
        var spot = spotOf(role);
        var job = seats[(int)role].Job;
        Walk(role, gait, spot);
        Once(role, job, () => !HeadingFor(role, spot) || Vector2.Distance(At(role), spot) < SpotSeenFrom && SpotTakenBy(role, spot, spotOf) is not null, () =>
        {
            if (HeadingFor(role, spot) && Vector2.Distance(At(role), spot) > OffTheSpot + 0.5f && SpotTakenBy(role, spot, spotOf) is { } occupant)
                FindTheSpotTaken(role, gait, spot, spotOf, occupant);
        });
    }

    private bool HeadingFor(PartyRole role, Vector2 spot)
        => seats[(int)role] is var seat && seat.HasWalk(Now) && seat.Stops.Length > 0 && Vector2.Distance(seat.Stops[^1], spot) < 0.1f;

    private PartyRole? SpotTakenBy(PartyRole role, Vector2 spot, Func<PartyRole, Vector2> spotOf)
        => PerRole.All.Where(other => other != role && Member(other) is not null && !Moving(other) && Now - lastMotion[(int)other].At >= Settled
                                      && Vector2.Distance(At(other), spot) < MathF.Min(TakenWithin, Vector2.Distance(At(other), spotOf(other))))
            .Cast<PartyRole?>().FirstOrDefault();

    private void FindTheSpotTaken(PartyRole role, Gait gait, Vector2 spot, Func<PartyRole, Vector2> spotOf, PartyRole occupant)
    {
        var seat = seats[(int)role];
        var draw = seat.TakenSpots++;
        var harmful = PerRole.All.Any(r => NisiOf(r) is not null) && Threat(role, occupant);
        var choice = Dice(role, "spot-taken", draw);
        if (choice < (harmful ? 0.1f : 0.5f))
        {
            seat.Ignoring = occupant;
            return;
        }
        var job = seat.Job;
        var hold = choice < 0.85f
            ? ShortOfTheOccupant(role, occupant, harmful ? Sample(role, "wait-short", 2.5f, 3.5f, 4.5f, draw) : Sample(role, "wait-short", 1.8f, 2.95f, 4.2f, draw), spotOf)
            : BesideTheOccupant(role, occupant, harmful ? Sample(role, "beside", 2f, 2.4f, 3f, draw) : Sample(role, "beside", 1.1f, 1.5f, 2f, draw), spotOf);
        Stand(role, hold);
        Once(role, job, () => Member(occupant) is null || Vector2.Distance(At(occupant), spot) >= TakenWithin, () => Walk(role, gait with { Shaped = false }, spot));
    }

    private Vector2 ShortOfTheOccupant(PartyRole role, PartyRole occupant, float away, Func<PartyRole, Vector2> spotOf)
    {
        var there = At(occupant);
        var off = At(role) - there;
        var way = off.LengthSquared() > 1e-4f ? Vector2.Normalize(off) : Vector2.UnitX;
        foreach (var reach in new[] { away, away + 1f })
        foreach (var turn in new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f })
            if (there + Rotate(way, turn) * reach is var spot && FreeToWait(role, occupant, spot, NearestAllowed, spotOf))
                return spot;
        return there + way * away;
    }

    private Vector2 BesideTheOccupant(PartyRole role, PartyRole occupant, float gap, Func<PartyRole, Vector2> spotOf)
    {
        var there = At(occupant);
        var off = At(role) - there;
        var way = off.LengthSquared() > 1e-4f ? Vector2.Normalize(off) : Vector2.UnitX;
        foreach (var turn in new[] { 70f, -70f, 100f, -100f, 40f, -40f })
            if (there + Rotate(way, turn) * gap is var spot && FreeToWait(role, occupant, spot, MathF.Min(NearestAllowed, gap), spotOf))
                return spot;
        return ShortOfTheOccupant(role, occupant, MathF.Max(gap, 2f), spotOf);
    }

    private bool FreeToWait(PartyRole role, PartyRole occupant, Vector2 spot, float clearance, Func<PartyRole, Vector2> spotOf)
        => spot.Length() <= ArenaEdge - 0.5f && SafeToStand(role, spot, clearance)
           && PerRole.All.All(other => other == role || (other == occupant || Member(other) is null || Vector2.Distance(At(other), spot) >= ContactMargin + 0.2f)
                                       && Vector2.Distance(spotOf(other), spot) >= TakenWithin);

    private void Stand(PartyRole role, Vector2 spot)
    {
        if (Member(role) is not { } member) return;
        if (Vector2.Distance(At(role), spot) > OffTheSpot)
        {
            Walk(role, Stroll with { Shaped = false, Settle = 0f }, spot);
            return;
        }
        var seat = seats[(int)role];
        Halt(member, seat);
        seat.Stops = [];
        seat.Arrived = null;
        seat.RetryAt = float.NaN;
        seat.Wander = 0f;
    }

    private static PartyRole PartnerOf(PartyRole dps) => dps switch
    {
        MeleeDpsA => MainTank,
        MeleeDpsB => RegenHealer,
        PhysRangedDps => OffTank,
        _ => ShieldHealer,
    };

    private static PartyRole DpsPartnerOf(PartyRole support) => Dps.First(d => PartnerOf(d) == support);

    private Vector2 LureSpot(SimEnemy? boss, PartyRole tank, Vector2 bossTo)
    {
        if (boss is null || Member(tank) is not { } member) return ClockSpot(tank);
        var away = bossTo - Flat(boss.Position);
        return away.LengthSquared() < 0.01f
            ? ClockSpot(tank)
            : bossTo + Vector2.Normalize(away) * (boss.HitboxRadius + member.HitboxRadius + RobotReach);
    }

    private void PlanTheChakrams()
    {
        foreach (var (role, delay) in Departures("edge-out", (MainTank, 0.25f, 1.33f, 1.93f), (OffTank, 0.28f, 1.13f, 2.53f), (RegenHealer, 0.25f, 1.43f, 2.73f),
                     (ShieldHealer, 0.25f, 1.43f, 2.53f), (MeleeDpsA, 1.03f, 2.03f, 3.03f), (MeleeDpsB, 1.03f, 1.73f, 2.63f), (PhysRangedDps, 0.25f, 1.43f, 2.63f),
                     (CasterDps, 0.25f, 1.53f, 2.63f)))
            When(ChakramsShow + delay, () => GoToTheEdge(role));
        foreach (var (role, delay) in Departures("edge-back", (MainTank, 0.6f, 1.0f, 1.7f), (OffTank, 0.3f, 0.6f, 1.2f), (RegenHealer, 0.35f, 0.85f, 1.55f),
                     (ShieldHealer, 0.35f, 0.85f, 1.55f), (MeleeDpsA, 0.35f, 0.85f, 1.55f), (MeleeDpsB, 0.35f, 0.85f, 1.55f), (PhysRangedDps, 0.35f, 0.85f, 1.55f),
                     (CasterDps, 0.35f, 0.85f, 1.55f)))
            When(ChakramsDash + MathF.Max(0.25f, delay), () =>
            {
                Job(role);
                TakeTheSpot(role, Stroll with { Due = BlastersLand - 0.6f, Settle = 0.3f, ClearOf = BlasterUnder(role) }, PhotonSpot);
            });
    }

    private void GoToTheEdge(PartyRole role)
    {
        if (ChakramAxisSeen() is not { } axis)
        {
            if (Now < OpticalSnapshot) world.Events.Add(LookEvery, () => GoToTheEdge(role));
            return;
        }
        if (edgeSpots.Count == 0) PickEdgeSpots(axis);
        Job(role);
        Walk(role, Stroll with { Due = OpticalSnapshot - 0.5f, Settle = 0.3f }, edgeSpots[role]);
    }

    private void PickEdgeSpots(int axis)
    {
        edgeSpots.Clear();
        var dashes = state.Objects.Chakrams.Where(c => c.IsActive).Select(c => (From: Flat(c.Position), Along: Forward(c.Rotation))).ToList();
        var taken = new List<Vector2>();
        foreach (var role in PerRole.All)
        {
            var radius = role is MeleeDpsA or MeleeDpsB ? MeleeEdgeRadius : EdgeRadius;
            var spot = EdgeSpotClearOfDashes(EdgeBearings[axis][(int)role], 45f * axis, radius, dashes, taken);
            edgeSpots[role] = spot;
            taken.Add(spot);
        }
    }

    private int? ChakramAxisSeen()
    {
        if (state.Objects.Chakrams.FirstOrDefault(c => c.IsActive) is not { } chakram) return null;
        var at = Flat(chakram.Position);
        var bearing = (float.RadiansToDegrees(MathF.Atan2(at.X, -at.Y)) % 180f + 180f) % 180f;
        return (int)MathF.Round(bearing / 45f) % 4;
    }

    private static Vector2 EdgeSpotClearOfDashes(float bearing, float axis, float radius, List<(Vector2 From, Vector2 Along)> dashes, List<Vector2> taken)
    {
        bool Clear(Vector2 spot) => dashes.All(d => MathF.Abs(Cross(d.Along, spot - d.From)) >= ClearOfDash) && taken.All(t => Vector2.Distance(t, spot) >= EdgeGap);
        foreach (var around in new[] { bearing, 2f * axis - bearing })
            for (var offset = 0f; offset <= EdgeSearch; offset += 2f)
                foreach (var sign in new[] { -1f, 1f })
                    if (Polar(around + sign * offset, radius) is var spot && Clear(spot)) return spot;
        for (var step = 0; step <= 40; step++)
        {
            var offset = (step + 1) / 2 * 2f * (step % 2 == 0 ? 1f : -1f);
            if (Polar(bearing + offset, radius) is var spot && Clear(spot)) return spot;
        }
        return Polar(bearing, radius);
    }

    private void PlanTheFirstPass()
    {
        foreach (var (dps, delay) in Departures("pass-1", Dps.Select(d => (d, 0.75f, 1.45f, 2.6f)).ToArray()))
            When(PhotonHits + delay, () => PassTo(dps, () => PartnerOf(dps), "pass-1", FirstPassGivesUp, TakeTheFirstStackSpot));
        foreach (var support in Supports)
            When(PhotonHits, () => WaitToBePassed(support, () => Passed(support, DpsPartnerOf(support)), FirstPassSupportsLeave,
                support switch
                {
                    MainTank => (0.3f, 1.5f, 2.7f),
                    OffTank => (0.3f, 1.4f, 2.4f),
                    RegenHealer => (1.0f, 2.1f, 3.7f),
                    _ => (1.5f, 3.0f, 5.0f),
                }, LeaveForTheFirstStacks,
                support == OffTank ? SpinCrusherCast - 0.9f - Vector2.Distance(At(OffTank), SpinCrusherBaitSpot) / AiManager.RunSpeed : float.MaxValue));
    }

    private void LeaveForTheFirstStacks(PartyRole support)
    {
        var job = Job(support);
        switch (support)
        {
            case MainTank:
                Walk(MainTank, Stroll with { Hops = Pick(MainTank, "drag-north", 2, 3), Settle = 0.5f, Due = 51f }, LightningPartnerSpot);
                break;
            case OffTank:
                BaitTheSpinCrusher(job);
                break;
            default:
                if (Holds(support, StatusId.CompressedWater))
                    Walk(support, Stroll with { Hops = Pick(support, "drift", 2, 3), Settle = 0f, Due = 51f }, D);
                else
                    Walk(support, Stroll with { Hops = Pick(support, "drift", 1, 3), Due = 51f },
                        Holder(StatusId.CompressedWater) == ShieldHealer ? NorthOfTheFirstWater : EastOfTheFirstWater);
                break;
        }
    }

    private void BaitTheSpinCrusher(int job)
    {
        Walk(OffTank, Stroll with { Settle = 0.4f, Due = SpinCrusherCast - 0.3f }, SpinCrusherBaitSpot);
        Once(OffTank, job, () => Now >= SpinCrusherCast, () => Later(OffTank, job, React(OffTank, "aside", 0.4f, 0.85f, 1.45f),
            () => Walk(OffTank, Stroll with { Due = SpinCrusherHits - 0.3f, Settle = 0f, Shaped = false }, BesideCruiseChaser())));
        Once(OffTank, job, () => Now >= SpinCrusherHits, () => Later(OffTank, job, React(OffTank, "back-south", 0.5f, 1.1f, 1.7f),
            () => Walk(OffTank, Stroll with { Settle = 0f, Due = 57f }, OffTankMissileSpot)));
    }

    private Vector2 BesideCruiseChaser()
    {
        if (state.Objects.CruiseChaser is not { } cruiseChaser) return SpinCrusherBaitSpot;
        var facing = Forward(cruiseChaser.Rotation);
        return Flat(cruiseChaser.Position) + new Vector2(facing.Y, -facing.X) * 4.5f;
    }

    private void TakeTheFirstStackSpot(PartyRole dps)
    {
        var job = Job(dps);
        if (Holds(dps, StatusId.CompressedLightning))
        {
            CarryTheFirstLightning(dps);
            return;
        }
        switch (dps)
        {
            case PhysRangedDps:
                Walk(dps, Stroll with { Hops = Pick(dps, "drift", 2, 3), Due = 51.5f }, PhysRangedFirstStackSpot);
                break;
            case CasterDps:
                Later(dps, job, React(dps, "join-water", 1f, 2.5f, 4.8f),
                    () => Walk(dps, Stroll with { Hops = Pick(dps, "drift", 2, 3), Due = 51.5f }, CasterFirstStackSpot));
                break;
            case MeleeDpsA:
                Walk(dps, Stroll, FirstMeleeSpot);
                if (Dice(dps, "step-east") < 0.5f)
                    Once(dps, job, () => Now >= SpinCrusherHits + Sample(dps, "step-east-at", -0.3f, 0.2f, 0.8f), () => Walk(dps, Stroll with { Settle = 0.3f },
                        () => Later(dps, job, Sample(dps, "step-east-back", 1.6f, 2.4f, 3.2f), () => Walk(dps, Stroll with { Due = 51.3f }, FirstMeleeSpot)),
                        FirstMeleeEastStep));
                break;
            default:
                Walk(dps, Stroll, SecondMeleeSpot);
                if (Dice(dps, "hit-cruise-chaser") < 0.5f)
                {
                    Once(dps, job, () => Now >= SpinCrusherCast + React(dps, "behind-cc", 0.4f, 0.9f, 1.6f), () =>
                    {
                        if (OutOfTheSpinCrusher(BehindCruiseChaser)) Walk(dps, Stroll with { Settle = 0.4f }, BehindCruiseChaser);
                    });
                    Once(dps, job, () => Now >= SpinCrusherHits + Sample(dps, "behind-cc-back", 0.8f, 1.4f, 2.2f),
                        () => Walk(dps, Stroll with { Due = 51.3f }, SecondMeleeSpot));
                }
                break;
        }
    }

    private bool OutOfTheSpinCrusher(Vector2 spot)
    {
        if (state.Objects.CruiseChaser is not { } cruiseChaser) return true;
        var away = spot - Flat(cruiseChaser.Position);
        if (away.Length() > cruiseChaser.HitboxRadius + 6.5f) return true;
        return Vector2.Dot(Vector2.Normalize(away), Forward(cruiseChaser.Rotation)) < MathF.Cos(float.DegreesToRadians(70f));
    }

    private void CarryTheFirstLightning(PartyRole dps)
    {
        firstLightningCarrier = dps;
        var side = dps == MeleeDpsB ? WestOfTheMainTank : EastOfTheMainTank;
        void StandBeside() => Accompany(dps, MainTank, side, FirstStacks + 0.3f);
        switch (dps)
        {
            case CasterDps:
                Walk(dps, Stroll with { Due = FirstStacks - 1.7f }, StandBeside, CasterLightningTurn);
                break;
            case PhysRangedDps:
                Walk(dps, Stroll with { Shaped = false, Due = FirstStacks - 1.7f }, StandBeside, PhysRangedLightningLoop);
                break;
            default:
                StandBeside();
                break;
        }
    }

    private void PlanTheFirstStacksBreakingUp()
    {
        var latest = SettlingTornadoes[0].Until - 2.6f - FirstStacks;
        foreach (var (role, delay) in Departures("after-first-stacks", _ => latest, (RegenHealer, 1.15f, 1.8f, 2.55f), (ShieldHealer, 0.96f, 1.6f, 2.16f),
                     (PhysRangedDps, 0.86f, 1.3f, 1.82f), (CasterDps, 0.85f, 1.5f, 2.4f), (MeleeDpsA, 1.04f, 1.49f, 3.46f), (MeleeDpsB, 0.74f, 1.39f, 2.36f)))
            When(FirstStacks + delay, () => LeaveTheFirstStacks(role));
        When(SettlingTornadoes[0].Until - 2.4f, () => KeepClearOfTornado(0));
    }

    private void LeaveTheFirstStacks(PartyRole role)
    {
        var hadLightning = role == firstLightningCarrier || Member(role) is { } member && member.ShowsStatus(StatusId.LightningResistanceDownII);
        if (role is MeleeDpsA or MeleeDpsB && !hadLightning) return;
        Job(role);
        var clear = Stroll with { ClearOf = TornadoReach(0) };
        switch (role)
        {
            case MeleeDpsA:
                Walk(role, Stroll, FirstMeleeSpot);
                break;
            case MeleeDpsB:
                Walk(role, Stroll, SecondMeleeSpot);
                break;
            case RegenHealer:
                Walk(role, clear, ClearOfTornado(0, WestHealerSpot));
                break;
            case ShieldHealer:
                Walk(role, clear with { PauseAtStops = true }, ClearOfTornado(0, EastHealerCrossing), EastHealerSpot);
                break;
            case PhysRangedDps:
                if (hadLightning) Walk(role, Stroll with { Sprint = true, Shaped = false, Settle = 0f, Due = EarthMissiles - 0.8f }, [.. PhysRangedLightningDown, RangedMissileSpot]);
                else Walk(role, clear with { Sprint = true, BowOutward = true, Settle = 0f, Due = EarthMissiles - 0.8f }, RangedMissileSpot);
                break;
            default:
                if (hadLightning) Walk(role, Stroll, CasterLightningExit, EastCasterSpot);
                else Walk(role, clear, CasterOutOfTheWater, EastCasterSpot);
                break;
        }
    }

    private void PlanTheMissilesAndMinefields()
    {
        When(EarthMissiles + React(OffTank, "minefield-step", 0.4f, 0.9f, 1.2f), () =>
        {
            Job(OffTank);
            Walk(OffTank, Stroll with { Sprint = true, Shaped = false, Due = Minefields - 0.2f, Settle = 0f }, OffTankMinefieldSpot);
        });
        When(EarthMissiles + React(PhysRangedDps, "minefield-step", 0.45f, 0.95f, 1.25f), () =>
        {
            Job(PhysRangedDps);
            Walk(PhysRangedDps, Stroll with { Sprint = true, Shaped = false, Due = Minefields - 0.2f, Settle = 0f }, RangedMinefieldSpot);
        });
        When(Minefields + React(OffTank, "leave-minefield", 0.35f, 0.85f, 1.35f), () =>
        {
            Job(OffTank);
            Walk(OffTank, Stroll with { Sprint = true, Settle = state.IceTank == OffTank ? 0f : 0.5f, ClearOf = (OffTankMinefieldSpot, MinefieldsLand - 0.4f, MinefieldReach) },
                state.IceTank == OffTank ? OffTankIceSpot : OutOfOffTankMinefield);
        });
        When(Minefields + React(PhysRangedDps, "leave-minefield", 0.3f, 0.6f, 0.95f), () =>
        {
            Job(PhysRangedDps);
            Walk(PhysRangedDps, Stroll with { Due = Enumerations - 0.7f, ClearOf = (RangedMinefieldSpot, MinefieldsLand - 0.4f, MinefieldReach) },
                EnumerationsNeedFlex() ? WestThirdSpot : EastRangedSpot);
        });
        When(Markers + React(MeleeDpsA, "enumeration-group", 0.6f, 1.0f, 1.6f), () =>
        {
            Job(MeleeDpsA);
            if (EnumerationsNeedFlex()) Walk(MeleeDpsA, Stroll with { Due = Enumerations - 0.5f }, EastFlexWay, EastFlexSpot);
            else Walk(MeleeDpsA, Stroll with { Due = Enumerations - 0.5f }, WestThirdSpot);
        });
        When(Markers + React(MainTank, "ice-out", 0.75f, 1.2f, 1.8f), () =>
        {
            if (state.IceTank != MainTank) return;
            Job(MainTank);
            Walk(MainTank, Stroll with { Due = Enumerations - 0.4f, Settle = 0f }, MainTankIceSpot);
        });
        for (var t = 65.75f; t < 83f; t += 0.5f) When(t, StepOutOfPuddles);
        foreach (var role in AllButTanks)
        {
            var looks = Markers + Sample(role, "enumeration-look", 2f, 2.4f, 3f);
            Watch(role, () => Now >= looks && !seats[(int)role].HasWalk(Now) && EnumerationNearby(role) is { } marked && !Moving(marked)
                              && Vector2.Distance(At(marked), At(role)) < 6.5f && Vector2.Distance(At(marked), EnumerationGroupMiddle(role)) < 4f,
                () => StepIntoTheEnumeration(role), Enumerations - 0.5f);
            Watch(role, () => Now >= Enumerations - 1.8f && !seats[(int)role].HasWalk(Now) && OtherEnumeration(role) is { } other && !Moving(other)
                              && Vector2.Distance(At(other), At(role)) < EnumerationReach + 0.5f,
                () => StepOutOfTheOtherEnumeration(role), Enumerations - 0.5f);
        }
    }

    private PartyRole? OtherEnumeration(PartyRole role)
        => Now < Markers || role.IsTank()
            ? null
            : state.Enumerations.Where(r => r != role && InTheWestGroup(r) != InTheWestGroup(role) && Member(r) is not null).Cast<PartyRole?>().FirstOrDefault();

    private void StepOutOfTheOtherEnumeration(PartyRole role)
    {
        if (OtherEnumeration(role) is not { } other) return;
        var there = At(other);
        var away = At(role) - there;
        var to = there + (away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : Vector2.UnitX) * (EnumerationReach + 1.2f);
        if (EnumerationNearby(role) is { } mine && Vector2.Distance(At(mine), to) > EnumerationReach - 0.5f || !SafeToStand(role, to, NearestAllowed)) return;
        Walk(role, Stroll with { Shaped = false, Settle = 0f, Due = Enumerations - 0.2f }, to);
    }

    private PartyRole? EnumerationNearby(PartyRole role)
    {
        if (Now < Markers || role.IsTank() || state.Enumerations.Contains(role)) return null;
        return state.Enumerations.Where(r => InTheWestGroup(r) == InTheWestGroup(role) && Member(r) is not null).Cast<PartyRole?>().FirstOrDefault();
    }

    private bool InTheWestGroup(PartyRole role) => role is RegenHealer or MeleeDpsB || role == (EnumerationsNeedFlex() ? PhysRangedDps : MeleeDpsA);

    private Vector2 EnumerationGroupMiddle(PartyRole role) => InTheWestGroup(role) ? new Vector2(-1.6f, -4.1f) : new Vector2(6.9f, -2.3f);

    private void StepIntoTheEnumeration(PartyRole role)
    {
        if (Member(role) is not { } member || EnumerationNearby(role) is not { } marked) return;
        var at = Flat(member.Position);
        var there = At(marked);
        var distance = Vector2.Distance(at, there);
        if (distance <= 4.2f) return;
        var to = there + (at - there) / distance * Sample(role, "enumeration-in", 3.3f, 3.7f, 4f);
        if (state.Enumerations.Any(r => r != marked && Member(r) is not null && Vector2.Distance(At(r), to) < 5.6f)) return;
        Walk(role, Stroll with { Shaped = false, Settle = 0f, Due = Enumerations - 0.2f }, to);
    }

    private bool EnumerationsNeedFlex()
    {
        var marked = Dps.Where(r => state.Enumerations.Contains(r)).ToList();
        return marked.Count == 2 && (marked.All(r => r is MeleeDpsA or MeleeDpsB) || marked.All(r => r is PhysRangedDps or CasterDps));
    }

    private void PlanTheMinesAndTheSecondPass()
    {
        Watch(OffTank, () => minesArmedAt is not null, () =>
        {
            if (state.IceTank == OffTank) When(Enumerations + React(OffTank, "pop", 0.3f, 0.6f, 1f), PopTheMines);
            else When(minesArmedAt!.Value + React(OffTank, "pop", 0.6f, 1.2f, 2.2f), PopTheMines);
        });
        When(Enumerations + React(MainTank, "ice-back", 0.5f, 1.2f, 1.8f), () =>
        {
            if (state.IceTank != MainTank) return;
            Job(MainTank);
            Walk(MainTank, Stroll with { Settle = 0.5f }, LightningPartnerSpot);
        });
        var walkers = Departures("pass-2", (CasterDps, 1.2f, 1.8f, 2.6f), (MeleeDpsB, 1.4f, 2.0f, 2.8f), (MeleeDpsA, 0.7f, 1.5f, 2.4f));
        foreach (var (dps, delay) in walkers)
            When(Enumerations + delay, () => PassTo(dps, () => PartnerOf(dps), "pass-2", SecondPassGivesUp, TakeTheSecondStackSpotAfterPassing));
        When(Enumerations + React(PhysRangedDps, "toward-the-ot", 0.6f, 1.0f, 1.6f), StageForTheSecondPass);
        Watch(PhysRangedDps, () => MinesPopped, () =>
            Later(PhysRangedDps, seats[(int)PhysRangedDps].Job,
                MathF.Max(React(PhysRangedDps, "mines-popped", 0.4f, 0.9f, 1.6f), Enumerations + React(PhysRangedDps, "enumeration-done", 0.3f, 0.5f, 0.8f) - Now),
                () => PassTo(PhysRangedDps, () => OffTank, "pass-2", SecondPassGivesUp, TakeTheSecondStackSpotAfterPassing)),
            SecondPassGivesUp - 1.5f, () => PassTo(PhysRangedDps, () => OffTank, "pass-2", SecondPassGivesUp, TakeTheSecondStackSpotAfterPassing));
        foreach (var healer in new[] { RegenHealer, ShieldHealer })
            When(Enumerations, () => WaitToBePassed(healer, () => Passed(healer, DpsPartnerOf(healer)) || CopyRunsOut(healer, DpsPartnerOf(healer)), LastSecondPass,
                (1.0f, 2.5f, 5.0f), TakeTheSecondStackSpot));
    }

    private void StageForTheSecondPass()
    {
        if (MinesPopped || mineSpots.Count == 0 || Member(PhysRangedDps) is not { } ranged) return;
        var from = Flat(ranged.Position);
        var unpopped = mineSpots.Where((_, k) => !minesPopped[k]).ToList();
        var stage = Enumerable.Range(0, 21).Select(k => Vector2.Lerp(from, OffTankAfterMines, 1f - k * 0.05f))
            .FirstOrDefault(spot => unpopped.All(m => Vector2.Distance(spot, m) >= MineBlast), from);
        if (Vector2.Distance(stage, from) < 1f) return;
        Job(PhysRangedDps);
        Walk(PhysRangedDps, Stroll with { Settle = 0.3f }, stage);
    }

    private void PopTheMines()
    {
        if (Member(OffTank) is not { } offTank) return;
        Job(OffTank);
        var from = Flat(offTank.Position);
        var order = mineSpots.Count > 0 ? mineSpots.OrderBy(m => Vector2.Distance(m, from)).ToList() : [OffTankMinefieldSpot, RangedMinefieldSpot];
        Walk(OffTank, Stroll with { Sprint = true, Shaped = false, Settle = 0f },
            () => WaitToBePassed(OffTank, () => Passed(OffTank, PhysRangedDps) || CopyRunsOut(OffTank, PhysRangedDps), LastSecondPass, (0.6f, 1.2f, 2f), CreepToTheShieldFacing),
            [.. order, OffTankAfterMines]);
    }

    private void CreepToTheShieldFacing(PartyRole offTank)
    {
        Job(offTank);
        Walk(offTank, Stroll with { Hops = Pick(offTank, "creep", 2, 4), Due = SecondStacks - 1.5f, Settle = 0f }, FacingSpotForShield());
    }

    private void TakeTheSecondStackSpotAfterPassing(PartyRole dps)
    {
        var job = Job(dps);
        var (low, middle, high) = dps switch
        {
            MeleeDpsA => (0.2f, 1.2f, 3.4f),
            MeleeDpsB => (0.6f, 3.4f, 6.4f),
            CasterDps => (0.6f, 2.4f, 4.4f),
            _ => (0f, 0.1f, 0.8f),
        };
        Later(dps, job, MathF.Min(React(dps, "after-pass-2", low, middle, high), SecondStacks - 6f - Now), () => TakeTheSecondStackSpot(dps));
    }

    private void TakeTheSecondStackSpot(PartyRole role)
    {
        Job(role);
        var due = Stroll with { Due = SecondStacks - 0.8f };
        if (Holds(role, StatusId.CompressedWater))
        {
            Walk(role, due with { Settle = 0f, BowOutward = true }, B);
            return;
        }
        var iceNorthWest = state.IceTank == MainTank;
        switch (role)
        {
            case MeleeDpsA:
                Walk(role, due with { Hops = Pick(role, "melee-hops", 1, 3) }, iceNorthWest ? FirstMeleeEastOfTheIce : FirstMeleeWestOfBruteJustice);
                break;
            case MeleeDpsB:
                Walk(role, due, iceNorthWest ? SecondMeleeEastOfTheIce : SecondMeleeWestOfBruteJustice);
                break;
            case CasterDps:
                Walk(role, due, SecondStackCasterSpot);
                break;
            case PhysRangedDps:
                Walk(role, due with { BowOutward = true }, SecondStackRangedSpot);
                break;
            default:
                if (Member(role) is { } member && member.ShowsStatus(StatusId.WaterResistanceDownIIP2))
                {
                    var hops = Pick(role, "to-the-mt-hops", 1, 3);
                    var halfway = Vector2.Lerp(At(role), LightningPartnerSpot + BesideTheLightningMainTank, 0.55f);
                    if (hops > 1) Walk(role, due, () => Accompany(role, MainTank, BesideTheLightningMainTank, SecondStacks + 0.3f), halfway);
                    else Accompany(role, MainTank, BesideTheLightningMainTank, SecondStacks + 0.3f);
                }
                else Walk(role, due, SecondStackHealerSpot);
                break;
        }
    }

    private Vector2 FacingSpotForShield()
    {
        if (state.Objects.CruiseChaser is not { } cruiseChaser || Member(OffTank) is not { } offTank) return new Vector2(1.1f, 7.2f);
        var stop = cruiseChaser.HitboxRadius + offTank.HitboxRadius + RobotReach;
        return Flat(cruiseChaser.Position) + Polar(ShieldFacing, 1f) * (stop - 0.9f);
    }

    private void PlanTheShieldPhase()
    {
        var latest = SettlingTornadoes[1].Until - 2.6f - SecondStacks;
        foreach (var (role, delay) in Departures("after-second-stacks", _ => latest, (RegenHealer, 1.05f, 1.7f, 2.4f), (ShieldHealer, 1.07f, 1.6f, 2.8f),
                     (PhysRangedDps, 1.0f, 1.6f, 2.2f), (CasterDps, 0.96f, 1.8f, 2.7f)))
            When(SecondStacks + delay, () => TakeTheShieldSpot(role));
        if (Dice(MeleeDpsA, "west-step") < 0.5f)
        {
            When(SecondStacks + Sample(MeleeDpsA, "west-step-at", 1.1f, 2.1f, 3.6f), () =>
            {
                var job = Job(MeleeDpsA);
                Walk(MeleeDpsA, Stroll with { Settle = 0.4f },
                    () => Later(MeleeDpsA, job, Sample(MeleeDpsA, "west-step-back", 1.2f, 2.0f, 3.0f),
                        () => Walk(MeleeDpsA, Stroll with { Due = Flarethrower - 2f }, FirstMeleeBackFromTheWest)),
                    FirstMeleeWestStep);
            });
        }
        When(SettlingTornadoes[1].Until - 2.4f, () => KeepClearOfTornado(1));
        When(Sample(MainTank, "flare-bait", 86.3f, 88f, 89.2f), BaitTheFlarethrower);
        foreach (var role in PerRole.All.Where(r => r != MainTank))
        {
            When(FlarethrowerCast + React(role, "flame-seen", 0.25f, 0.45f, 0.8f), () => KeepOutOfTheFlame(role));
            When(FlarethrowerCast, () => Watch(role, () => MainTankBaits() && !Moving(MainTank),
                () => When(Now + React(role, "flame-baited", 0.2f, 0.3f, 0.5f), () => KeepOutOfTheFlame(role)), Flarethrower - 1.2f));
            When(Flarethrower - 1.1f + React(role, "flame-again", 0.2f, 0.25f, 0.35f), () => KeepOutOfTheFlame(role));
        }
        When(Flarethrower + React(MainTank, "flare-back", 0.9f, 2.2f, 2.8f), () =>
        {
            Job(MainTank);
            Walk(MainTank, Stroll with { Settle = 0f }, MainTankLineSpot);
        });
        Watch(CasterDps, () => shieldSeenUp, () =>
        {
            foreach (var ranged in new[] { PhysRangedDps, CasterDps })
                When(Now + React(ranged, "shield-seen", 0.2f, 0.45f, 0.8f), () => FaceTheShield(ranged));
            foreach (var healer in new[] { RegenHealer, ShieldHealer })
                When(Now + React(healer, "shield-seen", 0.2f, 0.45f, 0.8f), () => StepInFrontOfTheShield(healer));
        }, ShieldGivesOut);
    }

    private void TakeTheShieldSpot(PartyRole role)
    {
        var job = Job(role);
        var clear = Stroll with { ClearOf = TornadoReach(1), Settle = 0.4f };
        var fromB = At(role).X > 9f;
        switch (role)
        {
            case CasterDps when fromB:
                Walk(role, clear with { Shaped = false }, CasterSouthOfB, CasterShieldSpot());
                break;
            case CasterDps:
                Walk(role, clear, CasterShieldSpot());
                break;
            case PhysRangedDps when fromB:
                Walk(role, clear with { Shaped = false }, [.. RangedAlongTheWall, RangedShieldSpot()]);
                break;
            case PhysRangedDps:
                Walk(role, clear, RangedShieldSpot());
                break;
            default:
                var lightning = Holds(role, StatusId.CompressedLightning);
                Walk(role, clear with { Settle = 0.3f, Shaped = false }, lightning ? LightningHealerLine() : OtherHealerLine());
                if (!lightning && !HealerLightningSeen()) WatchForTheLightning(role, job);
                break;
        }
    }

    private bool HealerLightningSeen() => Holds(RegenHealer, StatusId.CompressedLightning) || Holds(ShieldHealer, StatusId.CompressedLightning);

    private void WatchForTheLightning(PartyRole healer, int job) => Later(healer, job, 0.15f, () =>
    {
        if (Holds(healer, StatusId.CompressedLightning))
            Later(healer, job, React(healer, "line-switch", 0.25f, 0.4f, 0.7f), () => Walk(healer,
                Stroll with { ClearOf = TornadoReach(1), Settle = 0.3f, Shaped = false }, LightningHealerLine()));
        else if (!HealerLightningSeen() && Now < SecondStacks + 2.5f) WatchForTheLightning(healer, job);
    });

    private void StepInFrontOfTheShield(PartyRole healer)
    {
        if (state.Objects.PlasmaShield is not { IsActive: true, Dead: false } shield || Member(healer) is not { } member) return;
        var from = Flat(member.Position);
        if (InFrontOf(shield, from)) return;
        if (seats[(int)healer].HasWalk(Now) || PerRole.All.Count(r => Member(r) is { } m && InFrontOf(shield, Flat(m.Position))) >= ShieldBreakers)
        {
            When(Now + 0.3f, () => StepInFrontOfTheShield(healer));
            return;
        }
        var at = Flat(shield.Position);
        var facing = Forward(shield.Rotation);
        var offset = from - at;
        var turn = float.RadiansToDegrees(MathF.Atan2(Cross(facing, offset), Vector2.Dot(facing, offset)));
        var distance = Math.Clamp(offset.Length(), 4f, ShieldReach - 2f);
        foreach (var reach in new[] { 0f, 1.5f, -1.5f, 3f })
        foreach (var inside in new[] { ShieldFront - 6f, ShieldFront - 12f, ShieldFront - 18f })
        {
            var to = at + Rotate(facing, Math.Clamp(turn, -inside, inside)) * Math.Clamp(distance + reach, 4f, ShieldReach - 2f);
            if (to.Length() > ArenaEdge - 0.5f || !SafeToStand(healer, to, NearestAllowed)
                || PerRole.All.Any(other => other != healer && Member(other) is { } them && Vector2.Distance(Flat(them.Position), to) < EdgeGap)) continue;
            Walk(healer, Stroll with { Shaped = false, Settle = 0f }, to);
            return;
        }
        When(Now + 0.3f, () => StepInFrontOfTheShield(healer));
    }

    private void FaceTheShield(PartyRole ranged)
    {
        if (state.Objects.PlasmaShield is not { IsActive: true } shield || Member(ranged) is not { } member || InFrontOf(shield, Flat(member.Position))) return;
        if (seats[(int)ranged].HasWalk(Now))
        {
            When(Now + 0.3f, () => FaceTheShield(ranged));
            return;
        }
        Job(ranged);
        var at = Flat(shield.Position);
        var facing = Forward(shield.Rotation);
        Walk(ranged, Stroll with { Shaped = false, Settle = 0f }, ranged == CasterDps ? ShieldSpot(at, facing, -20f, 5.2f, null) : ShieldSpot(at, facing, 20f, 6.8f, null));
    }

    private static bool InFrontOf(SimEnemy shield, Vector2 spot)
    {
        var offset = spot - Flat(shield.Position);
        var distance = offset.Length();
        return distance is > 1e-3f and <= ShieldReach
               && Vector2.Dot(Forward(shield.Rotation), offset / distance) >= MathF.Cos(float.DegreesToRadians(ShieldFront));
    }

    private (Vector2 At, Vector2 Facing) ExpectedShield()
    {
        if (state.Objects.CruiseChaser is not { } cruiseChaser) return (new Vector2(0.3f, 8.1f), Polar(ShieldFacing, 1f));
        var facing = Forward(cruiseChaser.Rotation);
        return (Flat(cruiseChaser.Position) + facing * ShieldAhead, facing);
    }

    private Vector2 RangedShieldSpot()
    {
        var (at, facing) = ExpectedShield();
        return ShieldSpot(at, facing, 20f, 6.8f, CasterShieldSpot());
    }

    private Vector2 CasterShieldSpot()
    {
        var (at, facing) = ExpectedShield();
        return ShieldSpot(at, facing, -20f, 5.2f, null);
    }

    private Vector2 ShieldSpot(Vector2 at, Vector2 facing, float degrees, float distance, Vector2? besides)
    {
        var keepClear = new List<Vector2> { LightningHealerLine(), OtherHealerLine(), FacingSpotForShield() };
        if (besides is { } other) keepClear.Add(other);
        foreach (var (turn, reach) in from turn in new[] { 0f, -8f, 8f, -16f, 16f } from reach in new[] { 0f, 1.5f, -1f, 3f } select (turn, reach))
        {
            var spot = at + Rotate(facing, degrees + turn) * (distance + reach);
            if (spot.Length() <= ArenaEdge && keepClear.All(k => Vector2.Distance(k, spot) >= 2.8f)
                && Vector2.Distance(spot, TornadoSpot(1)) >= TornadoClearance) return spot;
        }
        return at + Rotate(facing, degrees) * distance;
    }

    private Vector2 LightningHealerLine() => ClearOfTornado(1, LightningHealerLineSpot);

    private Vector2 OtherHealerLine() => ClearOfTornado(1, OtherHealerLineSpot);

    private void BaitTheFlarethrower()
    {
        var settled = SettlingTornadoes[1].Until - Now;
        if (settled > 0f && Vector2.Distance(FlarethrowerBaitSpot(), TornadoSpot(1)) < TornadoClearance)
            world.Events.Add(settled + 0.1f, BaitTheFlarethrower);
        else
        {
            Job(MainTank);
            Walk(MainTank, Stroll with { Shaped = false, Settle = 0f, Due = Flarethrower - 0.4f }, FlarethrowerBaitSpot());
        }
    }

    private bool MainTankBaits() => state.Objects.BruteJustice is { } bruteJustice && Member(MainTank) is { } mainTank
                                    && Vector2.Distance(Flat(bruteJustice.Position), Flat(mainTank.Position)) < 2f;

    private (Vector2 From, Vector2 Axis)? Flame()
    {
        if (state.Objects.BruteJustice is not { } bruteJustice) return null;
        var from = Flat(bruteJustice.Position);
        var aim = (MainTankBaits() ? At(MainTank) : FlarethrowerBaitSpot()) - from;
        return aim.LengthSquared() < 1e-4f ? null : (from, Vector2.Normalize(aim));
    }

    private bool InTheFlame(Vector2 spot) => Now is >= Flarethrower - 4.6f and <= Flarethrower + 0.3f && Flame() is { } flame
        && spot - flame.From is var offset && offset.Length() is var reach and > 1e-3f
        && float.RadiansToDegrees(MathF.Acos(Math.Clamp(Vector2.Dot(flame.Axis, offset / reach), -1f, 1f))) < FlameHalfWidth + FlameMargin;

    private void KeepOutOfTheFlame(PartyRole role)
    {
        if (Flame() is not { } flame || Member(role) is not { } member) return;
        var (from, axis) = flame;
        var seat = seats[(int)role];
        var at = Flat(member.Position);
        var goal = seat.HasWalk(Now) && seat.Stops.Length > 0 ? seat.Stops[^1] : at;
        if (!InTheFlame(at) && !InTheFlame(goal)) return;
        var offset = goal - from;
        var reach = Math.Clamp(offset.Length(), 5f, 14f);
        var side = Cross(axis, offset) >= 0f ? 1f : -1f;
        var spots = new[] { 8f, 4f, 14f, 20f, 28f }.SelectMany(extra => new[] { 1f, 0.85f, 1.15f },
            (extra, stretch) => from + Rotate(axis, side * (FlameHalfWidth + FlameMargin + extra)) * reach * stretch);
        foreach (var to in spots)
        {
            if (to.Length() > ArenaEdge - 0.5f || !SafeToStand(role, to, FidgetClearance - 0.4f)
                || PerRole.All.Any(other => other != role && Member(other) is { } them && Vector2.Distance(Flat(them.Position), to) < NearestAllowed)) continue;
            Walk(role, Stroll with { Shaped = false, Due = Flarethrower - 0.3f, Settle = 0f }, to);
            return;
        }
    }

    private Vector2 FlarethrowerBaitSpot()
    {
        if (state.Objects.BruteJustice is not { } bruteJustice) return new Vector2(1.1f, -10.4f);
        var from = Flat(bruteJustice.Position);
        var tornado = TornadoSpot(1);
        var aim = tornado - from;
        var toward = aim.LengthSquared() > 0.01f ? Rotate(Vector2.Normalize(aim), -FlarethrowerAimNorthOfTornado) : new Vector2(1f, 0f);
        return from + toward * 1.1f;
    }

    private void PlanTheThirdPass()
    {
        var delays = Departures("pass-3", Dps.Select(d => (d, 0.4f, 1.0f, 2.6f)).ToArray());
        foreach (var dps in Dps)
            Watch(dps, () => shieldDownAt is not null, () => When(shieldDownAt!.Value + delays[dps], () => PassThree(dps)), ShieldGivesOut, () => PassThree(dps));
        foreach (var support in Supports)
            When(SecondStacks + 4f, () => WaitForTheThirdPass(support));
        foreach (var (role, delay) in Departures("third-stacks", (PhysRangedDps, 0.05f, 0.45f, 2.25f), (MeleeDpsA, 0.15f, 0.85f, 3.25f),
                     (MeleeDpsB, 0.15f, 1.05f, 3.65f), (CasterDps, 0.15f, 1.05f, 4.25f), (RegenHealer, 1.05f, 2.55f, 3.95f), (ShieldHealer, 0.45f, 2.95f, 4.75f)))
            seats[(int)role].ThirdStackDelay = delay;
    }

    private void PassThree(PartyRole dps)
    {
        if (NisiOf(dps) is { } held && held == JudgmentOf(dps) && Supports.Any(s => NisiOf(s) == held && JudgmentOf(s) == held))
        {
            WaitOutTheWhirlwind(dps);
            return;
        }
        var job = Job(dps);
        PartyRole? chosen = null;
        PartyRole? Partner()
        {
            if (chosen is { } held && Member(held) is not null && (NisiOf(held) is not { } theirs || theirs == NisiOf(dps) || theirs == JudgmentOf(dps))) return held;
            chosen = PassThreeTarget(dps);
            return chosen;
        }
        if (Partner() is not { } support)
        {
            if (Now < ThirdPassGivesUp) Later(dps, job, 0.3f, () => PassThree(dps));
            else WaitOutTheWhirlwind(dps);
            return;
        }
        var side = dps is MeleeDpsA or MeleeDpsB ? -PassSide : PassSide;
        Walk(dps, Stroll with { Due = ThirdPassGivesUp - 2f }, () => PassTo(dps, Partner, "pass-3", ThirdPassGivesUp, WaitOutTheWhirlwind),
            At(support) + new Vector2(side, 0f));
    }

    private PartyRole? PassThreeTarget(PartyRole dps)
    {
        var mine = NisiOf(dps);
        var needed = JudgmentOf(dps);
        PartyRole? target = mine is { } held
            ? Supports.Where(s => JudgmentOf(s) == held && NisiOf(s) is null).Cast<PartyRole?>().FirstOrDefault()
            : needed is { } need ? Supports.Where(s => NisiOf(s) == need).Cast<PartyRole?>().FirstOrDefault() : null;
        if (target is { } t && NisiOf(t) is { } theirs && theirs != mine && theirs != needed) return null;
        return target;
    }

    private void WaitOutTheWhirlwind(PartyRole dps)
    {
        var job = Job(dps);
        Once(dps, job, () => Now >= SecondWhirlwind, () => Later(dps, job, LeaveForTheThirdStacks(dps) - Now, () => TakeTheThirdStackSpot(dps)));
    }

    private void WaitForTheThirdPass(PartyRole support)
    {
        var job = Job(support);
        seats[(int)support].Wander = 0f;
        switch (support)
        {
            case MainTank:
                break;
            case OffTank when !lightningThreeToOffTank:
                var dragFrom = Sample(support, "drag-from", 100.6f, 103f, 105.8f);
                Once(support, job, () => ThirdPassLanded(support) && Now >= dragFrom, () => DragCruiseChaserBehindGaol(PropellerWind),
                    ThirdPassSupportsLeave, () => DragCruiseChaserBehindGaol(PropellerWind));
                break;
            case OffTank:
                Once(support, job, () => ThirdPassLanded(support) && (shieldDownAt is not null || Now >= ShieldGivesOut),
                    () => Later(support, job, React(support, "wait-sw", 0.6f, 1.6f, 2.8f), WaitSouthWestForTheLightning),
                    ThirdPassSupportsLeave, WaitSouthWestForTheLightning);
                break;
            default:
                Once(support, job, () => ThirdPassLanded(support) && Now >= SecondWhirlwind,
                    () => Later(support, job, LeaveForTheThirdStacks(support) - Now, () => TakeTheThirdStackSpot(support)),
                    ThirdPassSupportsLeave, () => TakeTheThirdStackSpot(support));
                break;
        }
    }

    private void WaitSouthWestForTheLightning()
    {
        var job = Job(OffTank);
        Walk(OffTank, Stroll with { Settle = 0f, Due = ThirdStacks - 1f }, OffTankWaitsForLightning);
        Once(OffTank, job, () => Now >= ThirdStacks, () => Later(OffTank, job, React(OffTank, "drag-late", 0.4f, 0.8f, 1.4f),
            () => DragCruiseChaserBehindGaol(PropellerWind)));
    }

    private bool ThirdPassLanded(PartyRole support)
        => JudgmentOf(support) is { } need && NisiOf(support) == need
           || NisiOf(support) is { } held && Dps.Any(d => JudgmentOf(d) == held && NisiOf(d) == held);

    private PartyRole? Vulnerable() => PerRole.All
        .Where(r => r.IsDps() || r is RegenHealer or ShieldHealer)
        .Where(r => Member(r) is { } m && m.ShowsStatus(StatusId.WaterResistanceDownIIP2) && !m.ShowsStatus(StatusId.CompressedWater)).Cast<PartyRole?>().FirstOrDefault();

    private float LeaveForTheThirdStacks(PartyRole role)
    {
        var leave = MathF.Max(Now, SecondWhirlwind + seats[(int)role].ThirdStackDelay);
        return Holds(role, StatusId.CompressedWater) ? MathF.Min(leave, ThirdStacks - Vector2.Distance(At(role), B) / AiManager.RunSpeed - 3f) : leave;
    }

    private void TakeTheThirdStackSpot(PartyRole role)
    {
        Job(role);
        var due = Stroll with { Due = ThirdStacks - 0.8f };
        if (Holds(role, StatusId.CompressedWater))
        {
            Walk(role, due with { Settle = 0f }, B);
            return;
        }
        if (Holds(role, StatusId.CompressedLightning))
        {
            if (lightningThreeToOffTank) Accompany(role, OffTank, BesideTheWaitingOffTank, ThirdStacks + 0.3f);
            else if (Vulnerable() is { } vulnerable) Accompany(role, vulnerable, BesideTheSecondWater, ThirdStacks + 0.3f);
            return;
        }
        if (Vulnerable() == role)
        {
            if (At(role).Y < -3f) Walk(role, due with { Settle = 0f }, AroundTheThirdWater, SecondWaterAlone);
            else Walk(role, due with { Settle = 0f }, SecondWaterAlone);
            return;
        }
        switch (role)
        {
            case MeleeDpsA:
                Walk(role, due, FirstMeleeThirdWater);
                break;
            case MeleeDpsB:
                Walk(role, due, SecondMeleeThirdWater);
                break;
            default:
                Walk(role, due, role switch { PhysRangedDps => RangedThirdWater, CasterDps => CasterThirdWater, _ => HealerThirdWater });
                break;
        }
    }

    private void DragCruiseChaserBehindGaol(float until)
    {
        var job = Job(OffTank);
        var pillar = GaolSpot();
        var turns = new Queue<Vector2>([Inside(pillar + new Vector2(-3.2f, 4.6f)), Inside(pillar + new Vector2(-3.4f, -5.3f))]);
        var seat = seats[(int)OffTank];
        var settled = false;
        void Hop()
        {
            if (!Still(OffTank, job) || Now >= until || state.Objects.CruiseChaser is not { } cruiseChaser || Member(OffTank) is not { } offTank) return;
            var at = Flat(offTank.Position);
            var cruise = Flat(cruiseChaser.Position);
            var reach = cruiseChaser.HitboxRadius + offTank.HitboxRadius + RobotReach;
            var keepingUp = Vector2.Distance(at, cruise) < reach + 2f;
            if (!seat.Walking(Now) && keepingUp)
            {
                while (turns.Count > 0 && Vector2.Distance(at, turns.Peek()) < 0.5f) turns.Dequeue();
                if (turns.Count > 0)
                {
                    var next = turns.Peek();
                    var hop = Inside(Vector2.Distance(at, next) <= 4.5f ? next : at + Vector2.Normalize(next - at) * Sample(OffTank, "drag-hop", 2.5f, 3.5f, 4.5f, seat.Draws++));
                    Walk(OffTank, Stroll with { Shaped = false, Settle = 0f, Direct = true }, hop);
                }
                else
                {
                    var want = pillar + new Vector2(-1.35f, -0.4f);
                    var spot = (want - cruise).LengthSquared() > 1e-4f ? want + Vector2.Normalize(want - cruise) * reach : at;
                    var drift = Vector2.Distance(cruise, want);
                    settled |= drift <= 0.8f;
                    if (drift > (settled ? CruiseChaserDrift : 0.8f) && Velocity(OffTank).LengthSquared() < 0.01f && spot.Length() <= ArenaEdge
                        && Vector2.Distance(spot, at) > 0.3f)
                        Walk(OffTank, Stroll with { Shaped = false, Settle = 0f, Direct = true }, spot);
                }
            }
            world.Events.Add(Sample(OffTank, "drag-look", 0.6f, 0.9f, 1.4f, seat.Draws++), Hop);
        }
        Hop();
    }

    private List<Obstacle> Standing(PartyRole role, PartyRole except)
        => PerRole.All.Where(other => other != except && Threat(role, other)).Select(other => new Obstacle(At(other), NearestAllowed)).ToList();

    private static Vector2 Inside(Vector2 spot) => spot.Length() > ArenaEdge - 0.8f ? Vector2.Normalize(spot) * (ArenaEdge - 0.8f) : spot;

    private static float SlotOffset(NisiLetter letter) => letter switch
    {
        NisiLetter.Alpha => 3.2f,
        NisiLetter.Gamma => 6.6f,
        NisiLetter.Beta => 10f,
        _ => 13.4f,
    };

    private Vector2 GaolSpot()
        => state.Objects.GelidGaol is { IsActive: true } gaol ? Flat(gaol.Position)
            : state.Objects.Tornadoes.FirstOrDefault() is { IsActive: true } first ? Flat(first.Position)
            : D;

    private Vector2 LineSlot(PartyRole role) => JudgmentOf(role) is { } letter ? GaolSpot() + new Vector2(SlotOffset(letter), 0f) : GaolSpot() + new Vector2(8f, 3f);

    private bool HoldsOwnJudgment(PartyRole role) => JudgmentOf(role) is { } letter && NisiOf(role) == letter;

    private PartyRole? PairPartner(PartyRole role)
        => JudgmentOf(role) is { } letter
            ? PerRole.All.Where(r => r != role && r.IsDps() != role.IsDps() && JudgmentOf(r) == letter).Cast<PartyRole?>().FirstOrDefault()
            : null;

    private static bool ComesFromTheNorth(PartyRole role) => role is MainTank or OffTank or MeleeDpsA or MeleeDpsB;

    private void PlanTheLineup()
    {
        var latest = SettlingTornadoes[2].Until - 2.6f - ThirdStacks;
        foreach (var (role, delay) in Departures("lineup", r => r == MainTank ? float.MaxValue : latest, (MainTank, 1.05f, 2.15f, 5.15f), (RegenHealer, 1.05f, 2.15f, 5f),
                     (ShieldHealer, 1.15f, 2.15f, 4.25f), (MeleeDpsA, 0.95f, 1.45f, 2.65f), (MeleeDpsB, 0.8f, 1.7f, 2.95f), (PhysRangedDps, 0.95f, 1.65f, 3.75f),
                     (CasterDps, 1.05f, 1.85f, 3.45f)))
            When(ThirdStacks + delay, () => LineUp(role));
        When(SettlingTornadoes[2].Until - 2.4f, () => KeepClearOfTornado(2));
        Watch(OffTank, () => Casting(state.Objects.CruiseChaser, ActionId.PropellerWind) || Now >= PropellerWind + 0.4f,
            () => When(Now + React(OffTank, "propeller", 0.3f, 0.8f, 1.8f), () => LineUp(OffTank)));
    }

    private void LineUp(PartyRole role)
    {
        var job = Job(role);
        var slot = LineSlot(role);
        var staging = slot + new Vector2(0f, ComesFromTheNorth(role) ? -LineupStaging : LineupStaging);
        var approach = role == MainTank ? new[] { new Vector2(slot.X, -9.7f), staging } : new[] { staging };
        var clear = Stroll with
        {
            ClearOf = TornadoReach(2),
            Hops = role == MainTank ? 1 : Pick(role, "lineup-hops", 1, 2),
            PauseAtStops = role == MainTank,
        };
        if (HoldsOwnJudgment(role))
        {
            Walk(role, clear with
            {
                Settle = 0f,
                Due = role == OffTank ? PropellerWindLands - 0.6f : PropellerWind - 1f,
                PauseAtStops = role != OffTank,
                Pause = role == MainTank ? clear.Pause : (2f, 3.5f, 6f),
            }, [.. approach, slot]);
            return;
        }
        Walk(role, clear with { Settle = 0.3f, Due = role == OffTank ? PropellerWindLands - 1.2f : float.MaxValue }, approach);
        Once(role, job, () => PairPartner(role) is { } partner && (Vector2.Distance(At(partner), slot) < 1.3f && Velocity(partner).Length() < 0.6f
                                                                   || SoonestExpiry(role, partner) - Now < 2.5f),
            () => Later(role, job, MathF.Max(0f, MathF.Min(React(role, "join", 0.4f, 1.0f, 2.0f),
                    MathF.Min(SoonestExpiry(role, PairPartner(role)) - 2f, LineupGivesUp - 1.2f) - Now)),
                () => StandOnThePair(role)), LineupGivesUp - 1.2f, () => StandOnThePair(role));
    }

    private void StandOnThePair(PartyRole role)
    {
        Job(role);
        var seat = seats[(int)role];
        seat.Wander = 0f;
        seat.Chasing = new Chase(() => PairPartner(role), () => PairPartner(role) is { } partner && Member(partner) is { } member ? Ahead(role, partner, Flat(member.Position)) : null,
            Sample(role, "on-the-pair", 0.2f, 0.4f, 0.7f), () => Now >= StatusesClear - 0.05f, () => { }, StatusesClear, Stroll with { Shaped = false, Settle = 0f });
    }

    private void PlanAfterGavel()
    {
        var spots = new Dictionary<PartyRole, Vector2>
        {
            [MainTank] = new(-15.1f, -4.9f),
            [OffTank] = new(-15.4f, -3.9f),
            [RegenHealer] = new(-9.1f, 7.2f),
            [ShieldHealer] = new(-7.2f, -1.7f),
            [MeleeDpsA] = new(-7.2f, 3.1f),
            [MeleeDpsB] = new(-6.3f, 0.2f),
            [PhysRangedDps] = new(-3.5f, 1.5f),
            [CasterDps] = new(-8.4f, 5.4f),
        };
        foreach (var (role, delay) in Departures("after-gavel", (MainTank, 0.32f, 1.12f, 2.22f), (OffTank, 0.32f, 1.12f, 2.12f), (MeleeDpsA, 0.42f, 2.22f, 5.12f),
                     (MeleeDpsB, 0.42f, 2.22f, 5.12f), (RegenHealer, 1.02f, 2.92f, 4.82f), (ShieldHealer, 1.02f, 3.12f, 5.52f), (CasterDps, 1.02f, 2.92f, 6.82f),
                     (PhysRangedDps, 1.12f, 3.32f, 5.82f)))
            When(StatusesClear + delay, () =>
            {
                Job(role);
                Walk(role, Stroll with { Settle = role.IsTank() ? 0.3f : (float?)null, Due = role.IsTank() ? 146f : float.MaxValue }, spots[role]);
            });
    }

    private void PlanSuperJump()
    {
        When(Sample(PhysRangedDps, "super-jump-bait", 147.6f, 149.1f, 151.5f), () =>
        {
            Job(PhysRangedDps);
            Walk(PhysRangedDps, Stroll with { Settle = 0f, Due = SuperJumpCast - 0.5f }, PhysRangedSuperJumpBait);
        });
        var wall = new Dictionary<PartyRole, Vector2>
        {
            [RegenHealer] = new(-12.8f, 3.7f),
            [ShieldHealer] = new(-15.5f, 4.1f),
            [MeleeDpsA] = new(-12.8f, 1.0f),
            [MeleeDpsB] = new(-11.2f, 3.1f),
            [CasterDps] = new(-14.6f, 3.4f),
        };
        foreach (var (role, delay) in Departures("super-jump", wall.Keys.Select(r => (r, 0.3f, 1.0f, 2.2f)).ToArray()))
            When(SuperJumpCast + delay, () =>
            {
                Job(role);
                Walk(role, Stroll with { Due = SuperJumpLands - 0.5f }, wall[role]);
            });
        When(SuperJumpLands + React(PhysRangedDps, "out-of-the-ray", 0.45f, 0.55f, 0.9f), () =>
        {
            Job(PhysRangedDps);
            Walk(PhysRangedDps, Stroll with { Shaped = false, Settle = 0f, Due = 161f }, PhysRangedOutOfTheRay);
        });
        var back = new Dictionary<PartyRole, Vector2>
        {
            [PhysRangedDps] = new(-3.5f, 1.5f),
            [RegenHealer] = new(-9.1f, 7.2f),
            [ShieldHealer] = new(-7.2f, -1.7f),
            [MeleeDpsA] = new(-7.2f, 3.1f),
            [MeleeDpsB] = new(-6.3f, 0.2f),
            [CasterDps] = new(-8.4f, 5.4f),
        };
        foreach (var (role, delay) in Departures("after-the-ray", back.Keys.Select(r => (r, 0.3f, 0.8f, 1.6f)).ToArray()))
            When(LastRayHit + delay, () =>
            {
                Job(role);
                Walk(role, Stroll, back[role]);
            });
    }

    private void PassTo(PartyRole role, Func<PartyRole?> partner, string what, float until, Action<PartyRole> then)
    {
        var job = Job(role);
        var seat = seats[(int)role];
        seat.Wander = 0f;
        var cameFrom = At(role);
        var expires = SoonestExpiry(role, partner());
        var deadline = what == "pass-2" && expires < float.MaxValue ? MathF.Max(until, MathF.Min(expires - 0.25f, LastSecondPass)) : until;
        seat.Chasing = new Chase(partner, () => partner() is { } p && Member(p) is { } member ? Ahead(role, p, Flat(member.Position)) : null,
            Sample(role, what + "-stop", 0.16f, 0.31f, 0.5f),
            () => partner() is { } p && Passed(role, p),
            () => Later(role, job, MathF.Max(0f, MathF.Min(Sample(role, what + "-touch", 0.45f, 0.8f, 1.9f), SoonestExpiry(role, partner()) - Now - ExpiryMargin - 0.2f)),
                () => StepBackOut(role, partner(), cameFrom, () => then(role))),
            deadline, Stroll with { Shaped = false, Settle = 0f, Due = MathF.Min(deadline, expires) - 1.5f });
    }

    private Vector2 Ahead(PartyRole chaser, PartyRole partner, Vector2 at)
    {
        var velocity = Velocity(partner);
        if (velocity.LengthSquared() < 1f) return at;
        var ahead = at + velocity * MathF.Min(ChaseLead, Vector2.Distance(At(chaser), at) / AiManager.RunSpeed);
        return ahead.Length() > ArenaEdge - 0.3f ? Vector2.Normalize(ahead) * (ArenaEdge - 0.3f) : ahead;
    }

    private void StepBackOut(PartyRole role, PartyRole? partner, Vector2 cameFrom, Action then)
    {
        if (partner is not { } other || Member(other) is null || Vector2.Distance(At(role), At(other)) > 1.6f)
        {
            then();
            return;
        }
        var from = At(other);
        var offset = At(role) - from;
        var back = cameFrom - from;
        var way = offset.Length() > 0.15f ? Vector2.Normalize(offset) : back.LengthSquared() > 1e-4f ? Vector2.Normalize(back) : Vector2.UnitX;
        var reach = Sample(role, "step-back", 2.1f, 2.5f, 3.2f);
        var inside = new[] { 0f, 35f, -35f, 70f, -70f, 105f, -105f, 140f, -140f, 180f }.Select(turn => from + Rotate(way, turn) * reach)
            .Where(spot => spot.Length() <= ArenaEdge - 0.5f).ToList();
        if (inside.Count == 0)
        {
            then();
            return;
        }
        var to = inside.Where(spot => SafeToStand(role, spot, Clearance)).DefaultIfEmpty(inside[0]).First();
        Walk(role, Stroll with { Shaped = false, Settle = 0.3f, Aside = true }, then, to);
    }

    private void Accompany(PartyRole role, PartyRole receiver, Vector2 beside, float until)
    {
        Job(role);
        var seat = seats[(int)role];
        seat.Wander = 0f;
        var joined = false;
        var following = false;
        seat.Chasing = new Chase(() => null, () =>
        {
            if (Member(receiver) is not { } member) return null;
            var side = Flat(member.Position) + beside;
            var gap = Vector2.Distance(At(role), side);
            following = joined && gap > OffTheSpot && (following || gap > LeftBehind);
            joined |= gap <= OffTheSpot;
            return !Moving(receiver) && Now - lastMotion[(int)receiver].At >= Settled || Now >= until - 2.8f || following ? side : null;
        }, 0f, () => Now >= until, () => { }, until, Stroll with { Shaped = false, Settle = 0f });
    }

    private void WaitToBePassed(PartyRole support, Func<bool> passed, float until, (float Low, float Middle, float High) delay, Action<PartyRole> then,
        float latest = float.MaxValue)
    {
        var job = Job(support);
        seats[(int)support].Wander = 0f;
        Once(support, job, passed, () => Later(support, job, MathF.Min(React(support, $"passed-{until}", delay.Low, delay.Middle, delay.High), latest - Now),
            () => then(support)), MathF.Min(until, latest), () => then(support));
    }

    private bool Passed(PartyRole a, PartyRole b) => NisiOf(a) is { } letter && NisiOf(b) == letter;

    private bool CopyRunsOut(PartyRole support, PartyRole dps) => Now >= SecondPassSupportsLeave && SoonestExpiry(support, dps) - Now < 0.3f;

    private float SoonestExpiry(PartyRole role, PartyRole? partner)
        => new[] { (PartyRole?)role, partner }.Where(r => r is { } held && NisiOf(held) is not null).Select(r => nisiSince[(int)r!.Value] + NisiLasts)
            .DefaultIfEmpty(float.MaxValue).Min();

    private void KeepClearOfTornado(int k)
    {
        if (state.Objects.Tornadoes.Count <= k)
        {
            if (Now < SettlingTornadoes[k].Until - 1f) world.Events.Add(0.1f, () => KeepClearOfTornado(k));
            return;
        }
        foreach (var role in PerRole.All)
            if (OutsideTheTornado(role, k) is not null || LateOutOfTheTornado(role, k))
                When(Now + React(role, "tornado-step", 0.05f, 0.15f, 0.35f), () =>
                {
                    var seat = seats[(int)role];
                    if (OutsideTheTornado(role, k) is { } clear) Head(role, Stroll with { Shaped = false }, seat.Arrived, [clear]);
                    else if (LateOutOfTheTornado(role, k)) Head(role, seat.Gait with { ClearOf = TornadoReach(k), Shaped = false }, seat.Arrived, seat.Stops);
                });
    }

    private Vector2? OutsideTheTornado(PartyRole role, int k)
    {
        var seat = seats[(int)role];
        if (Member(role) is not { } member || seat.Chasing is not null) return null;
        var to = seat.HasWalk(Now) && seat.Stops.Length > 0 ? seat.Stops[^1] : Flat(member.Position);
        var clear = ClearOfTornado(k, to);
        return Vector2.Distance(clear, to) > 0.1f ? clear : null;
    }

    private bool LateOutOfTheTornado(PartyRole role, int k)
    {
        var seat = seats[(int)role];
        var (tornado, by, reach) = TornadoReach(k);
        if (seat.Chasing is not null || !seat.HasWalk(Now) || seat.Stops.Length == 0 || Vector2.Distance(seat.Stops[^1], tornado) < reach) return false;
        return Vector2.Distance(seat.Walking(Now) ? CourseAt(seat.Course, by) : At(role), tornado) < reach - 0.05f;
    }

    private Vector2 TornadoSpot(int k) => state.Objects.Tornadoes.ElementAtOrDefault(k) is { } rage ? Flat(rage.Position) : SettlingTornadoes[k].Spot;

    private (Vector2 Where, float By, float Reach) TornadoReach(int k) => (TornadoSpot(k), SettlingTornadoes[k].Until - 0.4f, TornadoClearance);

    private Vector2 ClearOfTornado(int k, Vector2 spot)
    {
        var tornado = TornadoSpot(k);
        var away = spot - tornado;
        if (away.Length() >= TornadoClearance) return spot;
        var outward = away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : tornado.LengthSquared() > 1e-4f ? -Vector2.Normalize(tornado) : Vector2.UnitX;
        return Enumerable.Range(0, 37).Select(n => tornado + Rotate(outward, (n + 1) / 2 * 10f * (n % 2 == 0 ? 1f : -1f)) * TornadoClearance)
            .FirstOrDefault(clear => clear.Length() <= ArenaEdge, spot);
    }

    private List<Vector2> Blasters()
        => world.Children.OfType<SimEnemy>().Where(e => e.IsActive && e.IsCasting && e.CastActionId == ActionId.HawkBlasterP2 && e.CastTargetLocation is not null)
            .Select(e => Flat(e.CastTargetLocation!.Value)).ToList();

    private (Vector2 Where, float By, float Reach)? BlasterUnder(PartyRole role)
    {
        var at = At(role);
        var under = Blasters().Where(b => Vector2.Distance(b, at) < BlasterReach).ToList();
        return under.Count == 0 ? null : (under.MinBy(b => Vector2.Distance(b, at)), BlastersResolve - 0.4f, BlasterReach);
    }

    private bool InABlaster(Vector2 spot)
        => Now is >= BlastersResolve - 2f and <= BlastersResolve + LateJudged && Blasters().Any(b => Vector2.Distance(b, spot) < BlasterReach);

    private bool NearASettlingTornado(Vector2 spot)
        => Enumerable.Range(0, SettlingTornadoes.Length).Any(k => Now >= SettlingTornadoes[k].From - 0.5f && Now <= SettlingTornadoes[k].Until + LateJudged
                                                                  && Vector2.Distance(spot, TornadoSpot(k)) < TornadoClearance);

    private void StepOutOfPuddles()
    {
        var puddles = Puddles();
        if (puddles.Count == 0) return;
        foreach (var role in PerRole.All)
        {
            if (Member(role) is not { } member) continue;
            var at = Flat(member.Position);
            if (OutOf(puddles, at) == at) continue;
            var seat = seats[(int)role];
            var stops = seat.HasWalk(Now) ? seat.Stops : [];
            if (stops.Length > 0 && ClearOf(puddles, OutOf(puddles, at), stops)) continue;
            Head(role, Stroll with { Shaped = false }, seat.Arrived, stops.Length > 0 ? stops : [at]);
        }
    }

    private static bool ClearOf(IReadOnlyList<Obstacle> puddles, Vector2 from, IReadOnlyList<Vector2> stops)
    {
        foreach (var stop in stops)
        {
            if (OutOf(puddles, stop) != stop || !SegmentClear(from, stop, puddles)) return false;
            from = stop;
        }
        return true;
    }

    private void Look()
    {
        var now = Now;
        See(now);
        foreach (var watch in watches.ToList())
        {
            if (!watches.Contains(watch)) continue;
            if (watch.Job >= 0 && !Still(watch.Role, watch.Job))
            {
                watches.Remove(watch);
                continue;
            }
            if (watch.Ready())
            {
                watches.Remove(watch);
                watch.Then();
            }
            else if (now >= watch.Until)
            {
                watches.Remove(watch);
                watch.Otherwise?.Invoke();
            }
        }
        foreach (var role in PerRole.All) Steer(role, now);
        if (now < LastLook) world.Events.Add(LookEvery, Look);
    }

    private void See(float now)
    {
        foreach (var role in PerRole.All)
        {
            var seen = sightings[(int)role];
            if (Member(role) is not { } member)
            {
                seen.Clear();
                continue;
            }
            var at = Flat(member.Position);
            if (seen.Count > 0 && seen.Last() is var (was, where) && now - was < 0.15f && Vector2.Distance(where, at) > LaggedStep) jerkySeen[(int)role] = now;
            seen.Enqueue((now, at));
            while (seen.Count > 4) seen.Dequeue();
            if (Stepping(seen))
            {
                if (now - steppedAt[(int)role] <= SteppingAgain) jerkySeen[(int)role] = now;
                steppedAt[(int)role] = now;
            }
            if (SeenVelocity(role) is var velocity && velocity.LengthSquared() >= 1f) lastMotion[(int)role] = (velocity, now);
        }
        if (now is > 63f and < 77f) SeeTheMines(now);
        if (now is > 92.5f and < 101f) SeeTheShield(now);
        Meet(now);
    }

    private void Meet(float now)
    {
        foreach (var role in PerRole.All)
        {
            var seat = seats[(int)role];
            foreach (var other in PerRole.All)
            {
                if (other == role) continue;
                if (Member(role) is null || Member(other) is null)
                {
                    seat.Met[(int)other] = null;
                    continue;
                }
                var gap = Vector2.Distance(At(role), At(other));
                var touching = gap < ContactMargin + 0.2f;
                if (seat.Met[(int)other] is not { } met)
                {
                    if (gap >= ComingWithin || !touching && !MovingCloser(other, At(role))) continue;
                    met = seat.Met[(int)other] = new Encounter { Draw = seat.Meetings++ };
                }
                else if (gap >= GoneBeyond)
                {
                    seat.Met[(int)other] = null;
                    continue;
                }
                if (Welcome(role, other))
                {
                    met.Expected = met.Judged = met.Noticed = true;
                    met.NoticedAt = now;
                }
                if (met.Judged || !Threat(role, other)) continue;
                met.Judged = true;
                met.Noticed = Dice(role, "notice", met.Draw) < (touching && !Moving(other) ? TouchNoticeChance : NoticeChance);
                met.NoticedAt = now + React(role, "notice-react", 0.7f, 1.4f, 2.9f, met.Draw);
            }
        }
    }

    private bool Noticed(PartyRole role, PartyRole other) => seats[(int)role].Met[(int)other] is { Noticed: true } met && Now >= met.NoticedAt;

    private bool Expected(PartyRole role, PartyRole other) => seats[(int)role].Met[(int)other] is { Expected: true };

    private void SeeTheMines(float now)
    {
        var casting = world.Children.OfType<SimEnemy>()
            .Where(e => e.IsActive && e.IsCasting && e.CastActionId == ActionId.HiddenMinefield && e.CastTargetLocation is not null).ToList();
        foreach (var enemy in casting)
        {
            var spot = Flat(enemy.CastTargetLocation!.Value);
            if (mineSpots.Any(m => Vector2.Distance(m, spot) < 0.5f)) continue;
            mineSpots.Add(spot);
            minesPopped.Add(false);
        }
        if (mineSpots.Count > 0 && casting.Count == 0) minesArmedAt ??= now;
        if (minesArmedAt is null) return;
        for (var k = 0; k < mineSpots.Count; k++)
            if (!minesPopped[k] && PerRole.All.Any(r => Member(r) is { } m && Vector2.Distance(Flat(m.Position), mineSpots[k]) <= MineTrigger)) minesPopped[k] = true;
    }

    private bool MinesPopped => minesArmedAt is not null && minesPopped.Count > 0 && minesPopped.All(p => p);

    private void SeeTheShield(float now)
    {
        var shield = state.Objects.PlasmaShield;
        var up = shield is { IsActive: true, Dead: false } && shield.HpFraction > 0.001f;
        if (up) shieldSeenUp = true;
        else if (shieldSeenUp) shieldDownAt ??= now;
    }

    private void Steer(PartyRole role, float now)
    {
        if (Member(role) is not { } member) return;
        var seat = seats[(int)role];
        var at = Flat(member.Position);
        if (seat.Chasing is { } chase)
        {
            Pursue(role, member, seat, chase, at, now);
            return;
        }
        if (seat.Walking(now))
        {
            if (seat.Walk == seat.DodgeWalk)
            {
                if (Conflict(role, seat.Course, now, seat.Urgent, seat.DodgingFrom) is { } near) GiveWay(role, member, seat, near, now, seat.Gait, seat.Arrived, seat.Stops);
                return;
            }
            if (Paused(seat, now) && Dodge(role, seat, at, now, true)) return;
            if (Conflict(role, seat.Course, now, seat.Urgent, seat.Ignoring, Pressed(seat.Gait, seat.Urgent, at, seat.Stops, now)) is { } conflict)
                GiveWay(role, member, seat, conflict, now, seat.Gait, seat.Arrived, seat.Stops);
            return;
        }
        if (!float.IsNaN(seat.RetryAt))
        {
            if (Dodge(role, seat, at, now, true) || Unstick(role, seat, at, true)) return;
            if (seat.Dodged is { } dodged && seat.Halts >= MostDodgeHalts)
            {
                seat.Dodged = null;
                seat.Halts = 0;
                Head(role, dodged.Gait, dodged.Arrived, dodged.Stops);
                return;
            }
            if (now < seat.RetryAt || seat.HaltedFor is { } yielded && Member(yielded) is not null && !MovingAway(yielded, at)
                && now < seat.RetryAt + (Pressed(seat.Gait, seat.Urgent, at, seat.Stops, now) || !Wary(yielded) ? PressedYield : StandAfterRetry)) return;
            seat.HaltedFor = null;
            Head(role, seat.Gait with { Shaped = false }, seat.Arrived, seat.Stops);
            return;
        }
        if (Dodge(role, seat, at, now, false) || Arriving(seat, at, now) || Unstick(role, seat, at, false) || Strayed(role, seat, at, now)) return;
        Fidget(role, member, seat, at, now);
    }

    private static bool Arriving(Seat seat, Vector2 at, float now)
        => seat.Course.Count > 0 && now - seat.Course[^1].Eta < LookEvery && Vector2.Distance(at, seat.Course[^1].At) > OffTheSpot;

    private static bool Paused(Seat seat, float now) => Vector2.Distance(CourseAt(seat.Course, now), CourseAt(seat.Course, now + LookEvery)) < 1e-3f;

    private bool Strayed(PartyRole role, Seat seat, Vector2 at, float now)
    {
        if (seat.Walk == 0 || HeldStill(role, now) || Moving(role) || Vector2.Distance(at, seat.Anchor) <= seat.Wander + OffTheSpot) return false;
        Head(role, Stroll with { Shaped = false, Settle = seat.Wander }, null, [seat.Anchor]);
        return true;
    }

    private void GiveWay(PartyRole role, SimCharacter member, Seat seat, (PartyRole Who, float Gap) conflict, float now, Gait gait, Action? arrived, Vector2[] stops)
    {
        if (Moving(conflict.Who))
        {
            var yields = YieldsTo(role, conflict.Who);
            if ((!yields || seat.Halts >= MostYields || Leaving(gait, Flat(member.Position), now))
                && (conflict.Gap >= PassingGap || !Wary(conflict.Who) && conflict.Gap >= UrgentNearest)) return;
            var at = Flat(member.Position);
            if (seat.Chasing is null && OnThePathOf(conflict.Who, at) && Dodge(role, seat, at, now, true, conflict.Who)) return;
            if (!yields && seat.Chasing is null && now - seat.SteeredAt > SteerAgain)
            {
                seat.SteeredAt = now;
                Head(role, gait with { Shaped = false }, arrived, stops);
                return;
            }
            Halt(member, seat);
            seat.Stops = stops;
            seat.Gait = gait;
            seat.Arrived = arrived;
            seat.HaltedFor = yields ? conflict.Who : null;
            seat.Halts++;
            seat.RetryAt = now + Backoff(seat, Pressed(gait, seat.Urgent, at, stops, now) || !Wary(conflict.Who))
                * (seat.Urgent ? React(role, "give-way", 0.1f, 0.15f, 0.25f, seat.Draws++) : React(role, "give-way", 0.25f, 0.4f, 0.6f, seat.Draws++));
            return;
        }
        Head(role, gait with { Shaped = false }, arrived, stops);
    }

    private bool OnThePathOf(PartyRole other, Vector2 at, bool early = false)
    {
        var velocity = Velocity(other);
        if (velocity.LengthSquared() < 1f) return false;
        var there = At(other);
        var wary = Wary(other);
        var within = wary ? WaryDodgeWithin : DodgeWithin;
        var ahead = early && velocity.LengthSquared() >= WalkingPace * WalkingPace ? CollisionAhead : wary ? Foresight : DodgeAhead;
        if (Vector2.Distance(at, there) > MathF.Max(DodgeFrom, velocity.Length() * ahead + within) || Vector2.Dot(velocity, at - there) <= 0f) return false;
        for (var t = 0f; t <= ahead + 1e-3f; t += LookEvery)
            if (Vector2.Distance(at, there + velocity * t) < within) return true;
        return false;
    }

    private bool Bearing(PartyRole other, Vector2 spot)
    {
        var velocity = Velocity(other);
        if (velocity.LengthSquared() < 1f) return false;
        var there = At(other);
        for (var t = 0f; t <= Foresight + 1e-3f; t += LookEvery)
            if (Vector2.Distance(spot, there + velocity * t) < DodgedClear) return true;
        return false;
    }

    private static bool Pressed(Gait gait, bool urgent, Vector2 at, Vector2[] stops, float now)
        => urgent || gait.ClearOf is { } hazard && now <= hazard.By + LateJudged && Vector2.Distance(at, hazard.Where) < hazard.Reach
           || stops.Length > 0 && gait.Due - now - Vector2.Distance(at, stops[^1]) / AiManager.RunSpeed < StandAfterRetry + 0.4f;

    private static bool Leaving(Gait gait, Vector2 at, float now)
        => gait.ClearOf is { } hazard && now <= hazard.By + LateJudged && Vector2.Distance(at, hazard.Where) < hazard.Reach;

    private static float Backoff(Seat seat, bool pressed) => pressed ? 1f : 1f + 0.5f * Math.Clamp(seat.Halts - 1, 0, 4);

    private bool MovingAway(PartyRole other, Vector2 from) => Velocity(other) is var velocity && velocity.LengthSquared() >= 1f && Vector2.Dot(velocity, At(other) - from) > 0f;

    private bool MovingCloser(PartyRole other, Vector2 to) => Velocity(other) is var velocity && velocity.LengthSquared() >= 1f && Vector2.Dot(velocity, to - At(other)) > 0f;

    private bool Lettered(PartyRole me, PartyRole other) => nisi[(int)me] is not null || (nisi[(int)other] ?? receiving[(int)other]) is not null;

    private bool Wary(PartyRole other) => Now - jerkySeen[(int)other] < LagRemembered;

    private bool Moving(PartyRole role) => Velocity(role).LengthSquared() >= 1f;

    private bool Escaping(PartyRole role) => At(role) is var at && (NearASettlingTornado(at) || InABlaster(at));

    private bool YieldsTo(PartyRole me, PartyRole other)
    {
        var mine = Velocity(me);
        var theirs = Velocity(other);
        if (mine.LengthSquared() < 1f) return true;
        if (theirs.LengthSquared() < 1f) return false;
        if (Escaping(me) != Escaping(other)) return Escaping(other);
        var toOther = At(other) - At(me);
        if (toOther.LengthSquared() < 1e-4f) return me > other;
        var way = Vector2.Normalize(toOther);
        var side = Cross(Vector2.Normalize(mine), way);
        var theirSide = Cross(Vector2.Normalize(theirs), -way);
        return MathF.Abs(side - theirSide) < 0.05f ? me > other : side > theirSide;
    }

    private void Pursue(PartyRole role, SimCharacter member, Seat seat, Chase chase, Vector2 at, float now)
    {
        if (chase.Done())
        {
            seat.Chasing = null;
            seat.RetryAt = float.NaN;
            chase.Then();
            return;
        }
        if (now >= chase.Until)
        {
            seat.Chasing = null;
            seat.RetryAt = float.NaN;
            chase.Then();
            return;
        }
        if (chase.Target() is not { } seen)
        {
            if (seat.Walking(now)) Halt(member, seat);
            return;
        }
        var target = seen.Length() > ArenaEdge - 0.3f ? Vector2.Normalize(seen) * (ArenaEdge - 0.3f) : seen;
        var away = at - target;
        var stop = target + (away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : Vector2.UnitX) * chase.StopShort;
        if (Vector2.Distance(at, stop) < 0.1f) return;
        var drifted = chase.AimedAt is not { } aimed || Vector2.Distance(aimed, target) > ReAim;
        if (seat.Walking(now) && (!drifted || now < chase.NextAim))
        {
            if (Conflict(role, seat.Course, now, seat.Urgent) is { } conflict) GiveWay(role, member, seat, conflict, now, chase.Gait, null, [stop]);
            return;
        }
        if (now < chase.NextAim) return;
        chase.AimedAt = target;
        chase.NextAim = now + React(role, "aim", 0.2f, 0.3f, 0.45f, seat.Draws++);
        Head(role, chase.Gait, null, [stop]);
    }

    private bool Dodge(PartyRole role, Seat seat, Vector2 at, float now, bool resume, PartyRole? from = null)
    {
        if (dodgingFrom is not null || HeldStill(role, now) || HeldStill(role, now + Settling) || !resume && Vector2.Distance(at, seat.Anchor) > 0.6f + seat.Wander) return false;
        foreach (var other in from is { } only ? new[] { only } : PerRole.All)
        {
            var surprised = from is null && !Expected(role, other);
            if (!Threat(role, other) || from is null && !Noticed(role, other)
                || !OnThePathOf(other, at, surprised || !resume && Lettered(role, other) && !HeldStill(role, now + EarlyDodgeReturns))) continue;
            if (!surprised && seat.DangerFrom != other)
            {
                seat.DangerFrom = other;
                seat.DangerSince = now;
                seat.DangerReact = React(role, "dodge-react", 0.2f, 0.25f, 0.4f, seat.Draws++);
            }
            if (!surprised && now - seat.DangerSince < seat.DangerReact) return false;
            var step = surprised ? Sample(role, "step-aside", 1.1f, 1.8f, 2.7f, seat.Met[(int)other]!.Draw) : DodgeStep;
            var velocity = Velocity(other);
            var ahead = Vector2.Normalize(velocity);
            var across = new Vector2(-ahead.Y, ahead.X);
            if (Vector2.Dot(across, at - At(other)) < 0f) across = -across;
            foreach (var to in new[] { at + across * step, at + Vector2.Normalize(across + ahead) * step, at + Vector2.Normalize(across - ahead) * step })
            {
                if (to.Length() > ArenaEdge - 0.5f || !SafeToStand(role, to, FidgetClearance - 0.6f) || !SegmentClear(at, to, Standing(role, other))) continue;
                var after = seat.Dodged ?? (resume
                    ? new Resume(seat.Gait with { Shaped = false }, seat.Arrived, seat.Stops.Length > 0 ? seat.Stops : [seat.Anchor])
                    : new Resume(Stroll with { Shaped = false, Settle = seat.Wander }, null, [seat.Anchor]));
                var mine = after with { };
                var job = seat.Job;
                dodgingFrom = other;
                Head(role, Stroll with { Shaped = false, Settle = 0f, Direct = true }, () => ComeBack(role, seat, job, mine, other), [to]);
                dodgingFrom = null;
                seat.Dodged = mine;
                seat.DodgeWalk = seat.Walk;
                seat.DodgingFrom = other;
                seat.DangerFrom = null;
                return true;
            }
            return false;
        }
        if (from is null) seat.DangerFrom = null;
        return false;
    }

    private void ComeBack(PartyRole role, Seat seat, int job, Resume after, PartyRole dodged)
    {
        var giveUp = Now + 3f;
        void Return()
        {
            if (!Still(role, job) || !ReferenceEquals(seat.Dodged, after) || seat.Chasing is not null) return;
            if (after.Gait.Aside && !seat.HasWalk(Now) && SafeToStand(role, At(role), Clearance))
            {
                seat.Dodged = null;
                after.Arrived?.Invoke();
                return;
            }
            var toward = after.Stops.Length > 0 ? after.Stops[0] : At(role);
            if (seat.HasWalk(Now) || Now < giveUp && Member(dodged) is not null
                && (Bearing(dodged, toward) || Bearing(dodged, At(role)) || Vector2.Distance(At(dodged), toward) < Clearance))
            {
                world.Events.Add(0.3f, Return);
                return;
            }
            seat.Dodged = null;
            Head(role, after.Gait, after.Arrived, after.Stops);
        }
        Later(role, job, React(role, "dodged", 0.6f, 1.0f, 1.6f, seat.Draws++), Return);
    }

    private bool Unstick(PartyRole role, Seat seat, Vector2 at, bool resume)
    {
        foreach (var other in PerRole.All)
        {
            if (!Threat(role, other) || Moving(other) || !Noticed(role, other)) continue;
            var there = At(other);
            var away = at - there;
            var gap = away.Length();
            if (gap >= ContactMargin + 0.2f) continue;
            var way = gap > 0.05f ? away / gap : Vector2.UnitX;
            foreach (var turn in new[] { 0f, 40f, -40f, 80f, -80f, 120f, -120f })
            {
                var to = there + Rotate(way, turn) * Sample(role, "unstick", 2.1f, 2.4f, 2.9f);
                if (!SafeToStand(role, to, Clearance)) continue;
                var mine = (seat.Dodged ?? (resume
                    ? new Resume(seat.Gait with { Shaped = false }, seat.Arrived, seat.Stops.Length > 0 ? seat.Stops : [seat.Anchor])
                    : new Resume(Stroll with { Shaped = false, Settle = seat.Wander }, null, [seat.Anchor]))) with { };
                var job = seat.Job;
                Head(role, Stroll with { Shaped = false, Settle = 0.3f }, () => ComeBack(role, seat, job, mine, other), [to]);
                seat.Dodged = mine;
                return true;
            }
            return false;
        }
        return false;
    }

    private void Fidget(PartyRole role, SimCharacter member, Seat seat, Vector2 at, float now)
    {
        if (now < seat.NextFidget) return;
        seat.NextFidget = now + FidgetEvery(role) * Sample(role, "fidget-gap", 0.5f, 1f, 1.8f, seat.Draws++);
        if (seat.Wander <= 0f || HeldStill(role, now) || Vector2.Distance(at, seat.Anchor) > seat.Wander + 0.5f) return;
        var angle = MathF.Tau * Dice(role, "fidget-way", seat.Draws);
        var reach = seat.Wander * MathF.Sqrt(Dice(role, "fidget-reach", seat.Draws++));
        var to = seat.Anchor + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
        if (Vector2.Distance(to, at) < 0.25f || !SafeToStand(role, to, FidgetClearance) || !SegmentClear(at, to, Obstacles(role, Puddles()))) return;
        seat.Walk++;
        seat.Urgent = false;
        seat.Course.Clear();
        seat.Course.Add((at, now));
        seat.Course.Add((to, now + Vector2.Distance(at, to) / AiManager.RunSpeed));
        member.MoveTo(new Vector3(to.X, 0f, to.Y), speed: AiManager.RunSpeed);
    }

    private static float FidgetEvery(PartyRole role) => role switch
    {
        MainTank => 4f,
        OffTank => 4.5f,
        RegenHealer => 25f,
        ShieldHealer => 8f,
        MeleeDpsA => 3f,
        MeleeDpsB => 9f,
        PhysRangedDps => 7f,
        _ => 5f,
    };

    private static float Wanders(PartyRole role) => role switch
    {
        MainTank or OffTank => 0.8f,
        RegenHealer or ShieldHealer => 0.5f,
        MeleeDpsA => 0.9f,
        _ => 0.7f,
    };

    private bool HeldStill(PartyRole role, float now)
        => HoldStill.Any(w => now >= w.From && now <= w.Until && w.Roles.Contains(role) && !(w.UntilPassed && PassLanded(role, now)));

    private bool PassLanded(PartyRole support, float now) => now < 90f ? Passed(support, DpsPartnerOf(support)) : ThirdPassLanded(support);

    private bool SafeToStand(PartyRole role, Vector2 spot, float clearance)
    {
        if (spot.Length() > ArenaEdge || NearASettlingTornado(spot) || InTheFlame(spot) || InABlaster(spot) || OutOf(Puddles(), spot) != spot) return false;
        foreach (var other in PerRole.All)
        {
            if (!Threat(role, other)) continue;
            var there = At(other);
            if (Vector2.Distance(spot, there) < clearance) return false;
            var velocity = Velocity(other);
            if (velocity.LengthSquared() < 0.25f) continue;
            for (var t = 0.3f; t <= 0.9f + 1e-3f; t += 0.3f)
                if (Vector2.Distance(spot, there + velocity * t) < clearance) return false;
        }
        return true;
    }

    private void Walk(PartyRole role, Gait gait, params Vector2[] stops) => Walk(role, gait, null, stops);

    private void Walk(PartyRole role, Gait gait, Action? arrived, params Vector2[] stops)
    {
        var seat = seats[(int)role];
        seat.Chasing = null;
        seat.Dodged = null;
        seat.Rewalks = 0;
        seat.Halts = 0;
        seat.Stuck = 0;
        seat.HaltedFor = null;
        seat.Ignoring = null;
        Head(role, gait, arrived, stops);
    }

    private void Head(PartyRole role, Gait gait, Action? arrived, Vector2[] stops)
    {
        var seat = seats[(int)role];
        if (Member(role) is not { } member || stops.Length == 0) return;
        var now = Now;
        var from = Flat(member.Position);
        var fromRest = !seat.Walking(now) && Velocity(role).LengthSquared() < 1f;
        var puddles = Puddles();
        var goal = stops.Select(s => OutOf(puddles, s)).ToArray();
        if (gait.ClearOf is { } left && now <= left.By + LateJudged && Vector2.Distance(from, left.Where) >= left.Reach)
            goal = goal.Where((g, k) => k == goal.Length - 1 || Vector2.Distance(g, left.Where) >= left.Reach).ToArray();
        seat.Stops = goal;
        seat.Gait = gait;
        seat.Arrived = arrived;
        seat.RetryAt = float.NaN;
        var squeezing = seat.Stuck >= MostYields;
        var obstacles = Obstacles(role, puddles, squeezing, dodgingFrom ?? seat.Ignoring, !gait.Direct);
        var path = Route(from, goal, obstacles);
        var (course, sprint) = Plan(role, from, goal, path, obstacles, gait, now);
        if (!squeezing && !OnTime(course, gait))
        {
            obstacles = Obstacles(role, puddles, true, dodgingFrom ?? seat.Ignoring, !gait.Direct);
            path = Route(from, goal, obstacles);
            (course, sprint) = Plan(role, from, goal, path, obstacles, gait, now);
        }
        var urgent = squeezing || Urgent(course, gait);
        seat.Urgent = urgent;
        var conflicted = Conflict(role, course, now, urgent, dodgingFrom ?? seat.Ignoring, Pressed(gait, urgent, from, goal, now));
        if (gait.ClearOf is { } hazard && Vector2.Distance(from, hazard.Where) < hazard.Reach
            && Vector2.Distance(goal[^1], hazard.Where) >= hazard.Reach
            && (conflicted is not null && now >= hazard.By - 1.5f || Vector2.Distance(CourseAt(course, hazard.By), hazard.Where) < hazard.Reach - 0.05f)
            && Escape(role, from, hazard.Where, hazard.Reach) is { } exit)
        {
            var then = gait with { ClearOf = null };
            var onward = goal.Where((g, k) => k == goal.Length - 1 || Vector2.Distance(g, hazard.Where) >= hazard.Reach).ToArray();
            Head(role, Stroll with { Shaped = false, Settle = 0f, Due = hazard.By }, () => Head(role, then, arrived, onward), [exit]);
            return;
        }
        if (conflicted is { } conflict && !(conflict.Gap >= PassingGap && Moving(conflict.Who) && !YieldsTo(role, conflict.Who))
            && !((seat.Halts >= MostYields || Leaving(gait, from, now)) && Moving(conflict.Who) && !Wary(conflict.Who) && conflict.Gap >= UrgentNearest))
        {
            if (Moving(conflict.Who) && seat.Chasing is null && OnThePathOf(conflict.Who, from) && Dodge(role, seat, from, now, true, conflict.Who)) return;
            if (!Moving(conflict.Who) && Array.FindIndex(goal, g => Vector2.Distance(At(conflict.Who), g) < NearestAllowed + 0.1f) is var blocked and >= 0)
            {
                seat.Blocked = seat.BlockedBy == conflict.Who ? seat.Blocked + 1 : 1;
                seat.BlockedBy = conflict.Who;
                if (seat.Blocked >= (urgent ? 1 : 3) && BesideTheBlocker(role, goal[blocked], At(conflict.Who)) is { } beside)
                {
                    seat.Blocked = 0;
                    var settled = (Vector2[])goal.Clone();
                    settled[blocked] = beside;
                    Head(role, gait, arrived, settled);
                    return;
                }
            }
            if (!Moving(conflict.Who) && ShortOf(course, At(conflict.Who), urgent ? UrgentNearest : NearestAllowed) is { } shorter && Conflict(role, shorter, now, urgent, seat.Ignoring) is null)
            {
                var there = Vector2.Distance(shorter[^1].At, course[^1].At) < CloseEnough;
                Follow(role, member, seat, shorter, sprint, there);
                if (!there) seat.RetryAt = shorter[^1].Eta + React(role, "hesitate", 0.2f, 0.35f, 0.6f, seat.Draws++);
                return;
            }
            Halt(member, seat);
            seat.HaltedFor = Moving(conflict.Who) && YieldsTo(role, conflict.Who) ? conflict.Who : null;
            seat.Halts++;
            if (!Moving(conflict.Who)) seat.Stuck++;
            seat.RetryAt = now + Backoff(seat, Pressed(gait, urgent, from, goal, now) || !Wary(conflict.Who))
                * (urgent ? React(role, "hesitate", 0.1f, 0.15f, 0.25f, seat.Draws++) : React(role, "hesitate", 0.2f, 0.35f, 0.6f, seat.Draws++));
            return;
        }
        if (dodgingFrom is null && fromRest && seat.LetGo < MostLetGo && !Pressed(gait, urgent, from, goal, now)
            && PerRole.All.Any(other => other != role && Threat(role, other) && Lettered(role, other) && Moving(other) && !MovingAway(other, from)
                                        && Vector2.Distance(At(other), from) < Circling))
        {
            seat.LetGo++;
            Halt(member, seat);
            seat.RetryAt = now + React(role, "circled", 0.2f, 0.3f, 0.45f, seat.Draws++);
            return;
        }
        if (dodgingFrom is null && fromRest && !urgent && Vector2.Distance(from, course[^1].At) >= LetGoTrip)
        {
            var heading = CourseAt(course, now + Foresight) - from;
            if (seat.LetGo < 2 && heading.LengthSquared() > 0.01f && PerRole.All.Any(other => other != role && Threat(role, other) && SetOff(other) is { } start
                    && Vector2.Distance(start, from) is > 0.05f and < LetGoWithin && Vector2.Dot(Vector2.Normalize(start - from), Vector2.Normalize(heading)) > 0.5f))
            {
                seat.LetGo++;
                Halt(member, seat);
                seat.RetryAt = now + React(role, "let-go", 0.15f, 0.25f, 0.4f, seat.Draws++);
                return;
            }
        }
        seat.LetGo = 0;
        seat.Blocked = 0;
        seat.Stuck = 0;
        Follow(role, member, seat, course, sprint);
    }

    private Vector2? Escape(PartyRole role, Vector2 from, Vector2 hazard, float reach)
    {
        var away = from - hazard;
        var distance = away.Length();
        var way = distance > 1e-3f ? away / distance : -Vector2.Normalize(hazard);
        foreach (var turn in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
        {
            var spot = from + Rotate(way, turn) * (reach - distance + 0.8f);
            if (Vector2.Distance(spot, hazard) >= reach && spot.Length() <= ArenaEdge - 0.5f && SafeToStand(role, spot, NearestAllowed)
                && SegmentClear(from, spot, Obstacles(role, Puddles()).Where(o => Vector2.Distance(o.At, from) >= o.Radius).ToList())) return spot;
        }
        foreach (var turn in Enumerable.Range(1, 18).Select(n => (n + 1) / 2 * 10f * (n % 2 == 0 ? -1f : 1f)))
        {
            var spot = hazard + Rotate(way, turn) * (reach + 0.8f);
            if (spot.Length() <= ArenaEdge - 0.5f && SafeToStand(role, spot, NearestAllowed)
                && SegmentClear(from, spot, [.. Obstacles(role, Puddles()).Where(o => Vector2.Distance(o.At, from) >= o.Radius), new Obstacle(hazard, distance - 0.05f)])) return spot;
        }
        return null;
    }

    private Vector2? BesideTheBlocker(PartyRole role, Vector2 goal, Vector2 blocker)
    {
        var away = goal - blocker;
        var way = away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : Vector2.UnitX;
        foreach (var turn in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
        {
            var spot = blocker + Rotate(way, turn) * (NearestAllowed + 0.3f);
            if (spot.Length() <= ArenaEdge - 0.5f && SafeToStand(role, spot, NearestAllowed)) return spot;
        }
        return null;
    }

    private Vector2? SetOff(PartyRole role)
    {
        var seen = sightings[(int)role];
        if (seen.Count < 3) return null;
        var points = seen.ToArray();
        return Vector2.Distance(points[0].Where, points[1].Where) < 0.03f && Vector2.Distance(points[^2].Where, points[^1].Where) > 0.15f ? points[1].Where : null;
    }

    private void Halt(SimCharacter member, Seat seat)
    {
        member.StopMoving();
        seat.Walk++;
        seat.Course.Clear();
        seat.Anchor = Flat(member.Position);
    }

    private void Follow(PartyRole role, SimCharacter member, Seat seat, List<(Vector2 At, float Eta)> course, bool sprint, bool arrives = true)
    {
        var walk = ++seat.Walk;
        var now = course[0].Eta;
        var speed = sprint ? AiManager.SprintSpeed : AiManager.RunSpeed;
        seat.Course.Clear();
        seat.Course.AddRange(course);
        if (sprint) (member as ISimPartyMember)?.UseSprint(course[^1].Eta - now + 1f);
        for (var k = 1; k < course.Count; k++)
        {
            var (to, _) = course[k];
            var (previous, starts) = course[k - 1];
            if (Vector2.Distance(previous, to) < 1e-3f) continue;
            var target = new Vector3(to.X, 0f, to.Y);
            var resumes = k >= 2 && Vector2.Distance(course[k - 2].At, previous) < 1e-3f;
            if (starts - now <= 1e-4f) member.MoveTo(target, speed: speed);
            else
                world.Events.Add(starts - now, () =>
                {
                    if (seat.Walk != walk || !member.IsAlive()) return;
                    if (seat.Stops.Length > 1 && Vector2.Distance(seat.Stops[0], previous) < 1e-3f)
                    {
                        seat.Stops = seat.Stops[1..];
                        if (resumes)
                        {
                            Head(role, seat.Gait with { Shaped = false }, seat.Arrived, seat.Stops);
                            return;
                        }
                    }
                    member.MoveTo(target, speed: speed);
                });
        }
        world.Events.Add(MathF.Max(0f, course[^1].Eta - now), () =>
        {
            if (seat.Walk != walk || !arrives) return;
            if (member.IsAlive() && seat.Rewalks < MostRewalks && Vector2.Distance(Flat(member.Position), course[^1].At) > OffTheSpot)
            {
                seat.Rewalks++;
                Head(role, seat.Gait with { Shaped = false }, seat.Arrived, seat.Stops.Length > 0 ? seat.Stops : [course[^1].At]);
                return;
            }
            seat.Rewalks = 0;
            Arrive(role, seat, Vector2.Distance(course[0].At, course[^1].At));
        });
    }

    private void Arrive(PartyRole role, Seat seat, float length)
    {
        var now = Now;
        seat.Anchor = seat.Course.Count > 0 ? seat.Course[^1].At : seat.Anchor;
        seat.Wander = seat.Gait.Settle ?? Wanders(role);
        seat.Halts = 0;
        seat.NextFidget = length >= 4f && Dice(role, "correct", seat.Draws) < 0.26f
            ? now + React(role, "correct-at", 0.4f, 0.9f, 1.6f, seat.Draws++)
            : now + FidgetEvery(role) * Sample(role, "fidget-gap", 0.5f, 1f, 1.8f, seat.Draws++);
        var arrived = seat.Arrived;
        seat.Arrived = null;
        arrived?.Invoke();
    }

    private (List<(Vector2 At, float Eta)> Course, bool Sprint) Plan(PartyRole role, Vector2 from, Vector2[] goal, List<Vector2> path,
        IReadOnlyList<Obstacle> obstacles, Gait gait, float now)
    {
        var plain = path.Select(p => (At: p, Pause: goal.Take(goal.Length - 1).Any(g => Vector2.Distance(g, p) < 1e-3f) && gait.PauseAtStops
            ? Sample(role, "stop-pause", gait.Pause.Low, gait.Pause.Middle, gait.Pause.High, seats[(int)role].Draws++)
            : 0f)).ToList();
        var speed = gait.Sprint ? AiManager.SprintSpeed : AiManager.RunSpeed;
        if (gait.Shaped && Shape(role, from, plain, obstacles, gait) is { } shaped && Timed(from, shaped, now, speed) is var shapedCourse
            && OnTime(shapedCourse, gait, ShapeSlack)) return (shapedCourse, gait.Sprint);
        var course = Timed(from, plain, now, speed);
        if (OnTime(course, gait) || gait.Sprint) return (course, gait.Sprint);
        return (Timed(from, plain.Select(p => (p.At, 0f)).ToList(), now, AiManager.SprintSpeed), true);
    }

    private static bool OnTime(List<(Vector2 At, float Eta)> course, Gait gait, float slack = 0f)
        => course[^1].Eta <= gait.Due - slack
           && (gait.ClearOf is not { } clearOf || Vector2.Distance(CourseAt(course, clearOf.By - slack), clearOf.Where)
               >= MathF.Min(clearOf.Reach, Vector2.Distance(course[^1].At, clearOf.Where)) - 0.05f);

    private List<(Vector2 At, float Pause)>? Shape(PartyRole role, Vector2 from, List<(Vector2 At, float Pause)> plain, IReadOnlyList<Obstacle> obstacles, Gait gait)
    {
        if (plain.Count != 1) return null;
        var to = plain[0].At;
        var chord = to - from;
        var length = chord.Length();
        if (length < 6f) return null;
        var draw = seats[(int)role].Draws++;
        var legs = new List<(Vector2 At, float Pause)> { (to, plain[0].Pause) };
        var lateral = MathF.Min(Sample(role, "bow", 0.4f, 1.2f, 2.8f, draw), 0.18f * length);
        var across = new Vector2(-chord.Y, chord.X) / length;
        var side = gait.BowOutward
            ? Vector2.Dot(across, (from + to) * 0.5f) >= 0f ? 1f : -1f
            : AwayFromNeighbours(role, from, to, across) ?? (Dice(role, "bow-side", draw) < 0.5f ? -1f : 1f);
        var mid = from + chord * Sample(role, "bow-at", 0.4f, 0.5f, 0.6f, draw) + across * side * lateral;
        if (mid.Length() <= ArenaEdge && SegmentClear(from, mid, obstacles) && SegmentClear(mid, to, obstacles)) legs.Insert(0, (mid, 0f));
        if (gait.Hops > 1) return Hop(role, from, legs, gait.Hops, draw);
        if (Dice(role, "stop-and-go", draw) >= 0.45f) return legs;
        return Hop(role, from, legs, 2, draw, Sample(role, "stop-and-go-at", 0.35f, 0.55f, 0.75f, draw), Sample(role, "stop-and-go-for", 0.15f, 0.35f, 0.7f, draw));
    }

    private float? AwayFromNeighbours(PartyRole role, Vector2 from, Vector2 to, Vector2 across)
    {
        var chord = to - from;
        var lengthSquared = chord.LengthSquared();
        var lean = 0f;
        foreach (var other in PerRole.All)
        {
            if (!Threat(role, other)) continue;
            var there = At(other) + Velocity(other) * MotionCarriesOn;
            var off = there - (from + chord * Math.Clamp(Vector2.Dot(there - from, chord) / lengthSquared, 0f, 1f));
            var distance = off.Length();
            if (distance < 6f) lean -= MathF.Sign(Vector2.Dot(off, across)) * (6f - distance);
        }
        return MathF.Abs(lean) < 0.5f ? null : MathF.Sign(lean);
    }

    private List<(Vector2 At, float Pause)> Hop(PartyRole role, Vector2 from, List<(Vector2 At, float Pause)> legs, int hops, int draw, float? at = null, float? pause = null)
    {
        var points = new List<Vector2> { from };
        points.AddRange(legs.Select(l => l.At));
        var total = 0f;
        for (var k = 1; k < points.Count; k++) total += Vector2.Distance(points[k - 1], points[k]);
        var cuts = Enumerable.Range(1, hops - 1).Select(h => (at ?? (float)h / hops) * total).ToList();
        var result = new List<(Vector2 At, float Pause)>();
        var walked = 0f;
        var cut = 0;
        for (var k = 1; k < points.Count; k++)
        {
            var leg = Vector2.Distance(points[k - 1], points[k]);
            while (cut < cuts.Count && cuts[cut] <= walked + leg && leg > 1e-3f)
            {
                var point = Vector2.Lerp(points[k - 1], points[k], (cuts[cut] - walked) / leg);
                result.Add((point, pause ?? Sample(role, "hop-pause", 0.6f, 1.2f, 2.2f, draw * 8 + cut)));
                cut++;
            }
            result.Add((points[k], legs[k - 1].Pause));
            walked += leg;
        }
        return result;
    }

    private static List<(Vector2 At, float Eta)> Timed(Vector2 from, List<(Vector2 At, float Pause)> legs, float start, float speed)
    {
        var course = new List<(Vector2 At, float Eta)> { (from, start) };
        var eta = start;
        var at = from;
        foreach (var (to, pause) in legs)
        {
            eta += Vector2.Distance(at, to) / speed;
            course.Add((to, eta));
            if (pause > 0f)
            {
                eta += pause;
                course.Add((to, eta));
            }
            at = to;
        }
        return course;
    }

    private static Vector2 CourseAt(IReadOnlyList<(Vector2 At, float Eta)> course, float time)
    {
        if (course.Count == 0) return Vector2.Zero;
        if (time <= course[0].Eta) return course[0].At;
        for (var k = 1; k < course.Count; k++)
        {
            var (to, eta) = course[k];
            if (time > eta) continue;
            var (from, start) = course[k - 1];
            return eta - start < 1e-4f ? to : Vector2.Lerp(from, to, (time - start) / (eta - start));
        }
        return course[^1].At;
    }

    private static bool Urgent(IReadOnlyList<(Vector2 At, float Eta)> course, Gait gait)
        => course.Count > 0 && (course[^1].Eta > gait.Due - 0.4f
                                || gait.ClearOf is { } clearOf && Vector2.Distance(CourseAt(course, clearOf.By - 0.4f), clearOf.Where)
                                < MathF.Min(clearOf.Reach, Vector2.Distance(course[^1].At, clearOf.Where)) - 0.05f);

    private static List<(Vector2 At, float Eta)>? ShortOf(List<(Vector2 At, float Eta)> course, Vector2 there, float nearest)
    {
        var reached = course[0].Eta;
        for (var t = reached + 0.05f; t <= course[^1].Eta + 1e-4f; t += 0.05f)
        {
            if (Vector2.Distance(CourseAt(course, t), there) < nearest + 0.15f) break;
            reached = t;
        }
        var end = CourseAt(course, reached);
        if (Vector2.Distance(end, course[0].At) < 0.5f) return null;
        var shorter = course.Where(c => c.Eta < reached - 1e-4f).ToList();
        shorter.Add((end, reached));
        return shorter;
    }

    private (PartyRole Who, float Gap)? Conflict(PartyRole role, IReadOnlyList<(Vector2 At, float Eta)> course, float now, bool urgent, PartyRole? ignore = null,
        bool hurried = false)
    {
        if (course.Count == 0) return null;
        var start = CourseAt(course, now);
        var heading = CourseAt(course, now + Foresight) - start;
        var walking = heading.LengthSquared() > 0.01f;
        (PartyRole Who, float Gap)? worst = null;
        foreach (var other in PerRole.All)
        {
            if (other == ignore || !Threat(role, other)) continue;
            var there = At(other);
            var velocity = Velocity(other);
            var moving = velocity.LengthSquared() >= 1f;
            var wary = moving && Wary(other);
            var both = moving && walking;
            var parallel = both && Lettered(role, other) && Vector2.Dot(Vector2.Normalize(heading), Vector2.Normalize(velocity)) > 0.5f;
            var nearest = wary ? urgent ? UrgentMovingNearest : MovingNearest : urgent ? UrgentNearest : NearestAllowed;
            var carry = wary ? urgent ? UrgentWaryCarry : Foresight : urgent ? UrgentCarry : MotionCarriesOn;
            var already = Vector2.Distance(start, there);
            var crossing = both && MathF.Abs(Cross(Vector2.Normalize(heading), Vector2.Normalize(velocity))) > 0.5f;
            var alongside = !hurried && parallel && Vector2.Distance(CourseAt(course, now + MathF.Min(Foresight, carry)), there + velocity * MathF.Min(Foresight, carry)) < nearest;
            for (var t = 0.1f; t <= Foresight + 1e-3f; t += 0.1f)
            {
                var at = CourseAt(course, now + t);
                var gap = Vector2.Distance(at, there + velocity * MathF.Min(t, carry));
                if (crossing) gap = MathF.Min(gap, Vector2.Distance(at, there));
                if (gap >= nearest || !alongside && gap >= already - 0.05f || worst is { } known && known.Gap <= gap) continue;
                worst = (other, gap);
            }
        }
        return worst;
    }

    private List<Obstacle> Obstacles(PartyRole role, List<Obstacle> puddles, bool tight = false, PartyRole? except = null, bool threats = true)
    {
        var obstacles = new List<Obstacle>(puddles);
        for (var k = 0; k < SettlingTornadoes.Length; k++)
            if (Now <= SettlingTornadoes[k].Until && state.Objects.Tornadoes.ElementAtOrDefault(k) is { IsActive: true } rage)
                obstacles.Add(new Obstacle(Flat(rage.Position), TornadoClearance));
        foreach (var other in PerRole.All)
        {
            if (!threats || other == except || !Threat(role, other)) continue;
            var there = At(other);
            var velocity = Velocity(other);
            var clearance = tight ? UrgentNearest + 0.2f : Clearance;
            if (velocity.LengthSquared() < 1f || !Wary(other))
            {
                obstacles.Add(new Obstacle(there, clearance));
                if (velocity.LengthSquared() >= 0.25f) obstacles.Add(new Obstacle(there + velocity * MotionCarriesOn, clearance));
                continue;
            }
            var reach = tight ? UrgentMovingNearest + 0.2f : MovingClearance;
            obstacles.Add(new Obstacle(there, reach));
            obstacles.Add(new Obstacle(there + velocity * MotionCarriesOn, reach));
            obstacles.Add(new Obstacle(there + velocity * Foresight, reach));
        }
        return obstacles;
    }

    private bool Threat(PartyRole me, PartyRole other)
    {
        if (other == me || Member(other) is null) return false;
        ReadLetters();
        if (nisi[(int)me] is null && (nisi[(int)other] ?? receiving[(int)other]) is null)
            return lettersHandedOut || me.IsDps() == other.IsDps() && Now is >= 38.5f and <= 45.5f or >= 68.8f and <= 75.6f or >= 95f and <= 107f or >= 112f and <= 135.3f;
        return !Welcome(me, other);
    }

    private bool Welcome(PartyRole me, PartyRole other)
    {
        var mine = nisi[(int)me];
        if (seats[(int)me].Chasing?.Partner() == other) return mine is null || nisi[(int)other] is not { } partners || partners == mine;
        var theirs = nisi[(int)other] ?? receiving[(int)other];
        if (mine is { } own && theirs is { } held && own != held) return false;
        if (me.IsDps() == other.IsDps()) return false;
        var now = Now;
        var shared = mine is not null && nisi[(int)other] == mine;
        var lasts = !shared || SoonestExpiry(me, other) - now > ExpiryMargin;
        var pending = (mine is null) != (nisi[(int)other] is null) && nisiSince[(int)(mine is null ? other : me)] < Markers;
        if (now is >= 38.5f and <= 45.5f or >= 68.8f and <= 75.6f || pending && now is > 75.6f and <= LastSecondPass && SoonestExpiry(me, other) - now > 0.2f)
        {
            var (dps, support) = me.IsDps() ? (me, other) : (other, me);
            if (PartnerOf(dps) == support && lasts) return true;
        }
        if (now is >= 112f and <= 135.3f && judgment[(int)me] is { } letter && judgment[(int)other] == letter) return true;
        if (now is >= 95f and <= 107f or >= 112f and <= 135.3f)
        {
            if (mine is null && theirs is { } given && judgment[(int)me] == given) return true;
            if ((theirs is null || nisi[(int)other] is null && theirs == mine) && mine is { } holding && judgment[(int)other] == holding) return true;
            if (now <= 107f && shared && lasts) return true;
        }
        return false;
    }

    private void ReadLetters()
    {
        var now = Now;
        if (now == lettersReadAt) return;
        lettersReadAt = now;
        foreach (var role in PerRole.All)
        {
            var member = Member(role);
            var letter = member is null ? null : Enum.GetValues<NisiLetter>().Where(l => member.ShowsStatus(StatusId.FinalDecreeNisi(l))).Cast<NisiLetter?>().FirstOrDefault();
            if (letter != nisi[(int)role]) nisiSince[(int)role] = now;
            nisi[(int)role] = letter;
            judgment[(int)role] = member is null ? null : Enum.GetValues<NisiLetter>().Where(l => member.ShowsStatus(StatusId.FinalJudgment(l))).Cast<NisiLetter?>().FirstOrDefault();
        }
        foreach (var role in PerRole.All)
            receiving[(int)role] = nisi[(int)role] is null && Member(role) is { } bare
                ? PerRole.All.Where(r => nisi[(int)r] is not null && Member(r) is not null && Vector2.Distance(At(r), Flat(bare.Position)) < Receiving)
                    .OrderBy(r => Vector2.Distance(At(r), Flat(bare.Position))).Select(r => nisi[(int)r]).FirstOrDefault()
                : null;
        if (Casting(state.Objects.BruteJustice, ActionId.JudgmentNisi)) judgmentNisiSeenAt = now;
        lettersHandedOut = now - judgmentNisiSeenAt < 5.5f;
    }

    private NisiLetter? NisiOf(PartyRole role)
    {
        ReadLetters();
        return nisi[(int)role];
    }

    private NisiLetter? JudgmentOf(PartyRole role)
    {
        ReadLetters();
        return judgment[(int)role];
    }

    private static bool Stepping(Queue<(float At, Vector2 Where)> seen)
    {
        if (seen.Count < 4) return false;
        var p = seen.ToArray();
        var a = Vector2.Distance(p[0].Where, p[1].Where);
        var b = Vector2.Distance(p[1].Where, p[2].Where);
        var c = Vector2.Distance(p[2].Where, p[3].Where);
        return a > SteadyStep && b < StillStep && c > SteadyStep || a < StillStep && b > PoseStep && c < StillStep;
    }

    private Vector2 Velocity(PartyRole role)
    {
        var velocity = SeenVelocity(role);
        if (velocity.LengthSquared() >= 1f || !Wary(role)) return velocity;
        var (last, at) = lastMotion[(int)role];
        return Now - at < JustStopped ? last : velocity;
    }

    private Vector2 SeenVelocity(PartyRole role)
    {
        var seen = sightings[(int)role];
        if (seen.Count < 2) return Vector2.Zero;
        var (from, was) = seen.Peek();
        var (to, now) = seen.Last();
        return to - from > 0.05f ? (now - was) / (to - from) : Vector2.Zero;
    }

    private static bool Casting(SimEnemy? enemy, uint actionId) => enemy is { IsActive: true, IsCasting: true } && enemy.CastActionId == actionId;

    private List<Vector2> Route(Vector2 from, IReadOnlyList<Vector2> stops, IReadOnlyList<Obstacle> obstacles)
    {
        var path = new List<Vector2>();
        var at = from;
        var puddleExit = OutOf(obstacles.Where(o => o.Puddle).ToList(), from);
        if (puddleExit != from)
        {
            path.Add(puddleExit);
            at = puddleExit;
        }
        foreach (var stop in stops)
        {
            path.AddRange(PlanLeg(at, stop, obstacles));
            at = stop;
        }
        return path;
    }

    private List<Obstacle> Puddles()
        => world.Children.OfType<SimEventObject>().Where(o => o.IsActive)
            .Select(o => new Obstacle(Flat(o.Position), PuddleRadius(o.EObjRowId) + PuddleMargin, true))
            .Where(p => p.Radius > PuddleMargin).ToList();

    private static float PuddleRadius(uint eobjId) => eobjId switch
    {
        EObjId.VoidzoneEarthMissile => Geometry.MissilePuddleRadius,
        EObjId.VoidzoneIceSmall or EObjId.VoidzoneIceLarge => Geometry.LargeIceRadius,
        _ => 0f,
    };

    private static Vector2 OutOf(IReadOnlyList<Obstacle> puddles, Vector2 spot)
    {
        foreach (var puddle in puddles)
        {
            if (!puddle.Puddle) continue;
            var away = spot - puddle.At;
            var distance = away.Length();
            if (distance >= puddle.Radius) continue;
            var way = distance > 0.01f ? away / distance : Vector2.UnitX;
            spot = Enumerable.Range(0, 19).Select(k => puddle.At + Rotate(way, (k + 1) / 2 * 10f * (k % 2 == 0 ? 1f : -1f)) * puddle.Radius)
                .FirstOrDefault(exit => exit.Length() <= ArenaEdge - 0.6f, puddle.At + way * puddle.Radius);
        }
        return spot;
    }

    private static readonly int Cells = (int)(2f * ArenaEdge / Cell) + 1;
    private static readonly bool[,] OffTheArena = OffTheArenaCells();

    private static bool[,] OffTheArenaCells()
    {
        var off = new bool[Cells, Cells];
        for (var i = 0; i < Cells; i++)
        for (var j = 0; j < Cells; j++)
            off[i, j] = CellPoint(i, j).Length() > ArenaEdge;
        return off;
    }

    private List<Vector2> PlanLeg(Vector2 from, Vector2 to, IReadOnlyList<Obstacle> obstacles)
    {
        var blocking = obstacles
            .Select(o => !o.Puddle && Vector2.Distance(o.At, from) < o.Radius ? o with { Radius = Vector2.Distance(o.At, from) - 0.05f } : o)
            .Select(o => !o.Puddle && Vector2.Distance(o.At, to) < o.Radius ? o with { Radius = Vector2.Distance(o.At, to) - 0.05f } : o)
            .Where(o => o.Radius > 0.3f && Vector2.Distance(o.At, to) >= o.Radius)
            .ToList();
        if (SegmentClear(from, to, blocking)) return [to];
        var g = grid ??= new Grid();
        var blocked = g.Blocked;
        Array.Copy(OffTheArena, blocked, OffTheArena.Length);
        foreach (var o in blocking)
        {
            var reach = (int)MathF.Ceiling(o.Radius / Cell);
            var (ci, cj) = CellOf(o.At);
            for (var i = Math.Max(0, ci - reach); i <= Math.Min(Cells - 1, ci + reach); i++)
            for (var j = Math.Max(0, cj - reach); j <= Math.Min(Cells - 1, cj + reach); j++)
                if (Vector2.Distance(CellPoint(i, j), o.At) < o.Radius) blocked[i, j] = true;
        }
        var start = CellOf(from);
        var goal = CellOf(to);
        blocked[start.I, start.J] = false;
        blocked[goal.I, goal.J] = false;
        if (start == goal) return [to];
        StepOut(blocked, start, blocking);
        StepOut(blocked, goal, blocking);
        var search = ++g.Search;
        g.Open.Clear();
        g.Cost[start.I, start.J] = 0f;
        g.Seen[start.I, start.J] = search;
        g.Open.Enqueue(start, 0f);
        var found = false;
        var expanded = 0;
        while (g.Open.TryDequeue(out var cell, out _))
        {
            if (g.Closed[cell.I, cell.J] == search) continue;
            g.Closed[cell.I, cell.J] = search;
            if (cell == goal)
            {
                found = true;
                break;
            }
            if (++expanded > MostExpanded) break;
            var cost = g.Cost[cell.I, cell.J];
            for (var di = -1; di <= 1; di++)
            for (var dj = -1; dj <= 1; dj++)
            {
                if (di == 0 && dj == 0) continue;
                var next = (I: cell.I + di, J: cell.J + dj);
                if (next.I < 0 || next.J < 0 || next.I >= Cells || next.J >= Cells || blocked[next.I, next.J] || g.Closed[next.I, next.J] == search) continue;
                var step = cost + (di != 0 && dj != 0 ? 1.4142f : 1f) * Cell;
                if (g.Seen[next.I, next.J] == search && g.Cost[next.I, next.J] <= step) continue;
                g.Cost[next.I, next.J] = step;
                g.Seen[next.I, next.J] = search;
                g.Came[next.I, next.J] = cell;
                g.Open.Enqueue(next, step + Vector2.Distance(CellPoint(next.I, next.J), to));
            }
        }
        if (!found) return [to];
        var cells = new List<Vector2>();
        for (var c = goal; c != start; c = g.Came[c.I, c.J]) cells.Add(CellPoint(c.I, c.J));
        cells.Reverse();
        cells[^1] = to;
        var path = new List<Vector2>();
        var anchor = from;
        var k = 0;
        while (k < cells.Count)
        {
            var farthest = k;
            for (var m = cells.Count - 1; m > k; m--)
                if (SegmentClear(anchor, cells[m], blocking))
                {
                    farthest = m;
                    break;
                }
            path.Add(cells[farthest]);
            anchor = cells[farthest];
            k = farthest + 1;
        }
        return path;
    }

    private static void StepOut(bool[,] blocked, (int I, int J) cell, IReadOnlyList<Obstacle> blocking)
    {
        var centre = CellPoint(cell.I, cell.J);
        for (var di = -1; di <= 1; di++)
        for (var dj = -1; dj <= 1; dj++)
        {
            var (i, j) = (cell.I + di, cell.J + dj);
            if (i < 0 || j < 0 || i >= Cells || j >= Cells || !blocked[i, j] || OffTheArena[i, j]) continue;
            var next = CellPoint(i, j);
            var away = true;
            for (var k = 0; k < blocking.Count && away; k++)
            {
                var distance = Vector2.Distance(next, blocking[k].At);
                away = distance >= blocking[k].Radius || distance > Vector2.Distance(centre, blocking[k].At);
            }
            if (away) blocked[i, j] = false;
        }
    }

    private static (int I, int J) CellOf(Vector2 p)
        => (Math.Clamp((int)MathF.Round((p.X + ArenaEdge) / Cell), 0, Cells - 1), Math.Clamp((int)MathF.Round((p.Y + ArenaEdge) / Cell), 0, Cells - 1));

    private static Vector2 CellPoint(int i, int j) => new(i * Cell - ArenaEdge, j * Cell - ArenaEdge);

    private static bool SegmentClear(Vector2 from, Vector2 to, IReadOnlyList<Obstacle> obstacles)
    {
        var leg = to - from;
        var lengthSquared = leg.LengthSquared();
        foreach (var o in obstacles)
        {
            var t = lengthSquared < 1e-6f ? 0f : Math.Clamp(Vector2.Dot(o.At - from, leg) / lengthSquared, 0f, 1f);
            if (Vector2.Distance(from + leg * t, o.At) < o.Radius) return false;
        }
        return true;
    }

    private Dictionary<PartyRole, float> Departures(string what, params (PartyRole Role, float Low, float Middle, float High)[] roles)
        => Departures(what, _ => float.MaxValue, roles);

    private Dictionary<PartyRole, float> Departures(string what, Func<PartyRole, float> latest, params (PartyRole Role, float Low, float Middle, float High)[] roles)
        => roles.ToDictionary(r => r.Role, r => MathF.Max(Reaction, MathF.Min(Sample(r.Role, what, r.Low, r.Middle, r.High), latest(r.Role))));

    private float Sample(PartyRole role, string what, float low, float middle, float high, int draw = 0)
    {
        var u = Dice(role, what, draw);
        var below = low - 0.25f * (middle - low);
        var above = high + 0.25f * (high - middle);
        return u < 0.1f ? float.Lerp(below, low, u / 0.1f)
            : u < 0.5f ? float.Lerp(low, middle, (u - 0.1f) / 0.4f)
            : u < 0.9f ? float.Lerp(middle, high, (u - 0.5f) / 0.4f)
            : float.Lerp(high, above, (u - 0.9f) / 0.1f);
    }

    private float React(PartyRole role, string what, float low, float middle, float high, int draw = 0) => MathF.Max(Reaction, Sample(role, what, low, middle, high, draw));

    private int Pick(PartyRole role, string what, int fewest, int most) => fewest + (int)(Dice(role, what) * (most - fewest + 1) - 1e-4f);

    private float Dice(PartyRole role, string what, int draw = 0)
    {
        var hash = 2166136261u;
        Mix(ref hash, state.BotPacing);
        Mix(ref hash, (int)role);
        Mix(ref hash, draw);
        foreach (var c in what) hash = (hash ^ c) * 16777619u;
        hash ^= hash >> 15;
        hash *= 0x2c1b3c6du;
        hash ^= hash >> 12;
        return (hash >> 8) / 16777216f;
    }

    private static void Mix(ref uint hash, int value)
    {
        for (var shift = 0; shift < 32; shift += 8) hash = (hash ^ (uint)((value >> shift) & 0xff)) * 16777619u;
    }

    private float Now => world.Events.Elapsed;

    private void When(float time, Action action) => world.Events.Add(MathF.Max(0f, time - Now), action);

    private int Job(PartyRole role)
    {
        var seat = seats[(int)role];
        seat.Dodged = null;
        seat.Ignoring = null;
        return ++seat.Job;
    }

    private bool Still(PartyRole role, int job) => seats[(int)role].Job == job;

    private void Later(PartyRole role, int job, float delay, Action action) => world.Events.Add(MathF.Max(0f, delay), () =>
    {
        if (Still(role, job)) action();
    });

    private void Once(PartyRole role, int job, Func<bool> ready, Action then, float until = float.MaxValue, Action? otherwise = null)
        => watches.Add(new Watcher(role, job, ready, then, until, otherwise));

    private void Watch(PartyRole role, Func<bool> ready, Action then, float until = float.MaxValue, Action? otherwise = null)
        => watches.Add(new Watcher(role, -1, ready, then, until, otherwise));

    private SimCharacter? Member(PartyRole role) => world.Party.Get(role) is { } member && member.IsAlive() ? member : null;

    private Vector2 At(PartyRole role) => Member(role) is { } member ? Flat(member.Position) : Vector2.Zero;

    private bool Holds(PartyRole role, ushort statusId) => Member(role) is { } member && member.ShowsStatus(statusId);

    private PartyRole? Holder(ushort statusId) => PerRole.All.Where(r => Holds(r, statusId)).Cast<PartyRole?>().FirstOrDefault();

    private static Vector2 Polar(float degrees, float radius) => Flat(Geometry.OnCompass(degrees, radius));

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        var r = float.DegreesToRadians(degrees);
        var (sin, cos) = MathF.SinCos(r);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static Vector2 Forward(float rotation) => new(MathF.Sin(rotation), MathF.Cos(rotation));

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static readonly Gait Stroll = new();

    private sealed record Gait
    {
        public bool Sprint { get; init; }
        public bool Shaped { get; init; } = true;
        public int Hops { get; init; } = 1;
        public bool PauseAtStops { get; init; }
        public bool Direct { get; init; }
        public bool Aside { get; init; }
        public (float Low, float Middle, float High) Pause { get; init; } = (0.5f, 1.1f, 2f);
        public float Due { get; init; } = float.MaxValue;
        public float? Settle { get; init; }
        public bool BowOutward { get; init; }
        public (Vector2 Where, float By, float Reach)? ClearOf { get; init; }
    }

    private sealed record Obstacle(Vector2 At, float Radius, bool Puddle = false);

    private sealed record Resume(Gait Gait, Action? Arrived, Vector2[] Stops);

    private sealed class Grid
    {
        public readonly bool[,] Blocked = new bool[Cells, Cells];
        public readonly float[,] Cost = new float[Cells, Cells];
        public readonly (int I, int J)[,] Came = new (int I, int J)[Cells, Cells];
        public readonly int[,] Seen = new int[Cells, Cells];
        public readonly int[,] Closed = new int[Cells, Cells];
        public readonly PriorityQueue<(int I, int J), float> Open = new();
        public int Search;
    }

    private sealed record Watcher(PartyRole Role, int Job, Func<bool> Ready, Action Then, float Until, Action? Otherwise);

    private sealed class Chase(Func<PartyRole?> partner, Func<Vector2?> target, float stopShort, Func<bool> done, Action then, float until, Gait gait)
    {
        public Func<PartyRole?> Partner { get; } = partner;
        public Func<Vector2?> Target { get; } = target;
        public float StopShort { get; } = stopShort;
        public Func<bool> Done { get; } = done;
        public Action Then { get; } = then;
        public float Until { get; } = until;
        public Gait Gait { get; } = gait;
        public Vector2? AimedAt { get; set; }
        public float NextAim { get; set; }
    }

    private sealed class Seat
    {
        public int Job;
        public int Walk;
        public Vector2[] Stops = [];
        public Gait Gait = Stroll;
        public Action? Arrived;
        public readonly List<(Vector2 At, float Eta)> Course = [];
        public Chase? Chasing;
        public Vector2 Anchor;
        public float Wander;
        public float NextFidget;
        public int Draws;
        public float RetryAt = float.NaN;
        public bool Urgent;
        public int Blocked;
        public int LetGo;
        public PartyRole? BlockedBy;
        public float ThirdStackDelay;
        public int Rewalks;
        public Resume? Dodged;
        public int DodgeWalk = -1;
        public PartyRole? DodgingFrom;
        public PartyRole? HaltedFor;
        public int Halts;
        public int Stuck;
        public float SteeredAt = float.NegativeInfinity;
        public PartyRole? DangerFrom;
        public float DangerSince;
        public float DangerReact;
        public readonly Encounter?[] Met = new Encounter?[8];
        public int Meetings;
        public PartyRole? Ignoring;
        public int TakenSpots;

        public bool Walking(float now) => Course.Count > 0 && now < Course[^1].Eta;

        public bool HasWalk(float now) => Walking(now) || !float.IsNaN(RetryAt);

        public void Reset()
        {
            Job = 0;
            Walk = 0;
            Stops = [];
            Gait = Stroll;
            Arrived = null;
            Course.Clear();
            Chasing = null;
            Anchor = Vector2.Zero;
            Wander = 0f;
            NextFidget = 0f;
            Draws = 0;
            RetryAt = float.NaN;
            Urgent = false;
            Blocked = 0;
            LetGo = 0;
            BlockedBy = null;
            ThirdStackDelay = 0f;
            Rewalks = 0;
            Dodged = null;
            DodgeWalk = -1;
            DodgingFrom = null;
            HaltedFor = null;
            Halts = 0;
            Stuck = 0;
            DangerFrom = null;
            SteeredAt = float.NegativeInfinity;
            Array.Clear(Met);
            Meetings = 0;
            Ignoring = null;
            TakenSpots = 0;
        }
    }

    private sealed class Encounter
    {
        public int Draw;
        public bool Expected;
        public bool Judged;
        public bool Noticed;
        public float NoticedAt;
    }
}
