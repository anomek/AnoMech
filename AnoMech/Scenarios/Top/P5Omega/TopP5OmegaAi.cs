using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P5Omega;

public class TopP5OmegaAi : IScenarioAi<TopP5OmegaState>
{
    public string Name => "Standard";

    private TopP5OmegaState state = null!;
    private readonly Random rng = new Random();

    private RoleList? helloWorld1;
    private RoleList? helloWorld2;

    public void Run(TopP5OmegaState s, SimWorld world)
    {
        state = s;
        helloWorld1 = solveHelloWorld1(world.Party);
        var ai = new AiManager(world);
        ai.Move(0f, InitialPositions);
        ai.Move(20f, Dodge(0), arrivalTime: 24f);
        ai.Move(24f, Dodge(1), arrivalTime: 28f);
        ai.Automarker(28f, () => HelloWorldMarkers(helloWorld1));
        ai.Move(32f, HelloWorld1Pos, jitter: 0.1f, arrivalTime: 41f);
        world.Events.Add(46f, () => helloWorld2 = solveHelloWorld2(world.Party));
        ai.Automarker(47f, () => HelloWorldMarkers(helloWorld2));
        ai.Move(48f, GatherMiddle);
        ai.Move(53f, HelloWorld2Pos, arrivalTime: 57f);
        ai.Move(62f, InitialPositions);
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

    private Func<IAiMove> Dodge(int attack)
    {
        return () => AiMove.All(new(0, -1f))
                           .ApplyPositions(
                               AdjustSafeCardinal(attack),
                               AdjustSafeSpot(attack)
                           );
    }

    private Dictionary<PartyRole, Sign> HelloWorldMarkers(RoleList? list)
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

    private IAiMove HelloWorld1Pos()
    {
        return AiMove.Create(
            new(10f, 0),
            new(.2f,-10),
            new(-10, -10),
            new(-10, 10),
            new(0.2f, -19),
            new(19, -4),
            new(19, 4),
            new (0.2f, 19)
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
        .Assignments(helloWorld2?.List)
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
        .Assignments(helloWorld2?.List)
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
        var monitorTarget = monitorTargets();
        var jumpTargets = RoleList.AllExcept(party, monitorTarget[0], monitorTarget[1], state.HelloWorldTargets[0],
                                             state.HelloWorldTargets[1]);
        return new RoleList(party,
            [
                state.HelloWorldTargets[0], state.HelloWorldTargets[1], monitorTarget[0], monitorTarget[1],
                jumpTargets[0], jumpTargets[1], jumpTargets[2], jumpTargets[3]
            ]
        );
    }

    private RoleList solveHelloWorld2(SimParty party)
    {
        List<PartyRole> freeAgents = [];
        List<PartyRole> tethers = [];
        foreach (var role in Enum.GetValues<PartyRole>())
        {
           if  (role == state.HelloWorldTargets[2] || role == state.HelloWorldTargets[3])
               continue;
           if (party.Get(role)?.FindStatus(TopConstants.StatusId.QuickeningDynamis) is { Stacks: 3 })
               tethers.Add(role);
           else
               freeAgents.Add(role);
        }
        freeAgents = freeAgents.Shuffle().ToList();
        tethers = tethers.Shuffle().ToList();
        return new RoleList(party,
            [
                state.HelloWorldTargets[2], state.HelloWorldTargets[3], tethers[0], tethers[1],
                freeAgents[0], freeAgents[1], freeAgents[2], freeAgents[3]
            ]);
    }


    private List<PartyRole> monitorTargets()
    {
        List<PartyRole> mustTakeMonitor = [];
        List<PartyRole> canTakeMonitor = [];
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (state.HelloWorldTargets[0] == role || state.HelloWorldTargets[1] == role)
                continue;
            if (!state.DoubleDynamicTargets.Contains(role))
                continue;

            if (state.HelloWorldTargets[2] == role || state.HelloWorldTargets[3] == role)
                mustTakeMonitor.Add(role);
            else
                canTakeMonitor.Add(role);
        }

        while (mustTakeMonitor.Count < 2)
        {
            var selected = canTakeMonitor[rng.Next(canTakeMonitor.Count)];
            canTakeMonitor.Remove(selected);
            mustTakeMonitor.Add(selected);
        }

        return mustTakeMonitor.Shuffle().ToList();
    }
}
