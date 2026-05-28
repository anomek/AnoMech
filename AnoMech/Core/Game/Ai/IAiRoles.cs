using AnoMech.Core.Game.Party;

namespace AnoMech.Core.Game.Ai;

// The mutation surface handed to AiMove.ApplySwaps lambdas. Swaps only ever
// permute the role->coordinate assignment; coordinates never move. Because
// every operation here is a swap / rotation of a bijection, the assignment
// stays a full permutation no matter how they are chained. RoleAt/PositionOf
// read the *live* assignment so predicates see the effect of prior swaps.
public interface IAiRoles
{
    // The role currently assigned to coordinate position `position`.
    PartyRole RoleAt(int position);

    // The coordinate position currently assigned to `role`.
    int PositionOf(PartyRole role);

    // Swap the two roles assigned to positions a and b.
    void ByPosition(int a, int b);

    // Swap the coordinates assigned to two roles (swap where each of them stands).
    void ByRole(PartyRole r1, PartyRole r2);

    // Swap two position-pairs: (2a, 2a+1) <-> (2b, 2b+1).
    void ByPositionPair(int a, int b);

    // Bulk permutation (scatter): the role at position i moves to position order[i].
    // `order` must be a permutation of 0..7.
    void Reorder(int[] order);

    // Cyclic shift (gather): position i takes the role currently at (i + count).
    void Offset(int count);
}
