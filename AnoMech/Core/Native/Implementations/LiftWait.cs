namespace AnoMech.Core.Native.Implementations;

// How long the zone guard holds a blocked lift before it stops the game. The client loads the inn
// on its own time, then sends post-load packets the filter must still catch, so while it reports
// that load the lift waits up to InnLoadSeconds from the reload. Any other blocker gets
// RetrySeconds from the first check.
internal static class LiftWait
{
    internal const double RetrySeconds = 3;
    internal const double InnLoadSeconds = 10;

    internal static bool Waits(bool innLoading, double sinceReload, double sinceCheck)
        => innLoading ? sinceReload < InnLoadSeconds : sinceCheck < RetrySeconds;
}
