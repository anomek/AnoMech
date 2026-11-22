using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Native.Interfaces;

// The real player's input: what they're doing, and the locks a mechanic puts on it.
public interface ILocalPlayerInput : IDisposable
{
    // Raised after the player successfully executes an action: type, action id, target id.
    event Action<ActionType, uint, ulong>? ActionExecuted;

    bool MovementInputActive { get; }
    bool IsJumping { get; }
    bool IsAutoAttacking { get; }

    // True once per action used since the last poll.
    bool PollActionUsed();

    // True once per cast-cancel request (cancel keybind) since the last poll.
    bool PollCancelCast();

    // Actions the player pressed, oldest first; a bounded debug history.
    IReadOnlyCollection<(uint ActionId, ActionType Type)> RecentActions { get; }

    // Walks the player along a world XZ direction as if the stick were held, over the keyboard;
    // null lets go. The game moves them at its own speed; `speed` is the run
    // or sprint speed the sim expects, which a headless client moves them at.
    void Steer(System.Numerics.Vector2? direction, float speed);

    // Asked whenever the client settles which action a button is, how long a spell casts and why an
    // action can't be used, to stand in for state the firewall keeps from it; null keeps the game's
    // own answer. AdjustActionStatus is asked only about a refusal, and 0 lifts it; its flag says
    // whether a running recast counts (a press) or not (the hotbar, which shows it as a sweep).
    Func<uint, uint?>? AdjustActionId { get; set; }
    Func<ActionType, uint, int?>? AdjustCastTime { get; set; }
    Func<ActionType, uint, bool, uint?>? AdjustActionStatus { get; set; }
}
