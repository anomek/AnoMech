using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.UserActions;

// The server's ActionEffect for the player's own actions, which the sim firewall eats: replayed so
// the client plays the action landing (the hit on its target) and, for a linear combo step, carries
// the Type-0x1B marker that advances the client's own combo. Combo routes (Gnashing Fang etc.) use
// a separate system. Dedup of queued/re-entrant executions is UserActions' job, so OnAction only
// sees actual, once-per-execution calls.
internal sealed unsafe class ActionEffectHandler : IUserActionHandler
{
    public void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action) return;
        var role = PlayerCombo.RoleOf(actionId);

        var player = BattleCharaProxy.LocalPlayer;
        if (!player.Exists) return;
        var am = ActionManager.Instance();
        if (am == null) return;
        // SourceSequence makes the client treat it as that action resolving.
        var target = DeliveryTarget(player);
        player.ReceiveActionEffect(new ActionEffectData(
            actionId, ActionType.Action, 0f, (ushort)actionId, 0, 0, player.Rotation, player.Position,
            AnimationTarget: target,
            // Every action lands so its hit plays on the target; a combo step also carries its marker.
            Targets: [role == ComboRole.None
                ? new ActionEffectTarget(target)
                : new ActionEffectTarget(target, [ActionEffectEntry.ActionMarker(actionId, role == ComboRole.Continuation)])],
            SourceSequence: am->LastUsedActionSequence));
    }

    // The player's target, as the server delivers it; the player itself when the target isn't a
    // registered BattleChara the client could deliver to.
    private static GameObjectId DeliveryTarget(BattleCharaProxy player)
    {
        var tgt = (GameObject*)(Plugin.TargetManager.Target?.Address ?? 0);
        if (tgt == null) return player.GameObjectId;
        var cm = CharacterManager.Instance();
        if (cm == null || cm->LookupBattleCharaByEntityId(tgt->EntityId) == null) return player.GameObjectId;
        return tgt->GetGameObjectId();
    }
}
