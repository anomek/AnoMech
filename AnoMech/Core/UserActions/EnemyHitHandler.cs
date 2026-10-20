using System.Linq;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.UserActions;

// Tells an enemy the player's action landed on it (SimEnemy.Attacked); auto-attacks report from
// AutoAttackHandler.
internal sealed class EnemyHitHandler : IUserActionHandler
{
    public void OnAction(ActionType actionType, uint actionId, ulong targetId)
    {
        if (actionType != ActionType.Action || Plugin.Game is not { Player: { } player } game) return;
        if (game.World.Children.OfType<SimEnemy>().FirstOrDefault(e => e.IsActive && (ulong)e.GameObjectId == targetId) is not { } enemy) return;
        Report(enemy, player);
    }

    public static void Report(SimEnemy enemy, SimCharacter by)
    {
        enemy.NoteAttack(by);
        HostReport.Attacked(enemy);
    }
}
