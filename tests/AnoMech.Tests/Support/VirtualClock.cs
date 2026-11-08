namespace AnoMech.Tests;

// A session's clock, for every client and the relay alike: it moves only when the test moves it,
// and fires each timer due by then on the test's own thread, in the order they fall due.
internal sealed class VirtualClock : TimeProvider
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly PriorityQueue<(Timer Timer, long Version), (long DueTicks, long Order)> due = new();
    // The relay's quiet close resumes on the thread pool (ManagedWebSocket's CloseAsync waits on its
    // own receive lock), and can set or drop a timer from there.
    private readonly object gate = new();
    private long order;
    private long ticks;

    public double Now => ticks / (double)TimeSpan.TicksPerSecond;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => ticks;
    public override DateTimeOffset GetUtcNow() => Epoch.AddTicks(ticks);
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void AdvanceTo(double seconds)
    {
        ticks = Math.Max(ticks, (long)Math.Round(seconds * TimeSpan.TicksPerSecond));
        while (true)
        {
            (Timer Timer, long Version) next;
            lock (gate)
            {
                if (!due.TryPeek(out next, out var at) || at.DueTicks > ticks) return;
                due.Dequeue();
            }
            next.Timer.Fire(next.Version);
        }
    }

    private void Schedule(Timer timer, long version, long dueTicks) => due.Enqueue((timer, version), (dueTicks, order++));

    private sealed class Timer(VirtualClock clock, TimerCallback callback, object? state) : ITimer
    {
        // Each Change or Dispose leaves what was scheduled before it to fire as nothing.
        private long version;
        private long periodTicks;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock.gate)
            {
                var current = ++version;
                if (dueTime == Timeout.InfiniteTimeSpan) return true;
                periodTicks = period == Timeout.InfiniteTimeSpan ? 0 : period.Ticks;
                clock.Schedule(this, current, clock.ticks + Math.Max(0, dueTime.Ticks));
                return true;
            }
        }

        public void Fire(long scheduled)
        {
            lock (clock.gate)
            {
                if (scheduled != version) return;
                if (periodTicks > 0) clock.Schedule(this, version, clock.ticks + periodTicks);
            }
            callback(state);
        }

        public void Dispose()
        {
            lock (clock.gate) version++;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
