using System.Collections.Generic;
using System.Linq;
using AnoMech.SafetyTests.Harness;
using Mono.Cecil;
using Xunit;

namespace AnoMech.SafetyTests.Contract;

// The fakes stand in for real Dalamud, ClientStructs and Lumina types under the same names. They may
// leave members out; what they declare must be what the real assemblies declare: every enum value,
// every explicit field offset, every member's signature. Harness plumbing and the few deliberate
// approximations are listed below with the reason.
public class FakeShapeContractTests
{
    private static readonly string[] RealFiles = ["Dalamud.dll", "FFXIVClientStructs.dll", "Lumina.dll", "Lumina.Excel.dll"];
    private static readonly string[] MirroredNamespaces = ["Dalamud.", "FFXIVClientStructs.", "Lumina."];

    // Members only the harness needs, on fake types.
    private static readonly HashSet<string> HarnessOnly =
    [
        "Dalamud.Hooking.Hook`1::Role", "Dalamud.Hooking.Hook`1::Detour", "Dalamud.Hooking.Hook`1::Target",
        "Dalamud.Hooking.Hook`1::ForceDisable", "Dalamud.Hooking.Hook`1::ForceDispose",
        "FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject::FakeId",
        "FFXIVClientStructs.FFXIV.Client.Game.InventoryManager::Equipped",
    ];

    // Shapes the fakes simplify, where only what the firewall code does with them matters.
    private static readonly Dictionary<string, string> Approximated = new()
    {
        ["Reserved"] = "padding in a fake struct whose real layout the firewall never reads",
    };

    private static ModuleDefinition Fakes { get; } = ModuleDefinition.ReadModule(typeof(VirtualGame).Assembly.Location);

    private static IEnumerable<TypeDefinition> FakeTypes()
        => Fakes.GetTypes().Where(t => MirroredNamespaces.Any(n => Outermost(t).Namespace.StartsWith(n)) && !t.Name.StartsWith('<'));

    private static TypeDefinition Outermost(TypeDefinition t) => t.DeclaringType is { } outer ? Outermost(outer) : t;

    private static TypeDefinition? Real(TypeDefinition fake)
        => RealFiles.Select(f => RealDalamud.Module(f).GetType(fake.FullName)).FirstOrDefault(t => t != null);

    [Fact]
    public void EveryFakeType_IsARealType()
    {
        var missing = FakeTypes().Where(t => Real(t) == null && !t.Name.StartsWith('<')).Select(t => t.FullName).ToList();
        Assert.True(missing.Count == 0, $"Fake types with no real counterpart: {string.Join(", ", missing)}");
    }

    [Fact]
    public void FakeEnums_HaveTheRealValues()
    {
        var wrong = new List<string>();
        foreach (var fake in FakeTypes().Where(t => t.IsEnum))
        {
            if (Real(fake) is not { } real) continue;
            var underlying = (Underlying(fake), Underlying(real));
            if (underlying.Item1 != underlying.Item2) wrong.Add($"{fake.FullName} is {underlying.Item1} in the fake, {underlying.Item2} in reality");
            var values = real.Fields.Where(f => f.IsLiteral).GroupBy(f => f.Name).ToDictionary(g => g.Key, g => System.Convert.ToInt64(g.First().Constant));
            foreach (var member in fake.Fields.Where(f => f.IsLiteral))
            {
                var fakeValue = System.Convert.ToInt64(member.Constant);
                if (!values.TryGetValue(member.Name, out var value)) wrong.Add($"{fake.FullName}.{member.Name} does not exist");
                else if (value != fakeValue) wrong.Add($"{fake.FullName}.{member.Name} is {fakeValue} in the fake, {value} in reality");
            }
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public void ExplicitlyLaidOutFakeFields_SitAtTheRealOffsets()
    {
        var wrong = new List<string>();
        foreach (var fake in FakeTypes().Where(t => t.IsExplicitLayout))
        {
            if (Real(fake) is not { } real) continue;
            foreach (var field in fake.Fields.Where(f => !f.IsStatic))
            {
                var match = real.Fields.FirstOrDefault(f => f.Name == field.Name && !f.IsStatic);
                if (match == null) wrong.Add($"{fake.FullName}.{field.Name} does not exist");
                else if (match.Offset != field.Offset) wrong.Add($"{fake.FullName}.{field.Name} is at {field.Offset} in the fake, {match.Offset} in reality");
                else if (match.FieldType.FullName != field.FieldType.FullName) wrong.Add($"{fake.FullName}.{field.Name} is {field.FieldType.FullName} in the fake, {match.FieldType.FullName} in reality");
            }
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // Methods, fields and properties the fakes declare publicly: same kind, same static-ness, same
    // types. (The harness interprets arguments by position, so a reordered native is a real change.)
    [Fact]
    public void FakeMembers_HaveTheRealSignatures()
    {
        var wrong = new List<string>();
        foreach (var fake in FakeTypes().Where(t => !t.IsEnum))
        {
            if (Real(fake) is not { } real) continue;
            foreach (var method in fake.Methods.Where(m => m.IsPublic && !m.IsConstructor && !m.IsGetter && !m.IsSetter && !m.IsAddOn && !m.IsRemoveOn))
            {
                if (Skip(fake, method.Name)) continue;
                var signature = Signature(method);
                var candidates = AllMethods(real).Where(m => m.Name == method.Name).ToList();
                if (!candidates.Any(m => Signature(m) == signature))
                    wrong.Add($"{fake.FullName}::{signature} -- real: {(candidates.Count == 0 ? "none" : string.Join(" / ", candidates.Select(Signature)))}");
            }
            foreach (var field in fake.Fields.Where(f => f.IsPublic && !f.IsLiteral && !f.IsSpecialName))
            {
                if (Skip(fake, field.Name)) continue;
                var type = MemberType(real, field.Name, field.IsStatic);
                if (type != field.FieldType.FullName) wrong.Add($"{fake.FullName}.{field.Name}: {field.FieldType.FullName} -- real: {type ?? "none"}");
            }
            foreach (var property in fake.Properties.Where(p => p.GetMethod is { IsPublic: true }))
            {
                if (Skip(fake, property.Name)) continue;
                var type = MemberType(real, property.Name, property.GetMethod.IsStatic);
                if (type != property.PropertyType.FullName) wrong.Add($"{fake.FullName}.{property.Name}: {property.PropertyType.FullName} -- real: {type ?? "none"}");
            }
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    private static string Underlying(TypeDefinition e) => e.Fields.Single(f => f.Name == "value__").FieldType.FullName;

    private static bool Skip(TypeDefinition fake, string member)
        => HarnessOnly.Contains($"{fake.FullName}::{member}") || Approximated.ContainsKey(member);

    private static IEnumerable<MethodDefinition> AllMethods(TypeDefinition type)
    {
        for (var t = type; t != null; t = t.BaseType?.Resolve())
            foreach (var m in t.Methods)
                yield return m;
    }

    private static string Signature(MethodDefinition m)
        => $"{(m.IsStatic ? "static " : "")}{m.ReturnType.FullName} {m.Name}({string.Join(", ", m.Parameters.Select(p => p.ParameterType.FullName))})";

    private static string? MemberType(TypeDefinition type, string name, bool isStatic)
    {
        for (var t = type; t != null; t = t.BaseType?.Resolve())
        {
            if (t.Fields.FirstOrDefault(f => f.Name == name && f.IsStatic == isStatic) is { } field) return field.FieldType.FullName;
            if (t.Properties.FirstOrDefault(p => p.Name == name && (p.GetMethod?.IsStatic ?? false) == isStatic) is { } property) return property.PropertyType.FullName;
        }
        return null;
    }
}
