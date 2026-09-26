using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace AnoMech.SafetyTests.Architecture;

// Rules over the built plugin that keep the firewall's guarantees true as the code grows: who may
// touch the filter, who may move the real character, what a scenario may never do, and the call
// orders the Firewall tests assume. A failure names the rule and the code that broke it. When a
// new call site is genuinely safe, add it to the rule's allowlist with the reason, and get that
// reviewed like any change to the firewall itself.
public class ArchitectureTests
{
    private const string Zone = "AnoMech.Core.Map.ZoneSession";
    private const string Map = "AnoMech.Core.Map.MapController";
    private const string Game = "AnoMech.Core.Game.Game";

    private static void ForEachBuild(Func<PluginIL, IEnumerable<string>> rule)
    {
        var failures = PluginIL.Builds.SelectMany(b => rule(b).Select(f => $"[{b}] {f}")).ToList();
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static IEnumerable<string> CallersMustBeIn(PluginIL il, string callee, params string[] allowed)
    {
        var callers = il.CallersOf($"^{Regex.Escape(callee)}$");
        if (callers.Count == 0) yield return $"nothing calls {callee} any more; if it was renamed, update this rule.";
        foreach (var c in callers.Where(c => !allowed.Contains(c)))
            yield return $"{c} calls {callee}; only {string.Join(", ", allowed)} may.";
    }

    // ---- the filter itself ------------------------------------------------------------------

    private static readonly Dictionary<string, string[]> HookOperations = new()
    {
        [$"{Zone}::EnableFirewall"] = ["send:Enable", "receive:Enable"],
        [$"{Zone}::DisableFirewall"] = ["send:Disable", "receive:Disable", "send:get_IsEnabled", "receive:get_IsEnabled"],
        [$"{Zone}::HoldSendFirewall"] = ["send:Enable", "send:Disable", "send:get_IsEnabled"],
        [$"{Zone}::SendPacketDetour"] = ["send:get_Original"],
        [$"{Zone}::ReceivePacketDetour"] = ["receive:get_Original"],
        [$"{Zone}::InjectIncomingPacket"] = ["receive:get_Original"],
        [$"{Zone}::FirewallArmed"] = ["send:get_IsEnabled", "send:get_IsDisposed", "receive:get_IsEnabled", "receive:get_IsDisposed"],
        [$"{Zone}::TickSessionGuard"] = ["send:get_IsEnabled", "receive:get_IsEnabled"],
        [$"{Zone}::LiftBlockedReason"] = ["send:get_IsEnabled", "receive:get_IsEnabled"],
        [$"{Zone}::BuildStateSnapshot"] = ["send:get_IsEnabled", "send:get_IsDisposed", "receive:get_IsEnabled", "receive:get_IsDisposed"],
        [$"{Zone}::Dispose"] = ["send:Dispose", "receive:Dispose"],
    };

    [Fact]
    public void TheFilterHooks_AreOnlyTouchedWhereTheirUseIsVerified()
    {
        ForEachBuild(il => Check(il));

        static IEnumerable<string> Check(PluginIL il)
        {
            var fields = new Dictionary<string, string> { [$"{Zone}::sendPacketHook"] = "send", [$"{Zone}::receivePacketHook"] = "receive" };
            var seen = new HashSet<string>();
            foreach (var (m, i, f) in il.FieldAccesses())
            {
                if (!fields.TryGetValue(PluginIL.Name(f), out var which)) continue;
                var owner = PluginIL.Owner(m);
                if (i.OpCode.Code is Code.Stfld)
                {
                    if (owner != $"{Zone}::.ctor") yield return $"{owner} assigns the {which} filter hook; only the constructor may.";
                    continue;
                }
                var op = NextHookMember(i);
                if (op == null)
                {
                    yield return $"{owner} uses the {which} filter hook in a way this rule can't read (stored or passed on?). Keep every use a direct call on the field.";
                    continue;
                }
                var entry = $"{which}:{op}";
                seen.Add($"{owner}|{entry}");
                if (!HookOperations.TryGetValue(owner, out var allowed) || !allowed.Contains(entry))
                    yield return $"{owner} calls {op} on the {which} filter hook. Only the methods in HookOperations may, each for the listed operations.";
            }
            foreach (var needed in new[] { $"{Zone}::EnableFirewall|send:Enable", $"{Zone}::EnableFirewall|receive:Enable", $"{Zone}::SendPacketDetour|send:get_Original" })
                if (!seen.Contains(needed)) yield return $"expected {needed.Replace("|", " to call ")} and found no such call.";
        }

        static string? NextHookMember(Instruction load)
        {
            for (var i = load.Next; i != null && i.Offset - load.Offset < 64; i = i.Next)
                if (i.Operand is MethodReference m)
                    return PluginIL.TypeName(m.DeclaringType) == "Dalamud.Hooking.Hook" ? m.Name : null;
            return null;
        }
    }

    [Fact]
    public void RaisingAndLoweringTheFilter_HasExactlyTheReviewedCallers()
    {
        ForEachBuild(il => new[]
        {
            CallersMustBeIn(il, $"{Zone}::EnableFirewall", $"{Zone}::Enter"),
            CallersMustBeIn(il, $"{Zone}::DisableFirewall", $"{Zone}::Enter", $"{Zone}::TryLift", $"{Zone}::Dispose"),
            CallersMustBeIn(il, $"{Zone}::TryLift", $"{Zone}::LiftFirewallOrDie", $"{Zone}::TickSessionGuard"),
            CallersMustBeIn(il, $"{Zone}::LiftFirewallOrDie", $"{Zone}::Revert", $"{Zone}::Dispose"),
            CallersMustBeIn(il, $"{Zone}::Revert", $"{Map}::Unload", $"{Zone}::Enter", $"{Zone}::Dispose"),
            CallersMustBeIn(il, $"{Zone}::Enter", $"{Map}::Load"),
            CallersMustBeIn(il, $"{Map}::Load", $"{Map}::TryLoad"),
            CallersMustBeIn(il, $"{Map}::TryLoad", $"{Game}::RunScenarioInternal"),
            CallersMustBeIn(il, $"{Map}::Unload", $"{Game}::Leave"),
            CallersMustBeIn(il, $"{Zone}::HoldSendFirewall", $"{Map}::HoldSendFirewall", $"{Zone}::Dispose"),
            CallersMustBeIn(il, $"{Map}::HoldSendFirewall", "AnoMech.Core.Native.TimelineDebug::Arm", "AnoMech.Core.Native.TimelineDebug::OnUpdate", "AnoMech.Core.Native.TimelineDebug::Shutdown"),
            CallersMustBeIn(il, $"{Zone}::Die", $"{Zone}::Enter", $"{Zone}::Revert", $"{Zone}::Trip", $"{Zone}::TryLift"),
            CallersMustBeIn(il, $"{Zone}::SetLocalPlayerPosition", $"{Zone}::LoadZoneInternal", $"{Zone}::RestoreBeforeLift", $"{Zone}::Revert", $"{Zone}::Dispose", $"{Zone}::HoldSendFirewall"),
            CallersMustBeIn(il, $"{Zone}::InjectIncomingPacket", $"{Map}::InjectIncomingPacket"),
            CallersMustBeIn(il, $"{Map}::InjectIncomingPacket", "AnoMech.Core.Native.RawActionEffect::TryInject"),
        }.SelectMany(x => x));
    }

    // A second hook anywhere could pass traffic around the filter, or act on the character outside
    // a sim from a detour.
    private static readonly string[] HookSites =
    [
        $"{Zone}::.ctor",
        "AnoMech.Core.Map.MapEffects::.ctor",
        "AnoMech.Core.Native.LocalPlayerInputHooks::.ctor",
        "AnoMech.Core.Native.VfxSpawnLog::Enable",
    ];

    // Debug builds only (the menu is compiled out of Release).
    private static readonly string[] DebugHookSites =
    [
        // Observes inbound action effects for the position log; never calls into the send path.
        "AnoMech.Windows.DebugMenu::TogglePositionLog",
    ];

    [Fact]
    public void Hooks_AreOnlyCreatedAtReviewedSites()
    {
        ForEachBuild(il =>
        {
            var failures = new List<string>();
            foreach (var (m, _, callee) in il.Calls())
            {
                var name = PluginIL.Name(callee);
                var owner = PluginIL.Owner(m);
                if (Regex.IsMatch(name, @"IGameInteropProvider::HookFrom") && !HookSites.Contains(owner) && !(!il.IsRelease && DebugHookSites.Contains(owner)))
                    failures.Add($"{owner} creates a hook ({callee.Name}); hooks may only be created at: {string.Join(", ", HookSites)}.");
                if (name.EndsWith("IGameInteropProvider::InitializeFromAttributes")
                    && !(owner.StartsWith("AnoMech.Pointers.") && owner.EndsWith("::Initialize")) && owner != "AnoMech.Core.Native.LocalPlayerInputHooks::.ctor")
                    failures.Add($"{owner} initializes signature attributes; only the Pointers classes and LocalPlayerInputHooks may.");
            }
            // Read from the raw attribute blob: decoding it would need Dalamud's own assemblies.
            var detourName = System.Text.Encoding.UTF8.GetBytes("DetourName");
            foreach (var t in il.Module.GetTypes())
            foreach (var member in t.Fields.Cast<IMemberDefinition>().Concat(t.Properties).Concat(t.Methods))
            foreach (var a in member.CustomAttributes.Where(a => a.AttributeType.Name == "SignatureAttribute"))
                if (a.GetBlob().AsSpan().IndexOf(detourName) >= 0)
                    failures.Add($"{t.FullName}.{member.Name} declares a signature-attribute hook (DetourName); create hooks at a reviewed site instead.");
            return failures;
        });
    }

    [Fact]
    public void TheFilteredFunctions_AreOnlyLocatedByTheZoneSession()
    {
        const string sendSignature = "48 89 5C 24 ?? 48 89 74 24 ?? 4C 89 64 24 ?? 55 41 56 41 57 48 8B EC 48 83 EC 70";
        ForEachBuild(il =>
        {
            var failures = new List<string>();
            var found = false;
            foreach (var m in il.Methods)
            foreach (var i in m.Body.Instructions)
            {
                var owner = PluginIL.Owner(m);
                if (i.Operand is string s && s == sendSignature)
                {
                    found = true;
                    if (owner != $"{Zone}::.ctor") failures.Add($"{owner} scans for the filtered send function; only the zone session may.");
                }
                if (i.Operand is MemberReference r && r.Name.Contains("StaticVirtualTablePointer") && r.DeclaringType.Name == "PacketDispatcher" && owner != $"{Zone}::.ctor")
                    failures.Add($"{owner} reads the packet dispatcher's vtable (the filtered receive function); only the zone session may.");
            }
            if (!found) failures.Add("the zone session no longer scans for the send signature this rule knows; update it with the new one.");
            return failures;
        });
    }

    // The inbound allowlist decides which server packets reach a client in the sim.
    [Fact]
    public void TheFilterConfiguration_IsOnlyWrittenByItsOwners()
    {
        ForEachBuild(il => new[]
        {
            CallersMustBeIn(il, "AnoMech.Configuration::set_ZoneDownOpcodes", "AnoMech.Core.Map.OpcodeUpdater::DownloadOpcodes"),
            CallersMustBeIn(il, "AnoMech.Configuration::set_ZoneFirewallGameVersion", "AnoMech.Core.Map.OpcodeUpdater::DownloadOpcodes"),
            il.CallersOf(@"^AnoMech\.Configuration::set_SafeMode$").Where(c => c != "AnoMech.Windows.ConfigWindow::Draw")
                .Select(c => $"{c} writes the SafeMode switch; only the config window may."),
        }.SelectMany(x => x));
    }

    // ---- the safety stop -------------------------------------------------------------------

    [Fact]
    public void OnlyTheSafetyStop_EndsTheProcess()
    {
        ForEachBuild(il => il.Calls()
            .Where(c => Regex.IsMatch(PluginIL.Name(c.Callee), @"^System\.Environment::(FailFast|Exit)$|^System\.Diagnostics\.Process::Kill$"))
            .Select(c => PluginIL.Owner(c.Method)).Where(o => o != $"{Zone}::Die").Distinct()
            .Select(o => $"{o} ends the process; only ZoneSession.Die may (it logs first and never lowers the filter)."));
    }

    // TryLift and Revert fall through after Die on the understanding that Die cannot return.
    [Fact]
    public void TheSafetyStop_NeverReturns()
    {
        ForEachBuild(il =>
        {
            var die = il.Method(Zone, "Die");
            var flow = new ControlFlow(die, "System.Environment::FailFast");
            var escapes = flow.Reachable().Where(i => i.OpCode.Code is Code.Ret or Code.Throw or Code.Rethrow).ToList();
            var failFast = die.Body.Instructions.Where(i => i.Operand is MethodReference m && PluginIL.Name(m) == "System.Environment::FailFast").ToList();
            var failures = new List<string>();
            if (failFast.Count == 0) failures.Add("ZoneSession.Die no longer calls Environment.FailFast.");
            if (escapes.Count > 0) failures.Add($"ZoneSession.Die can return or throw without reaching Environment.FailFast (at {string.Join(", ", escapes.Select(e => $"IL_{e.Offset:X4}"))}).");
            foreach (var f in failFast)
                if (die.Body.ExceptionHandlers.Any(h => h.HandlerType == ExceptionHandlerType.Catch && f.Offset >= h.TryStart.Offset && f.Offset < h.TryEnd.Offset))
                    failures.Add("ZoneSession.Die calls FailFast inside a try with a catch.");
            return failures;
        });
    }

    // ---- ordering proofs --------------------------------------------------------------------

    private static IEnumerable<string> EveryPathPasses(PluginIL il, MethodDefinition method, string guard, bool mustBeTruthy, string target, params string[] noReturn)
    {
        var flow = new ControlFlow(method, noReturn);
        var guards = method.Body.Instructions.Where(i => i.Operand is MethodReference m && PluginIL.Name(m) == guard).ToList();
        var targets = method.Body.Instructions.Where(i => i.Operand is MethodReference m && PluginIL.Name(m) == target).ToList();
        var where = PluginIL.Owner(method);
        if (guards.Count != 1)
        {
            yield return $"{where}: expected one call to {guard}, found {guards.Count}; update this rule if the check moved.";
            yield break;
        }
        if (targets.Count == 0)
        {
            yield return $"{where}: expected a call to {target}; update this rule if it moved.";
            yield break;
        }
        if (ControlFlow.OutcomeOf(guards[0]) is not { } outcome)
        {
            yield return $"{where}: the result of {guard} is not branched on in a shape this rule reads; keep it a plain if-check.";
            yield break;
        }
        var good = mustBeTruthy ? outcome.WhenTruthy : outcome.WhenFalsy;
        foreach (var t in targets)
            if (!flow.EveryPathTakes(t, outcome.Branch, good))
                yield return $"{where}: {target} (IL_{t.Offset:X4}) is reachable without {guard} having returned {(mustBeTruthy ? "true / non-null" : "false / null")}.";
    }

    [Fact]
    public void Enter_ArmsOnlyAfterTheGate_AndLoadsOnlyWithTheFilterConfirmedUp()
    {
        ForEachBuild(il =>
        {
            var enter = il.Method(Zone, "Enter");
            return EveryPathPasses(il, enter, $"{Zone}::StartBlockedReason", false, $"{Zone}::EnableFirewall")
                .Concat(EveryPathPasses(il, enter, $"{Zone}::StartBlockedReason", false, $"{Zone}::LoadZoneInternal"))
                .Concat(EveryPathPasses(il, enter, $"{Zone}::FirewallArmed", true, $"{Zone}::LoadZoneInternal"));
        });
    }

    // The character is only ever moved to the arena, and the scenario only ever runs, once the
    // zone session has armed the filter and loaded the sim.
    private static readonly string[] AfterTheZoneLoads =
    [
        $"{Game}::TeleportPlayerToSpawn",
        $"{Game}::TeleportPlayerToSpawnIfOutsideArena",
        "AnoMech.Core.SimObjects.SimWorld::CreateParty",
        "AnoMech.Scenarios.IZone::Run",
        "AnoMech.Scenarios.IPhase::Run",
        "AnoMech.Scenarios.IScenario::Run",
        "AnoMech.Scenarios.IScenario::RunInstanceEvents",
        "AnoMech.Scenarios.IZone::RunClientSetup",
        "AnoMech.Scenarios.IPhase::RunClientSetup",
    ];

    [Fact]
    public void RunScenarioInternal_TouchesTheCharacterOnlyAfterTheGateAndTheLoad()
    {
        ForEachBuild(il =>
        {
            var run = il.Method(Game, "RunScenarioInternal");
            return AfterTheZoneLoads.SelectMany(t =>
                EveryPathPasses(il, run, $"{Zone}::StartBlockedReason", false, t)
                    .Concat(EveryPathPasses(il, run, $"{Map}::TryLoad", true, t)));
        });
    }

    [Fact]
    public void TryLift_LowersTheFilterOnlyWhenNothingBlocksIt()
    {
        ForEachBuild(il => EveryPathPasses(il, il.Method(Zone, "TryLift"), $"{Zone}::LiftBlockedReason", false, $"{Zone}::DisableFirewall", $"{Zone}::Die"));
    }

    // Revert outside a session moved the character to a stale position with no filter at all.
    [Fact]
    public void Revert_DoesNothingOutsideASession()
    {
        ForEachBuild(il =>
        {
            var revert = il.Method(Zone, "Revert");
            return new[] { $"{Zone}::LoadZoneInternal", $"{Zone}::SetLocalPlayerPosition", $"{Zone}::LiftFirewallOrDie" }
                .SelectMany(t => EveryPathPasses(il, revert, $"{Zone}::get_IsActive", true, t, $"{Zone}::Die"));
        });
    }

    // ---- wiring the guard needs --------------------------------------------------------------

    [Fact]
    public void TheGuardRunsEveryFrame_BeforeAndApartFromEverythingElse()
    {
        ForEachBuild(il =>
        {
            var update = il.Method("AnoMech.Plugin", "OnFrameworkUpdate");
            var failures = new List<string>();
            var calls = update.Body.Instructions.Where(i => i.Operand is MethodReference).ToList();
            var guard = calls.FirstOrDefault(i => PluginIL.Name((MethodReference)i.Operand) == $"{Zone}::TickGuard");
            var gameTick = calls.FirstOrDefault(i => PluginIL.Name((MethodReference)i.Operand) == $"{Game}::Tick");
            if (guard == null) return ["Plugin.OnFrameworkUpdate no longer calls ZoneSession.TickGuard."];
            if (gameTick != null && gameTick.Offset < guard.Offset) failures.Add("ZoneSession.TickGuard must run before Game.Tick.");
            var guardTry = update.Body.ExceptionHandlers.FirstOrDefault(h => guard.Offset >= h.TryStart.Offset && guard.Offset < h.TryEnd.Offset);
            if (guardTry == null) failures.Add("ZoneSession.TickGuard is not in its own try: a throw would take the whole frame's update down with it.");
            else if (calls.Any(c => c != guard && c.Offset >= guardTry.TryStart.Offset && c.Offset < guardTry.TryEnd.Offset && PluginIL.Name((MethodReference)c.Operand).StartsWith("AnoMech.")))
                failures.Add("ZoneSession.TickGuard shares its try with other plugin code, which could keep it from running.");
            var flow = new ControlFlow(update);
            if (!flow.Reachable().Contains(guard)) failures.Add("ZoneSession.TickGuard is unreachable in Plugin.OnFrameworkUpdate.");
            return failures;
        });
    }

    [Fact]
    public void GameEventsReachTheGuard()
    {
        ForEachBuild(il =>
        {
            var failures = new List<string>();
            var ctor = il.Method("AnoMech.Plugin", ".ctor");
            foreach (var (handler, evt) in new[] { ("OnTerritoryChanged", "add_TerritoryChanged"), ("OnLogout", "add_Logout") })
            {
                var subscribed = ctor.Body.Instructions.Any(i => i.OpCode.Code == Code.Ldftn && i.Operand is MethodReference m && m.Name == handler
                    && NextCall(i) is { } c && c.Name == evt);
                if (!subscribed) failures.Add($"Plugin no longer subscribes {handler} to the client state's {evt[4..]} event.");
            }
            var first = il.Method("AnoMech.Plugin", "OnTerritoryChanged").Body.Instructions.First(i => i.Operand is MethodReference);
            if (PluginIL.Name((MethodReference)first.Operand) != $"{Zone}::NoteTerritoryChanged")
                failures.Add("Plugin.OnTerritoryChanged must tell the zone session first, before anything that could throw or return.");
            if (!il.CallersOf($"^{Regex.Escape(Zone)}::NoteLogout$").Contains("AnoMech.Plugin::OnLogout"))
                failures.Add("Plugin.OnLogout no longer tells the zone session.");
            if (!il.CallersOf($"^{Regex.Escape(Zone)}::NoteActionPressed$").Contains("AnoMech.Core.Native.LocalPlayerInputHooks::RecordRecentAction"))
                failures.Add("LocalPlayerInputHooks.RecordRecentAction no longer reports Teleport/Return presses to the zone session.");
            var detour = il.Method("AnoMech.Core.Native.LocalPlayerInputHooks", "UseActionDetour");
            var calls = detour.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => (i, PluginIL.Name((MethodReference)i.Operand))).ToList();
            var record = calls.FindIndex(c => c.Item2.EndsWith("::RecordRecentAction"));
            var original = calls.FindIndex(c => c.Item2.EndsWith("::get_Original"));
            if (record < 0 || original < 0 || record > original)
                failures.Add("UseActionDetour must record the press (and so latch a Teleport/Return) before it can pass the action on.");
            return failures;
        });

        static MethodReference? NextCall(Instruction i)
        {
            for (var n = i.Next; n != null; n = n.Next)
                if (n.Operand is MethodReference m && n.OpCode.Code is Code.Call or Code.Callvirt) return m;
            return null;
        }
    }

    // Dalamud disposes the plugin's hooks once Dispose returns or throws; a sim must be reverted
    // (or the game stopped) by then, whatever else in the teardown fails.
    [Fact]
    public void Unload_AlwaysRevertsTheZoneBeforeTheHooksGo()
    {
        ForEachBuild(il =>
        {
            var dispose = il.Method("AnoMech.Plugin", "Dispose");
            var zoneDispose = dispose.Body.Instructions.FirstOrDefault(i => i.Operand is MethodReference m && PluginIL.Name(m) == $"{Zone}::Dispose");
            if (zoneDispose == null) return ["Plugin.Dispose no longer disposes the zone session itself."];
            var inFinally = dispose.Body.ExceptionHandlers.FirstOrDefault(h => h.HandlerType == ExceptionHandlerType.Finally
                && zoneDispose.Offset >= h.HandlerStart.Offset && (h.HandlerEnd == null || zoneDispose.Offset < h.HandlerEnd.Offset));
            if (inFinally == null) return ["Plugin.Dispose disposes the zone session outside a finally: a throw earlier in the teardown would skip it."];
            var unprotected = dispose.Body.Instructions
                .Where(i => i.Operand is MethodReference m && PluginIL.Name(m).StartsWith("AnoMech.") && i != zoneDispose
                            && PluginIL.Name(m) != $"{Zone}::get_Current" && PluginIL.Name(m) != "AnoMech.Core.DiagnosticLog::Shutdown")
                .Where(i => !(i.Offset >= inFinally.TryStart.Offset && i.Offset < inFinally.TryEnd.Offset))
                .Select(i => PluginIL.Name((MethodReference)i.Operand)).ToList();
            return unprotected.Select(u => $"Plugin.Dispose calls {u} outside the try its zone-session finally covers.");
        });
    }

    [Fact]
    public void Leave_UnloadsTheZone()
    {
        ForEachBuild(il => il.CallersOf($"^{Regex.Escape(Map)}::Unload$").Contains($"{Game}::Leave")
            ? Array.Empty<string>()
            : ["Game.Leave no longer calls MapController.Unload."]);
    }

    // ---- the real character ------------------------------------------------------------------

    // Every native write of a game object's position or rotation. The local player can only be
    // moved through these, and each one is either sim-only or gated on the filter being up.
    private static readonly string[] NativePositionWriters =
    [
        "AnoMech.Core.SimObjects.SimCharacter::SetPosition",
        "AnoMech.Core.SimObjects.SimCharacter::SetRotation",
        "AnoMech.Core.SimObjects.SimEventObject::SetPosition",
        "AnoMech.Core.SimObjects.SimTower::Spawn",
        "AnoMech.Core.SimObjects.SimEnemy::Spawn",
        "AnoMech.Core.Game.Party.PartyCreator::SpawnNative",
        $"{Zone}::SetLocalPlayerPosition",
        "AnoMech.Core.Native.LocalPlayerInputHooks::UpdateDetour",
        // Turns a casting sim enemy toward its target.
        "AnoMech.Core.SimObjects.SimCast::FaceTarget",
    ];

    internal static bool IsNativeTransformCall(string name)
        => Regex.IsMatch(name, @"^FFXIVClientStructs\..*\.(GameObject|Character|BattleChara)::(SetPosition|SetRotation|SetDrawOffset|Teleport)$");

    private static bool IsNativeTransformField(string name)
        => Regex.IsMatch(name, @"^FFXIVClientStructs\..*\.(GameObject|Character|BattleChara)::(Position|Rotation)$")
           || Regex.IsMatch(name, @"^FFXIVClientStructs\.FFXIV\.Client\.Graphics\.Scene\.(Object|DrawObject)::Position$");

    [Fact]
    public void NativePositionWrites_OnlyInReviewedMethods()
    {
        ForEachBuild(il =>
        {
            var failures = new List<string>();
            foreach (var (m, i, callee) in il.Calls())
            {
                var name = PluginIL.Name(callee);
                if (!IsNativeTransformCall(name)) continue;
                var owner = PluginIL.Owner(m);
                if (!NativePositionWriters.Contains(owner)) failures.Add($"{owner} writes a game object's position natively ({name}); only the reviewed writers may.");
            }
            foreach (var (m, i, f) in il.FieldAccesses())
            {
                if (i.OpCode.Code is not (Code.Stfld or Code.Stsfld)) continue;
                var name = PluginIL.Name(f);
                if (!IsNativeTransformField(name)) continue;
                var owner = PluginIL.Owner(m);
                if (!NativePositionWriters.Contains(owner)) failures.Add($"{owner} stores a game object's position field ({name}); only the reviewed writers may.");
            }
            return failures;
        });
    }

    // Fabricated server packets run the client's own handlers, which can move or re-zone the
    // local player. Reviewed callers only.
    private static readonly string[] ActorControlCallers =
    [
        $"{Zone}::ReleasePlayerConditions",
        "AnoMech.Core.Native.ForcedMovement::CarryTo",
        "AnoMech.Core.SimObjects.SimEventObject::DirectorEObjMod",
        "AnoMech.Core.SimObjects.SimEventObject::FadeOut",
        "AnoMech.Core.SimObjects.SimEventObject::PlayBeat",
        "AnoMech.Core.UserActions.CastInterruptHandler::InterruptCast",
        "AnoMech.Scenarios.Uwu.UltimatePredation.UltimatePredationScenario::BombBoulder",
        "AnoMech.Scenarios.Uwu.UltimateSuppression.UltimateSuppressionScenario::Garuda",
        "AnoMech.Scenarios.Uwu.UltimateSuppression.UltimateSuppressionScenario::Lockon",
        "AnoMech.Scenarios.Uwu.UltimateSuppression.UltimateSuppressionScenario::Titan",
    ];

    [Fact]
    public void FabricatedServerPackets_OnlyFromReviewedCallers()
    {
        ForEachBuild(il =>
            il.CallersOf(@"^FFXIVClientStructs\.FFXIV\.Client\.Network\.PacketDispatcher::HandleActorControlPacket$").Where(c => !ActorControlCallers.Contains(c))
                .Select(c => $"{c} fabricates an ActorControl packet; only the reviewed callers may.")
                .Concat(CallersMustBeIn(il, "AnoMech.Core.Native.ForcedMovement::CarryTo", "AnoMech.Core.SimObjects.SimCharacter::CarryTo")));
    }

    // Calls that make the game send something to the server of its own accord. The filter holds
    // them during a stay; anywhere else they are real traffic.
    [Fact]
    public void ServerReachingCalls_OnlyFromReviewedCallers()
    {
        ForEachBuild(il =>
        {
            var failures = new List<string>();
            foreach (var (m, _, callee) in il.Calls())
            {
                var name = PluginIL.Name(callee);
                if (!Regex.IsMatch(name, @"^FFXIVClientStructs\..*::(UseAction|UseActionLocation|UseGeneralAction|ExecuteCommand\w*|ProcessChatBoxEntry|SendMessage|SendChat\w*|RequestData|Teleport\w*)$")) continue;
                var owner = PluginIL.Owner(m);
                if (owner != "AnoMech.Core.Native.LocalPlayerInputHooks::UpdateDetour")
                    failures.Add($"{owner} calls {name}, which makes the game talk to the server; only reviewed callers may.");
            }
            return failures;
        });
    }

    // Every WeatherManager write tried leaked out of the sim and survived the inn reload.
    [Fact]
    public void TheWeatherManager_IsNeverWritten()
    {
        ForEachBuild(il =>
        {
            var failures = new List<string>();
            foreach (var (m, i, f) in il.FieldAccesses())
                if (i.OpCode.Code is Code.Stfld or Code.Stsfld && (f.DeclaringType.Name is "WeatherManager" or "Weather" || f.DeclaringType.DeclaringType?.Name == "WeatherManager"))
                    failures.Add($"{PluginIL.Owner(m)} writes {PluginIL.Name(f)}.");
            foreach (var (m, _, callee) in il.Calls())
                if (callee.DeclaringType.Name is "WeatherManager" or "Weather" && Regex.IsMatch(callee.Name, "^(Set|set_|Change|Force)"))
                    failures.Add($"{PluginIL.Owner(m)} calls {PluginIL.Name(callee)}.");
            return failures;
        });
    }

    // ---- what a scenario may never do ----------------------------------------------------------

    // A scenario runs inside a stay, on the scenario's event timeline, and must not outlive it:
    // anything it schedules elsewhere can run after the lift and act on the real character with
    // nothing holding the result from the server.
    private static readonly (string Pattern, string Why)[] ScenarioForbidden =
    [
        (@"^System\.Threading\.Tasks\.(Task|TaskFactory|Parallel|ValueTask)::", "starts work outside the sim's timeline; use world.Events"),
        (@"^System\.Threading\.(Thread|ThreadPool|Timer)::", "starts work outside the sim's timeline; use world.Events"),
        (@"^System\.Timers\.", "starts work outside the sim's timeline; use world.Events"),
        (@"^System\.Runtime\.CompilerServices\.Async\w*MethodBuilder", "is async; its continuations outlive the sim"),
        (@"^Dalamud\.Plugin\.Services\.IFramework::", "runs on Dalamud's framework, outside the sim's timeline"),
        (@"^Dalamud\.Plugin\.Services\.IGameInteropProvider::", "hooks the game"),
        (@"^Dalamud\.Hooking\.", "hooks the game"),
        (@"^Dalamud\.Plugin\.Services\.(IClientState|ICondition|IDutyState)::add_", "subscribes to game events that outlive the sim"),
        (@"^AnoMech\.Core\.Map\.ZoneSession::", "reaches into the zone session"),
        (@"^AnoMech\.Core\.Map\.MapController::(Load|TryLoad|Unload|HoldSendFirewall|Dispose)$", "loads or unloads zones"),
        (@"^AnoMech\.Core\.Game\.Game::(Leave|Reset|RunScenario\w*|Dispose)$", "starts or ends runs"),
        (@"^AnoMech\.Plugin::get_(Framework|GameInterop)$", "reaches Dalamud services that outlive the sim"),
        (@"^System\.Environment::(FailFast|Exit)$", "ends the process"),
        (@"^FFXIVClientStructs\..*ActionManager::", "uses the real action manager"),
        (@"^FFXIVClientStructs\..*WeatherManager::", "touches the weather manager"),
        (@"^FFXIVClientStructs\..*EventFramework::", "touches the duty director"),
        (@"^AnoMech\.Pointers\.(GameMainPointers|EventFrameworkPointers)::", "loads zones or directors"),
    ];

    // Scenario code that reaches a forbidden member today, each reviewed.
    private static readonly string[] ScenarioExceptions =
    [
        // Suppression's stun sets the local player's afflictions; every lift path clears them.
        "AnoMech.Scenarios.Uwu.UltimateSuppression.UltimateSuppressionScenario::SetStun|FFXIVClientStructs.FFXIV.Client.Game.Conditions::Instance",
    ];

    [Fact]
    public void Scenarios_NeverActOutsideTheirStay()
    {
        ForEachBuild(il =>
        {
            var failures = new List<string>();
            foreach (var m in il.Methods)
            {
                var top = PluginIL.TopLevel(m.DeclaringType);
                if (!top.Namespace.StartsWith("AnoMech.Scenarios")) continue;
                var owner = PluginIL.Owner(m);
                if (m.DeclaringType.Interfaces.Any(i => i.InterfaceType.Name == "IAsyncStateMachine"))
                    failures.Add($"{owner} is async; a scenario's continuations would outlive its stay.");
                foreach (var i in m.Body.Instructions)
                {
                    string? name = i.Operand switch
                    {
                        MethodReference r => PluginIL.Name(r),
                        FieldReference f => PluginIL.Name(f),
                        _ => null,
                    };
                    if (name == null) continue;
                    if (name == "FFXIVClientStructs.FFXIV.Client.Game.Conditions::Instance" && !ScenarioExceptions.Contains($"{owner}|{name}"))
                        failures.Add($"{owner} writes the client's conditions; the lift only clears the ones it knows.");
                    foreach (var (pattern, why) in ScenarioForbidden)
                        if (Regex.IsMatch(name, pattern) && !ScenarioExceptions.Contains($"{owner}|{name}"))
                            failures.Add($"{owner} uses {name}: a scenario {why}.");
                    if (i.OpCode.Code == Code.Stsfld && i.Operand is FieldReference sf && HoldsSimState(sf.FieldType))
                        failures.Add($"{owner} keeps {PluginIL.TypeName(sf.FieldType)} in a static field ({PluginIL.Name(sf)}), where it outlives the run and can move the real character later.");
                }
            }
            return failures.Distinct();
        });

        static bool HoldsSimState(TypeReference t)
        {
            var name = PluginIL.TypeName(t);
            return name.StartsWith("AnoMech.Core.SimObjects.") || name is "AnoMech.Core.Game.Game" or "AnoMech.Core.Map.MapController";
        }
    }

    // Every zone a scenario can load, each a duty territory with a Duty Finder entry. A territory
    // without one makes the load back out; a wrong one loads the wrong zone behind the filter.
    private static readonly Dictionary<uint, string> ReviewedTerritories = new()
    {
        [733] = "The Unending Coil of Bahamut (Ultimate)",
        [777] = "The Weapon's Refrain (Ultimate)",
        [1122] = "The Omega Protocol (Ultimate)",
        [1363] = "Dancing Mad (Ultimate)",
    };

    [Fact]
    public void Zones_OnlyLoadReviewedDutyTerritories()
    {
        ForEachBuild(il =>
        {
            var failures = new List<string>();
            var zones = il.Module.GetTypes().Where(t => !t.IsInterface && t.Interfaces.Any(i => PluginIL.TypeName(i.InterfaceType) == "AnoMech.Scenarios.IZone")).ToList();
            if (zones.Count == 0) failures.Add("found no IZone implementations; update this rule.");
            foreach (var zone in zones)
            {
                var getter = zone.Methods.FirstOrDefault(m => m.Name.EndsWith("get_TerritoryId") && m.HasBody);
                var constants = getter?.Body.Instructions.Where(i => i.OpCode.Code is Code.Ldc_I4 or Code.Ldc_I4_S).Select(i => Convert.ToUInt32(i.Operand)).ToList();
                if (getter == null || constants is not { Count: 1 } || getter.Body.Instructions.Count(i => i.OpCode.Code == Code.Ret) != 1)
                    failures.Add($"{zone.FullName}.TerritoryId is not a plain constant; make it one so it can be reviewed here.");
                else if (!ReviewedTerritories.ContainsKey(constants[0]))
                    failures.Add($"{zone.FullName} loads territory {constants[0]}, which is not a reviewed duty territory. Check it has a Duty Finder entry and add it to ReviewedTerritories.");
            }
            return failures;
        });
    }

    // Real content must never see the sim's faked job gauge: the hooks restore it before they go.
    [Fact]
    public void InputHooks_RestoreTheFakedGaugeBeforeTheyGo()
    {
        ForEachBuild(il =>
        {
            var dispose = il.Method("AnoMech.Core.Native.LocalPlayerInputHooks", "Dispose");
            var calls = dispose.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => PluginIL.Name((MethodReference)i.Operand)).ToList();
            var restore = calls.FindIndex(c => c.EndsWith("::RestoreGaugeIllusion"));
            var firstHookDispose = calls.FindIndex(c => c == "Dalamud.Hooking.Hook::Dispose");
            return restore < 0 || (firstHookDispose >= 0 && restore > firstHookDispose)
                ? ["LocalPlayerInputHooks.Dispose must restore the gauge illusion before disposing its hooks."]
                : Array.Empty<string>();
        });
    }

    // ---- multiplayer --------------------------------------------------------------------------

    // Host messages act on the peer's real character; outside the peer's own run they must be
    // dropped, or a host could move the character with no filter up.
    private static readonly string[] PeerWorldHandlers =
    [
        "OnWorldSnapshotReceived", "OnRolesSnapshotReceived", "OnTeleportReceived", "OnCarryReceived", "OnPushReceived",
        "OnFollowReceived", "OnRoleKilledReceived", "OnKnockbackReceived", "OnSpawnOmenReceived",
    ];

    [Fact]
    public void HostMessagesThatMoveTheCharacter_AreDroppedOutsideTheRun()
    {
        ForEachBuild(il =>
        {
            var failures = new List<string>();
            var manager = il.Module.GetTypes().First(t => t.FullName == "AnoMech.Multiplayer.MultiplayerManager");
            var movers = new Regex(@"::(SetPosition|SetRotation|TeleportTo|CarryTo|Push|Follow|Knockback|MoveTo|Intercept)$");
            foreach (var m in manager.Methods.Where(m => m.HasBody && Regex.IsMatch(m.Name, "^On\\w+Received$")))
            {
                var moves = m.Body.Instructions.Any(i => i.Operand is MethodReference r && movers.IsMatch(PluginIL.Name(r)) && PluginIL.Name(r).StartsWith("AnoMech.Core."));
                if (!moves && !PeerWorldHandlers.Contains(m.Name)) continue;
                // Closure allocations the compiler hoists to the top of the method come first.
                var first = m.Body.Instructions.FirstOrDefault(i => i.Operand is MethodReference r
                    && !(i.OpCode.Code == Code.Newobj && r.DeclaringType.Name.StartsWith('<')));
                var gate = first?.Operand is MethodReference g && PluginIL.Name(g) == "AnoMech.Multiplayer.MultiplayerManager::get_PeerInRun";
                if (!gate) failures.Add($"MultiplayerManager.{m.Name} {(moves ? "moves characters" : "applies host state")} without first checking PeerInRun.");
                else if (ControlFlow.OutcomeOf(first!) is not { } outcome || !ControlFlow.LeadsStraightToReturn(outcome.WhenFalsy))
                    failures.Add($"MultiplayerManager.{m.Name} does not return straight away when PeerInRun is false.");
            }
            foreach (var name in PeerWorldHandlers)
                if (manager.Methods.All(m => m.Name != name)) failures.Add($"MultiplayerManager.{name} no longer exists; update this rule.");
            return failures;
        });
    }

    // ---- what only Debug builds may do --------------------------------------------------------

    [Fact]
    public void ReleaseBuilds_CannotReachTheDebugSendHold()
    {
        ForEachBuild(il => !il.IsRelease
            ? Array.Empty<string>()
            : il.CallersOf(@"^AnoMech\.Core\.Native\.TimelineDebug::DrawControls$").Select(c => $"{c} reaches the debug timeline bench (and its send-only hold) in a Release build."));
    }

    [Fact]
    public void ReleaseBuilds_CompileTheReceiveFilterOn()
    {
        ForEachBuild(il => !il.IsRelease
            ? Array.Empty<string>()
            : il.Method(Zone, "ReceivePacketDetour").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "get_SafeMode")
                ? ["The Release receive filter reads the SafeMode switch; it must be compiled on."]
                : Array.Empty<string>());
    }

    [Fact]
    public void TheAnalysedBuildIsTheShippedConfiguration()
    {
        Assert.Contains(PluginIL.Builds, b => b.IsRelease);
    }
}
