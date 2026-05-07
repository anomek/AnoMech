using System;
using System.Numerics;

namespace UltiSim.Core;

// Holds 8 scenario-local XZ positions (one per party slot). Null entries mean
// "no movement for that slot." Fluent transforms (MultiplyX, Reorder) let
// position functions adjust for eye-spawn and tether-order before handing off
// to SimAI, which converts each non-null entry to world space and drives movement.
public sealed class AiMove
{
    private readonly Vector2?[] positions = new Vector2?[8];

    public static AiMove Single(int index, Vector2? position)
    {
        var positions = new Vector2?[8];
        positions[index] = position;
        return new AiMove(positions);
    }
    
    public AiMove(params Vector2?[] initialPositions)
    {
        for (var i = 0; i < positions.Length && i < initialPositions.Length; i++)
        {
            positions[i] = initialPositions[i];
        }
    }

    public Vector2? this[int i] => positions[i];
    
    public AiMove Apply(params Action<AiMove>[] actions)
    {
        foreach (var action in actions)
            action(this);
        return this;
    }
    
    public void AddX(int i, float add)
    {
        if (positions[i] is { } v)
            positions[i] = v with { X = v.X + add };
    }
    
    public void AddY(int i, int add)
    {
        if (positions[i] is { } v)
            positions[i] = v with { Y = v.Y + add };
    }

    public AiMove MultiplyX(float mul)
    {
        for (int i = 0; i < positions.Length; i++)
            if (positions[i] is { } v)
                positions[i] = v with { X = v.X * mul };
        return this;
    }
    
    public AiMove MultiplyY(float mul)
    {
        for (int i = 0; i < positions.Length; i++)
            MultiplyY(i, mul);
        return this;
    }
    
    public void MultiplyY(int i, float mul)
    {
        if (positions[i] is { } v)
            positions[i] = v with { Y = v.Y * mul };
    }

    public AiMove Reorder(int[] order)
    {
        var old = (Vector2?[])positions.Clone();
        for (int i = 0; i < positions.Length; i++)
            positions[order[i]] = old[i];
        return this;
    }

    // Applies each action to this AiMove in sequence.

    public AiMove Swap(int a, int b)
    {
        (positions[a], positions[b]) = (positions[b], positions[a]);
        return this;
    }

}
