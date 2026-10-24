namespace AnoMech.Core.Native.Interfaces;

// The local player's own action state in the game's memory: its job gauge and its linear combo.
// With no game client behind the player there is neither.
public interface IPlayerActionState
{
    // False while there is no job gauge to read or write.
    bool HasJobGauge { get; }

    // The action a running linear combo continues from, and the seconds left to continue it; 0
    // while none runs.
    uint ComboAction { get; }
    float ComboTimer { get; }

    // The id the game uses `actionId` as at the player's level: an upgrade's.
    uint AdjustedActionId(uint actionId);
}
