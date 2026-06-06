using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P5Sigma;

// First-pass AI for TOP P5 Sigma. The auto-generated beats below come from
// the cast timeline at major resolve points; the user replaces the bare
// positions with state-aware ones (NewNorthA / SpinnerRotation / etc.) as
// they harden the choreography. See TopP5DeltaAi for the canonical shape
// once choreography matures (named methods, Apply-chained transforms).
public sealed class TopP5SigmaAi : IScenarioAi<TopP5SigmaState>
{
    public string Name => "Standard";

    private TopP5SigmaState state = null!;
    private RoleList markingsOrder = null!;

    public void Run(TopP5SigmaState s, SimWorld world)
    {
        state = s;
        var ai = new AiManager(world);

        var handBait = state.DynamisTargets.Random(2, state.HelloWorldTargets.List);
        var hWJumpsOrder = RoleList.AllExcept(world.Party, state.HelloWorldTargets.List.Concat(handBait.List).ToArray());
        markingsOrder = new(world.Party, [handBait[0], hWJumpsOrder[0], handBait[1], hWJumpsOrder[1],
                        hWJumpsOrder[2], hWJumpsOrder[3], state.HelloWorldTargets[0], state.HelloWorldTargets[1]]);

        ai.Move(0.5f, InitialPositions);
        ai.Move(14.1f, LineupNextToOmegaM);
        ai.Move(20f, WaveCannonSpread, arrivalTime: 29f);
        ai.Move(32f, KnockbackPrePosition);
        ai.Move(36f, KnockbackPosition, jitter: 0.1f, arrivalTime: 39.5f);
        ai.Move(41.5f, TowerPositions, jitter: 0.1f);
        ai.Automarker(43.5f, MarkerMapping);
        ai.Move(44f, InitialPositions, jitter: 3f);
        ai.Move(50f, RearLasersPrePosition, arrivalTime: 56.5f);
        ai.Move(58f, AdjustForLegs, arrivalTime: 60.5f);
        ai.Move(62f, HelloWorldPositions, arrivalTime: 66.5f);
        ai.Move(73f, InitialPositions);
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

    private IAiMove LineupNextToOmegaM()
    {
        return AiMove.Create(
            new(-2, -18), new(2, -18),
            new(-2, -15), new(2, -15),
            new(-2, -13), new(2, -13),
            new(-2, -11), new(2, -11)
        )
        .Assignments(state.Order.List)
        .ApplyPositions(state.NewNorthA.Apply);
    }

    private IAiMove WaveCannonSpread()
    {
        return AiMove.Create(
            new(0f, -12.5f),  // N
            new(8.8f, -8.8f), // NE
            new(12.5f, 0f),   // E
            new(8.8f, 8.8f),  // SE
            new(0f, 12.5f),   // S
            new(-8.8f, 8.8f), // SW
            new(-12.5f, 0f),  // W
            new(-8.8f, -8.8f) // NW
        )
        .Assignments(WaveCannonAssignments())
        .ApplyPositions(FarGlitchWaveCannonAdjustment, state.NewNorthA.Apply);
    }
    
    private IReadOnlyList<PartyRole> WaveCannonAssignments()
    {
        return [
            state.FullPair(0).left,
            state.HalfPair(1).baiting,
            state.FullPair(1).right,
            state.HalfPair(0).marked,
            state.FullPair(0).right,
            state.HalfPair(1).marked,
            state.FullPair(1).left,
            state.HalfPair(0).baiting,
        ];
    }

    private IAiMove KnockbackPrePosition()
    {
        return AiMove.Create(
            new(0f, -4f),     // N (absolute)
            new(2.8f, -2.8f), // NE
            new(4f, 0f),      // E
            new(2.8f, 2.8f),  // SE
            new(0f, 4f),      // S
            new(-2.8f, 2.8f), // SW
            new(-4f, 0f),     // W
            new(-2.8f, -2.8f) // NW
        )
        .Assignments(WaveCannonAssignments())
        .ApplySwaps(AbsoluteToRelativeClockSpot);
    }
    

    private IAiMove KnockbackPosition()
    {
        return (state.GlitchType == GlitchType.Mid
                    ? AiMove.Create(
                        new(-1.5f, 0.7f),  // A
                        new(-0.7f, -1.5f), // 1
                        new(-0.7f, 1.5f),  // B
                        new(0.7f, -1.5f),  // 2
                        new(0.7f, 1.5f),   // C
                        new(-1.5f, 0.7f),  //  3
                        new(1.5f, 0.7f),  //D
                        new(1.5f, 0.7f)   //4
                    )
                    : AiMove.Create(
                        new (-1.8f, 0),    // A
                        new (0, -1.8f),    // 1
                        new (-1.2f, 1.2f), // B
                        new (0, -1.8f),    // 2
                        new (1.2f, 1.2f),  // C
                        new (-1.2f, 1.2f), // 3
                        new(1.8f, 0),      // D
                        new (1.2f, 1.2f)   // 4
                    )
               )
               .Assignments(WaveCannonAssignments())
               .ApplySwaps(AbsoluteToRelativeClockSpot)
               .ApplyPositions(state.AdjustedNorthA.Apply);
    }

    private IAiMove TowerPositions()
    {
        return (state.GlitchType == GlitchType.Mid
                    ? AiMove.Create(
                        new(-15.7f, 6.5f),  // A
                        new(-6.5f, -15.7f), // 1
                        new(-6.5f, 15.7f),  // B
                        new(6.5f, -15.7f),  // 2
                        new(6.5f, 15.7f),   // C
                        new(-15.7f, 6.5f),  //  3
                        new(15.7f, 6.5f),   //D
                        new(15.7f, 6.5f)    //4
                    )
                    : AiMove.Create(
                        new (-19f, -1),    // A
                        new (1, -19f),    // 1
                        new (-13.7f, 13.1f), // B
                        new (-1, -19f),    // 2
                        new (13.1f, 13.7f),  // C
                        new (-13.1f, 13.7f), // 3
                        new(19f, -1),      // D
                        new (13.7f, 13.1f)   // 4
                    )
               )
               .Assignments(WaveCannonAssignments())
               .ApplySwaps(AbsoluteToRelativeClockSpot)
               .ApplyPositions(state.AdjustedNorthA.Apply);
    }

    private Dictionary<PartyRole, Sign> MarkerMapping()
    {
        return new Dictionary<PartyRole, Sign>()
        {
            [markingsOrder[0]] = Sign.Bind1,
            [markingsOrder[1]] = Sign.Attack1,
            [markingsOrder[2]] = Sign.Bind2,
            [markingsOrder[3]] = Sign.Attack2,
            [markingsOrder[4]] = Sign.Attack3,
            [markingsOrder[5]] = Sign.Attack4,
            [markingsOrder[6]] = Sign.Triangle,
            [markingsOrder[7]] = Sign.Cross,
        };
    }

    private IAiMove RearLasersPrePosition()
    {
        return AiMove.Create(
            new(6.5f, -17),
            new(6.5f, -17),
            new(6.5f, -17),
            new(-6.5f, 17),
            new(-6.5f, 17),
            new(-6.5f, 17),
            new(-6.5f, 17),
            new(-6.5f, 17)
        )
        .Assignments(markingsOrder.List)
        .ApplyPositions(SpinnerRotation, state.NewNorthB.Apply);
    }

    private IAiMove AdjustForLegs()
    {
        return state.OmegaFAttack == OmegaAttack.Staff
            ? AiMove.Create().NaturalOrder()
            : AiMove.Create(
                new(2f, -18f),
                new(2f, -18f),
                new(2f, -18f),
                new (-2f, 18f),
                new (-2f, 18f),
                new (-2f, 18f),
                new (-2f, 18f),
                new (-2f, 18f)
            )
            .Assignments(markingsOrder.List)
            .ApplyPositions(SpinnerRotation, state.NewNorthB.Apply);
    }


    private IAiMove HelloWorldPositions()
    {
        return AiMove.Create(
            new(-13.5f, -14.2f),
            new(0, -19.5f),
            new(13.5f, -14.2f),
            new(19.5f, 0),
            new(18.9f, 5),
            new(0, 19.5f),
            new(10f, 0),
            new(0f, 10f)
        )
        .Assignments(markingsOrder.List)
        .ApplyPositions(SpinnerRotation, state.NewNorthB.Apply);
    }

    private void AbsoluteToRelativeClockSpot(IAiRoles s)
    {
        s.Offset(-state.NewNorthA.Index());
    }

    private void FarGlitchWaveCannonAdjustment(IAiPositions move)
    {
        if (state.GlitchType == GlitchType.Far)
        {
            move.Multiply(1.5f); // goes to wall
        }
    }

    private void SpinnerRotation(IAiPositions move)
    {
        move.MultiplyX(state.SpinnerRotation.Mul);
    }
}
