using AnoMech.Core.Game.Party;

namespace AnoMech.Core.Game.Ai;

// The mutation surface handed to AiMove.ApplyPositions lambdas. These only ever
// transform coordinates, never the role assignment. Coordinates can be addressed
// two ways: by their fixed authoring index (the common case), or by role — "the
// coordinate whoever-is-RoleX was assigned" — which is why ApplyPositions must
// run after ApplySwaps (the assignment has to be final). Null coordinates are
// left untouched.
public interface IAiPositions
{
    // --- by authoring index ---
    void AddX(int i, float add);
    void AddY(int i, float add);
    void MultiplyX(int i, float mul);
    void MultiplyY(int i, float mul);
    void Rotate(int i, float radiansFromNorth);

    // --- by role (resolves to the position currently assigned to the role) ---
    void AddX(PartyRole role, float add);
    void AddY(PartyRole role, float add);
    void MultiplyX(PartyRole role, float mul);
    void MultiplyY(PartyRole role, float mul);
    void Rotate(PartyRole role, float radiansFromNorth);

    // --- bulk (every slot) ---
    void MultiplyX(float mul);
    void MultiplyY(float mul);
    void Multiply(float mul);
    void Rotate(float radiansFromNorth);
}
