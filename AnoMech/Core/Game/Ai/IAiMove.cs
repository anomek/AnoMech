using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Core.Game.Ai;

// Staged builder contract for AiMove. The interfaces form a capability chain so
// the pipeline order is enforced at compile time, not at runtime:
//
//     AiMove.Create(coords)        // IAssignStep
//           .Assignments(order)    // ISwapStep   (or .NaturalOrder())
//           .ApplySwaps(a, b)      // ISwapStep
//           .ApplyPositions(c);    // IPositionStep — consumable as IAiMove
//
// You must pick a base ordering first; only swaps before positions; no swaps
// after a position transform. Illegal orders simply don't expose the method.
//
// (ISwapStep / IPositionStep are builder *stages*; IAiRoles / IAiPositions are the
// lambda mutation surfaces handed to ApplySwaps / ApplyPositions.)

// The consumable result: the destination for each party slot. A chain becomes an
// IAiMove the moment a base ordering is chosen (both steps below derive from it).
public interface IAiMove
{
    // Destination for party slot `role`: the coordinate assigned to that role,
    // or null for "no movement."
    Vector2? this[int role] { get; }
}

// After a base ordering: transform coordinates (by index, by role, or bulk).
// No swaps from here — the assignment is final, so by-role tweaks are well-defined.
public interface IPositionStep : IAiMove
{
    IPositionStep ApplyPositions(params Action<IAiPositions>[] adjustments);
}

// After a base ordering: permute the role->coordinate assignment, then move on to
// position transforms.
public interface ISwapStep : IPositionStep
{
    ISwapStep ApplySwaps(params Action<IAiRoles>[] swaps);
}

// The mandatory first choice: pin the base role->coordinate ordering. Not itself
// consumable — an AiMove is unusable until one of these is called.
public interface IAssignStep : IAiMove
{
    // position i is taken by roles[i]; `null` keeps natural order. Must be a full
    // 8-role permutation.
    ISwapStep Assignments(IReadOnlyList<PartyRole>? roles);

    // Identity ordering: position i -> role i (natural PartyRole order).
    ISwapStep NaturalOrder();
}
