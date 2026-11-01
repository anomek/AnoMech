using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// Applies a player action's client-side effects from the JobActions table: first clears
// any statuses the action ends (StatusClearedOnAction), then applies its effects — gauge
// writes and status grants, gated by Combo(...) / Random(...) wrappers. Replaces the old
// resource-generation and Continuation handlers. MUST run before ActionEffectHandler: combo-gated
// effects read the PRE-advance combo state, which ActionEffectHandler advances.
internal sealed class JobActionHandler : IUserActionHandler
{
    private readonly Random rng = new();

    public void OnAction(ActionType actionType, uint actionId, ulong targetId)
    {
        if (actionType != ActionType.Action) return;
        var player = Plugin.Game?.Player;
        if (player == null) return;

        var free = JobActions.IsFree(player, actionId);
        // Clear first so a line action can re-grant after its own weaponskill clear.
        JobActions.ClearStatuses(player, actionId);
        if (!free) JobActions.ApplyCost(actionId);   // generic spender pass (sheet PrimaryCost)

        JobActions.ApplyEffects(player, actionId, targetId, rng);
    }
}
