using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Forsaken;

// Party-member movement choreography for UMAD P2 Forsaken. Reads the shared
// UmadP2ForsakenState so movement stays in sync with the randomized layout, and
// schedules moves through the AiManager. See TopP5DeltaAi for the canonical shape.
public sealed class UmadP2ForsakenAi
{
    private readonly UmadP2ForsakenState state;

    public UmadP2ForsakenAi(UmadP2ForsakenState state)
    {
        this.state = state;
    }

    private IReadOnlyList<PartyRole> alpha = [];
    private IReadOnlyList<PartyRole> beta = [];

    public void Run(SimWorld world)
    {
        var ai = new AiManager(world);
        Init();

        ai.Move(1f, InitialLineup);
        ai.Move(10.16f, TowerPositions(0), jitter: .0f, arrivalTime: 22.16f);
        ai.Move(25.17f, TowerPositions(1), jitter: .0f, arrivalTime: 32.16f);
        ai.Move(33f, AllThingsEndsBait(0), arrivalTime: 37f);
        ai.Move(39.21f, TowerPositions(2), jitter: .0f, arrivalTime: 43.21f);
        ai.Move(47.22f, TowerPositions(3), jitter: .0f, arrivalTime: 53.22f);
        ai.Move(54f, AllThingsEndsBait(1), arrivalTime: 57f);
        ai.Move(59.26f, TowerPositions(4), jitter: .0f, arrivalTime: 63.86f);
        ai.Move(65.27f, TowerPositions(5), jitter: .0f, arrivalTime: 73.27f);
        ai.Move(75f, AllThingsEndsBait(2), arrivalTime: 78f);
        ai.Move(79.31f, TowerPositions(6), jitter: .0f, arrivalTime: 83.8f);
        ai.Move(90.32f, TowerPositions(7), jitter: .0f, arrivalTime: 94.32f);
    }

    private Func<IAiMove> AllThingsEndsBait(int i)
    {
        return () => AiMove.All(new(0, 0)) // they actually dont bait anything, leave it for player to bait
                           .ApplyPositions(AdjustForFuture(i), state.NewNorthAt(2 * i + 2).Apply);
    }

    private Action<IAiPositions> AdjustForFuture(int i)
    {
        return pos =>
        {
            if (state.EndAttacks[i] == EndAttack.PastsEnd)
                pos.Multiply(-1);
        };
    }

    private void Init()
    {
        var stacks = state.Lockons.Where(pair => pair.Value == LockonId.ForsakenStack).Select(pair => pair.Key)
                          .ToList();
        var supportPair = (PartyRole)(((int)stacks[0] + 2) % 4);
        var dpsPair = (PartyRole)((((int)stacks[1] - 2) % 4) + 4);
        var list = new List<PartyRole>([stacks[0], stacks[1], supportPair, dpsPair]);
        list.Sort();
        (list[0], list[1]) = (list[1], list[0]); // swap tank and healer
        alpha = list.AsReadOnly();
        list = Enum.GetValues<PartyRole>()
                   .Where(role => !list.Contains(role))
                   .ToList();
        (list[0], list[1]) = (list[1], list[0]); // swap tank and healer
        beta = list.AsReadOnly();
    }


    private PartyRole ActiveRole(uint mechanic, int towerId, int order)
    {
        var array = towerId is < 3 or 7 ? alpha : beta;
        try
        {
            return array
                   .Where(role => state.Lockons[role] == mechanic)
                   .Skip(order)
                   .First();
        }
        catch (InvalidOperationException)
        {
            Plugin.Log.Warning($"Lockons {string.Join(",", state.Lockons)}");
            Plugin.Log.Warning($"Can't find {mechanic}.{order}, for {towerId}, for {string.Join(",", array)}");
            throw;
        }
    }

    private PartyRole PassiveRole(int towerId, int order)
    {
        var array = towerId is < 3 or 7 ? beta : alpha;
        return array[order];
    }

    private IAiMove InitialLineup()
    {
        return AiMove.Create(
            new(-2.5f, -2.5f),
            new(-2.5f, 2.5f),
            new(-7.5f, -2.5f),
            new(-7.5f, 2.5f),
            new(2.5f, -2.5f),
            new(2.5f, 2.5f),
            new(7.5f, -2.5f),
            new(7.5f, 2.5f)
        );
    }

    private Func<IAiMove> TowerPositions(int i)
    {
        return () => i % 2 == 1 ? EvenTower(i) : OddTower(i); // odd/even flipped because 0 indexing teehee
    }

    private IAiMove OddTower(int i)
    {
        return AiMove.Create(
                         // active grop
                         new(5.5f, -5.5f),  // stack
                         new(8f, -8f),      // cone
                         new(-5.4f, -2.4f), // stack
                         new(-5.4f, -8.5f), // chariot
                         // passive group
                         new(9.5f, -9.5f),  // outer cone bait
                         new(2.5f, -2.5f),  // inner cone bait
                         new(-3.5f, -1.9f), // in stack
                         new(-3.5f, -1.9f)  //  in stack
                     )
                     .Assignments([
                         ActiveRole(LockonId.ForsakenStack, i, 0),
                         ActiveRole(LockonId.ForsakenCone, i, 0),
                         ActiveRole(LockonId.ForsakenStack, i, 1),
                         ActiveRole(LockonId.ForsakenChariot, i, 0),
                         PassiveRole(i, 0),
                         PassiveRole(i, 1),
                         PassiveRole(i, 2),
                         PassiveRole(i, 3)
                     ])
                     .ApplyPositions(state.NewNorthAt(i).Apply);
    }

    private IAiMove EvenTower(int i)
    {
        return AiMove.Create(
                         // active group
                         new(3.2f, -3.2f),  // cone1
                         new(8, -8),        // chariot1
                         new(-3.2f, -3.2f), // cone2
                         new(-8, -8),       // chariot2
                         // passive group
                         new(8.8f, -2.4f),    // cone bait1
                         new(3.8f, 4.2f),  // clone bait1
                         new(-3.8f, 4.2f), // clone bait2
                         new(-8.8f, -2.4f)    // cone bait2 
                     ).Assignments([
                         ActiveRole(LockonId.ForsakenCone, i, 0),
                         ActiveRole(LockonId.ForsakenChariot, i, 0),
                         ActiveRole(LockonId.ForsakenCone, i, 1),
                         ActiveRole(LockonId.ForsakenChariot, i, 1),
                         PassiveRole(i, 0),
                         PassiveRole(i, 1),
                         PassiveRole(i, 2),
                         PassiveRole(i, 3),
                     ])
                     .ApplyPositions(state.NewNorthAt(i).Apply);
    }
}
