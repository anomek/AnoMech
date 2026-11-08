using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// No game memory behind the headless player: no job gauge to edit, and no combo running.
internal sealed class FakePlayerActionState : IPlayerActionState
{
    public bool HasJobGauge => false;
    public uint ComboAction => 0;
    public float ComboTimer => 0f;
    public uint AdjustedActionId(uint actionId) => actionId;
}
