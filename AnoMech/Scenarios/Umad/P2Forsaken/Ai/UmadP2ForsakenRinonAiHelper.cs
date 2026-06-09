using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

// Party-member movement choreography for UMAD P2 Forsaken. Reads the shared
// UmadP2ForsakenState so movement stays in sync with the randomized layout, and
// schedules moves through the AiManager. See TopP5DeltaAi for the canonical shape.
//
// This is NOT a selectable strat (it does not implement IScenarioAi). It is a
// reusable helper composed by strat classes such as UmadP2ForsakenKroxyRinonAi.
// The single plug point is `reorderActive`: after each Odd/EvenTower the active
// 4-role group (alpha or beta) is handed to the strat for an optional in-place
// reorder, so consecutive towers of the same group can rotate assignments. The
// default no-op keeps standalone use behaviour-identical to the old strat.
public sealed class UmadP2ForsakenRinonAiHelper
{
    private readonly Vector2?[] oddCoords;
    private readonly Vector2?[] evenCoords;
    private readonly Action<UmadP2ForsakenRinonAiHelper, int, IList<PartyRole>> reorderActive;

    private UmadP2ForsakenState state = null!;

    private IReadOnlyList<PartyRole> alphaInitial = [];
    private IReadOnlyList<PartyRole> betaInitial = [];
    private List<PartyRole> alpha = [];
    private List<PartyRole> beta = [];

    public UmadP2ForsakenRinonAiHelper(
        Vector2?[] oddCoords,
        Vector2?[] evenCoords,
        Action<UmadP2ForsakenRinonAiHelper, int, IList<PartyRole>> reorderActive)
    {
        this.oddCoords = oddCoords;
        this.evenCoords = evenCoords;
        this.reorderActive = reorderActive;
    }

    public void Run(UmadP2ForsakenState s, SimWorld world)
    {
        state = s;
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
        alpha = list;
        alphaInitial = new List<PartyRole>(alpha);
        list = Enum.GetValues<PartyRole>()
                   .Where(role => !list.Contains(role))
                   .ToList();
        (list[0], list[1]) = (list[1], list[0]); // swap tank and healer
        beta = list;
        betaInitial = new List<PartyRole>(beta);
    }


    public PartyRole ActiveRole(uint mechanic, int towerId, int order)
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
        var array = towerId is < 3 or 7 ? betaInitial : alphaInitial;
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
        return () =>
        {
            var move = i % 2 == 1 ? EvenTower(i) : OddTower(i); // odd/even flipped because 0 indexing teehee
            reorderActive(this, i, i is < 3 or 7 ? alpha : beta); // plug point: same active-group rule as ActiveRole
            return move;
        };
    }

    private IAiMove OddTower(int i)
    {
        return AiMove.Create(oddCoords)
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
        return AiMove.Create(evenCoords)
                     .Assignments([
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

    // --- Coordinate sets ---
    // 16 scenario-local XZ coords per set: 8 for odd-index towers, 8 for even-index
    // towers (active 4 + passive 4 each). Passed into the constructor by the strats.
    // AiMove copies these on Create, so its per-move rotation never mutates the set.

    // Standard layout — the values the helper used to hardcode. Shared by the
    // "South Flex 341" and "Kroxy-Rinon 341" strats.
    public static readonly Vector2?[] StandardOdd =
    [
        // active group
        new(5.5f, -5.5f),  // stack
        new(8f, -8f),      // cone
        new(-5.4f, -2.4f), // stack
        new(-5.4f, -8.5f), // chariot
        // passive group
        new(9.5f, -9.5f),  // outer cone bait
        new(2.5f, -2.5f),  // inner cone bait
        new(-3.5f, -1.9f), // in stack
        new(-3.5f, -1.9f)  // in stack
    ];

    public static readonly Vector2?[] OldEven =
    [
        // active group
        new(3.2f, -3.2f),  // cone1
        new(8, -8),        // chariot1
        new(-3.2f, -3.2f), // cone2
        new(-8, -8),       // chariot2
        // passive group
        new(8.8f, -2.4f),  // cone bait1
        new(3.8f, 4.2f),   // clone bait1
        new(-3.8f, 4.2f),  // clone bait2
        new(-8.8f, -2.4f)  // cone bait2
    ];

    public static readonly Vector2?[] DiamonMarkersEven =
    [
        // active group
        new(4.8f, -2.2f),  // TODO cone1
        new(6.3f, -9.2f),        // TODO chariot1
        new(-4.8f, -2.2f), // TODO cone2
        new(-6.3f, -9.2f),       // TODO chariot2
        // passive group
        new(10.5f, 0f),  // TODO cone bait1
        new(3.8f, 5.3f),   // TODO clone bait1
        new(-3.8f, 5.3f),  // TODO clone bait2
        new(-10.5f, 0f)  // TODO cone bait2
    ];

    // --- Reorder behaviors ---
    // The strats' plug point, shared so the set-1 and set-2 variants stay in sync.
    // Invoked from TowerPositions with the helper instance + the active 4-role group.

    // South Flex: rotate the active group's assignment per tower.
    public static void SouthFlexReorder(UmadP2ForsakenRinonAiHelper helper, int index, IList<PartyRole> active)
    {
        if (index % 2 == 0) // odd tower
        {
            List<PartyRole> reordered =
            [
                helper.ActiveRole(LockonId.ForsakenStack, index, 0),
                helper.ActiveRole(LockonId.ForsakenCone, index, 0),
                helper.ActiveRole(LockonId.ForsakenChariot, index, 0),
                helper.ActiveRole(LockonId.ForsakenStack, index, 1)
            ];
            active.Clear();
            reordered.ForEach(active.Add);
        }
        else // even tower
        {
            List<PartyRole> reordered =
            [
                helper.ActiveRole(LockonId.ForsakenCone, index, 0),
                helper.ActiveRole(LockonId.ForsakenChariot, index, 0),
                helper.ActiveRole(LockonId.ForsakenChariot, index, 1),
                helper.ActiveRole(LockonId.ForsakenCone, index, 1)
            ];
            active.Clear();
            reordered.ForEach(active.Add);
        }
    }

    // Kroxy: no reorder — standalone behaviour-identical to the original helper.
    public static void KroxyReorder(UmadP2ForsakenRinonAiHelper helper, int index, IList<PartyRole> active)
    {
    }
}
