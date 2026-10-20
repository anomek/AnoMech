using System.Collections.Generic;

namespace AnoMech.Core.EnemyActions;

// One cast in flight, for EnemyActionHandler to tell whether a CancelCast reached it before its
// resolve and release.
internal sealed class EnemyActionCast
{
    public bool IsCancelled { get; private set; }

    // Its resolve and release, still queued; a cancel takes them off the queue.
    internal List<System.Action> PendingSteps { get; } = [];

    internal void Cancelled() => IsCancelled = true;
}
