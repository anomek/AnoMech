using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P4BlueScreen;

public sealed class TopP4BlueScreenAi : IScenarioAi<TopP4BlueScreenState>
{
    public string Name => "NAUR";

    private const float MovePrompt = 0.3f;

    private static readonly Dictionary<PartyRole, Vector2> LineupSpots = new()
    {
        [PartyRole.MainTank] = new(-13.4f, -5.5f), [PartyRole.CasterDps] = new(-14.2f, 0.1f),
        [PartyRole.RegenHealer] = new(-12.3f, 5.4f), [PartyRole.MeleeDpsA] = new(-9.9f, 9.9f),
        [PartyRole.OffTank] = new(12.8f, -5.2f), [PartyRole.PhysRangedDps] = new(13.8f, 0f),
        [PartyRole.ShieldHealer] = new(12.3f, 5.3f), [PartyRole.MeleeDpsB] = new(9.4f, 9.4f),
    };

    private static readonly Dictionary<PartyRole, Vector2> BlueScreenSpots = new()
    {
        [PartyRole.MainTank] = new(-0.9f, 7.3f), [PartyRole.CasterDps] = new(-3.9f, 9.3f),
        [PartyRole.RegenHealer] = new(-2.8f, 7.2f), [PartyRole.MeleeDpsA] = new(-3.0f, 8.4f),
        [PartyRole.OffTank] = new(6.1f, 4.8f), [PartyRole.PhysRangedDps] = new(0f, 8.7f),
        [PartyRole.ShieldHealer] = new(1.1f, 8.1f), [PartyRole.MeleeDpsB] = new(3.6f, 7.7f),
    };

    private static readonly Vector2 WestStack = new(-4.8f, 13.5f);
    private static readonly Vector2 EastStack = new(4.8f, 13.5f);
    private const float StackHuddle = 0.7f;
    private const float RingRadius = 14f;
    private const float MaxRingStep = 50f;
    private const float InsideRepeaterRadius = 9.5f;
    private const float StepSlack = 0.4f;

    private TopP4BlueScreenState state = null!;
    private AiManager ai = null!;

    public void Run(TopP4BlueScreenState s, SimWorld world)
    {
        state = s;
        ai = new AiManager(world);

        LineUp(0.5f);
        TakeStacks(24.40f, 0);
        WalkBackAlongTheRing(29.45f, 0);
        DuckUnderTheRepeaterOnTheWayToTheStacks(34.45f, 35.90f, 1);
        LineUp(39.60f);
        TakeStacks(44.80f, 2);
        ClumpSouthForBlueScreen(51.25f);
    }

    private void LineUp(float time)
    {
        foreach (var role in PerRole.All)
            Go(time, role, LineupSpots[role]);
    }

    private void TakeStacks(float time, int set)
    {
        foreach (var role in PerRole.All)
            Go(time, role, () => StackSpot(set, role));
    }

    private void WalkBackAlongTheRing(float time, int set)
    {
        foreach (var role in PerRole.All)
        {
            var from = StackCentre(set, role);
            var at = time;
            foreach (var step in RingPath(from, LineupSpots[role]))
            {
                Go(at, role, step);
                at += Vector2.Distance(from, step) / AiManager.RunSpeed + StepSlack;
                from = step;
            }
        }
    }

    private void DuckUnderTheRepeaterOnTheWayToTheStacks(float duck, float stack, int set)
    {
        foreach (var role in PerRole.All)
        {
            Go(duck, role, InsideTheRepeater(LineupSpots[role], StackCentre(set, role)));
            Go(stack, role, () => StackSpot(set, role));
        }
    }

    private void ClumpSouthForBlueScreen(float time)
    {
        foreach (var role in PerRole.All)
            Go(time, role, BlueScreenSpots[role]);
    }

    private Vector2 StackSpot(int set, PartyRole role)
        => StackCentre(set, role) + TopCompass.Point(StackHuddle, Array.IndexOf(PerRole.All, role) * 45f);

    private Vector2 StackCentre(int set, PartyRole role) => StacksWest(set, role) ? WestStack : EastStack;

    private bool StacksWest(int set, PartyRole role)
    {
        var targets = state.StackTargets[set];
        var westTargets = targets.Count(TopP4BlueScreenState.IsWest);
        if (westTargets == 1) return TopP4BlueScreenState.IsWest(role);
        var crossing = targets.MaxBy(LineupRow);
        if (role == crossing) return westTargets == 0;
        if (role == (westTargets == 2 ? PartyRole.MeleeDpsB : PartyRole.MeleeDpsA)) return westTargets == 2;
        return TopP4BlueScreenState.IsWest(role);
    }

    private static int LineupRow(PartyRole role) => role switch
    {
        PartyRole.MainTank or PartyRole.OffTank => 0,
        PartyRole.CasterDps or PartyRole.PhysRangedDps => 1,
        PartyRole.RegenHealer or PartyRole.ShieldHealer => 2,
        _ => 3,
    };

    private static IEnumerable<Vector2> RingPath(Vector2 from, Vector2 to)
    {
        var start = TopCompass.Bearing(from);
        var turn = TopCompass.Turn(start, TopCompass.Bearing(to));
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(turn) / MaxRingStep));
        for (var k = 1; k < steps; k++)
            yield return TopCompass.Point(RingRadius, start + turn * k / steps);
        yield return to;
    }

    private static Vector2 InsideTheRepeater(Vector2 from, Vector2 to)
    {
        var start = TopCompass.Bearing(from);
        return TopCompass.Point(InsideRepeaterRadius, start + TopCompass.Turn(start, TopCompass.Bearing(to)) / 2f);
    }

    private void Go(float time, PartyRole role, Vector2 spot) => Go(time, role, () => spot);

    private void Go(float time, PartyRole role, Func<Vector2> spot)
        => ai.Move(time + MovePrompt, () => AiMove.Single(role, spot()), jitter: 0.15f);
}
