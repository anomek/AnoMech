using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AnoMech.SafetyTests.Architecture;

// What can run what in the built plugin. Edges are direct calls, virtual and interface calls to
// every plugin implementation, and delegates. A delegate run on the spot (invoked, or handed to a
// method that only invokes it, like List.ForEach) counts as a call from the method that made it,
// and so does one handed to a reviewed scheduler (one the engine only drains during a stay).
// Handed anywhere else (a timer, a task, a Dalamud event, a field), its target can run at any
// time: a root. So is everything the runtime or Dalamud calls: overrides of external members,
// static constructors, and methods nothing in the plugin calls.
public sealed class CallGraph
{
    public sealed record Edge(MethodDefinition From, Instruction At, MethodDefinition To);

    public sealed record Root(MethodDefinition Method, string Why);

    private readonly ModuleDefinition module;
    private readonly Func<string, bool> isScheduler;
    private readonly Dictionary<MethodDefinition, List<Edge>> callees = new();
    private readonly Dictionary<string, HashSet<MethodDefinition>> implementations = new();
    private readonly Dictionary<(MethodDefinition, int), bool> parameterEscapes = new();

    // BCL members that run a delegate argument before they return.
    private static readonly string[] RunsOnTheSpot =
    [
        "System.Collections.Generic.List::ForEach", "System.Collections.Generic.List::Find", "System.Collections.Generic.List::FindAll",
        "System.Collections.Generic.List::FindIndex", "System.Collections.Generic.List::FindLast", "System.Collections.Generic.List::Exists",
        "System.Collections.Generic.List::TrueForAll", "System.Collections.Generic.List::RemoveAll", "System.Collections.Generic.List::Sort",
        "System.Collections.Generic.List::ConvertAll", "System.Array::ForEach", "System.Array::Find", "System.Array::FindAll",
        "System.Array::FindIndex", "System.Array::Exists", "System.Array::TrueForAll", "System.Array::Sort", "System.Array::ConvertAll",
        "System.MemoryExtensions::Sort", "System.Collections.Concurrent.ConcurrentDictionary::GetOrAdd",
        "System.Collections.Concurrent.ConcurrentDictionary::AddOrUpdate",
    ];

    public List<Root> Roots { get; } = new();

    public CallGraph(PluginIL il, Func<string, bool> isScheduler)
    {
        module = il.Module;
        this.isScheduler = isScheduler;
        foreach (var t in module.GetTypes()) MapImplementations(t);
        var methods = module.GetTypes().SelectMany(t => t.Methods).Where(m => m.HasBody).ToList();
        var escaping = new Dictionary<MethodDefinition, string>();
        var called = new HashSet<MethodDefinition>();
        foreach (var m in methods)
        {
            var edges = new List<Edge>();
            foreach (var i in m.Body.Instructions)
            {
                if (i.Operand is not MethodReference r) continue;
                switch (i.OpCode.Code)
                {
                    case Code.Call or Code.Newobj:
                        if (Local(r) is { HasBody: true } direct) edges.Add(new Edge(m, i, direct));
                        break;
                    case Code.Callvirt:
                        foreach (var t in Dispatch(r)) edges.Add(new Edge(m, i, t));
                        break;
                    case Code.Ldftn or Code.Ldvirtftn:
                        var targets = i.OpCode.Code == Code.Ldvirtftn ? Dispatch(r).ToList() : Local(r) is { HasBody: true } d ? [d] : [];
                        if (targets.Count == 0 || i.Next?.OpCode.Code != Code.Newobj) break;
                        var fate = Fate(m, i.Next, 0);
                        if (fate == null)
                            edges.AddRange(targets.Select(t => new Edge(m, i, t)));
                        else
                            foreach (var t in targets)
                                escaping.TryAdd(t, $"a delegate made in {PluginIL.Owner(m)} is {fate}");
                        break;
                }
            }
            callees[m] = edges;
            foreach (var e in edges) called.Add(e.To);
        }
        foreach (var (method, why) in escaping) Roots.Add(new Root(method, $"can run at any time: {why}"));
        foreach (var m in methods.Where(m => !escaping.ContainsKey(m)))
        {
            if (m.IsConstructor && m.IsStatic) Roots.Add(new Root(m, "a static constructor, run whenever its type is first used"));
            else if (OverridesExternal(m)) Roots.Add(new Root(m, "overrides or implements an external member, so the runtime or Dalamud calls it"));
            else if (!called.Contains(m)) Roots.Add(new Root(m, "nothing in the plugin calls it, so only the runtime or Dalamud can"));
        }
    }

    public IReadOnlyList<Edge> Callees(MethodDefinition m) => callees.TryGetValue(m, out var e) ? e : Array.Empty<Edge>();

    // ---- dispatch ------------------------------------------------------------------------------

    private static string Slot(MethodReference m) => $"{PluginIL.TypeName(m.DeclaringType)}::{m.Name}/{m.Parameters.Count}";

    private MethodDefinition? Local(MethodReference r)
    {
        if (r.DeclaringType.Scope != module && r.DeclaringType.Module != module) return null;
        try
        {
            var d = r.Resolve();
            return d?.Module == module ? d : null;
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    private TypeDefinition? Local(TypeReference t)
    {
        if (t.Scope != module && t.Module != module) return null;
        try
        {
            var d = t.Resolve();
            return d?.Module == module ? d : null;
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    private TypeDefinition? BaseOf(TypeDefinition t) => t.BaseType is { } b ? Local(b) : null;

    // Every plugin method a virtual or interface call to `r` can land in.
    private IEnumerable<MethodDefinition> Dispatch(MethodReference r)
    {
        var declared = Local(r);
        if (declared is { HasBody: true } && !declared.IsVirtual) return [declared];
        var found = implementations.TryGetValue(Slot(r), out var set) ? set : [];
        return declared is { HasBody: true } ? found.Append(declared).Distinct() : found;
    }

    // For each type: the methods it (or a base) supplies for every interface member it has, and
    // every override of a plugin base class member it declares.
    private void MapImplementations(TypeDefinition t)
    {
        foreach (var m in t.Methods.Where(m => m.IsVirtual && m.HasBody))
        {
            foreach (var o in m.Overrides) Add(Slot(o), m);
            for (var b = BaseOf(t); b != null; b = BaseOf(b))
                foreach (var bm in b.Methods.Where(bm => bm.IsVirtual && SameShape(bm, m)))
                    Add(Slot(bm), m);
        }
        if (t.IsInterface) return;
        foreach (var iface in AllInterfaces(t))
            foreach (var im in iface.Methods)
            {
                var impl = FindImplementation(t, im);
                if (impl is { HasBody: true }) Add(Slot(im), impl);
                else if (im.HasBody) Add(Slot(im), im);
            }
    }

    private MethodDefinition? FindImplementation(TypeDefinition t, MethodDefinition im)
    {
        for (var c = t; c != null; c = BaseOf(c))
        {
            var explicitImpl = c.Methods.FirstOrDefault(m => m.Overrides.Any(o => Slot(o) == Slot(im)));
            if (explicitImpl != null) return explicitImpl;
            var implicitImpl = c.Methods.FirstOrDefault(m => m.IsVirtual && !m.IsStatic && SameShape(m, im) && m.IsPublic);
            if (implicitImpl != null) return implicitImpl;
        }
        return null;
    }

    private static bool SameShape(MethodDefinition a, MethodDefinition b) => a.Name == b.Name && a.Parameters.Count == b.Parameters.Count;

    private void Add(string slot, MethodDefinition m)
    {
        if (!implementations.TryGetValue(slot, out var set)) implementations[slot] = set = new();
        set.Add(m);
    }

    private IEnumerable<TypeDefinition> AllInterfaces(TypeDefinition t)
    {
        var seen = new HashSet<TypeDefinition>();
        var work = new Stack<TypeDefinition>();
        for (var c = t; c != null; c = BaseOf(c)) work.Push(c);
        while (work.Count > 0)
        {
            var c = work.Pop();
            foreach (var i in c.Interfaces)
                if (Local(i.InterfaceType) is { } d && seen.Add(d))
                {
                    yield return d;
                    work.Push(d);
                }
        }
    }

    // A method outside code calls through a slot no plugin type declares: an override of an
    // external base member (Window.Draw, object.Finalize) or an implementation of an external
    // interface (IDisposable.Dispose).
    private bool OverridesExternal(MethodDefinition m)
    {
        if (!m.IsVirtual || m.DeclaringType.IsInterface) return false;
        if (m.Overrides.Count > 0) return m.Overrides.Any(o => Local(o) == null);
        var inPluginInterface = AllInterfaces(m.DeclaringType).Any(i => i.Methods.Any(im => SameShape(im, m)));
        // The compiler marks an implicit interface implementation newslot and final.
        if (m.IsNewSlot) return m.IsFinal && !inPluginInterface;
        for (var t = BaseOf(m.DeclaringType); t != null; t = BaseOf(t))
            if (t.Methods.Any(b => b.IsVirtual && SameShape(b, m))) return false;
        return true;
    }

    // ---- delegates -----------------------------------------------------------------------------

    // Null when the delegate `producer` leaves on the stack (at `below` values from the top) only
    // runs on the spot or goes to a reviewed scheduler; otherwise where it goes instead.
    private string? Fate(MethodDefinition m, Instruction producer, int depth, int below = 0)
    {
        if (depth > 6) return "passed along further than this rule follows";
        var (consumer, argument) = Consumer(producer, below);
        if (consumer == null) return "used in a way this rule can't follow";
        switch (consumer.OpCode.Code)
        {
            case Code.Dup:
                return Fate(m, consumer, depth + 1, 0) ?? Fate(m, consumer, depth + 1, 1);
            case Code.Call or Code.Callvirt or Code.Newobj:
                var r = (MethodReference)consumer.Operand;
                var name = PluginIL.Name(r);
                if (r.Name == "Invoke" && IsDelegateType(r.DeclaringType)) return null;
                if (isScheduler(name)) return null;
                if (Local(r) is { HasBody: true } local && argument >= 0)
                    return ParameterEscapes(local, argument, depth) ? $"handed to {name}, which keeps it" : null;
                if (RunsOnTheSpot.Contains(name) || name.StartsWith("System.Linq.Enumerable::")) return null;
                return $"handed to {name}";
            case Code.Stloc_0 or Code.Stloc_1 or Code.Stloc_2 or Code.Stloc_3 or Code.Stloc_S or Code.Stloc:
                var index = LocalIndex(consumer);
                var fates = m.Body.Instructions.Where(u => u.Offset > consumer.Offset && IsLoadOf(u, index))
                    .Select(u => Fate(m, u, depth + 1)).Where(f => f != null).Distinct().ToList();
                return fates.Count == 0 ? null : fates[0];
            case Code.Stfld or Code.Stsfld:
                var field = (FieldReference)consumer.Operand;
                // The compiler's cache of a lambda it has already made.
                if (field.Name.StartsWith("<>9__")) return null;
                // Captured by a closure: it goes wherever the closure's own lambdas go.
                if (field.DeclaringType.Name.StartsWith("<>c__DisplayClass"))
                {
                    var closures = m.Body.Instructions.Where(u => u.OpCode.Code is Code.Ldftn && u.Operand is MethodReference t
                                                                 && t.DeclaringType.FullName == field.DeclaringType.FullName && u.Next?.OpCode.Code == Code.Newobj).ToList();
                    return closures.Count == 0
                        ? $"captured by a closure this rule can't follow ({PluginIL.Name(field)})"
                        : closures.Select(u => Fate(m, u.Next, depth + 1)).FirstOrDefault(f => f != null);
                }
                return $"stored in {PluginIL.Name(field)}";
            default:
                return $"used by {consumer.OpCode.Name}";
        }
    }

    // Whether a plugin method keeps (or passes on to something that keeps) its delegate parameter.
    private bool ParameterEscapes(MethodDefinition m, int parameter, int depth)
    {
        if (parameterEscapes.TryGetValue((m, parameter), out var known)) return known;
        parameterEscapes[(m, parameter)] = true;
        var index = parameter + (m.HasThis ? 1 : 0);
        var escapes = m.Body.Instructions.Where(i => LoadsArgument(i, index)).Any(i => Fate(m, i, depth + 1) != null);
        parameterEscapes[(m, parameter)] = escapes;
        return escapes;
    }

    private static bool IsDelegateType(TypeReference t)
    {
        var name = PluginIL.TypeName(t);
        if (name.StartsWith("System.Action") || name.StartsWith("System.Func") || name.StartsWith("System.Predicate") || name.StartsWith("System.Comparison")) return true;
        try
        {
            return t.Resolve()?.BaseType?.FullName == "System.MulticastDelegate";
        }
        catch (AssemblyResolutionException)
        {
            return false;
        }
    }

    // The instruction that pops the value `producer` leaves on the stack (`depth` values below the
    // top), and for a call the index of the parameter it lands in (-1 for `this`). Straight-line
    // code only.
    public static (Instruction? Consumer, int Argument) Consumer(Instruction producer, int depth)
    {
        for (var i = producer.Next; i != null; i = i.Next)
        {
            if (i.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Throw) return (null, 0);
            var pops = Pops(i);
            if (pops > depth)
            {
                if (i.Operand is MethodReference r && i.OpCode.Code is Code.Call or Code.Callvirt or Code.Newobj)
                {
                    var position = pops - 1 - depth;
                    var hasThis = r.HasThis && i.OpCode.Code != Code.Newobj;
                    return (i, hasThis ? position - 1 : position);
                }
                return (i, 0);
            }
            depth = depth - pops + Pushes(i);
        }
        return (null, 0);
    }

    private static int Pops(Instruction i)
    {
        if (i.OpCode.StackBehaviourPop == StackBehaviour.Varpop)
        {
            if (i.Operand is MethodReference r) return r.Parameters.Count + (r.HasThis && i.OpCode.Code != Code.Newobj ? 1 : 0);
            return i.OpCode.Code == Code.Ret ? 1 : 0;
        }
        return i.OpCode.StackBehaviourPop switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
            StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8
                or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8 or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
            StackBehaviour.PopAll => int.MaxValue,
            _ => 3,
        };
    }

    private static int Pushes(Instruction i)
    {
        if (i.OpCode.StackBehaviourPush == StackBehaviour.Varpush)
            return i.Operand is MethodReference r && (i.OpCode.Code == Code.Newobj || r.ReturnType.FullName != "System.Void") ? 1 : 0;
        return i.OpCode.StackBehaviourPush switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1_push1 => 2,
            _ => 1,
        };
    }

    private static int? LocalIndex(Instruction i) => i.OpCode.Code switch
    {
        Code.Stloc_0 or Code.Ldloc_0 => 0,
        Code.Stloc_1 or Code.Ldloc_1 => 1,
        Code.Stloc_2 or Code.Ldloc_2 => 2,
        Code.Stloc_3 or Code.Ldloc_3 => 3,
        _ => (i.Operand as VariableDefinition)?.Index,
    };

    private static bool IsLoadOf(Instruction i, int? index)
        => i.OpCode.Code is Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3 or Code.Ldloc_S or Code.Ldloc && LocalIndex(i) == index;

    private static bool LoadsArgument(Instruction i, int index) => i.OpCode.Code switch
    {
        Code.Ldarg_0 => index == 0,
        Code.Ldarg_1 => index == 1,
        Code.Ldarg_2 => index == 2,
        Code.Ldarg_3 => index == 3,
        Code.Ldarg_S or Code.Ldarg => i.Operand is ParameterDefinition p && p.Index + (p.Method is MethodReference { HasThis: true } ? 1 : 0) == index,
        _ => false,
    };
}
