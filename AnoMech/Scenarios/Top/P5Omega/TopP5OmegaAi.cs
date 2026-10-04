using System;
using System.Collections.Generic;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Top.P5Omega;

public class TopP5OmegaAi : IScenarioAi<TopP5OmegaState>
{
    public string Name => "Standard";

    private TopP5OmegaState state = null!;

    private RoleList? helloWorld1;

    public void Run(TopP5OmegaState s, SimWorld world)
    {
        state = s;
        helloWorld1 = solveHelloWorld1(world.Party);
        var ai = new AiManager(world);
        ai.Move(0f, InitialPositions);
        ai.Automarker(9.45f, () => HelloWorldMarkers(helloWorld1?.List));
        ai.Move(20f, Dodge(0), arrivalTime: 24f);
        ai.Move(24.6f, Dodge(1), arrivalTime: 28.1f);
        ai.Move(32f, HelloWorld1Pos, jitter: 0.1f, arrivalTime: 41f);
        ai.Automarker(41.5f, () => HelloWorldMarkers(state.HelloWorld2));
        ai.Move(48f, GatherMiddle, jitter: 0f);
        ai.Move(53f, HelloWorld2Pos, arrivalTime: 57f);
        ai.Automarker(59.7f, NoMarkers);
        ai.Move(62f, InitialPositions);
        ai.Move(68f, OffTankOutOfSolarRay);
        ai.Move(76.9f, SwapTankSpots);
        ai.Move(84.5f, BlindFaithLine);
    }


    private IAiMove InitialPositions()
    {
        return AiMove.Create(
            new(-2.10f, -5.08f),
            new(2.10f, -5.08f),
            new(-0.7f, 5.7f),
            new(-0.7f, 6.5f),
            new(-0.7f, 7.3f),
            new(0.7f, 5.7f),
            new(0.7f, 6.5f),
            new(0.7f, 7.3f)
        ).NaturalOrder();
    }

    private IAiMove OffTankOutOfSolarRay()
    {
        return AiMove.Create(null, new(5.9f, -1.2f), null, null, null, null, null, null).NaturalOrder();
    }

    private IAiMove SwapTankSpots()
    {
        return AiMove.Create(new(5.9f, -1.2f), new(-0.4f, -6.4f), null, null, null, null, null, null).NaturalOrder();
    }

    private IAiMove BlindFaithLine()
    {
        return AiMove.Create(
            new(0.3f, 5.0f),
            new(0.1f, -0.6f),
            new(0f, 6.4f),
            new(0.7f, 7.9f),
            new(-0.6f, 2.6f),
            new(0.6f, 2.6f),
            new(0f, 3.9f),
            new(0f, 1.4f)
        ).NaturalOrder();
    }

    private Func<IAiMove> Dodge(int attack)
    {
        return () => AiMove.All(new(0, -1f))
                           .ApplyPositions(
                               AdjustSafeCardinal(attack),
                               AdjustSafeSpot(attack)
                           );
    }

    private Dictionary<PartyRole, Sign> HelloWorldMarkers(IReadOnlyList<PartyRole>? list)
    {
        if (list == null) return [];
        return new Dictionary<PartyRole, Sign>() {
            [list[0]] = Sign.Triangle,
            [list[1]] = Sign.Cross,
            [list[2]] = Sign.Bind1,
            [list[3]] = Sign.Bind2,
            [list[4]] = Sign.Attack1,
            [list[5]] = Sign.Attack2,
            [list[6]] = Sign.Attack3,
            [list[7]] = Sign.Attack4,
        };
    }

    private Dictionary<PartyRole, Sign> NoMarkers() => [];

    private IAiMove HelloWorld1Pos()
    {
        return AiMove.Create(
            new(10f, 0),
            new(1.5f,-10),
            new(-10, -10),
            new(-10, 10),
            new(1.5f, -19),
            new(19, -4),
            new(19, 4),
            new (1.5f, 19)
        )
        .Assignments(helloWorld1?.List)
        .ApplyPositions(AdjustForSafeMonitorSide);
    }

    private IAiMove GatherMiddle()
    {
        return AiMove.Create(
            new (0, 0),
            new (0, 0),
            new(0, -3f),
            new(0, -3f),
            new (0, 0),
            new (0, 0),
            new (0, 0),
            new (0, 0)
        )
        .Assignments(state.HelloWorld2)
        .ApplyPositions(state.BettleSpawnDirection.Apply);
    }

    private IAiMove HelloWorld2Pos()
    {
        return AiMove.Create(
            new (0, 10),
            new (10, 0),
            new (-9.5f, -16.5f),
            new (9.5f, -16.5f),
            new (-19f, 0),
            new (-4, 19f),
            new (4, 19f),
            new (19f, 0)
        )
        .Assignments(state.HelloWorld2)
        .ApplyPositions(state.BettleSpawnDirection.Apply);
    }

    Action<IAiPositions> AdjustSafeCardinal(int attack)
    {
        var startDirection = state.AttackDirections[attack * 2];
        var adjustmentToSafeVertical = startDirection.Index() % 4 == 1 ? -1 : +1;
        var safeAdjustment = state.FirstWaveCannonFront ? -1 : +1;
        var doubleAdjustment = attack == 0 ? 1 : -1;
        var safe = startDirection.Rotate(adjustmentToSafeVertical * safeAdjustment * doubleAdjustment);
        return safe.Apply;
    }

    Action<IAiPositions> AdjustSafeSpot(int attack)
    {
        var attackf = state.OmegaAttacks[attack * 2];
        var attackm = state.OmegaAttacks[attack * 2 + 1];
        float mul;
        if (attackf == OmegaAttack.Legs)
            mul = attackm == OmegaAttack.Sword ? 2.5f : -2.5f;
        else
            mul = attackm == OmegaAttack.Sword ? -17f : -10f;
        return move => move.Multiply(mul);
    }

    private void AdjustForSafeMonitorSide(IAiPositions move)
    {
        move.MultiplyX(state.MonitorSide.Mul);
    }

    private RoleList solveHelloWorld1(SimParty party)
    {
        var jumpTargets = state.HelloWorld1JumpOrder;
        return new RoleList(party,
            [
                state.HelloWorldTargets[0], state.HelloWorldTargets[1], state.MonitorTargets[0], state.MonitorTargets[1],
                jumpTargets[0], jumpTargets[1], jumpTargets[2], jumpTargets[3]
            ]
        );
    }
}
