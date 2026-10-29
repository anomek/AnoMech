using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// The gauge the local player's HUD shows: empty solo in the inn, then whatever its ActorControls set.
internal sealed class FakeLimitBreakController(FakeCharacter player) : ILimitBreakController
{
    public LimitBreakBars? Read() => player.LimitBreak;
}
