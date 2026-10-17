using System;

namespace AnoMech.Core.Native.Interfaces;

// The player's own presses resolved client-side (Core/UserActions), as far as a run drives it.
public interface IUserActions : IDisposable
{
    bool Enabled { get; }
    void Enable();
    void Disable();

    // A true zone entry: snapshots the real job gauge for OnSessionEnd to restore.
    void OnSessionStart();
    void OnScenarioStart();
    void OnSessionEnd();

    void Tick(float deltaSeconds);

    // Each step the server would show the rest of the party, whether the module is on or not:
    // the player's own client has already played it for them.
    event Action<PlayerAction>? Acted;
}
