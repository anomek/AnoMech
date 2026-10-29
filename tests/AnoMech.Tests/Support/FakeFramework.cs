using System.Reflection;
using Dalamud.Plugin.Services;

namespace AnoMech.Tests;

// Dalamud queues Framework.Run work and drains it at the start of the next frame, before Update.
// Only Run(Action) is modelled; any other member throws.
public class FakeFramework : DispatchProxy
{
    private readonly Queue<Action> pending = new();

    internal static FakeFramework Create() => (FakeFramework)(object)Create<IFramework, FakeFramework>();

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is { Name: nameof(IFramework.Run) } && args is [Action action, ..])
        {
            pending.Enqueue(action);
            return Task.CompletedTask;
        }
        throw new NotSupportedException($"FakeFramework does not model IFramework.{method?.Name}");
    }

    internal bool Idle => pending.Count == 0;

    internal void RunPending()
    {
        for (var count = pending.Count; count > 0; count--)
            pending.Dequeue()();
    }
}
