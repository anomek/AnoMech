using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AnoMech.SafetyTests.Architecture;

// The built plugin, read as IL (never loaded or run). Release by default, because that is what
// users get; ANOMECH_DLLS (';'-separated) adds or replaces builds, e.g. a Debug one.
public sealed class PluginIL
{
    private static readonly Lazy<IReadOnlyList<PluginIL>> Loaded = new(LoadAll);

    public static IReadOnlyList<PluginIL> Builds => Loaded.Value;

    public string Path { get; }
    public bool IsRelease { get; }
    public ModuleDefinition Module { get; }
    public IReadOnlyList<MethodDefinition> Methods { get; }

    private PluginIL(string path)
    {
        Path = path;
        Module = ModuleDefinition.ReadModule(path, new ReaderParameters { ReadSymbols = false, InMemory = true });
        IsRelease = !JitOptimizationsDisabled(Module.Assembly);
        Methods = Module.GetTypes().SelectMany(t => t.Methods).Where(m => m.HasBody).ToList();
    }

    // Debug builds carry [Debuggable] with DisableOptimizations (0x100).
    private static bool JitOptimizationsDisabled(AssemblyDefinition assembly)
    {
        var debuggable = assembly.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "DebuggableAttribute");
        if (debuggable == null) return false;
        var args = debuggable.ConstructorArguments;
        if (args.Count == 1) return (Convert.ToInt32(args[0].Value) & 0x100) != 0;
        return args.Count == 2 && args[1].Value is true;
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(System.IO.Path.Combine(dir.FullName, "AnoMech.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find AnoMech.sln above the test binaries.");
    }

    private static IReadOnlyList<PluginIL> LoadAll()
    {
        var root = RepoRoot();
        var configured = Environment.GetEnvironmentVariable("ANOMECH_DLLS");
        var paths = !string.IsNullOrWhiteSpace(configured)
            ? configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : new[] { "AnoMech/bin/x64/Release/AnoMech.dll", "AnoMech/bin/Release/AnoMech.dll" }
                .Select(p => System.IO.Path.Combine(root, p)).Where(File.Exists).Take(1).ToList();
        if (paths.Count == 0)
            throw new InvalidOperationException(
                "No built plugin found. Build it first: dotnet build AnoMech.sln -c Release -p:Platform=x64 (or set ANOMECH_DLLS).");
        var newestSource = Directory.EnumerateFiles(System.IO.Path.Combine(root, "AnoMech"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}") && !f.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}"))
            .Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max();
        foreach (var p in paths)
        {
            if (!File.Exists(p)) throw new InvalidOperationException($"ANOMECH_DLLS names {p}, which does not exist.");
            if (File.GetLastWriteTimeUtc(p) < newestSource && Environment.GetEnvironmentVariable("ANOMECH_ALLOW_STALE_DLL") != "1")
                throw new InvalidOperationException($"{p} is older than the plugin's sources; rebuild it before running these tests.");
        }
        return paths.Select(p => new PluginIL(p)).ToList();
    }

    // "Namespace.Type::Member" for a method reference, with generic arguments dropped.
    public static string Name(MethodReference m) => $"{TypeName(m.DeclaringType)}::{m.Name}";

    public static string Name(FieldReference f) => $"{TypeName(f.DeclaringType)}::{f.Name}";

    public static string TypeName(TypeReference t)
    {
        var e = t.GetElementType();
        var name = e.FullName;
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    // The user-written method a compiler-generated one belongs to (a lambda, a local function,
    // an iterator or async state machine), as "Namespace.Type::Method".
    public static string Owner(MethodDefinition m)
    {
        var type = m.DeclaringType;
        string? method = GeneratedFrom(m.Name);
        while (type.IsNested && IsCompilerGenerated(type))
        {
            method ??= GeneratedFrom(type.Name);
            type = type.DeclaringType;
        }
        return $"{TypeName(type)}::{method ?? m.Name}";
    }

    private static string? GeneratedFrom(string name)
    {
        var match = Regex.Match(name, "^<([^>]+)>");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsCompilerGenerated(TypeDefinition t)
        => t.Name.StartsWith('<') || t.CustomAttributes.Any(a => a.AttributeType.Name == "CompilerGeneratedAttribute");

    // The top-level type a method's code lives in, for namespace rules.
    public static TypeDefinition TopLevel(TypeDefinition t)
    {
        while (t.IsNested) t = t.DeclaringType;
        return t;
    }

    public MethodDefinition Method(string type, string name, int? parameters = null)
    {
        var matches = Module.GetTypes().Where(t => TypeName(t) == type).SelectMany(t => t.Methods)
            .Where(m => m.Name == name && (parameters == null || m.Parameters.Count == parameters)).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException($"Expected one method {type}::{name}{(parameters is { } p ? $" with {p} parameters" : "")} in {Path}, found {matches.Count}. If it was renamed or moved, update the safety rule that names it.");
        return matches[0];
    }

    public IEnumerable<(MethodDefinition Method, Instruction Instruction, MethodReference Callee)> Calls()
    {
        foreach (var m in Methods)
        foreach (var i in m.Body.Instructions)
            if (i.Operand is MethodReference callee && (i.OpCode.Code is Code.Call or Code.Callvirt or Code.Newobj or Code.Ldftn or Code.Ldvirtftn))
                yield return (m, i, callee);
    }

    public IEnumerable<(MethodDefinition Method, Instruction Instruction, FieldReference Field)> FieldAccesses()
    {
        foreach (var m in Methods)
        foreach (var i in m.Body.Instructions)
            if (i.Operand is FieldReference f)
                yield return (m, i, f);
    }

    // Owners of every call to a member matching `callee` (a regex over "Type::Member").
    public IReadOnlyList<string> CallersOf(string callee)
        => Calls().Where(c => Regex.IsMatch(Name(c.Callee), callee)).Select(c => Owner(c.Method)).Distinct().OrderBy(x => x).ToList();

    public override string ToString() => $"{System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path))}/{System.IO.Path.GetFileName(Path)} ({(IsRelease ? "Release" : "Debug")})";
}
