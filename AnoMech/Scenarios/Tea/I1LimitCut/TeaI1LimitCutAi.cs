using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.I1LimitCut;

public sealed class TeaI1LimitCutAi : IScenarioAi<TeaI1LimitCutState>
{
    public string Name => "1256";

    private const float Exact = 0f;
    private const float EvenBehindCruiseChaser = 3f;
    private const float ArenaEdge = 18.3f;
    private const float BehindCruiseChaserOutOfAlphaSword = 4f;
    private static readonly float AlphaSwordClearCos = MathF.Cos(float.DegreesToRadians(60f));

    private static readonly int[] SecondHalf = [3, 4, 7, 8];

    private TeaI1LimitCutState state = null!;
    private SimWorld world = null!;

    public void Run(TeaI1LimitCutState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);

        ai.Move(0.3f, StackInCentre);
        ai.Move(6.27f, FanOutNorthOfCentre, Exact, arrivalTime: 8.49f);
        ai.Move(8.49f, FollowSecondBlasters, Exact, arrivalTime: 10.69f);
        ai.Kbi(10.64f, [Number(1)]);
        ai.Move(10.69f, OneTakesAlphaSwordSpot, Exact, arrivalTime: 12.40f);
        ai.Move(10.69f, SecondHalfFollowsFourthBlasters, Exact, arrivalTime: 12.90f);
        ai.Kbi(12.14f, [Number(2)]);
        world.Events.Add(12.40f, () => FaceClockwise(1));
        ai.Move(12.55f, FiveAndSixDodgeOnesAlphaSword, Exact, arrivalTime: 13.64f);
        ai.Move(12.67f, TwoBaitsTheCharge, Exact, sprint: true);
        ai.Move(12.90f, ThreeTakesAlphaSwordSpot, Exact, arrivalTime: 15.05f);
        ai.Move(13.84f, FiveAndSixClearTheCharge, Exact, arrivalTime: 15.05f);
        world.Events.Add(14.10f, () => FaceCruiseChaser(2));
        ai.Move(15.05f, SevenAndEightLineUpBehindThree, Exact, arrivalTime: 17.16f);
        ai.Kbi(15.31f, [Number(3)]);
        ai.Move(15.34f, FirstHalfFollowsSixthBlasters, Exact, arrivalTime: 17.25f);
        world.Events.Add(16.00f, () => FaceClockwise(3));
        ai.Kbi(16.80f, [Number(4)]);
        ai.Move(17.16f, SevenAndEightDodgeThreesAlphaSword, Exact, arrivalTime: 18.31f);
        ai.Move(17.25f, FirstHalfFollowsSeventhBlasters, Exact, arrivalTime: 19.49f);
        ai.Move(17.41f, FourBaitsTheCharge, Exact, sprint: true);
        ai.Move(18.51f, ThreeSevenAndEightClearTheCharge, Exact, arrivalTime: 19.80f);
        world.Events.Add(19.10f, () => FaceCruiseChaser(4));
        ai.Move(19.49f, FiveTakesAlphaSwordSpot, Exact, arrivalTime: 21.68f);
        ai.Move(19.80f, ThreeSevenAndEightFollowEighthBlasters, Exact, arrivalTime: 21.68f);
        ai.Kbi(19.95f, [Number(5)]);
        ai.Move(20.00f, FourFollowsEighthBlasters, Exact, arrivalTime: 21.68f);
        ai.Kbi(21.42f, [Number(6)]);
        world.Events.Add(21.65f, () => FaceClockwise(5));
        ai.Move(21.68f, SecondHalfFollowsNinthBlasters, Exact, arrivalTime: 23.83f);
        ai.Move(21.80f, OneAndTwoDodgeFivesAlphaSword, Exact, arrivalTime: 22.95f);
        ai.Move(22.05f, SixBaitsTheCharge, Exact, sprint: true);
        ai.Move(23.15f, OneAndTwoClearTheCharge, Exact, arrivalTime: 24.42f);
        world.Events.Add(23.60f, () => FaceCruiseChaser(6));
        ai.Move(23.83f, SevenTakesAlphaSwordSpot, Exact, arrivalTime: 26.07f);
        ai.Kbi(24.58f, [Number(7)]);
        ai.Move(24.62f, FirstHalfLeavesSevensAlphaSword, Exact, arrivalTime: 27.58f);
        ai.Kbi(26.05f, [Number(8)]);
        world.Events.Add(26.10f, () => FaceClockwise(7));
        ai.Move(26.50f, ThreeAndFourDodgeSevensAlphaSword, Exact, arrivalTime: 27.58f);
        ai.Move(26.75f, EightBaitsTheCharge, Exact, sprint: true);
        ai.Move(27.78f, ThreeAndFourClearTheCharge, Exact, arrivalTime: 29.05f);
        world.Events.Add(28.20f, () => FaceCruiseChaser(8));
        ai.Move(29.25f, StackInCentre);
    }

    private static IAiMove StackInCentre() => AiMove.All(Vector2.Zero);

    private IAiMove FanOutNorthOfCentre() => InHalves(
        new(-0.8f, -8.0f), new(-1.7f, -7.5f), new(-1.5f, -5.2f), new(-1.4f, -6.4f),
        new(-1.6f, -6.2f), new(-0.5f, -2.0f), new(-3.6f, -5.1f), new(-4.0f, -5.1f));

    private IAiMove FollowSecondBlasters() => InHalves(
        new(8.9f, -9.6f), new(5.5f, -12.5f), new(4.5f, -11.1f), new(5.4f, -12.2f),
        new(7.3f, -12.7f), new(5.7f, -12.4f), new(2.3f, -15.0f), new(0.9f, -15.1f));

    private IAiMove OneTakesAlphaSwordSpot() => InHalves(
        new(13.0f, -0.5f), new(11.4f, -7.8f), null, null,
        new(15.6f, -5.4f), new(14.9f, -6.7f), null, null);

    private IAiMove SecondHalfFollowsFourthBlasters() => InHalves(
        null, null, new(13.3f, -2.2f), new(13.3f, -3.6f),
        null, null, new(14.0f, -7.2f), new(13.3f, -8.4f));

    private IAiMove ThreeTakesAlphaSwordSpot() => InHalves(
        null, null, new(10.0f, 9.0f), new(13.3f, 5.7f),
        null, null, new(16.4f, 5.7f), new(16.9f, 4.2f));

    private IAiMove FiveAndSixClearTheCharge() => InHalves(
        null, null, null, null,
        new(17.8f, 3.9f), new(18.0f, 2.1f), null, null);

    private IAiMove SevenAndEightLineUpBehindThree() => InHalves(
        null, null, null, null,
        null, null, new(14.8f, 10.8f), new(15.8f, 9.2f));

    private IAiMove FirstHalfFollowsSixthBlasters() => InHalves(
        new(15.1f, 10.4f), new(15.6f, 6.1f), null, null,
        new(10.3f, 9.2f), new(11.7f, 7.4f), null, null);

    private IAiMove FirstHalfFollowsSeventhBlasters() => InHalves(
        new(3.8f, 17.7f), new(7.0f, 16.4f), null, null,
        new(1.0f, 13.7f), new(5.3f, 12.8f), null, null);

    private IAiMove ThreeSevenAndEightClearTheCharge() => InHalves(
        null, null, new(5.0f, 15.0f), null,
        null, null, new(9.2f, 15.5f), new(9.9f, 15.0f));

    private IAiMove FiveTakesAlphaSwordSpot() => InHalves(
        new(-9.7f, 15.3f), new(-6.8f, 16.4f), null, null,
        new(-9.0f, 10.2f), new(-2.4f, 13.6f), null, null);

    private IAiMove ThreeSevenAndEightFollowEighthBlasters() => InHalves(
        null, null, new(-0.5f, 18.2f), null,
        null, null, new(-3.0f, 13.0f), new(-0.6f, 14.6f));

    private IAiMove FourFollowsEighthBlasters() => InHalves(
        null, null, null, new(6.6f, 12.6f),
        null, null, null, null);

    private IAiMove SecondHalfFollowsNinthBlasters() => InHalves(
        null, null, new(-12.2f, 11.3f), new(-6.6f, 11.4f),
        null, null, new(-13.7f, 1.6f), new(-12.6f, 9.0f));

    private IAiMove OneAndTwoClearTheCharge() => InHalves(
        new(-12.5f, 13.4f), new(-13.5f, 12.3f), null, null,
        null, null, null, null);

    private IAiMove FirstHalfLeavesSevensAlphaSword() => InHalves(
        new(-16.3f, -5.6f), new(-16.2f, -8.5f), null, null,
        new(-17.2f, -5.0f), new(-17.2f, 0.9f), null, null);

    private IAiMove SevenTakesAlphaSwordSpot() => InHalves(
        null, null, new(-15.0f, -2.7f), new(-13.5f, -1.0f),
        null, null, new(-10.1f, -9.2f), new(-13.4f, -2.6f));

    private IAiMove ThreeAndFourClearTheCharge() => InHalves(
        null, null, new(-11.8f, -13.6f), new(-13.2f, -12.6f),
        null, null, null, null);

    private IAiMove FiveAndSixDodgeOnesAlphaSword() => OutOfAlphaSword(1, 5, 6);

    private IAiMove SevenAndEightDodgeThreesAlphaSword() => OutOfAlphaSword(3, 7, 8);

    private IAiMove OneAndTwoDodgeFivesAlphaSword() => OutOfAlphaSword(5, 1, 2);

    private IAiMove ThreeAndFourDodgeSevensAlphaSword() => OutOfAlphaSword(7, 3, 4);

    private IAiMove TwoBaitsTheCharge() => BehindCruiseChaser(2);

    private IAiMove FourBaitsTheCharge() => BehindCruiseChaser(4);

    private IAiMove SixBaitsTheCharge() => BehindCruiseChaser(6);

    private IAiMove EightBaitsTheCharge() => BehindCruiseChaser(8);

    private IAiMove InHalves(params Vector2?[] byNumber)
    {
        for (var number = 1; number <= 8; number++)
            if (byNumber[number - 1] is { } spot && OddAhead(number, world.Events.Elapsed) is { } odd)
                byNumber[number - 1] = BehindPlayerOdd(odd, spot);
        var heading = float.DegreesToRadians(FirstHalfHeading());
        return AiMove.Create(byNumber).Assignments(state.Numbers).ApplyPositions(p =>
        {
            foreach (var number in SecondHalf) p.Rotate(number - 1, MathF.PI);
            p.Rotate(heading);
        });
    }

    private static int? OddAhead(int number, float now) => number switch
    {
        5 or 6 when now < 14.14f => 1,
        1 or 2 when now > 14.14f && now < 23.42f => 5,
        7 or 8 when now < 18.80f => 3,
        3 or 4 when now > 18.80f && now < 28.05f => 7,
        _ => null,
    };

    private Vector2 BehindPlayerOdd(int odd, Vector2 spot)
    {
        if (Member(odd) is not { } baiter || !baiter.IsAlive() || world.Party.IsBotDriven(baiter)) return spot;
        var baiterAt = InHalf(odd, Flat(baiter.Position));
        if (baiterAt.LengthSquared() < 0.01f || WrapDegrees(CompassDegrees(spot) - CompassDegrees(baiterAt)) <= 0f) return spot;
        return Vector2.Normalize(baiterAt) * spot.Length();
    }

    private IAiMove OutOfAlphaSword(int odd, params int[] numbers)
    {
        var coords = new Vector2?[8];
        if (state.Objects.CruiseChaser is { } cruiseChaser && Flat(cruiseChaser.Position) is var lineUp && lineUp.LengthSquared() > 0.01f)
            foreach (var number in numbers) coords[number - 1] = OnEdgeOutsideCruiseChaser(odd, lineUp);
        return AiMove.Create(coords).Assignments(state.Numbers);
    }

    private Vector2 OnEdgeOutsideCruiseChaser(int odd, Vector2 cruiseChaser)
    {
        var spot = Vector2.Normalize(cruiseChaser) * ArenaEdge;
        if (Member(odd) is not { } baiter || !baiter.IsAlive() || Flat(baiter.Position) - cruiseChaser is var toBaiter && toBaiter.LengthSquared() < 0.01f)
            return spot;
        var cone = Vector2.Normalize(toBaiter);
        if (Vector2.Dot(Vector2.Normalize(spot - cruiseChaser), cone) < AlphaSwordClearCos) return spot;
        var behind = cruiseChaser - cone * BehindCruiseChaserOutOfAlphaSword;
        return behind.Length() > ArenaEdge ? Vector2.Normalize(behind) * ArenaEdge : behind;
    }

    private IAiMove BehindCruiseChaser(int even)
    {
        if (state.Objects.CruiseChaser is not { } cruiseChaser) return AiMove.Single(Number(even), null);
        var position = Flat(cruiseChaser.Position);
        var counterclockwise = position.LengthSquared() > 1f
            ? Vector2.Normalize(new Vector2(position.Y, -position.X))
            : -new Vector2(MathF.Sin(cruiseChaser.Rotation), MathF.Cos(cruiseChaser.Rotation));
        return AiMove.Single(Number(even), position + counterclockwise * EvenBehindCruiseChaser);
    }

    private void FaceClockwise(int number)
    {
        if (Bot(number) is not { } member) return;
        var position = Flat(member.Position);
        var clockwise = Vector2.Normalize(new Vector2(-position.Y, position.X));
        member.Face(member.Position + new Vector3(clockwise.X, 0f, clockwise.Y));
    }

    private void FaceCruiseChaser(int number)
    {
        if (Bot(number) is { } member && state.Objects.CruiseChaser is { } cruiseChaser) member.Face(cruiseChaser.Position);
    }

    private PartyRole Number(int number) => state.Numbers[number - 1];

    private SimCharacter? Member(int number) => world.Party.Get(Number(number));

    private SimCharacter? Bot(int number)
        => Member(number) is { } member && member.IsAlive() && world.Party.IsBotDriven(member) ? member : null;

    private float FirstHalfHeading() => state.FirstBlasterAxis switch { 0 => 0f, 1 => 45f, 2 => 270f, _ => 315f };

    private Vector2 InHalf(int number, Vector2 position)
    {
        var heading = float.DegreesToRadians(-FirstHalfHeading() - (SecondHalf.Contains(number) ? 180f : 0f));
        return new Vector2(position.X * MathF.Cos(heading) - position.Y * MathF.Sin(heading), position.X * MathF.Sin(heading) + position.Y * MathF.Cos(heading));
    }

    private static float CompassDegrees(Vector2 v) => float.RadiansToDegrees(MathF.Atan2(v.X, -v.Y));

    private static float WrapDegrees(float degrees) => (degrees % 360f + 540f) % 360f - 180f;

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
