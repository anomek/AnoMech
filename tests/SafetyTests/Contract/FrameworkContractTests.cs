using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnoMech.SafetyTests.Harness;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace AnoMech.SafetyTests.Contract;

// What the harness's frame assumes of Dalamud's framework: Framework.Run always queues, the queue
// drains once per tick before the Update handlers, work queued while draining waits a tick, a
// tick's tasks run in no set order, and every Update handler runs on its own every tick.
public class FrameworkContractTests
{
    private const string Framework = "Dalamud.Game.Framework";

    [Fact]
    public void PluginFrameworkRun_QueuesOnTheFrameworkScheduler_NeverInline()
    {
        var scoped = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", "Dalamud.Game.FrameworkPluginScoped"), "Run", "System.Action", "System.Threading.CancellationToken");
        Assert.Equal(new[] { $"{Framework}::Run" }, RealDalamud.CalledNames(scoped));

        var framework = RealDalamud.Type("Dalamud.dll", Framework);
        var run = RealDalamud.Method(framework, "Run", "System.Action", "System.Threading.CancellationToken");
        var calls = RealDalamud.CalledNames(run).ToList();
        Assert.Contains("System.Threading.Tasks.TaskFactory::StartNew", calls);
        Assert.DoesNotContain(calls, c => c.EndsWith("::Invoke") || c.Contains("RunOnFrameworkThread") || c.Contains("IsInFrameworkUpdateThread"));

        var ctor = framework.Methods.Single(m => m.IsConstructor && !m.IsStatic);
        var created = ctor.Body.Instructions.Where(i => i.OpCode == OpCodes.Newobj).Select(i => RealDalamud.Name((MethodReference)i.Operand)).ToList();
        var scheduler = created.IndexOf("Dalamud.Utility.ThreadBoundTaskScheduler::.ctor");
        var factory = created.IndexOf("System.Threading.Tasks.TaskFactory::.ctor");
        Assert.True(scheduler >= 0 && factory > scheduler, "The framework's task factory is no longer built over its ThreadBoundTaskScheduler.");
    }

    // The real scheduler, driven the way RunFrameworkTick drives it, and the harness's framework
    // queue, on the same script.
    [Fact]
    public void TheScheduler_RunsWhatWasQueuedBeforeTheDrain_AndDefersWhatTasksQueue()
    {
        Assert.Equal(RealDrains(), HarnessDrains());
    }

    // It walks a ConcurrentDictionary's keys: the fuzz tries a frame's tasks in random orders.
    [Fact]
    public void TheScheduler_PromisesNoOrderWithinATick()
    {
        var run = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", "Dalamud.Utility.ThreadBoundTaskScheduler"), "Run");
        Assert.Contains("System.Collections.Concurrent.ConcurrentDictionary`2::get_Keys", RealDalamud.CalledNames(run));
    }

    [Fact]
    public void QueuedTasks_RunBeforeTheUpdateHandlers_AsTheHarnessDefaults()
    {
        var tick = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", Framework), "RunFrameworkTick");
        var instructions = tick.Body.Instructions.ToList();
        var lastDrain = instructions.FindLastIndex(i => i.Operand is MethodReference m && RealDalamud.Name(m) == "Dalamud.Utility.ThreadBoundTaskScheduler::Run");
        var update = instructions.FindIndex(i => i.Operand is FieldReference f && f.Name == "Update" && f.DeclaringType.FullName == Framework);
        Assert.True(lastDrain >= 0 && update > lastDrain, "Dalamud no longer drains the framework queue before dispatching Update.");
        using var game = new VirtualGame();
        Assert.True(game.FrameworkTasksBeforeUpdate);
    }

    // A throwing handler (the debug bench's, say) cannot stop the plugin's own, which runs the guard.
    [Fact]
    public void EachUpdateHandler_RunsInItsOwnTryCatch()
    {
        var forward = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", "Dalamud.Game.FrameworkPluginScoped"), "OnUpdateForward");
        Assert.Contains($"{Framework}::ProfileAndInvoke", RealDalamud.CalledNames(forward));
        var invoke = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", Framework), "ProfileAndInvoke");
        Assert.Contains("System.Delegate::EnumerateInvocationList", RealDalamud.CalledNames(invoke));
        var handlerCalls = RealDalamud.CallsIn(invoke)
            .Where(i => RealDalamud.Name((MethodReference)i.Operand) == "Dalamud.Plugin.Services.IFramework/OnUpdateDelegate::Invoke")
            .ToList();
        Assert.NotEmpty(handlerCalls);
        Assert.All(handlerCalls, c => Assert.True(RealDalamud.IsInsideCatchedTry(invoke, c), "An Update handler is no longer invoked inside its own try/catch."));
    }

    // Update is dispatched every tick from construction until Dalamud itself unloads.
    [Fact]
    public void UpdateHandlers_RunEveryTick_UntilDalamudUnloads()
    {
        var setters = RealDalamud.CallersOf("Dalamud.dll", $"{Framework}::set_DispatchUpdateEvents");
        Assert.Equal(new[] { $"{Framework}::UnloadDalamud" }, setters);
        var ctor = RealDalamud.Type("Dalamud.dll", Framework).Methods.Single(m => m.IsConstructor && !m.IsStatic);
        Assert.Contains(ctor.Body.Instructions, i => i.OpCode == OpCodes.Stfld
                                                     && ((FieldReference)i.Operand).Name == "<DispatchUpdateEvents>k__BackingField"
                                                     && i.Previous.OpCode == OpCodes.Ldc_I4_1);
        var tick = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", Framework), "RunFrameworkTick");
        Assert.Contains($"{Framework}::get_DispatchUpdateEvents", RealDalamud.CalledNames(tick));
    }

    // A, B and D queued before the first drain (D from another thread); A queues C as it runs.
    private static List<string> RealDrains()
    {
        var schedulerType = RealDalamud.RuntimeType("Dalamud", "Dalamud.Utility.ThreadBoundTaskScheduler");
        var scheduler = (TaskScheduler)Activator.CreateInstance(schedulerType, Thread.CurrentThread)!;
        var drain = schedulerType.GetMethod("Run")!;
        var factory = new TaskFactory(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, scheduler);
        var ran = new List<string>();
        void Queue(string name) => factory.StartNew(() =>
        {
            lock (ran) ran.Add(name);
            if (name == "A") Queue("C");
        }, CancellationToken.None);
        return Script(Queue, () => drain.Invoke(scheduler, null), ran, fromAnotherThread: name => Task.Run(() => Queue(name)).Wait());
    }

    private static List<string> HarnessDrains()
    {
        using var game = new VirtualGame();
        var ran = new List<string>();
        void Queue(string name) => game.Framework.Run(() =>
        {
            ran.Add(name);
            if (name == "A") Queue("C");
        });
        return Script(Queue, () => game.Frame(), ran, fromAnotherThread: Queue);
    }

    private static List<string> Script(Action<string> queue, Action drain, List<string> ran, Action<string> fromAnotherThread)
    {
        var drains = new List<string>();
        void Note(string when)
        {
            lock (ran)
            {
                drains.Add($"{when}: {string.Join(",", ran.OrderBy(n => n))}");
                ran.Clear();
            }
        }
        queue("A");
        queue("B");
        fromAnotherThread("D");
        Note("queued");
        for (var i = 1; i <= 3; i++)
        {
            drain();
            Note($"drain {i}");
        }
        return drains;
    }
}
