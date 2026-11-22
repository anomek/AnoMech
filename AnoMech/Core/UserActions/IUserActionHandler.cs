using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// One feature of the UserActions module — resolves some player behaviour the sim
// firewall would otherwise leave unhandled (combo, cast interruption, resource
// generation, ...). Every hook is optional (default no-op); a handler implements only
// what it needs. Handlers are stored and dispatched as a list by UserActions.
internal interface IUserActionHandler
{
    // Off, the handler is skipped while the module is disabled.
    bool RunsWhenDisabled => false;

    // A player action resolved: at once for an instant action, when the cast bar completes
    // for a hard cast. UserActions dedupes this to once per actual execution (a
    // queued/spammed press or our own re-entrant fire won't call it again).
    void OnAction(ActionType actionType, uint actionId) { }

    // Same, for a handler that needs who the action was aimed at.
    void OnAction(ActionType actionType, uint actionId, ulong targetId) => OnAction(actionType, actionId);

    // A player action began a cast bar; OnAction follows if it completes, OnCastInterrupted if not.
    void OnCastStart(ActionType actionType, uint actionId) { }

    void OnCastInterrupted(ActionType actionType, uint actionId) { }

    // A scenario started — reset cooldowns/gauges the server would otherwise track.
    void OnScenarioStart() { }

    // Per-frame, for continuous behaviour (e.g. cast interruption).
    void OnTick(float deltaSeconds) { }

    // Answers for state the client never receives (ILocalPlayerInput): what a button is (Ninjutsu
    // after mudras), a cast time in ms (a spell Dualcast makes instant), and 0 to lift a refusal.
    // null leaves it to the game.
    uint? AdjustActionId(uint actionId) => null;
    int? AdjustCastTime(ActionType actionType, uint actionId) => null;
    uint? AdjustActionStatus(ActionType actionType, uint actionId, bool checkRecast) => null;
}
