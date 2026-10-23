using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.I1LimitCut;

public sealed class TeaI1LimitCutAi : IScenarioAi<TeaI1LimitCutState>
{
    public string Name => "1256";

    private const float EvenBehindCruiseChaser = 3f;
    private const float HitHold = 0.2f;
    private const float SteerEvery = 0.15f;
    private const float SteerLead = 0.3f;
    private const float StandBelow = 0.8f;
    private const float WalkSpeed = 2.4f;

    private static readonly float[] KnockbackImmunityAt = [9.64f, 11.14f, 14.31f, 15.80f, 18.95f, 20.42f, 23.58f, 25.05f];
    private static readonly float[] FaceAt = [11.40f, 13.10f, 15.00f, 18.10f, 20.65f, 22.60f, 25.10f, 27.20f];
    private static readonly float[] BehindCruiseChaserAt = [11.67f, 16.41f, 21.05f, 25.75f];
    private static readonly float[] HitAt = [12.64f, 14.14f, 17.31f, 18.80f, 21.95f, 23.42f, 26.58f, 28.05f];
    private static readonly (float From, float To)[] SprintWindows = [(7.7f, 17.7f), default, default, default, (14.4f, 24.4f), default, (20.4f, 30.4f), default];

    private static readonly (float At, float Degrees, float Radius)[][] Paths =
    [
        [(6.0f, -151f, 0.3f), (6.5f, -37f, 1.6f), (6.7f, -26f, 2.9f), (7.2f, -9f, 6.5f), (8.1f, 1f, 11.8f), (8.5f, 12f, 12.5f), (9.1f, 35f, 12.8f),
         (10.2f, 53f, 13.1f), (11.0f, 83f, 13.0f), (11.4f, 88f, 13.0f), (12.5f, 88f, 13.1f), (13.2f, 90f, 15.9f), (13.4f, 94f, 17.0f),
         (13.8f, 109f, 18.2f), (14.2f, 117f, 18.3f), (14.7f, 112f, 18.3f), (15.3f, 123f, 18.3f), (16.7f, 125f, 18.0f), (17.5f, 148f, 15.5f),
         (18.1f, 166f, 15.5f), (18.9f, 176f, 15.5f), (19.5f, -165f, 15.5f), (20.3f, -145f, 17.9f), (22.4f, -141f, 18.3f), (22.9f, -127f, 17.3f),
         (23.3f, -120f, 16.0f), (24.2f, -104f, 17.9f), (25.1f, -87f, 18.3f), (25.3f, -85f, 18.3f), (25.8f, -85f, 18.3f), (26.2f, -80f, 18.3f),
         (27.2f, -57f, 15.7f), (27.6f, -48f, 16.8f), (28.0f, -35f, 17.6f), (28.3f, -30f, 15.7f), (28.7f, -28f, 9.8f), (29.0f, -35f, 5.9f),
         (29.3f, -47f, 4.0f), (30.0f, -63f, 3.1f)],
        [(6.0f, -8f, 0.3f), (6.6f, -24f, 2.1f), (8.1f, -6f, 11.2f), (8.8f, 3f, 13.2f), (9.4f, 14f, 13.8f), (9.9f, 30f, 13.7f), (10.4f, 34f, 13.6f),
         (10.9f, 59f, 13.6f), (11.1f, 61f, 13.6f), (11.6f, 55f, 13.8f), (12.1f, 60f, 13.8f), (14.0f, 67f, 14.0f), (14.4f, 77f, 14.6f),
         (14.9f, 107f, 16.9f), (15.2f, 117f, 18.0f), (16.6f, 118f, 18.2f), (16.8f, 121f, 17.7f), (17.5f, 142f, 15.5f), (18.1f, 160f, 15.5f),
         (18.9f, 170f, 15.5f), (19.5f, -171f, 15.5f), (20.3f, -151f, 17.9f), (22.4f, -147f, 18.3f), (23.0f, -131f, 17.0f), (23.4f, -125f, 16.0f),
         (23.9f, -108f, 17.4f), (24.5f, -102f, 18.3f), (26.1f, -70f, 18.3f), (27.0f, -56f, 18.3f), (27.3f, -56f, 18.3f), (28.1f, -66f, 17.7f),
         (28.4f, -65f, 15.3f), (29.2f, -62f, 6.0f), (29.6f, -63f, 3.4f), (30.0f, -81f, 2.0f)],
        [(6.0f, -123f, 0.4f), (6.6f, -87f, 0.9f), (7.0f, -31f, 2.3f), (7.6f, -14f, 6.0f), (8.3f, -11f, 9.6f), (9.1f, 4f, 12.0f),
         (10.2f, 36f, 12.1f), (10.9f, 58f, 13.4f), (11.8f, 79f, 13.5f), (12.4f, 88f, 13.6f), (13.1f, 108f, 13.7f), (14.1f, 129f, 13.4f),
         (14.4f, 132f, 13.5f), (17.3f, 132f, 13.7f), (18.8f, 170f, 16.6f), (19.3f, 164f, 17.9f), (19.6f, 171f, 18.1f), (21.2f, -145f, 17.1f),
         (21.7f, -136f, 16.7f), (22.2f, -120f, 16.9f), (23.1f, -95f, 15.9f), (23.6f, -83f, 16.7f), (24.3f, -60f, 18.3f), (24.7f, -53f, 18.3f),
         (27.1f, -51f, 18.3f), (27.7f, -40f, 18.3f), (27.9f, -38f, 18.3f), (28.1f, -38f, 16.8f), (28.2f, -63f, 15.7f), (28.4f, -148f, 13.0f),
         (28.6f, 119f, 9.2f), (28.7f, 98f, 7.4f), (28.9f, 98f, 4.4f), (29.2f, 100f, 1.8f), (30.0f, 92f, 0.8f)],
        [(6.0f, -57f, 0.3f), (6.8f, -28f, 1.8f), (7.1f, -13f, 3.7f), (7.7f, -12f, 7.8f), (8.3f, -1f, 10.3f), (9.0f, 17f, 11.8f),
         (9.5f, 17f, 12.9f), (9.8f, 26f, 13.6f), (10.5f, 40f, 14.0f), (11.5f, 69f, 13.8f), (12.5f, 83f, 13.9f), (13.4f, 105f, 13.9f),
         (14.1f, 114f, 13.4f), (16.6f, 113f, 14.6f), (18.5f, 113f, 13.8f), (18.9f, 118f, 14.0f), (19.1f, 128f, 14.7f), (19.5f, 157f, 16.6f),
         (19.8f, 171f, 17.5f), (21.2f, -151f, 17.1f), (21.8f, -139f, 16.7f), (22.3f, -122f, 16.8f), (23.1f, -101f, 15.9f), (24.5f, -61f, 18.3f),
         (26.3f, -56f, 18.3f), (27.0f, -58f, 18.3f), (27.6f, -47f, 18.3f), (27.9f, -44f, 18.3f), (28.1f, -44f, 16.9f), (28.4f, -54f, 12.6f),
         (28.6f, -54f, 8.7f), (28.8f, -46f, 4.7f), (29.2f, -39f, 0.8f), (30.0f, 166f, 0.4f)],
        [(6.0f, 14f, 0.6f), (6.4f, 31f, 0.8f), (6.8f, -28f, 2.0f), (7.0f, -32f, 2.7f), (7.4f, -13f, 6.7f), (7.9f, -8f, 13.2f), (8.2f, -3f, 15.2f),
         (9.1f, 29f, 15.5f), (9.9f, 40f, 15.5f), (10.3f, 50f, 15.5f), (10.9f, 74f, 16.8f), (11.2f, 81f, 17.7f), (13.2f, 84f, 18.3f),
         (13.4f, 88f, 18.3f), (13.8f, 103f, 18.3f), (14.1f, 109f, 18.2f), (14.5f, 115f, 17.2f), (15.2f, 129f, 14.1f), (16.8f, 133f, 13.7f),
         (18.1f, 172f, 13.8f), (18.8f, 179f, 13.7f), (20.3f, -139f, 13.5f), (21.9f, -137f, 13.8f), (22.5f, -134f, 15.9f), (23.0f, -119f, 16.7f),
         (23.4f, -113f, 15.7f), (23.7f, -106f, 15.5f), (24.8f, -71f, 16.1f), (25.4f, -64f, 18.0f), (25.9f, -54f, 18.3f), (27.4f, -47f, 18.3f),
         (27.9f, -38f, 17.4f), (28.2f, -33f, 15.1f), (28.6f, -31f, 9.0f), (28.8f, -17f, 5.7f), (28.9f, -13f, 4.3f), (29.0f, -25f, 3.1f),
         (29.2f, 4f, 1.4f), (29.5f, 30f, 1.0f), (30.0f, -36f, 1.3f)],
        [(6.0f, 146f, 0.4f), (6.8f, -115f, 0.6f), (7.2f, 168f, 1.2f), (7.3f, 118f, 2.0f), (7.7f, -16f, 7.9f), (8.1f, -11f, 13.9f),
         (8.4f, -3f, 15.6f), (9.1f, 23f, 15.5f), (9.9f, 34f, 15.5f), (10.3f, 44f, 15.5f), (10.9f, 68f, 16.8f), (11.2f, 75f, 17.7f),
         (13.1f, 77f, 18.3f), (13.3f, 79f, 18.3f), (13.8f, 97f, 18.3f), (14.2f, 105f, 18.2f), (14.7f, 107f, 17.0f), (15.5f, 120f, 13.8f),
         (16.5f, 123f, 13.9f), (17.1f, 130f, 14.3f), (18.3f, 156f, 13.7f), (19.2f, 164f, 14.1f), (20.7f, -169f, 13.8f), (23.2f, -160f, 14.0f),
         (25.1f, -117f, 16.7f), (25.7f, -106f, 16.4f), (26.6f, -86f, 18.3f), (27.1f, -77f, 18.3f), (27.5f, -76f, 17.9f), (28.1f, -74f, 15.4f),
         (28.3f, -74f, 13.4f), (28.4f, -78f, 11.8f), (28.9f, -122f, 5.0f), (29.2f, -130f, 2.6f), (30.0f, -117f, 1.0f)],
        [(6.0f, -110f, 0.1f), (6.7f, -17f, 2.2f), (7.3f, -14f, 6.9f), (7.9f, -18f, 14.9f), (8.3f, -17f, 18.1f), (9.3f, 3f, 15.6f),
         (10.9f, 52f, 15.5f), (11.7f, 71f, 15.5f), (12.4f, 82f, 15.7f), (13.1f, 102f, 15.9f), (14.0f, 121f, 18.3f), (14.4f, 126f, 18.3f),
         (17.4f, 127f, 18.3f), (18.3f, 152f, 15.9f), (18.8f, 164f, 16.2f), (19.4f, 171f, 14.8f), (20.5f, -159f, 13.3f), (21.2f, -139f, 13.1f),
         (21.8f, -127f, 13.5f), (22.4f, -108f, 14.0f), (23.5f, -80f, 13.7f), (24.5f, -49f, 13.7f), (26.4f, -44f, 13.5f), (26.8f, -45f, 14.4f),
         (27.2f, -44f, 16.4f), (27.6f, -35f, 18.0f), (27.9f, -32f, 18.3f), (28.3f, -30f, 16.3f), (28.6f, -19f, 13.2f), (28.9f, -13f, 9.5f),
         (29.5f, -24f, 4.9f), (30.0f, -23f, 3.0f)],
        [(6.0f, -92f, 0.4f), (6.4f, -42f, 1.4f), (6.7f, -31f, 3.0f), (7.2f, -22f, 6.4f), (7.9f, -25f, 15.1f), (8.3f, -23f, 18.1f),
         (9.3f, -3f, 15.6f), (10.9f, 46f, 15.5f), (11.7f, 65f, 15.5f), (12.4f, 76f, 15.7f), (13.1f, 96f, 15.9f), (14.0f, 115f, 18.3f),
         (14.4f, 120f, 18.3f), (17.4f, 121f, 18.3f), (18.3f, 146f, 15.9f), (18.8f, 158f, 16.4f), (19.3f, 155f, 16.2f), (19.8f, 167f, 15.2f),
         (20.2f, 168f, 14.9f), (21.1f, -164f, 14.5f), (21.6f, -156f, 14.7f), (22.4f, -134f, 15.4f), (24.2f, -99f, 15.8f), (24.6f, -92f, 14.8f),
         (25.1f, -78f, 13.5f), (25.5f, -70f, 13.7f), (28.2f, -70f, 14.5f), (28.6f, -68f, 12.9f), (29.2f, -58f, 9.5f), (30.0f, -45f, 7.6f)],
    ];

    private TeaI1LimitCutState state = null!;
    private SimWorld world = null!;

    public void Run(TeaI1LimitCutState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);

        ai.Move(0.3f, StackInCentre);
        for (var number = 1; number <= 8; number++)
        {
            var n = number;
            ai.UseAction(KnockbackImmunityAt[n - 1], state.Numbers[n - 1], KnockbackImmunity.ActionId);
            if (n % 2 == 1) world.Events.Add(FaceAt[n - 1], () => FaceClockwise(n));
            else
            {
                world.Events.Add(FaceAt[n - 1], () => FaceCruiseChaser(n));
                ai.Move(BehindCruiseChaserAt[n / 2 - 1], () => ByNumber((n, BehindCruiseChaser())));
            }
            var window = SprintWindows[n - 1];
            if (window.To > window.From)
                world.Events.Add(window.From, () => (Member(n) as ISimPartyMember)?.UseSprint(window.To - window.From));
            world.Events.Add(Paths[n - 1][0].At, () => Steer(n));
        }
    }

    private void Steer(int number)
    {
        var now = world.Events.Elapsed;
        if (now >= Paths[number - 1][^1].At) return;
        world.Events.Add(SteerEvery, () => Steer(number));
        if (Holding(number, now) || Member(number) is not { } member || !member.IsAlive()) return;
        var to = PathAt(number, now + SteerLead);
        var speed = Vector2.Distance(Flat(member.Position), to) / SteerLead;
        if (speed < StandBelow) return;
        var window = SprintWindows[number - 1];
        var top = now >= window.From && now < window.To ? AiManager.SprintSpeed : AiManager.RunSpeed;
        member.MoveTo(new Vector3(to.X, 0f, to.Y), Math.Clamp(speed, WalkSpeed, top));
    }

    private bool Holding(int number, float time)
    {
        var from = number % 2 == 1 ? FaceAt[number - 1] : BehindCruiseChaserAt[number / 2 - 1];
        return time > from && time <= HitAt[number - 1] + HitHold;
    }

    private Vector2 PathAt(int number, float time)
    {
        var path = Paths[number - 1];
        if (time <= path[0].At) return Polar(number, path[0].Degrees, path[0].Radius);
        for (var i = 1; i < path.Length; i++)
        {
            if (time > path[i].At) continue;
            var (from, to) = (path[i - 1], path[i]);
            var f = (time - from.At) / (to.At - from.At);
            var turn = ((to.Degrees - from.Degrees) % 360f + 540f) % 360f - 180f;
            return Polar(number, from.Degrees + turn * f, from.Radius + (to.Radius - from.Radius) * f);
        }
        return Polar(number, path[^1].Degrees, path[^1].Radius);
    }

    private IAiMove StackInCentre()
    {
        var coords = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++)
            coords[slot] = Flat(Geometry.OnCompass(45f * slot, 1f));
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove ByNumber(params (int Number, Vector2 Spot)[] spots)
    {
        var coords = new Vector2?[8];
        foreach (var (number, spot) in spots) coords[number - 1] = spot;
        return AiMove.Create(coords).Assignments(state.Numbers);
    }

    private Vector2 BehindCruiseChaser()
    {
        if (state.Objects.CruiseChaser is not { } cruiseChaser) return Vector2.Zero;
        return Flat(cruiseChaser.Position) - Forward(cruiseChaser.Rotation) * EvenBehindCruiseChaser;
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

    private SimCharacter? Member(int number) => world.Party.Get(state.Numbers[number - 1]);

    private SimCharacter? Bot(int number)
    {
        var member = Member(number);
        return member is not null && member.IsAlive() && world.Party.IsBotDriven(member) ? member : null;
    }

    private float GroupHeading(int number)
    {
        var firstWaveEndFromWestToNortheast = state.FirstBlasterAxis switch { 0 => 0f, 1 => 45f, 2 => 270f, _ => 315f };
        return number is 1 or 2 or 5 or 6 ? firstWaveEndFromWestToNortheast : firstWaveEndFromWestToNortheast + 180f;
    }

    private Vector2 Polar(int number, float degrees, float radius) => Flat(Geometry.OnCompass(GroupHeading(number) + degrees, radius));

    private static Vector2 Forward(float rotation) => new(MathF.Sin(rotation), MathF.Cos(rotation));

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
