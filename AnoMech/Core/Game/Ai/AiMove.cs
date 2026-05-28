using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Core.Game.Ai;

// Holds 8 scenario-local XZ coordinates plus the role->coordinate assignment,
// and produces the per-party-slot destinations AiManager consumes.
//
// Coordinates are authored once (by fixed position index) and only ever
// transformed geometrically — they never move between slots. "Who runs to which
// coordinate" lives entirely in the assignment, which usually starts as the
// scenario's randomized RoleList and is then permuted by swaps. This split is
// why the pipeline reads cleanly:
//
//     AiMove.Create(coords)
//           .Assignments(order.List)         // base: position i -> role (or NaturalOrder())
//           .ApplySwaps(swapA, swapB)        // permute the assignment (IAiRoles)
//           .ApplyPositions(adjA, adjB);     // transform coordinates (IAiPositions)
//
// The staged interfaces (IAssignStep -> ISwapStep -> IPositionStep -> IAiMove)
// enforce this order at compile time: you must pick a base ordering first, swaps
// only before positions, and a by-role position tweak is always well-defined
// because the assignment is final by then. AiManager reads this[i] for party
// slot i; null entries mean "no movement for that slot."
public sealed class AiMove : IAssignStep, ISwapStep, IAiRoles, IAiPositions
{
    private const int Size = 8;

    private readonly Vector2?[] coords = new Vector2?[Size];
    private RoleAssignment assignment = RoleAssignment.Identity();

    private AiMove(Vector2?[] initialCoords)
    {
        for (var i = 0; i < Size && i < initialCoords.Length; i++)
            coords[i] = initialCoords[i];
    }

    public static IAssignStep Create(params Vector2?[] coords) => new AiMove(coords);

    public static IAssignStep All(Vector2 position) =>
        new AiMove(Enumerable.Range(0, Size).Select(_ => new Vector2?(position)).ToArray());

    public static ISwapStep Single(PartyRole role, Vector2? position)
    {
        var coords = new Vector2?[Size];
        coords[0] = position; 
        return ((IAssignStep)new AiMove(coords)).NaturalOrder().ApplySwaps(s =>
        {
            var index = s.PositionOf(role);
            if (index != 0) s.ByPosition(0, index);
        });
    }
    
    public static IAssignStep Single(int index, Vector2? position)
    {
        var coords = new Vector2?[Size];
        coords[index] = position;
        return new AiMove(coords);
    }

    // --- IAssignStep (mandatory base ordering) ---

    // position i is taken by roles[i]. `null` keeps the identity ordering, which
    // covers steps with no role randomization. `roles` must be a full 8-role permutation.
    ISwapStep IAssignStep.Assignments(IReadOnlyList<PartyRole>? roles)
    {
        if (roles != null)
            assignment = RoleAssignment.From(roles);
        return this;
    }

    // Identity ordering: position i -> role i. The default, made explicit.
    ISwapStep IAssignStep.NaturalOrder() => this;

    // --- ISwapStep / IPositionStep (ordered transforms) ---

    ISwapStep ISwapStep.ApplySwaps(params Action<IAiRoles>[] swaps)
    {
        foreach (var swap in swaps)
            swap(this);
        return this;
    }

    IPositionStep IPositionStep.ApplyPositions(params Action<IAiPositions>[] adjustments)
    {
        foreach (var adjustment in adjustments)
            adjustment(this);
        return this;
    }

    // --- IAiMove (consumable result) ---

    // Destination for party slot `role`: the coordinate at the position that role
    // is currently assigned to.
    Vector2? IAiMove.this[int role] => coords[assignment.PositionOf((PartyRole)role)];

    // --- IAiRoles (assignment only) ---

    PartyRole IAiRoles.RoleAt(int position) => assignment.RoleAt(position);
    int IAiRoles.PositionOf(PartyRole role) => assignment.PositionOf(role);
    void IAiRoles.ByPosition(int a, int b) => assignment.ByPosition(a, b);
    void IAiRoles.ByRole(PartyRole r1, PartyRole r2) => assignment.ByRole(r1, r2);
    void IAiRoles.ByPositionPair(int a, int b) => assignment.ByPositionPair(a, b);
    void IAiRoles.Reorder(int[] order) => assignment.Reorder(order);
    void IAiRoles.Offset(int count) => assignment.Offset(count);

    // --- IAiPositions (coordinates only) ---

    void IAiPositions.AddX(int i, float add) => AddXAt(i, add);
    void IAiPositions.AddY(int i, float add) => AddYAt(i, add);
    void IAiPositions.MultiplyX(int i, float mul) => MultiplyXAt(i, mul);
    void IAiPositions.MultiplyY(int i, float mul) => MultiplyYAt(i, mul);
    void IAiPositions.Rotate(int i, float radians) => RotateAt(i, radians);

    void IAiPositions.AddX(PartyRole role, float add) => AddXAt(assignment.PositionOf(role), add);
    void IAiPositions.AddY(PartyRole role, float add) => AddYAt(assignment.PositionOf(role), add);
    void IAiPositions.MultiplyX(PartyRole role, float mul) => MultiplyXAt(assignment.PositionOf(role), mul);
    void IAiPositions.MultiplyY(PartyRole role, float mul) => MultiplyYAt(assignment.PositionOf(role), mul);
    void IAiPositions.Rotate(PartyRole role, float radians) => RotateAt(assignment.PositionOf(role), radians);

    void IAiPositions.MultiplyX(float mul)
    {
        for (var i = 0; i < Size; i++) MultiplyXAt(i, mul);
    }

    void IAiPositions.MultiplyY(float mul)
    {
        for (var i = 0; i < Size; i++) MultiplyYAt(i, mul);
    }

    void IAiPositions.Multiply(float mul)
    {
        for (var i = 0; i < Size; i++)
        {
            MultiplyXAt(i, mul);
            MultiplyYAt(i, mul);
        }
    }

    void IAiPositions.Rotate(float radians)
    {
        for (var i = 0; i < Size; i++) RotateAt(i, radians);
    }

    private void AddXAt(int i, float add)
    {
        if (coords[i] is { } v) coords[i] = v with { X = v.X + add };
    }

    private void AddYAt(int i, float add)
    {
        if (coords[i] is { } v) coords[i] = v with { Y = v.Y + add };
    }

    private void MultiplyXAt(int i, float mul)
    {
        if (coords[i] is { } v) coords[i] = v with { X = v.X * mul };
    }

    private void MultiplyYAt(int i, float mul)
    {
        if (coords[i] is { } v) coords[i] = v with { Y = v.Y * mul };
    }

    private void RotateAt(int i, float radiansFromNorth)
    {
        if (coords[i] is not { } v) return;
        var cos = MathF.Cos(radiansFromNorth);
        var sin = MathF.Sin(radiansFromNorth);
        coords[i] = new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}
