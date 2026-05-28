using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Core.Game.Ai;

// A bijection between the 8 position slots and the 8 party roles:
// roleAt[position] is the role currently standing at that position. This is the
// "who goes where" half of an AiMove — coordinates never move, only this mapping
// does. The invariant (each role appears exactly once) is enforced structurally:
// it can only be constructed from a full permutation, and every mutator is a
// swap / rotation, which cannot introduce a duplicate. So callers never have to
// re-validate, and an inconsistent assignment is simply unconstructable.
internal sealed class RoleAssignment
{
    private const int Size = 8;

    private readonly PartyRole[] roleAt;

    private RoleAssignment(PartyRole[] roleAt) => this.roleAt = roleAt;

    // Identity: position i holds role i (MainTank at 0, …, CasterDps at 7).
    public static RoleAssignment Identity() =>
        new(Enumerable.Range(0, Size).Select(i => (PartyRole)i).ToArray());

    // Base assignment from a role order: position i is taken by roles[i].
    // Throws unless `roles` is a full permutation of the 8 PartyRole values.
    public static RoleAssignment From(IReadOnlyList<PartyRole> roles)
    {
        if (roles.Count != Size)
            throw new ArgumentException(
                $"Assignment needs all {Size} roles, got {roles.Count}.", nameof(roles));

        var arr = roles.ToArray();
        var seen = new bool[Size];
        foreach (var role in arr)
        {
            var i = (int)role;
            if (i < 0 || i >= Size || seen[i])
                throw new ArgumentException(
                    $"Assignment must contain each role exactly once; '{role}' is duplicated or out of range.",
                    nameof(roles));
            seen[i] = true;
        }

        return new RoleAssignment(arr);
    }

    public PartyRole RoleAt(int position) => roleAt[position];

    public int PositionOf(PartyRole role) => Array.IndexOf(roleAt, role);

    // Swap the roles assigned to positions a and b (coordinates stay put).
    public void ByPosition(int a, int b) => (roleAt[a], roleAt[b]) = (roleAt[b], roleAt[a]);

    // Swap the coordinates assigned to two roles — i.e. swap the positions they sit at.
    public void ByRole(PartyRole r1, PartyRole r2) => ByPosition(PositionOf(r1), PositionOf(r2));

    // Swap two position-pairs: (2a,2a+1) <-> (2b,2b+1).
    public void ByPositionPair(int a, int b)
    {
        ByPosition(2 * a, 2 * b);
        ByPosition(2 * a + 1, 2 * b + 1);
    }

    // Bulk permutation, scatter semantics: the role currently at position i moves
    // to position order[i]. `order` must be a permutation of 0..7.
    public void Reorder(int[] order)
    {
        if (order.Length != Size)
            throw new ArgumentException($"Reorder needs {Size} indices, got {order.Length}.", nameof(order));
        var seen = new bool[Size];
        foreach (var o in order)
        {
            if (o < 0 || o >= Size || seen[o])
                throw new ArgumentException(
                    $"Reorder argument must be a permutation of 0..{Size - 1}; index {o} is duplicated or out of range.",
                    nameof(order));
            seen[o] = true;
        }

        var old = (PartyRole[])roleAt.Clone();
        for (var i = 0; i < Size; i++)
            roleAt[order[i]] = old[i];
    }

    // Cyclic shift, gather semantics: position i takes the role currently at
    // position (i + count), wrapping around. Negative counts are normalized.
    public void Offset(int count)
    {
        var old = (PartyRole[])roleAt.Clone();
        for (var i = 0; i < Size; i++)
            roleAt[i] = old[((i + count) % Size + Size) % Size];
    }
}
