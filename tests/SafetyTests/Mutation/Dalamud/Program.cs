// Writes a copy of Dalamud.dll with one behaviour changed, as a future Dalamud release might:
//   dalamudmutants <id> <in Dalamud.dll> <out Dalamud.dll>
using Mono.Cecil;
using Mono.Cecil.Cil;

var (id, input, output) = (args[0], args[1], args[2]);
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(input)!);
var module = ModuleDefinition.ReadModule(input, new ReaderParameters { AssemblyResolver = resolver, InMemory = true });

TypeDefinition T(string name) => module.GetType(name) ?? throw new Exception($"no {name}");
MethodDefinition M(string type, string name, Func<MethodDefinition, bool>? pick = null)
    => T(type).Methods.Single(m => m.Name == name && (pick == null || pick(m)));
static bool Calls(Instruction i, string name)
    => i.Operand is MethodReference r && $"{r.DeclaringType.GetElementType().FullName}::{r.Name}" == name;
static void ToPop(MethodDefinition m, Func<Instruction, bool> which, bool all = false)
{
    var hits = m.Body.Instructions.Where(which).ToList();
    if (hits.Count == 0) throw new Exception($"nothing to change in {m.FullName}");
    foreach (var i in all ? hits : hits.Take(1))
    {
        i.OpCode = OpCodes.Pop;
        i.Operand = null;
    }
}

const string Framework = "Dalamud.Game.Framework";
const string ClientState = "Dalamud.Game.ClientState.ClientState";

switch (id)
{
    case "D0": // unchanged, through the same write
        break;

    case "D1": // Framework.Run runs the action inline when called on the framework thread
    {
        var run = M(Framework, "Run", m => m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "System.Action");
        var inline = M(Framework, "RunOnFrameworkThread", m => m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.Action");
        run.Body.Instructions.Clear();
        run.Body.ExceptionHandlers.Clear();
        var il = run.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, inline);
        il.Emit(OpCodes.Ret);
        break;
    }

    case "D2": // a disposed Reloaded hook stays enabled
        ToPop(M("Dalamud.Hooking.Internal.ReloadedHook`1", "Dispose"), i => Calls(i, "Dalamud.Hooking.Hook`1::Disable"));
        break;

    case "D3": // SafetyHook swallows a failed native enable
    {
        var enable = M("Dalamud.Hooking.Internal.SafetyHookHook`1", "Enable");
        var t = enable.Body.Instructions.First(i => i.OpCode == OpCodes.Throw);
        t.OpCode = OpCodes.Pop;
        break;
    }

    case "D4": // ConditionFlag renumbered
        T("Dalamud.Game.ClientState.Conditions.ConditionFlag").Fields.Single(f => f.Name == "Occupied").Constant = 26;
        break;

    case "D6": // Dalamud's territory starts following GameMain every frame (it would then follow the sim's loads)
    {
        var setup = M(ClientState, "Setup").Body.Instructions.ToList();
        var set = setup.FindIndex(i => Calls(i, $"{ClientState}::set_TerritoryType"));
        var copy = setup.Skip(set - 3).Take(4).Select(i => i.Operand switch
        {
            null => Instruction.Create(i.OpCode),
            MethodReference r => Instruction.Create(i.OpCode, r),
            FieldReference f => Instruction.Create(i.OpCode, f),
            _ => throw new Exception("unexpected operand"),
        }).ToList();
        var update = M(ClientState, "OnFrameworkUpdate");
        var first = update.Body.Instructions[0];
        var il = update.Body.GetILProcessor();
        foreach (var i in copy) il.InsertBefore(first, i);
        break;
    }

    case "D7": // the plugin-scoped client state grows a field the plugin's territory sync writes
        T("Dalamud.Game.ClientState.ClientStatePluginScoped").Fields.Add(new FieldDefinition("territoryType", FieldAttributes.Private, module.TypeSystem.UInt16));
        break;

    case "D8": // TerritoryChanged raised even when the territory did not change
    {
        var setter = M(ClientState, "set_TerritoryType");
        var beq = setter.Body.Instructions.First(i => i.OpCode.Code is Code.Beq or Code.Beq_S);
        beq.OpCode = OpCodes.Pop;
        beq.Operand = null;
        setter.Body.GetILProcessor().InsertAfter(beq, Instruction.Create(OpCodes.Pop));
        break;
    }

    case "D9": // the tick no longer drains Framework.Run's queue before Update
        ToPop(M(Framework, "RunFrameworkTick"), i => Calls(i, "Dalamud.Utility.ThreadBoundTaskScheduler::Run"), all: true);
        break;

    case "D10": // Update handlers no longer isolated from one another
    {
        var invoke = M(Framework, "ProfileAndInvoke");
        foreach (var h in invoke.Body.ExceptionHandlers.Where(h => h.HandlerType == ExceptionHandlerType.Catch).ToList())
            invoke.Body.ExceptionHandlers.Remove(h);
        break;
    }

    case "D11": // hooks a plugin leaves are no longer disposed on unload
        ToPop(M("Dalamud.Hooking.Internal.GameInteropProviderPluginScoped", "Dalamud.IInternalDisposableService.DisposeService"), i => Calls(i, "System.IDisposable::Dispose"));
        break;

    case "D12": // SafetyHook dropped: both branches build a Reloaded hook
    {
        var create = M("Dalamud.Hooking.Hook`1", "CreateBackend");
        var news = create.Body.Instructions.Where(i => i.OpCode == OpCodes.Newobj && ((MethodReference)i.Operand).DeclaringType.Name.Contains("Hook`1")).ToList();
        var reloaded = news.First(i => ((MethodReference)i.Operand).DeclaringType.Name.StartsWith("ReloadedHook"));
        foreach (var i in news) i.Operand = reloaded.Operand;
        break;
    }

    case "D13": // the framework scheduler runs a task inline as it is queued
    {
        var scheduler = "Dalamud.Utility.ThreadBoundTaskScheduler";
        var tryExecute = M(scheduler, "Run").Body.Instructions.First(i => i.Operand is MethodReference { Name: "TryExecuteTask" }).Operand;
        var queue = M(scheduler, "QueueTask");
        queue.Body.Instructions.Clear();
        queue.Body.ExceptionHandlers.Clear();
        var il = queue.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, (MethodReference)tryExecute);
        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ret);
        break;
    }

    case "D14": // a disposed Reloaded hook's Original stays callable
        ToPop(M("Dalamud.Hooking.Internal.ReloadedHook`1", "get_Original"), i => Calls(i, "Dalamud.Hooking.Hook`1::CheckDisposed"));
        break;

    case "D15": // Reloaded hooks start enabled
        ToPop(M("Dalamud.Hooking.Internal.ReloadedHook`1", ".ctor"), i => Calls(i, "Reloaded.Hooks.Definitions.IHook`1::Disable"));
        break;

    case "D16": // Update handlers stop being dispatched outside Dalamud's own unload
    {
        var tick = M(Framework, "RunFrameworkTick");
        var setter = M(Framework, "set_DispatchUpdateEvents");
        var first = tick.Body.Instructions[0];
        var il = tick.Body.GetILProcessor();
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Call, setter));
        break;
    }

    case "D17": // conditions read from a per-frame cache instead of the game's memory
    {
        var condition = "Dalamud.Game.ClientState.Conditions.Condition";
        var cache = T(condition).Fields.Single(f => f.Name == "cache");
        var read = M(condition, "get_Item", m => m.Parameters[0].ParameterType.FullName == "System.Int32");
        var body = read.Body.Instructions;
        var address = body.First(i => Calls(i, $"{condition}::get_Address"));
        address.OpCode = OpCodes.Ldfld;
        address.Operand = cache;
        var conv = address.Next.Next;
        read.Body.GetILProcessor().Remove(conv.Next);
        read.Body.GetILProcessor().Remove(conv);
        body.First(i => i.OpCode == OpCodes.Ldind_U1).OpCode = OpCodes.Ldelem_U1;
        break;
    }

    default:
        throw new Exception($"unknown mutant {id}");
}

module.Write(output);
Console.WriteLine($"{id} written");
