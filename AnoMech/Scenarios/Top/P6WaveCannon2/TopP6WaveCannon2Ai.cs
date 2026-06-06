using System;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P6WaveCannon2;

// Party-member movement choreography for TOP P6 Wave Cannon 2. Reads the shared
// TopP6WaveCannon2State so movement stays in sync with the randomized layout, and
// schedules moves through the AiManager. See TopP5DeltaAi for the canonical shape.
public sealed class TopP6WaveCannon2Ai : IScenarioAi<TopP6WaveCannon2State>
{
    public string Name => "Standard";

    private TopP6WaveCannon2State state = null!;

    public void Run(TopP6WaveCannon2State s, SimWorld world)
    {
        state = s;
        var ai = new AiManager(world);

        ai.Move(2f, Corners(6, 9));
        ai.Move(10.5f, Corners(4, 11));

        if (state.InFirst)
        {
            ai.Move(14.5f, Split(11, -5, -11));
            ai.Move(18.5f, Support(1, -10));
            ai.Move(20.5f, Split(9, 1, -8));
        }
        else
        {
            ai.Move(14.5f, Corners(9, 9));
            ai.Move(16.5f, Split(11, 1, -10));
            ai.Move(18.5f, Split(9, 1, -8));
        }
        ai.Move(20f, WildChargeStack, jitter: .9f, arrivalTime: 27f);
    }


    private ISwapStep RolesOnCorners()
    {
        return AiMove.Create(
            new(1f, -1f),
            new(-1f, 1f),
            new(-1f, -1f),
            new(1f, 1f),
            new(-1f, 1f),
            new(1f, 1f),
            new(1f, -1f),
            new(-1f, -1f)
        ).NaturalOrder();
    }

    private ISwapStep SupportOnCorners()
    {
        return AiMove.Create(
            new(1f, -1f),
            new(-1f, 1f),
            new(-1f, -1f),
            new(1f, 1f)
        ).NaturalOrder();
    }

    public Func<IAiMove> Corners(float inFirst, float outFirst)
    {
        return () => RolesOnCorners()
            .ApplyPositions(Position(inFirst, outFirst));
    }

    public Func<IAiMove> Split(float dpsCorner, float supRight, float supFront)
    {
        return () => RolesOnCorners()
            .ApplyPositions(AdjustDps(dpsCorner), AdjustSupport(supRight, supFront));
    }

    private Func<IAiMove> Support(float right, float front)
    {
        return () => SupportOnCorners()
            .ApplyPositions(AdjustSupport(right, front));
    }


    private IAiMove WildChargeStack()
    {
        return AiMove.Create(
            new(0, 9), new(0, 9),
            new(0, 11),
            new(0, 11),
            new(0, 11),
            new(0, 11),
            new(0, 11),
            new(0, 11)
        );
    }


    public Action<IAiPositions> AdjustDps(float dist)
    {
        return p =>
        {
            p.Multiply(4, dist);
            p.Multiply(5, dist);
            p.Multiply(6, dist);
            p.Multiply(7, dist);
        };
    }

    public Action<IAiPositions> AdjustSupport(float right, float front)
    {
        return p =>
        {
            p.AddX(0, -right);
            p.AddY(0, front);
            p.AddX(1, right);
            p.AddY(1, -front);
            p.AddX(2, front);
            p.AddY(2, right);
            p.AddX(3, -front);
            p.AddY(3, -right);
        };
    }

    private Action<IAiPositions> Position(float inFirst, float outFirst)
    {
        return p => p.Multiply(state.InFirst ? inFirst : outFirst);
    }
}
