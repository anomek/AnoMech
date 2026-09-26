using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AnoMech.SafetyTests.Contract;

// The Dalamud the plugin builds against, found the way Dalamud.NET.Sdk finds it: DALAMUD_HOME,
// else XIVLauncher's dev folder (where CI unpacks the release). Read as metadata; where its code
// runs without the game, loaded into a context of its own and run.
internal static class RealDalamud
{
    public static string Folder { get; } = Locate();

    private static string Locate()
    {
        var home = Environment.GetEnvironmentVariable("DALAMUD_HOME");
        var dir = !string.IsNullOrEmpty(home)
            ? home
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
        if (!File.Exists(Path.Combine(dir, "Dalamud.dll")))
            throw new InvalidOperationException($"No Dalamud.dll in {dir}. The contract tests check the fakes against the Dalamud the plugin builds with: install it as for a plugin build, or set DALAMUD_HOME.");
        return dir;
    }

    // ---- metadata ----------------------------------------------------------------------------

    private static readonly ConcurrentDictionary<string, ModuleDefinition> Modules = new();

    public static ModuleDefinition Module(string file) => Modules.GetOrAdd(file, f =>
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Folder);
        return ModuleDefinition.ReadModule(Path.Combine(Folder, f), new ReaderParameters { AssemblyResolver = resolver });
    });

    // Nested types as "Outer/Inner".
    public static TypeDefinition Type(string file, string fullName)
        => Module(file).GetType(fullName) ?? throw new InvalidOperationException($"{file} no longer has {fullName}; update the contract (and the fake) to what replaced it.");

    public static MethodDefinition Method(TypeDefinition type, string name, params string[] parameterTypes)
    {
        var found = type.Methods.Where(m => m.Name == name
                                            && (parameterTypes.Length == 0 || m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameterTypes)))
            .ToList();
        return found.Count == 1
            ? found[0]
            : throw new InvalidOperationException($"{type.FullName}::{name}({string.Join(", ", parameterTypes)}) matches {found.Count} methods; update the contract.");
    }

    public static IEnumerable<Instruction> CallsIn(MethodDefinition method)
        => method.Body.Instructions.Where(i => i.Operand is MethodReference && i.OpCode.FlowControl == FlowControl.Call);

    public static string Name(MethodReference m) => $"{m.DeclaringType.GetElementType().FullName}::{m.Name}";

    public static IEnumerable<string> CalledNames(MethodDefinition method) => CallsIn(method).Select(i => Name((MethodReference)i.Operand));

    // Every method of the module that calls `callee` (by Name()).
    public static List<string> CallersOf(string file, string callee)
        => Module(file).GetTypes()
            .SelectMany(t => t.Methods.Where(m => m.HasBody))
            .Where(m => CalledNames(m).Contains(callee))
            .Select(m => $"{m.DeclaringType.FullName}::{m.Name}")
            .Distinct()
            .ToList();

    public static bool IsInsideCatchedTry(MethodDefinition method, Instruction instruction)
        => method.Body.ExceptionHandlers.Any(h => h.HandlerType == ExceptionHandlerType.Catch
                                                 && h.TryStart.Offset <= instruction.Offset
                                                 && (h.TryEnd == null || instruction.Offset < h.TryEnd.Offset));

    // ---- execution ---------------------------------------------------------------------------

    private static readonly Lazy<AssemblyLoadContext> Context = new(() =>
    {
        var context = new AssemblyLoadContext("real-dalamud");
        context.Resolving += (ctx, name) =>
        {
            var path = Path.Combine(Folder, name.Name + ".dll");
            return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
        };
        context.ResolvingUnmanagedDll += (_, name) =>
        {
            var path = Path.Combine(Folder, name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name : name + ".dll");
            return File.Exists(path) ? NativeLibrary.Load(path) : 0;
        };
        return context;
    });

    public static Assembly Load(string name) => Context.Value.LoadFromAssemblyName(new AssemblyName(name));

    public static System.Type RuntimeType(string assembly, string fullName)
        => Load(assembly).GetType(fullName, throwOnError: false) ?? throw new InvalidOperationException($"{assembly} no longer has {fullName}.");
}
