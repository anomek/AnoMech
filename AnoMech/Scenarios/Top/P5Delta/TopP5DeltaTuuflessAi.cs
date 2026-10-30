using System.Numerics;
using AnoMech.Core.Game.Ai;

namespace AnoMech.Scenarios.Top.P5Delta;

public sealed class TopP5DeltaTuuflessAi : TopP5DeltaAi
{
    public override string Name => "Tuufless (Elemental)";

    private int SafeSideSlot => state.SwivelCannonSide.Mul * state.EyeSpawn.Mul > 0 ? 5 : 4;

    protected override void ScheduleTetherBreak(AiManager ai)
    {
        ai.Move(49.2f, BothMoveIn);
        ai.Move(51f, TakeOuterAndInner);
    }

    private IAiMove BothMoveIn()
    {
        var slots = new Vector2?[8];
        slots[SafeSideSlot] = new Vector2(-9.5f, 5.5f);
        slots[9 - SafeSideSlot] = new Vector2(-9.5f, 3.5f);
        return AiMove.Create(slots)
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap45)
        .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }

    private IAiMove TakeOuterAndInner()
    {
        var slots = new Vector2?[8];
        slots[SafeSideSlot] = new Vector2(-13.5f, 13.5f);
        slots[9 - SafeSideSlot] = new Vector2(-9.192f, 9.192f);
        return AiMove.Create(slots)
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap45)
        .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }
}
