namespace AnoMech.Core.EnemyActions;

// One cast in flight, for EnemyActionHandler to tell whether a CancelCast reached it before its
// resolve and release.
internal sealed class EnemyActionCast
{
    public bool IsCancelled { get; private set; }

    internal void Cancelled() => IsCancelled = true;
}
