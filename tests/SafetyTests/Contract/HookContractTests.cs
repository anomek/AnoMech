using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using AnoMech.SafetyTests.Harness;
using Dalamud.Hooking;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace AnoMech.SafetyTests.Contract;

// The firewall tests run the plugin's hooks as the fake Hook<T>. Here Dalamud's own hooks, both
// backends, patch a function in this process, and every sequence of the operations the plugin
// uses runs on a real and a fake hook side by side. How the backends fail (not reachable without
// breaking the process) is read from Dalamud's IL.
public unsafe class HookContractTests
{
    public delegate nint Target(nint x);

    private const nint FunctionAdds = 0x1111;
    private const nint DetourAdds = 0x2222;
    private static readonly string[] Operations = ["Enable", "Disable", "Dispose"];

    public static TheoryData<HookBackend> Backends => new() { HookBackend.Reloaded, HookBackend.SafetyHook };

    [Theory]
    [MemberData(nameof(Backends))]
    public void EverySequenceOfTheOperationsThePluginUses_BehavesAsTheFake(HookBackend backend)
    {
        var differences = new List<string>();
        foreach (var sequence in Sequences(4))
        {
            var real = Observe(new RealHook(backend), sequence);
            var fake = Observe(new FakeHook(backend), sequence);
            if (!real.SequenceEqual(fake))
                differences.Add($"[{string.Join(", ", sequence)}]\n  real: {string.Join(" | ", real)}\n  fake: {string.Join(" | ", fake)}");
        }
        Assert.True(differences.Count == 0,
            $"Dalamud's {backend} hook no longer behaves as the fake Hook<T>; update the fake, then whatever in the firewall relied on the old behaviour:\n{string.Join("\n", differences.Take(8))}");
    }

    // Hook<T>.CreateBackend picks between exactly the two backends the fake models.
    [Fact]
    public void Dalamud_HasExactlyTheTwoModelledBackends()
    {
        var hook = RealDalamud.Type("Dalamud.dll", "Dalamud.Hooking.Hook`1");
        var built = RealDalamud.Method(hook, "CreateBackend").Body.Instructions
            .Where(i => i.OpCode == OpCodes.Newobj)
            .Select(i => ((MethodReference)i.Operand).DeclaringType.GetElementType().FullName)
            .Where(t => t.StartsWith("Dalamud.Hooking."))
            .ToHashSet();
        Assert.Equal(new HashSet<string> { "Dalamud.Hooking.Internal.ReloadedHook`1", "Dalamud.Hooking.Internal.SafetyHookHook`1" }, built);
        Assert.Contains("Dalamud.Hooking.Hook`1::CreateBackend", RealDalamud.CalledNames(RealDalamud.Method(hook, "FromAddress")));
    }

    // Executed: the backend FromAddress builds is the one DALAMUD_USE_SAFETYHOOK selects.
    [Fact]
    public void FromAddress_BuildsTheConfiguredBackend()
    {
        var useSafetyHook = (bool)RealDalamud.RuntimeType("Dalamud", "Dalamud.Configuration.Internal.EnvironmentConfiguration")
            .GetProperty("DalamudUseSafetyHook", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(null)!;
        var hookType = RealDalamud.RuntimeType("Dalamud", "Dalamud.Hooking.Hook`1").MakeGenericType(typeof(Target));
        var fromAddress = hookType.GetMethod("FromAddress", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        Target detour = x => x + DetourAdds;
        var hook = (IDisposable)Unwrap(() => fromAddress.Invoke(null, [CodePage.NewFunction(), detour, false, typeof(HookContractTests).Assembly]))!;
        try
        {
            Assert.Equal(useSafetyHook ? "SafetyHookHook`1" : "ReloadedHook`1", hook.GetType().Name);
        }
        finally
        {
            hook.Dispose();
        }
    }

    // SafetyHook reports a failed native patch by throwing InvalidOperationException from Enable
    // and Disable, with the hook left as it was: what the fake throws on a refused change.
    [Theory]
    [InlineData("Enable")]
    [InlineData("Disable")]
    public void SafetyHook_ThrowsWhenTheNativePatchFails_AsTheFakeDoes(string operation)
    {
        var method = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", "Dalamud.Hooking.Internal.SafetyHookHook`1"), operation);
        Assert.Contains($"Dalamud.Hooking.Internal.SafetyHookNative::{operation}", RealDalamud.CalledNames(method));
        Assert.Contains(method.Body.Instructions, i => i.OpCode == OpCodes.Throw
                                                        && i.Previous is { OpCode.Code: Code.Newobj, Operand: MethodReference ctor }
                                                        && ctor.DeclaringType.FullName == "System.InvalidOperationException");
        Assert.DoesNotContain(method.Body.ExceptionHandlers, h => h.HandlerType == ExceptionHandlerType.Catch);

        var fake = new FakeHook(HookBackend.SafetyHook);
        if (operation == "Disable") fake.Do("Enable");
        fake.Refuse = true;
        var before = fake.IsEnabled;
        Assert.Throws<InvalidOperationException>(() => fake.Do(operation));
        Assert.Equal(before, fake.IsEnabled);
    }

    // A created Reloaded hook only rewrites its own stub, so Enable and Disable have no failure of
    // their own; nothing Reloaded throws is caught on the way to the plugin.
    [Theory]
    [InlineData("Enable")]
    [InlineData("Disable")]
    public void Reloaded_CatchesNothing(string operation)
    {
        var method = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", "Dalamud.Hooking.Internal.ReloadedHook`1"), operation);
        Assert.Contains($"Reloaded.Hooks.Definitions.IHook`1::{operation}", RealDalamud.CalledNames(method));
        Assert.DoesNotContain(method.Body.ExceptionHandlers, h => h.HandlerType == ExceptionHandlerType.Catch);
    }

    // The plugin's hooks come from the plugin-scoped provider: built by Hook<T>.FromAddress and
    // tracked, and whatever the plugin leaves is disposed after it unloads (the harness's
    // DalamudDisposesPluginHooks).
    [Fact]
    public void ThePluginsHooks_ComeFromFromAddress_AndAreDisposedOnUnload()
    {
        var provider = RealDalamud.Type("Dalamud.dll", "Dalamud.Hooking.Internal.GameInteropProviderPluginScoped");
        var hookFromAddress = RealDalamud.Method(provider, "HookFromAddress", "System.IntPtr", "T", "Dalamud.Plugin.Services.IGameInteropProvider/HookBackend");
        var calls = RealDalamud.CalledNames(hookFromAddress).ToList();
        Assert.Contains("Dalamud.Hooking.Hook`1::FromAddress", calls);
        Assert.Contains("Dalamud.Utility.WeakConcurrentCollection`1::Add", calls);
        var dispose = RealDalamud.Method(provider, "Dalamud.IInternalDisposableService.DisposeService");
        Assert.Contains("System.IDisposable::Dispose", RealDalamud.CalledNames(dispose));
    }

    // ---- the two sides ----------------------------------------------------------------------

    private interface IHookUnderTest
    {
        void Do(string operation);
        nint Call(nint x);
        nint CallOriginal(nint x);
        bool IsEnabled { get; }
        bool IsDisposed { get; }
    }

    private static List<string[]> Sequences(int maxLength)
    {
        var all = new List<string[]> { Array.Empty<string>() };
        var frontier = all.ToList();
        for (var i = 0; i < maxLength; i++)
        {
            frontier = frontier.SelectMany(s => Operations.Select(op => s.Append(op).ToArray())).ToList();
            all.AddRange(frontier);
        }
        return all;
    }

    private static List<string> Observe(IHookUnderTest hook, string[] sequence)
    {
        var trace = new List<string> { $"created: {State(hook)}" };
        foreach (var operation in sequence)
            trace.Add($"{operation} {Outcome(() => hook.Do(operation))}: {State(hook)}");
        return trace;
    }

    private static string State(IHookUnderTest hook)
        => $"a call runs {Route(hook.Call(1))}, IsEnabled={hook.IsEnabled}, IsDisposed={hook.IsDisposed}, Original {Outcome(() => Route(hook.CallOriginal(1)))}";

    private static string Route(nint result) => (result - 1) switch
    {
        FunctionAdds => "the function",
        DetourAdds => "the detour",
        var other => $"something else (+0x{other:X})",
    };

    private static string Outcome(Action action)
    {
        try
        {
            action();
            return "ok";
        }
        catch (Exception e)
        {
            return $"throws {e.GetType().Name}";
        }
    }

    private static string Outcome(Func<string> value)
    {
        try
        {
            return $"runs {value()}";
        }
        catch (Exception e)
        {
            return $"throws {e.GetType().Name}";
        }
    }

    private static object? Unwrap(Func<object?> call)
    {
        try
        {
            return call();
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            ExceptionDispatchInfo.Throw(e.InnerException);
            throw;
        }
    }

    private sealed class RealHook : IHookUnderTest
    {
        private readonly object hook;
        private readonly nint function;
        private readonly Target detour = x => x + DetourAdds;

        public RealHook(HookBackend backend)
        {
            function = CodePage.NewFunction();
            var type = RealDalamud.RuntimeType("Dalamud", backend == HookBackend.Reloaded
                    ? "Dalamud.Hooking.Internal.ReloadedHook`1"
                    : "Dalamud.Hooking.Internal.SafetyHookHook`1")
                .MakeGenericType(typeof(Target));
            var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, [typeof(nint), typeof(Target), typeof(Assembly)])
                       ?? throw new InvalidOperationException($"{type.Name} no longer has the (address, detour, calling assembly) constructor.");
            hook = Unwrap(() => ctor.Invoke([function, detour, typeof(HookContractTests).Assembly]))!;
        }

        public void Do(string operation) => Unwrap(() => hook.GetType().GetMethod(operation, Type.EmptyTypes)!.Invoke(hook, null));
        public nint Call(nint x) => ((delegate* unmanaged<nint, nint>)function)(x);
        public nint CallOriginal(nint x) => ((Target)Get("Original"))(x);
        public bool IsEnabled => (bool)Get("IsEnabled");
        public bool IsDisposed => (bool)Get("IsDisposed");

        private object Get(string property) => Unwrap(() => hook.GetType().GetProperty(property)!.GetValue(hook))!;
    }

    private sealed class FakeHook : IHookUnderTest, IHookHost
    {
        private readonly Hook<Target> hook;

        public FakeHook(HookBackend backend)
        {
            Backend = backend;
            hook = new Hook<Target>(0x1000, x => x + DetourAdds, x => x + FunctionAdds, HookRole.Send, this);
        }

        public HookBackend Backend { get; }
        public bool Refuse { get; set; }

        public void Do(string operation)
        {
            switch (operation)
            {
                case "Enable": hook.Enable(); break;
                case "Disable": hook.Disable(); break;
                case "Dispose": hook.Dispose(); break;
                default: throw new ArgumentException(operation);
            }
        }

        public nint Call(nint x) => hook.Target(x);
        public nint CallOriginal(nint x) => hook.Original(x);
        public bool IsEnabled => hook.IsEnabled;
        public bool IsDisposed => hook.IsDisposed;

        bool IHookHost.Refuses(HookRole role, bool enabling) => Refuse;

        void IHookHost.OnHookStateChanged(HookRole role, bool enabled, string by)
        {
        }

        void IHookHost.OnHookDisposed(HookRole role)
        {
        }
    }

    // Functions to hook, each fresh: a hook leaves its jump in place after it is disposed.
    private static class CodePage
    {
        [DllImport("kernel32", SetLastError = true)]
        private static extern nint VirtualAlloc(nint address, nuint size, uint type, uint protect);

        private const uint CommitAndReserve = 0x3000;
        private const uint ExecuteReadWrite = 0x40;
        private const int PageSize = 0x10000;
        private const int Slot = 64;

        // mov rax, rcx; add rax, 0x1111; three add rax, 0; ret -- plain instructions, long enough
        // for either backend's jump.
        private static readonly byte[] Body =
            [0x48, 0x89, 0xC8, 0x48, 0x05, 0x11, 0x11, 0x00, 0x00, 0x48, 0x83, 0xC0, 0x00, 0x48, 0x83, 0xC0, 0x00, 0x48, 0x83, 0xC0, 0x00, 0xC3];

        private static nint page;
        private static int used = PageSize;

        public static nint NewFunction()
        {
            if (used + Slot > PageSize)
            {
                page = VirtualAlloc(0, PageSize, CommitAndReserve, ExecuteReadWrite);
                if (page == 0) throw new InvalidOperationException($"VirtualAlloc failed ({Marshal.GetLastPInvokeError()}).");
                used = 0;
            }
            var at = page + used;
            used += Slot;
            var code = Enumerable.Repeat((byte)0xCC, Slot).ToArray();
            Body.CopyTo(code, 0);
            Marshal.Copy(code, 0, at, Slot);
            return at;
        }
    }
}
