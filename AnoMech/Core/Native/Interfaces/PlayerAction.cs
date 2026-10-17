using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Native.Interfaces;

// A step of the player's own action: a cast bar starting (CastSeconds long), then resolving or
// interrupted; an instant action only resolves. TargetId is the GameObjectId it was aimed at.
public sealed record PlayerAction(PlayerActionStep Step, ActionType ActionType, uint ActionId, ulong TargetId, float CastSeconds = 0f);

public enum PlayerActionStep
{
    CastStarted,
    Resolved,
    Interrupted,
}
