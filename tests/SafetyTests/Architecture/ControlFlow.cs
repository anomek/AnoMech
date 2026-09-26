using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AnoMech.SafetyTests.Architecture;

// Instruction-level control flow of one method, including the edges into exception handlers, for
// proving that every path to a call passes a check first. Calls that never return (the safety
// stop) end their path.
public sealed class ControlFlow
{
    private readonly MethodDefinition method;
    private readonly Dictionary<Instruction, List<Instruction>> successors = new();

    public ControlFlow(MethodDefinition method, params string[] noReturn)
    {
        this.method = method;
        var instructions = method.Body.Instructions;
        foreach (var i in instructions)
        {
            var next = new List<Instruction>();
            var flow = i.OpCode.FlowControl;
            var endsPath = i.Operand is MethodReference callee && noReturn.Contains(PluginIL.Name(callee));
            if (!endsPath)
            {
                switch (flow)
                {
                    case FlowControl.Branch:
                        next.Add((Instruction)i.Operand);
                        break;
                    case FlowControl.Cond_Branch:
                        if (i.Operand is Instruction[] targets) next.AddRange(targets);
                        else next.Add((Instruction)i.Operand);
                        if (i.Next != null) next.Add(i.Next);
                        break;
                    case FlowControl.Return:
                    case FlowControl.Throw:
                        break;
                    default:
                        if (i.OpCode.Code is Code.Endfinally or Code.Endfilter) break;
                        if (i.Next != null) next.Add(i.Next);
                        break;
                }
            }
            foreach (var h in method.Body.ExceptionHandlers)
                if (Inside(i, h.TryStart, h.TryEnd))
                {
                    next.Add(h.HandlerStart);
                    if (h.FilterStart != null) next.Add(h.FilterStart);
                }
            successors[i] = next;
        }
    }

    private static bool Inside(Instruction i, Instruction start, Instruction? end)
        => i.Offset >= start.Offset && (end == null || i.Offset < end.Offset);

    public IEnumerable<Instruction> Reachable(Instruction? avoidFrom = null, Instruction? avoidTo = null)
    {
        var seen = new HashSet<Instruction>();
        var work = new Stack<Instruction>();
        work.Push(method.Body.Instructions[0]);
        while (work.Count > 0)
        {
            var i = work.Pop();
            if (!seen.Add(i)) continue;
            foreach (var n in successors[i])
                if (!(i == avoidFrom && n == avoidTo)) work.Push(n);
        }
        return seen;
    }

    // True when no path from the entry reaches `target` without taking the edge from -> to.
    public bool EveryPathTakes(Instruction target, Instruction from, Instruction to)
        => !Reachable(from, to).Contains(target);

    // Everything a path from the entry can reach without taking any of `avoided`. A branch on a
    // constant the path itself put there (Debug builds park `a && b` in a local: ldc.i4.0,
    // stloc, ldloc, brfalse) only goes the way that constant sends it.
    public HashSet<Instruction> FeasiblyReachableAvoiding(ICollection<(Instruction From, Instruction To)> avoided)
    {
        var reached = new HashSet<Instruction>();
        var seen = new HashSet<(Instruction, int?, int?, int?)>();
        var work = new Stack<(Instruction At, int? Top, int? Local, int? LocalValue)>();
        work.Push((method.Body.Instructions[0], null, null, null));
        while (work.Count > 0)
        {
            var state = work.Pop();
            if (!seen.Add(state)) continue;
            var (i, top, local, localValue) = state;
            reached.Add(i);
            int? nextTop = null;
            var nextLocal = local;
            var nextLocalValue = localValue;
            if (Constant(i) is { } c) nextTop = c;
            else if (i.OpCode.Code is Code.Nop or Code.Br or Code.Br_S or Code.Dup) nextTop = top;
            else if (LocalIndex(i) is { } index && IsStore(i))
            {
                (nextLocal, nextLocalValue) = top is { } value ? (index, value) : index == local ? (null, null) : (local, localValue);
            }
            else if (LocalIndex(i) is { } loaded && loaded == local) nextTop = localValue;
            var conditional = i.OpCode.Code is Code.Brtrue or Code.Brtrue_S or Code.Brfalse or Code.Brfalse_S;
            foreach (var n in successors[i])
            {
                if (avoided.Contains((i, n))) continue;
                if (conditional && top is { } condition && (Instruction)i.Operand != i.Next)
                {
                    var jumps = i.OpCode.Code is Code.Brtrue or Code.Brtrue_S ? condition != 0 : condition == 0;
                    if (jumps != (n == (Instruction)i.Operand)) continue;
                }
                work.Push((n, nextTop, nextLocal, nextLocalValue));
            }
        }
        return reached;
    }

    private static int? Constant(Instruction i) => i.OpCode.Code switch
    {
        Code.Ldc_I4_M1 => -1,
        Code.Ldc_I4_0 => 0,
        Code.Ldc_I4_1 => 1,
        Code.Ldc_I4_2 => 2,
        Code.Ldc_I4_3 => 3,
        Code.Ldc_I4_4 => 4,
        Code.Ldc_I4_5 => 5,
        Code.Ldc_I4_6 => 6,
        Code.Ldc_I4_7 => 7,
        Code.Ldc_I4_8 => 8,
        Code.Ldc_I4_S => (sbyte)i.Operand,
        Code.Ldc_I4 => (int)i.Operand,
        _ => null,
    };

    private static bool IsStore(Instruction i) => i.OpCode.Code is Code.Stloc_0 or Code.Stloc_1 or Code.Stloc_2 or Code.Stloc_3 or Code.Stloc_S or Code.Stloc;

    // Everything reachable from the entry without taking any of `avoided`.
    public HashSet<Instruction> ReachableAvoiding(ICollection<(Instruction From, Instruction To)> avoided)
    {
        var seen = new HashSet<Instruction>();
        var work = new Stack<Instruction>();
        work.Push(method.Body.Instructions[0]);
        while (work.Count > 0)
        {
            var i = work.Pop();
            if (!seen.Add(i)) continue;
            foreach (var n in successors[i])
                if (!avoided.Contains((i, n))) work.Push(n);
        }
        return seen;
    }

    // The branch deciding on a call's result: the edge taken when it returned true / non-null,
    // and the one taken when it returned false / null. Null when the code does not branch on it
    // in a shape this reads.
    public static (Instruction Branch, Instruction WhenTruthy, Instruction WhenFalsy)? OutcomeOf(Instruction call)
    {
        var inverted = false;
        int? stored = null;
        var i = call.Next;
        for (var steps = 0; i != null && steps < 8; steps++, i = i.Next)
        {
            // Another path landing here would take its own value into the branch.
            if (IsJumpTarget(i)) return null;
            switch (i.OpCode.Code)
            {
                case Code.Dup:
                case Code.Nop:
                    continue;
                case Code.Stloc_0 or Code.Stloc_1 or Code.Stloc_2 or Code.Stloc_3 or Code.Stloc_S or Code.Stloc:
                    stored = LocalIndex(i);
                    continue;
                case Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3 or Code.Ldloc_S or Code.Ldloc:
                    if (stored != LocalIndex(i)) return null;
                    continue;
                case Code.Ldnull when i.Next?.OpCode.Code is Code.Cgt_Un or Code.Ceq:
                case Code.Ldc_I4_0 when i.Next?.OpCode.Code == Code.Ceq:
                    continue;
                case Code.Cgt_Un when i.Previous.OpCode.Code == Code.Ldnull:
                    continue;
                case Code.Ceq when i.Previous.OpCode.Code is Code.Ldnull or Code.Ldc_I4_0:
                    inverted = !inverted;
                    continue;
                case Code.Brtrue or Code.Brtrue_S:
                    return inverted ? (i, i.Next, (Instruction)i.Operand) : (i, (Instruction)i.Operand, i.Next);
                case Code.Brfalse or Code.Brfalse_S:
                    return inverted ? (i, (Instruction)i.Operand, i.Next) : (i, i.Next, (Instruction)i.Operand);
                default:
                    return null;
            }
        }
        return null;
    }

    private static bool IsJumpTarget(Instruction target)
    {
        var first = target;
        while (first.Previous != null) first = first.Previous;
        for (var i = first; i != null; i = i.Next)
            if (i.Operand == target || i.Operand is Instruction[] targets && targets.Contains(target))
                return true;
        return false;
    }

    // Nothing but no-ops and jumps between here and a return (Debug builds route an early return
    // through a shared one).
    public static bool LeadsStraightToReturn(Instruction? i)
    {
        for (var steps = 0; i != null && steps < 16; steps++)
        {
            switch (i.OpCode.Code)
            {
                case Code.Ret:
                    return true;
                case Code.Nop:
                    i = i.Next;
                    continue;
                case Code.Br or Code.Br_S:
                    i = (Instruction)i.Operand;
                    continue;
                default:
                    return false;
            }
        }
        return false;
    }

    private static int? LocalIndex(Instruction i) => i.OpCode.Code switch
    {
        Code.Stloc_0 or Code.Ldloc_0 => 0,
        Code.Stloc_1 or Code.Ldloc_1 => 1,
        Code.Stloc_2 or Code.Ldloc_2 => 2,
        Code.Stloc_3 or Code.Ldloc_3 => 3,
        Code.Stloc_S or Code.Stloc or Code.Ldloc_S or Code.Ldloc => (i.Operand as VariableDefinition)?.Index,
        _ => null,
    };
}
