using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// One feature of the UserActions module — resolves some player behaviour the sim
// firewall would otherwise leave unhandled (combo, cast interruption, resource
// generation, ...). Every hook is optional (default no-op); a handler implements only
// what it needs. Handlers are stored and dispatched as a list by UserActions.
internal interface IUserActionHandler
{
    // A player action executed. UserActions dedupes this to once per actual execution
    // (a queued/spammed press or our own re-entrant fire won't call it again).
    void OnAction(ActionType actionType, uint actionId) { }

    // Same, for a handler that needs who the action was aimed at.
    void OnAction(ActionType actionType, uint actionId, ulong targetId) => OnAction(actionType, actionId);

    // A player action began a cast bar; its OnAction follows only if the cast completes.
    void OnCastStart(ActionType actionType, uint actionId) { }

    // A scenario started — reset cooldowns/gauges the server would otherwise track.
    void OnScenarioStart() { }

    // Per-frame, for continuous behaviour (e.g. cast interruption).
    void OnTick(float deltaSeconds) { }
}
