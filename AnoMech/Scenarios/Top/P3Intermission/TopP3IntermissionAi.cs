using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Top.P3Intermission;

public sealed class TopP3IntermissionAi(bool automarkers) : IScenarioAi<TopP3IntermissionState>
{
    public string Name => automarkers ? "NA (automarkers)" : "NA";

    private const float MovePrompt = 0.3f;

    private static readonly PartyRole[] Conga =
    [
        PartyRole.RegenHealer, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MainTank,
        PartyRole.OffTank, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.ShieldHealer,
    ];
    private const float CongaRow = 10.8f;
    private const float CongaGap = 2.6f;
    private const float DepartureSpread = 0.5f;
    private const float UnhurriedDeparture = 2f;
    private const float CrossingDeparture = 0.5f;
    private const float CrossingRadius = 8f;

    private static readonly float[] SpreadHomes = [270f, 330f, 30f, 90f];
    private static readonly float[] PairHomes = [204f, 156f];

    private const float Wall = 19f;
    private const float UnderTheOuterRing = 15.4f;
    private const float HandEdge = 38f;
    private const float InsideTheHand = 14f;
    private const float TuckRadius = 16.2f;
    private const float PartnerInset = 0.5f;
    private const float ClockRadius = 10f;

    private static readonly Dictionary<PartyRole, float> ClockSpots = new()
    {
        [PartyRole.MainTank] = 0f, [PartyRole.PhysRangedDps] = 45f, [PartyRole.ShieldHealer] = 90f, [PartyRole.MeleeDpsB] = 135f,
        [PartyRole.OffTank] = 180f, [PartyRole.MeleeDpsA] = 225f, [PartyRole.RegenHealer] = 270f, [PartyRole.CasterDps] = 315f,
    };

    private readonly record struct Plan(PartyRole Role, float Home, float Edge, float Tuck, float Inset);

    private TopP3IntermissionState state = null!;
    private Rng noise = null!;
    private AiManager ai = null!;
    private readonly Dictionary<PartyRole, Vector2> planned = new();

    public void Run(TopP3IntermissionState s, SimWorld world)
    {
        state = s;
        noise = world.Stream("top-p3-intermission-ai");
        ai = new AiManager(world);
        planned.Clear();

        LineUpInTheConga(0.5f);
        foreach (var plan in Plans())
        {
            var atHome = HeadHomeToTheWall(LeaveTheConga(plan) + Stagger(), plan);
            HugTheFirstHandsEdge(MathF.Max(14.4f, atHome), plan);
            DuckUnderTheOuterRing(19.5f, plan);
            BackToTheWall(21.8f, plan);
            TuckIntoTheBlownHand(27.3f, plan);
            TakeTheHelloWorldClockSpot(30.1f + Stagger(), plan);
        }

        if (!automarkers) return;
        ai.Automarker(10.5f, IntermissionMarkers);
        ai.Automarker(29.5f, () => new Dictionary<PartyRole, Sign>());
    }

    private float LeaveTheConga(Plan plan)
    {
        var debuffDeparture = state.Spreads.Contains(plan.Role) ? 10.5f : state.Stacks.Contains(plan.Role) ? 11.1f : 12.9f;
        var crossesTheMiddle = ClosestToCentre(planned[plan.Role], TopCompass.Point(Wall, plan.Home)) < CrossingRadius;
        return debuffDeparture + (crossesTheMiddle ? CrossingDeparture : UnhurriedDeparture);
    }

    private static float ClosestToCentre(Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var along = Math.Clamp(-Vector2.Dot(from, segment) / segment.LengthSquared(), 0f, 1f);
        return (from + segment * along).Length();
    }

    private Dictionary<PartyRole, Sign> IntermissionMarkers()
    {
        var marks = new Dictionary<PartyRole, Sign>();
        Sign[] spreadSigns = [Sign.Attack1, Sign.Attack2, Sign.Attack3, Sign.Attack4];
        Sign[] stackSigns = [Sign.Bind1, Sign.Bind2];
        Sign[] unmarkedSigns = [Sign.Ignore1, Sign.Ignore2];
        var spreads = Conga.Where(role => state.Spreads.Contains(role)).ToList();
        var stacks = Conga.Where(role => state.Stacks.Contains(role)).ToList();
        var unmarked = Conga.Where(role => state.Unmarked.Contains(role)).ToList();
        for (var i = 0; i < spreads.Count && i < spreadSigns.Length; i++) marks[spreads[i]] = spreadSigns[i];
        for (var i = 0; i < stacks.Count && i < stackSigns.Length; i++) marks[stacks[i]] = stackSigns[i];
        for (var i = 0; i < unmarked.Count && i < unmarkedSigns.Length; i++) marks[unmarked[i]] = unmarkedSigns[i];
        return marks;
    }

    private float Stagger() => noise.NextFloat(0f, DepartureSpread);

    private void LineUpInTheConga(float time)
    {
        for (var slot = 0; slot < Conga.Length; slot++)
        {
            var role = Conga[slot];
            var spot = new Vector2((slot - 3.5f) * CongaGap, CongaRow);
            ai.Move(time + MovePrompt, () => AiMove.Single(role, spot));
            planned[role] = spot;
        }
    }

    private float HeadHomeToTheWall(float time, Plan plan)
        => GoAlong(time, plan.Role, [TopCompass.Point(Wall, plan.Home)], 0.2f);

    private void HugTheFirstHandsEdge(float time, Plan plan)
        => GoAlong(time, plan.Role, Arc(Wall, plan.Home, plan.Edge), 0f);

    private void DuckUnderTheOuterRing(float time, Plan plan)
        => GoAlong(time, plan.Role, [TopCompass.Point(UnderTheOuterRing, plan.Edge)], 0f);

    private void BackToTheWall(float time, Plan plan)
        => GoAlong(time, plan.Role, [TopCompass.Point(Wall, plan.Edge)], 0f);

    private void TuckIntoTheBlownHand(float time, Plan plan)
        => GoAlong(time, plan.Role, [TopCompass.Point(TuckRadius - plan.Inset, plan.Tuck)], 0f);

    private void TakeTheHelloWorldClockSpot(float time, Plan plan)
        => GoAlong(time, plan.Role, [TopCompass.Point(ClockRadius, plan.Tuck), .. Arc(ClockRadius, plan.Tuck, ClockSpots[plan.Role])], 0.2f);

    private IEnumerable<Plan> Plans()
    {
        var spreads = Conga.Where(role => state.Spreads.Contains(role)).ToList();
        for (var i = 0; i < spreads.Count; i++)
            yield return PlanFor(spreads[i], SpreadHomes[i], 0f);
        var stacks = Conga.Where(role => state.Stacks.Contains(role)).ToList();
        var unmarked = Conga.Where(role => state.Unmarked.Contains(role)).ToList();
        for (var i = 0; i < PairHomes.Length; i++)
        {
            yield return PlanFor(stacks[i], PairHomes[i], 0f);
            yield return PlanFor(unmarked[i], PairHomes[i], PartnerInset);
        }
    }

    private Plan PlanFor(PartyRole role, float home, float inset)
    {
        var hand = state.SetSpots(0).Select(TopP3IntermissionState.SpotBearing).MinBy(bearing => MathF.Abs(TopCompass.Turn(bearing, home)));
        var side = MathF.Sign(TopCompass.Turn(hand, home));
        return new Plan(role, home, hand + side * HandEdge, hand + side * InsideTheHand, inset);
    }

    private float GoAlong(float time, PartyRole role, IEnumerable<Vector2> waypoints, float jitter)
    {
        var from = planned[role];
        foreach (var waypoint in waypoints)
        {
            ai.Move(time + MovePrompt, () => AiMove.Single(role, waypoint), jitter);
            time += Vector2.Distance(from, waypoint) / AiManager.RunSpeed + 0.05f;
            from = waypoint;
        }
        planned[role] = from;
        return time;
    }

    private static List<Vector2> Arc(float radius, float from, float to)
    {
        var turn = TopCompass.Turn(from, to);
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(turn) / 20f));
        return Enumerable.Range(1, steps).Select(i => TopCompass.Point(radius, from + turn * i / steps)).ToList();
    }
}
