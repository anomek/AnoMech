using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnoMech.Core.Map;
using AnoMech.SafetyTests.Fakes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace AnoMech.SafetyTests.Contract;

// The game state the guard reads through Dalamud, as the harness models it: conditions are
// ClientStructs' Conditions bytes, read live; Dalamud's territory moves only when the client
// handles a ZoneInit packet, never for the sim's own loads, and says so only on a change.
public class GameStateContractTests
{
    private const string ClientState = "Dalamud.Game.ClientState.ClientState";

    // ICondition[flag] is the byte at Conditions + flag, read on every call (as the fake reads it),
    // so a write by the plugin shows at once.
    [Fact]
    public void TheConditionService_ReadsClientStructsConditions_Live()
    {
        var byFlag = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", "Dalamud.Plugin.Services.ICondition"), "get_Item", "Dalamud.Game.ClientState.Conditions.ConditionFlag");
        Assert.Contains("Dalamud.Plugin.Services.ICondition::get_Item", RealDalamud.CalledNames(byFlag));
        var scoped = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", "Dalamud.Game.ClientState.Conditions.ConditionPluginScoped"), "get_Item", "System.Int32");
        Assert.Equal(new[] { "Dalamud.Game.ClientState.Conditions.Condition::get_Item" }, RealDalamud.CalledNames(scoped));

        var condition = RealDalamud.Type("Dalamud.dll", "Dalamud.Game.ClientState.Conditions.Condition");
        var read = RealDalamud.Method(condition, "get_Item", "System.Int32").Body.Instructions.Select(i => i.OpCode.Code).ToList();
        var address = RealDalamud.Method(condition, "get_Item", "System.Int32").Body.Instructions
            .First(i => i.Operand is MethodReference m && m.Name == "get_Address");
        var tail = RealDalamud.Method(condition, "get_Item", "System.Int32").Body.Instructions
            .SkipWhile(i => i != address).Skip(1).Take(4).Select(i => i.OpCode.Code).ToList();
        Assert.Equal(new[] { Code.Ldarg_1, Code.Conv_I, Code.Add, Code.Ldind_U1 }, tail);
        Assert.DoesNotContain(read, c => c is Code.Ldfld or Code.Ldelem_U1 or Code.Ldelem_I1);

        var ctor = condition.Methods.Single(m => m.IsConstructor && !m.IsStatic);
        Assert.Contains("FFXIVClientStructs.FFXIV.Client.Game.Conditions::Instance", RealDalamud.CalledNames(ctor));
    }

    // The plugin writes Conditions fields through ClientStructs and reads them back through
    // Dalamud's flags: both name the same byte.
    [Fact]
    public void ClientStructsConditionsFields_SitAtTheirDalamudFlagsOffset()
    {
        var flags = RealDalamud.Type("Dalamud.dll", "Dalamud.Game.ClientState.Conditions.ConditionFlag").Fields
            .Where(f => f.IsLiteral)
            .GroupBy(f => f.Name)
            .ToDictionary(g => g.Key, g => Convert.ToInt32(g.First().Constant));
        var fields = RealDalamud.Type("FFXIVClientStructs.dll", "FFXIVClientStructs.FFXIV.Client.Game.Conditions").Fields
            .Where(f => !f.IsStatic && f.FieldType.FullName == "System.Boolean")
            .ToList();
        var shared = fields.Where(f => flags.ContainsKey(f.Name)).ToList();
        var wrong = shared.Where(f => f.Offset != flags[f.Name]).Select(f => $"{f.Name}: offset {f.Offset}, flag {flags[f.Name]}").ToList();
        Assert.True(wrong.Count == 0, $"Conditions fields and Dalamud's flags disagree: {string.Join("; ", wrong)}");
        foreach (var written in new[] { "Occupied", "SufferingStatusAffliction", "SufferingStatusAffliction2", "SufferingStatusAffliction63", "SufferingStatusAffliction72", "SufferingStatusAffliction73" })
            Assert.Contains(shared, f => f.Name == written);
    }

    [Fact]
    public void DalamudsTerritory_MovesOnlyWhenTheClientHandlesZoneInit()
    {
        var setters = RealDalamud.CallersOf("Dalamud.dll", $"{ClientState}::set_TerritoryType").OrderBy(n => n);
        Assert.Equal(new[] { $"{ClientState}::Setup", $"{ClientState}::UIModuleHandlePacketDetour" }, setters);
        var detour = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", ClientState), "UIModuleHandlePacketDetour");
        Assert.Contains("Dalamud.Game.ClientState.ZoneInitEventArgs::Read", RealDalamud.CalledNames(detour));
    }

    // Raised from the setter alone, and only when the value changed: a zone-in to the territory
    // the client is already in raises nothing.
    [Fact]
    public void TerritoryChanged_IsRaisedOnlyOnAChange()
    {
        var raisers = RealDalamud.Module("Dalamud.dll").GetTypes()
            .SelectMany(t => t.Methods.Where(m => m.HasBody))
            .Where(m => m.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "TerritoryChanged" && f.DeclaringType.FullName == ClientState))
            .Select(m => $"{m.DeclaringType.FullName}::{m.Name}")
            .Where(n => !n.EndsWith("::add_TerritoryChanged") && !n.EndsWith("::remove_TerritoryChanged"))
            .ToList();
        Assert.Equal(new[] { $"{ClientState}::set_TerritoryType" }, raisers);

        var setter = RealDalamud.Method(RealDalamud.Type("Dalamud.dll", ClientState), "set_TerritoryType").Body.Instructions.ToList();
        var compare = setter.FindIndex(i => i.OpCode.Code is Code.Beq or Code.Beq_S);
        var store = setter.FindIndex(i => i.OpCode == OpCodes.Stfld && ((FieldReference)i.Operand).Name == "<TerritoryType>k__BackingField");
        Assert.True(compare >= 2 && compare < store && setter.Skip(compare - 2).Take(2).Any(i => i.Operand is FieldReference { Name: "<TerritoryType>k__BackingField" }),
            "set_TerritoryType no longer skips an unchanged value; the harness raises TerritoryChanged only on a change.");
    }

    // ZoneSession's territory sync looks for a writable TerritoryType by these names. On the
    // client state plugins get there is none, so Dalamud keeps reading the inn through a stay, as
    // the default fake does (FakeClientStateSyncable covers a Dalamud that had one).
    [Fact]
    public void ThePluginsTerritorySync_FindsNothingToWrite_AsWithTheDefaultFake()
    {
        var names = PluginStrings(nameof(ZoneSession), "SyncClientStateTerritoryType");
        Assert.Contains("TerritoryType", names);
        var scoped = RealDalamud.Type("Dalamud.dll", "Dalamud.Game.ClientState.ClientStatePluginScoped");
        Assert.DoesNotContain(scoped.Properties, p => names.Contains(p.Name) && p.SetMethod != null);
        Assert.DoesNotContain(scoped.Fields, f => names.Contains(f.Name) && !f.IsStatic);
        var getter = RealDalamud.Method(scoped, "get_TerritoryType");
        Assert.Equal(new[] { $"{ClientState}::get_TerritoryType" }, RealDalamud.CalledNames(getter));

        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        Assert.False(typeof(FakeClientState).GetProperty("TerritoryType", any)?.CanWrite ?? false);
        Assert.DoesNotContain(typeof(FakeClientState).GetFields(any), f => names.Contains(f.Name));
    }

    // IsInInn reads a missing territory as "not an inn"; the zone load's lookup of one throws, and
    // Enter backs out through the verified revert. The fake sheet does both.
    [Fact]
    public void SheetLookups_FailAsTheFakeSheetDoes()
    {
        var sheet = RealDalamud.Type("Lumina.dll", "Lumina.Excel.ExcelSheet`1");
        Assert.Contains("Lumina.Excel.ExcelSheet`1::GetRow", RealDalamud.CalledNames(RealDalamud.Method(sheet, "get_Item")));
        Assert.Contains(RealDalamud.Method(sheet, "GetRow").Body.Instructions, i => i.OpCode == OpCodes.Throw
            && i.Previous is { OpCode.Code: Code.Newobj, Operand: MethodReference ctor }
            && ctor.DeclaringType.FullName == "System.ArgumentOutOfRangeException");
        Assert.Equal("System.Nullable`1<T>", RealDalamud.Method(sheet, "GetRowOrDefault").ReturnType.FullName);

        var fake = new Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.ClassJob>(new Dictionary<uint, Lumina.Excel.Sheets.ClassJob>());
        Assert.Throws<ArgumentOutOfRangeException>(() => fake[7]);
        Assert.Null(fake.GetRowOrDefault(7));
    }

    private static HashSet<string> PluginStrings(string type, string method)
    {
        var module = ModuleDefinition.ReadModule(typeof(ZoneSession).Assembly.Location);
        var sync = module.GetType($"AnoMech.Core.Map.{type}").Methods.Single(m => m.Name == method);
        return sync.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand).ToHashSet();
    }
}
