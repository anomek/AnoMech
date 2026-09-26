using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace AnoMech.SafetyTests.Architecture;

// The real character may only be moved by code that runs during a stay, while the filter holds
// whatever the move would tell the server. The firewall tests never run scenario or engine code,
// so this follows every call path in the built plugin instead: from every place code can start
// running (Dalamud callbacks, UI, timers, tasks, anything a delegate is handed to), no path may
// reach a move of the character, or put something in the sim world, without passing a check
// that a stay is running.
public class StayBoundaryTests
{
    private const string Game = "AnoMech.Core.Game.Game";
    private const string Map = "AnoMech.Core.Map.MapController";
    private const string World = "AnoMech.Core.SimObjects.SimWorld";

    // Calls that move the real character, or may: any SimCharacter can be the SimPlayer, which
    // always resolves to the live character.
    private static readonly HashSet<string> Moves =
    [
        "AnoMech.Core.SimObjects.SimCharacter::SetPosition",
        "AnoMech.Core.SimObjects.SimCharacter::SetRotation",
        "AnoMech.Core.Native.ForcedMovement::CarryTo",
    ];

    // Native position writes elsewhere are moves too, except in the move methods' own bodies,
    // in these, which only ever touch objects the sim spawned, and in the zone session's own
    // (whose every write the firewall tests judge).
    private static readonly HashSet<string> SimObjectWriters =
    [
        "AnoMech.Core.SimObjects.SimEventObject::SetPosition",
        "AnoMech.Core.SimObjects.SimTower::Spawn",
        "AnoMech.Core.SimObjects.SimEnemy::Spawn",
        "AnoMech.Core.Game.Party.PartyCreator::SpawnNative",
        "AnoMech.Core.SimObjects.SimCast::FaceTarget",
    ];

    private const string ZoneSessionWriter = "AnoMech.Core.Map.ZoneSession::SetLocalPlayerPosition";

    // Checks that a stay is running. Each is true only between a verified arm and the lift:
    // IsInInstance and IsActive by the zone session's own design (the firewall tests check "in
    // instance implies both filters up" every frame), PeerInRun includes IsInInstance, TryLoad
    // succeeding is the arm, and Game.activeScenario is pinned by the tests below.
    private static bool IsStayCheck(Instruction i) => i.Operand switch
    {
        FieldReference f => i.OpCode.Code == Code.Ldfld && PluginIL.Name(f) == $"{Game}::activeScenario",
        MethodReference m => PluginIL.Name(m) is $"{Map}::get_IsInInstance" or "AnoMech.Core.Map.ZoneSession::get_IsActive"
            or "AnoMech.Multiplayer.MultiplayerManager::get_PeerInRun" or $"{Map}::TryLoad" or $"{Game}::get_ActiveScenario" or $"{Game}::get_IsScenarioActive",
        _ => false,
    };

    // Work handed to these runs no later than the stay it was added in: every EventScheduler is
    // either the game's own, cleared when a run ends, or a scenario's, ticked only while that
    // scenario is the active one.
    private static bool IsStayBoundScheduler(string name) => name == "AnoMech.Core.Game.EventScheduler::Add";

    // Everything the world ticks, it holds; it holds the player only during a stay (the party is
    // made only after the zone loaded and dropped before it unloads) and nothing else outside
    // one but the reviewed entries below, none of them the player. Pinned by the tests below; so
    // the world's own tick counts as inside a stay.
    private const string WorldTick = $"{World}::Tick";

    // Code outside a stay that puts objects in the world, each reviewed: none of it is the player
    // or moves it.
    private static readonly Dictionary<string, string> ReviewedWorldEntries = new()
    {
        ["AnoMech.Core.Native.TimelineDebug::SpawnChaosNearPlayer"] = "the debug timeline bench's test enemy (its controls are Debug-only)",
        ["AnoMech.Windows.DebugMenu::DrawDebugContent"] = "the debug menu's manual enemy and event-object spawns (Debug builds only)",
        ["AnoMech.Scenarios.Umad.P1TeleTrouncing.UmadP1TeleTrouncingScenario::FireAppearNow"] = "a settings button replaying the statue-appear tell: statue beats and effects",
        ["AnoMech.Scenarios.Umad.P1TeleTrouncing.UmadP1TeleTrouncingScenario::FireWindUpNow"] = "a settings button replaying the gaze wind-up tell: statue beats and effects",
    };

    [Fact]
    public void TheCharacterIsOnlyMovedFromCodeThatRunsDuringAStay()
    {
        var failures = PluginIL.Builds.SelectMany(b => Paths(b, (i, m) => IsMove(i, m), new Dictionary<string, string>()).Select(f => $"[{b}] {f}")).ToList();
        Assert.True(failures.Count == 0,
            "Code that can run outside a stay reaches a move of the real character with no check that a stay is running. Outside a stay nothing holds the move from the server: "
            + "move it into the scenario's timeline, or check IsInInstance first.\n\n" + string.Join("\n\n", failures));
    }

    [Fact]
    public void TheSimWorld_OnlyGainsObjectsDuringAStay()
    {
        var failures = PluginIL.Builds.SelectMany(b =>
        {
            var entries = WorldEntryMethods(b);
            return Paths(b, (i, m) => i.Operand is MethodReference r && entries.Contains(PluginIL.Name(r)) && PluginIL.TypeName(m.DeclaringType) != World, ReviewedWorldEntries)
                .Select(f => $"[{b}] {f}");
        }).ToList();
        Assert.True(failures.Count == 0,
            "Code that can run outside a stay puts objects into the sim world, whose tick then runs them outside a stay. Spawn from the scenario's timeline instead, "
            + "or, for a tool whose objects can never be or move the player, add the method to ReviewedWorldEntries with the reason and have it reviewed.\n\n" + string.Join("\n\n", failures));
    }

    // ---- the search ----------------------------------------------------------------------------

    private static IEnumerable<string> Paths(PluginIL il, Func<Instruction, MethodDefinition, bool> isSink, Dictionary<string, string> reviewed)
    {
        var graph = new CallGraph(il, IsStayBoundScheduler);
        var ungatedCache = new Dictionary<MethodDefinition, HashSet<Instruction>>();
        HashSet<Instruction> Ungated(MethodDefinition m)
        {
            if (ungatedCache.TryGetValue(m, out var set)) return set;
            if (PluginIL.Owner(m) == WorldTick) return ungatedCache[m] = new HashSet<Instruction>();
            var good = m.Body.Instructions.Where(IsStayCheck).Select(ControlFlow.OutcomeOf).Where(o => o != null).Select(o => (o!.Value.Branch, o.Value.WhenTruthy)).ToList();
            return ungatedCache[m] = new ControlFlow(m).FeasiblyReachableAvoiding(good);
        }

        var previous = new Dictionary<MethodDefinition, (MethodDefinition? From, string Root)>();
        var queue = new Queue<MethodDefinition>();
        foreach (var r in graph.Roots)
            if (previous.TryAdd(r.Method, (null, r.Why)))
                queue.Enqueue(r.Method);
        while (queue.Count > 0)
        {
            var m = queue.Dequeue();
            if (reviewed.ContainsKey(PluginIL.Owner(m))) continue;
            var ungated = Ungated(m);
            foreach (var e in graph.Callees(m))
                if (ungated.Contains(e.At) && previous.TryAdd(e.To, (m, previous[m].Root)))
                    queue.Enqueue(e.To);
        }

        foreach (var m in previous.Keys)
        {
            if (reviewed.ContainsKey(PluginIL.Owner(m))) continue;
            var ungated = Ungated(m);
            var sinks = m.Body.Instructions.Where(i => ungated.Contains(i) && isSink(i, m)).ToList();
            if (sinks.Count == 0) continue;
            var path = new List<string>();
            for (MethodDefinition? c = m; c != null; c = previous[c].From) path.Add(PluginIL.Owner(c));
            path.Reverse();
            path = path.Distinct().ToList();
            yield return $"{string.Join("\n  -> ", path)}\n  calls {string.Join(", ", sinks.Select(i => $"{PluginIL.Name((MethodReference)i.Operand)} (IL_{i.Offset:X4})").Distinct())}"
                         + $"\n  ({path[0]} {previous[m].Root})";
        }
    }

    private static bool IsMove(Instruction i, MethodDefinition m)
    {
        if (i.Operand is not MethodReference r || i.OpCode.Code is not (Code.Call or Code.Callvirt)) return false;
        var name = PluginIL.Name(r);
        var owner = PluginIL.Owner(m);
        if (Moves.Contains(name)) return !Moves.Contains(owner) && !MovesItselfAsAnNpc(i, m);
        return !Moves.Contains(owner) && !SimObjectWriters.Contains(owner) && owner != ZoneSessionWriter && ArchitectureTests.IsNativeTransformCall(name);
    }

    // A move whose target is statically an NPC (SimNpc and below: an index into the game's
    // character array the sim claimed), which is never the player.
    private static bool MovesItselfAsAnNpc(Instruction call, MethodDefinition m)
    {
        var receivers = m.Body.Instructions.Where(p => CallGraph.Consumer(p, 0) is var (consumer, argument) && consumer == call && argument == -1)
            .Select(p => StaticType(p, m)).ToList();
        return receivers.Count > 0 && receivers.All(t => t != null && IsNpcType(t));
    }

    private static TypeReference? StaticType(Instruction p, MethodDefinition m) => p.OpCode.Code switch
    {
        Code.Ldarg_0 when m.HasThis => m.DeclaringType,
        Code.Ldarg_0 or Code.Ldarg_1 or Code.Ldarg_2 or Code.Ldarg_3 or Code.Ldarg_S or Code.Ldarg => Parameter(p, m)?.ParameterType,
        Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3 => m.Body.Variables[p.OpCode.Code - Code.Ldloc_0].VariableType,
        Code.Ldloc_S or Code.Ldloc => ((VariableDefinition)p.Operand).VariableType,
        Code.Ldfld or Code.Ldsfld => ((FieldReference)p.Operand).FieldType,
        Code.Call or Code.Callvirt => ((MethodReference)p.Operand).ReturnType,
        Code.Newobj => ((MethodReference)p.Operand).DeclaringType,
        Code.Castclass or Code.Isinst => (TypeReference)p.Operand,
        _ => null,
    };

    private static ParameterDefinition? Parameter(Instruction p, MethodDefinition m)
    {
        var index = p.OpCode.Code switch
        {
            Code.Ldarg_0 => 0,
            Code.Ldarg_1 => 1,
            Code.Ldarg_2 => 2,
            Code.Ldarg_3 => 3,
            _ => ((ParameterDefinition)p.Operand).Index + (m.HasThis ? 1 : 0),
        } - (m.HasThis ? 1 : 0);
        return index >= 0 && index < m.Parameters.Count ? m.Parameters[index] : null;
    }

    private static bool IsNpcType(TypeReference t)
    {
        TypeDefinition? c;
        try
        {
            c = t.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return false;
        }
        while (c != null)
        {
            if (PluginIL.TypeName(c) == "AnoMech.Core.SimObjects.SimNpc") return true;
            try
            {
                c = c.BaseType?.Resolve();
            }
            catch (AssemblyResolutionException)
            {
                return false;
            }
        }
        return false;
    }

    // SimWorld methods that add to the objects it ticks.
    private static HashSet<string> WorldEntryMethods(PluginIL il)
        => il.Module.GetTypes().Where(t => PluginIL.TypeName(t) == World).SelectMany(t => t.Methods).Where(m => m.HasBody
                && m.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "children")
                && m.Body.Instructions.Any(i => i.Operand is MethodReference r && PluginIL.Name(r) == "System.Collections.Generic.List::Add"))
            .Select(m => $"{World}::{m.Name}").ToHashSet();

    // ---- what makes Game.activeScenario a stay check ------------------------------------------

    // Set to a scenario only once the zone loaded behind a verified filter.
    [Fact]
    public void TheActiveScenario_IsOnlySetAfterTheZoneLoaded()
    {
        var failures = new List<string>();
        foreach (var il in PluginIL.Builds)
            foreach (var m in il.Methods)
                foreach (var store in m.Body.Instructions.Where(i => i.OpCode.Code == Code.Stfld && PluginIL.Name((FieldReference)i.Operand) == $"{Game}::activeScenario"))
                {
                    if (store.Previous?.OpCode.Code == Code.Ldnull) continue;
                    var owner = PluginIL.Owner(m);
                    if (owner != $"{Game}::RunScenarioInternal")
                    {
                        failures.Add($"[{il}] {owner} sets Game.activeScenario; only RunScenarioInternal may, after the zone loaded.");
                        continue;
                    }
                    var load = m.Body.Instructions.Single(i => i.Operand is MethodReference r && PluginIL.Name(r) == $"{Map}::TryLoad");
                    if (ControlFlow.OutcomeOf(load) is not { } outcome || !new ControlFlow(m).EveryPathTakes(store, outcome.Branch, outcome.WhenTruthy))
                        failures.Add($"[{il}] RunScenarioInternal can set Game.activeScenario without TryLoad having returned true.");
                }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // Cleared, and the world emptied, before the stay can end: the zone only unloads from Leave,
    // after ResetInternal, which does both before anything else can fail.
    [Fact]
    public void TheActiveScenarioAndTheWorld_AreClearedBeforeTheZoneUnloads()
    {
        var failures = new List<string>();
        foreach (var il in PluginIL.Builds)
        {
            var reset = il.Method(Game, "ResetInternal");
            var straight = reset.Body.Instructions.TakeWhile(i => i.OpCode.FlowControl is FlowControl.Next or FlowControl.Call).ToList();
            if (!straight.Any(i => i.OpCode.Code == Code.Stfld && PluginIL.Name((FieldReference)i.Operand) == $"{Game}::activeScenario" && i.Previous?.OpCode.Code == Code.Ldnull))
                failures.Add($"[{il}] Game.ResetInternal no longer clears activeScenario before anything else.");
            if (!straight.Any(i => i.Operand is MethodReference r && PluginIL.Name(r) == $"{World}::Despawn"))
                failures.Add($"[{il}] Game.ResetInternal no longer empties the sim world before anything that could return early.");
            var despawn = il.Method(World, "Despawn");
            var drop = despawn.Body.Instructions.TakeWhile(i => i.OpCode.FlowControl is FlowControl.Next or FlowControl.Call).ToList();
            if (!drop.Any(i => i.Operand is MethodReference r && PluginIL.Name(r) == $"{World}::set_Party" && i.Previous?.Operand is FieldReference e && PluginIL.Name(e) == "AnoMech.Core.SimObjects.SimParty::Empty"))
                failures.Add($"[{il}] SimWorld.Despawn no longer drops the party (and with it the player).");
            var leave = il.Methods.Where(m => PluginIL.Owner(m) == $"{Game}::Leave")
                .FirstOrDefault(m => m.Body.Instructions.Any(i => i.Operand is MethodReference r && PluginIL.Name(r) == $"{Map}::Unload"));
            if (leave == null)
            {
                failures.Add($"[{il}] Game.Leave no longer unloads the zone; update this rule.");
                continue;
            }
            var resetCall = leave.Body.Instructions.FirstOrDefault(i => i.Operand is MethodReference r && PluginIL.Name(r) == $"{Game}::ResetInternal");
            var unload = leave.Body.Instructions.First(i => i.Operand is MethodReference r && PluginIL.Name(r) == $"{Map}::Unload");
            if (resetCall == null || new ControlFlow(leave).ReachableAvoiding([(resetCall, resetCall.Next)]).Contains(unload))
                failures.Add($"[{il}] Game.Leave can unload the zone without ResetInternal having run first.");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // The player's sim object is made in one place, only after the zone loaded.
    [Fact]
    public void TheSimPlayer_IsOnlyMadeForARun()
    {
        var failures = new List<string>();
        foreach (var il in PluginIL.Builds)
        {
            var makers = il.CallersOf(@"^AnoMech\.Core\.SimObjects\.SimPlayer::\.ctor$");
            if (!makers.SequenceEqual([$"{World}::CreateParty"]))
                failures.Add($"[{il}] the SimPlayer is made by {string.Join(", ", makers)}; only SimWorld.CreateParty may.");
            var parties = il.CallersOf($"^{Regex.Escape(World)}::CreateParty$");
            if (!parties.SequenceEqual([$"{Game}::RunScenarioInternal"]))
                failures.Add($"[{il}] SimWorld.CreateParty is called by {string.Join(", ", parties)}; only Game.RunScenarioInternal may (after TryLoad, see ArchitectureTests).");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
