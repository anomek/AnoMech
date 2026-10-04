using System.Numerics;

namespace AnoMech.Core.Native.Interfaces;

// The real player's input: what they're doing, and the locks a mechanic puts on it.
public interface ILocalPlayerInput
{
    bool MovementInputActive { get; }
    bool IsJumping { get; }
    bool IsAutoAttacking { get; }

    // True once per action used since the last poll.
    bool PollActionUsed();

    bool ZeroMovement { get; set; }
    bool ZeroRotation { get; set; }
    bool DisableAllActions { get; set; }

    // Facing held while set.
    float? LockedRotation { get; set; }

    // Conditions.SufferingStatusAffliction(2): the client's stunned state.
    void SetStatusAffliction(bool afflicted);

    // The debug bot's LB3, pressed through the client as its player would: a ground-targeted one
    // placed at worldLocation, the rest used on targetId. False when the client refuses it.
    bool PressLimitBreakThree(ulong targetId, Vector3? worldLocation);
}
