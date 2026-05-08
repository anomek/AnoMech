using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using UltiSim.Core.Map;
using UltiSim.Core;
using UltiSim.Core.SimObjects;
using UltiSim.Scenarios;

namespace UltiSim.Windows;

public unsafe class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private bool _leftPanelOpen = true;
    internal IScenario? SelectedScenario => _selectedScenario;
    private IScenario? _selectedScenario;

#if DEBUG
    private string debugBNpcBaseIdText = "0x3D69";
    private string debugSpawnScaleText = "0";
    private string debugTimelineIdText = "0x53C";
    private string debugMapEffectIndexText = "0x00";
    private string debugMapEffectStatusText = "0x0000";
    private string debugMapEffectFlagText = "0x00";
    private string debugBgmIdText = "964";
#endif

    public MainWindow(Plugin plugin)
        : base("UltiSim##MainWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 80),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Flags |= ImGuiWindowFlags.AlwaysAutoResize;

        this.plugin = plugin;
        IsOpen = true;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var leftWidth = _leftPanelOpen ? 180f : 30f;

        if (ImGui.BeginTable("##layout", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthFixed, leftWidth);
            ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            DrawScenariosPanel();
            ImGui.TableSetColumnIndex(1);
            DrawMainContent();
            ImGui.EndTable();
        }
    }

    private void DrawScenariosPanel()
    {
        if (_leftPanelOpen)
        {
            ImGui.TextUnformatted("Scenarios");
            ImGui.SameLine();
            if (ImGui.SmallButton("<##collapse")) _leftPanelOpen = false;
            ImGui.Separator();

            foreach (var scenario in plugin.Game.Scenarios)
            {
                var selected = _selectedScenario == scenario;
                if (selected) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                ImGui.PushID(scenario.Name);
                if (ImGui.Button(scenario.Name, new Vector2(-1, 0))) _selectedScenario = scenario;
                ImGui.PopID();
                if (selected) ImGui.PopStyleColor();
            }
        }
        else
        {
            if (ImGui.Button(">##expand")) _leftPanelOpen = true;
        }
    }

    private void DrawMainContent()
    {
        if (_selectedScenario == null)
        {
            ImGui.TextDisabled("Select a scenario");
            return;
        }

        var game = plugin.Game;

        ImGui.TextUnformatted(_selectedScenario.Name);
        ImGui.Separator();
        DrawLocationHint();

        if (ImGui.Button("Start")) game.RunScenario(_selectedScenario);
        ImGui.SameLine();
        if (ImGui.Button("Reset")) game.Reset();
        if (game.World.Map.IsInInstance)
        {
            ImGui.SameLine();
            if (ImGui.Button("Leave")) game.Leave();
        }

        var god = game.GodMode;
        if (ImGui.Checkbox("God mode", ref god)) game.GodMode = god;

#if DEBUG
        DrawSpeedControl();
#endif

        if (game.Paused) ImGui.TextDisabled("(scenario paused — press Reset to clear)");

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Scenario config", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            _selectedScenario.DrawSettings();
            ImGui.Unindent();
        }

#if DEBUG
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Debug"))
        {
            DrawDebugContent();
        }
#endif
    }

    private void DrawLocationHint()
    {
        var scenario = _selectedScenario!;
        var territory = Plugin.ClientState.TerritoryType;

        if (ZoneSession.IsInInn())
        {
            ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), "Full simulation available");
        }
        else if (IsScenarioTerritory(scenario, territory))
        {
            var name = GetTerritoryName(territory) ?? territory.ToString();
            ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1f), $"Some visuals might be missing — {name}");
        }
        else
        {
            ImGui.TextDisabled("Works anywhere · Inn gives the full experience");
        }

        ImGui.SameLine();
        var help =
            "Inn: full scenario with correct arena geometry.\n" +
            "Supported instance: some visuals might be missing, zone layout auto-detected.\n" +
            "Anywhere else: some visuals might be missing, not adjusted for ground geometry — origin anchors to your position.";
        var supportedNames = GetSupportedDutyNames(scenario);
        if (supportedNames.Count > 0)
            help += "\n\nSupported instances:\n  " + string.Join("\n  ", supportedNames);
        ImGuiComponents.HelpMarker(help);
    }

    private static bool IsScenarioTerritory(IScenario scenario, uint territory)
    {
        if (scenario.TargetInstance?.TerritoryId == territory) return true;
        foreach (var ovr in scenario.OriginOverrides)
            if (ovr.TerritoryId == territory) return true;
        return false;
    }

    private static List<string> GetSupportedDutyNames(IScenario scenario)
    {
        var names = new List<string>();
        foreach (var ovr in scenario.OriginOverrides)
        {
            var n = GetDutyName(ovr.TerritoryId);
            if (!string.IsNullOrEmpty(n)) names.Add(n);
        }
        return names;
    }

    private static string? GetTerritoryName(uint id) =>
        Plugin.DataManager.GetExcelSheet<TerritoryType>()
            ?.GetRowOrDefault(id)?.PlaceName.ValueNullable?.Name.ExtractText();

    private static string? GetDutyName(uint territoryId) =>
        Plugin.DataManager.GetExcelSheet<TerritoryType>()
            ?.GetRowOrDefault(territoryId)?.ContentFinderCondition.ValueNullable?.Name.ExtractText();

    // Buttons modify Game.EventTimeScale live so callers can speed up / slow down a
    // scenario mid-run. Only event scheduling is affected; cast bars and animations
    // continue at real time (see Game.Tick).
    private void DrawSpeedControl()
    {
        var game = plugin.Game;
        ImGui.TextUnformatted("Speed:");
        for (int x = 1; x <= 4; x++)
        {
            ImGui.SameLine();
            var active = MathF.Abs(game.EventTimeScale - x) < 0.01f;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            if (ImGui.Button($"x{x}")) game.EventTimeScale = x;
            if (active) ImGui.PopStyleColor();
        }
    }

#if DEBUG
    private void DrawDebugContent()
    {
        ImGui.TextUnformatted($"TerritoryId: {Plugin.ClientState.TerritoryType}");

        ImGui.Spacing();
        ImGui.TextUnformatted("Manual spawn");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("BNpcBaseId", ref debugBNpcBaseIdText, 16);
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Scale (0 = default)", ref debugSpawnScaleText, 16);
        if (ImGui.Button("Spawn"))
        {
            if (!TryParseId(debugBNpcBaseIdText, out var baseId))
            {
                Plugin.Log.Warning($"Spawn: can't parse BNpcBaseId '{debugBNpcBaseIdText}'");
            }
            else
            {
                float scale = 0f;
                var trimmed = debugSpawnScaleText.Trim();
                if (trimmed.Length > 0 && !float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
                    Plugin.Log.Warning($"Spawn: can't parse Scale '{debugSpawnScaleText}', using default");
                // Ad-hoc spawn outside any scenario — anchor to the player so
                // Offset=0 lands at our feet. Scenario runs overwrite this in
                // Game.RunScenarioInternal, so the stamp is non-leaking.
                var player = Plugin.ObjectTable.LocalPlayer;
                if (player != null) plugin.Game.World.ScenarioOrigin = player.Position;
                plugin.Game.World.SpawnEnemy(new EnemySpawnConfig(
                    BNpcBaseId: baseId,
                    Targetable: true,
                    Scale: scale));
            }
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Play animation on target");
        ImGui.Separator();
        ImGui.SetNextItemWidth(120);
        ImGui.InputText("TimelineId", ref debugTimelineIdText, 16);
        if (ImGui.Button("Play on target"))
        {
            if (TryParseId(debugTimelineIdText, out var timelineId))
                PlayAnimationOnTarget((ushort)timelineId);
            else Plugin.Log.Warning($"Play animation: can't parse TimelineId '{debugTimelineIdText}'");
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Dump objects near player");
        ImGui.Separator();
        if (ImGui.Button("Dump"))
            DumpNearbyObjects();

        ImGui.Spacing();
        ImGui.TextUnformatted("Map effect");
        ImGui.Separator();
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Index", ref debugMapEffectIndexText, 16);
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Status", ref debugMapEffectStatusText, 16);
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("Flag", ref debugMapEffectFlagText, 16);
        if (ImGui.Button("Apply map effect"))
        {
            if (!TryParseId(debugMapEffectIndexText, out var idx) || idx > 0xFF)
                Plugin.Log.Warning($"Map effect: can't parse Index '{debugMapEffectIndexText}'");
            else if (!TryParseId(debugMapEffectStatusText, out var status) || status > 0xFFFF)
                Plugin.Log.Warning($"Map effect: can't parse Status '{debugMapEffectStatusText}'");
            else if (!TryParseId(debugMapEffectFlagText, out var flag) || flag > 0xFF)
                Plugin.Log.Warning($"Map effect: can't parse Flag '{debugMapEffectFlagText}'");
            else
                plugin.Game.World.Map.AddEffect((status << 16) | (flag & 0xFFu), (byte)idx);
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Death system");
        ImGui.Separator();
        if (ImGui.Button("Kill MT"))
        {
            var mt = plugin.Game.World.Party.Get(PartyRole.MainTank);
            if (mt != null) plugin.Game.Kill(mt, "Debug kill");
        }
        ImGui.SameLine();
        if (ImGui.Button("Kill player") && plugin.Game.Player is { } p) plugin.Game.Kill(p, "Debug kill");

        ImGui.Spacing();
        ImGui.TextUnformatted("BGM test");
        ImGui.Separator();
        ImGui.SetNextItemWidth(80);
        ImGui.InputText("BgmId", ref debugBgmIdText, 16);
        ImGui.SameLine();
        if (ImGui.Button("Play##bgm"))
        {
            if (!TryParseId(debugBgmIdText, out var bgmId) || bgmId > ushort.MaxValue)
                Plugin.Log.Warning($"BGM: can't parse BgmId '{debugBgmIdText}'");
            else
                plugin.Game.Bgm.Play((ushort)bgmId);
        }
        ImGui.SameLine();
        if (ImGui.Button("Stop##bgm")) plugin.Game.Bgm.Reset();
    }

    // Accepts decimal ("1340") or hex ("0x53C" / "53Ch" — case-insensitive).
    private static bool TryParseId(string input, out uint value)
    {
        var s = input.Trim();
        if (s.Length == 0) { value = 0; return false; }
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        if (s.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(s[..^1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private void PlayAnimationOnTarget(ushort timelineId)
    {
        var target = Plugin.TargetManager.Target;
        if (target == null)
        {
            Plugin.Log.Warning("Play animation: no target selected");
            return;
        }
        var targetId = target.GameObjectId;
        foreach (var enemy in plugin.Game.World.Children.OfType<SimEnemy>())
        {
            if ((ulong)enemy.GameObjectId == targetId)
            {
                enemy.PlayActionTimeline(timelineId);
                Plugin.Log.Info($"Play animation: timeline 0x{timelineId:X} on '{enemy.DisplayName}'");
                return;
            }
        }
        Plugin.Log.Warning($"Play animation: target '{target.Name}' is not a tracked enemy");
    }

    // Logs every object in the table sorted by distance from the local player. BaseId is
    // the underlying BNpcBase row for BNpcs (and the equivalent base id for other kinds);
    // Scale is read from the unsafe GameObject struct.
    private static void DumpNearbyObjects()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) { Plugin.Log.Warning("DumpObjects: no local player"); return; }
        var origin = player.Position;

        var rows = new List<(float Dist, string Line)>();
        foreach (var obj in Plugin.ObjectTable)
        {
            var dx = obj.Position.X - origin.X;
            var dz = obj.Position.Z - origin.Z;
            var dist = MathF.Sqrt(dx * dx + dz * dz);
            var name = obj.Name.TextValue;
            if (string.IsNullOrEmpty(name)) name = "<unnamed>";
            var scale = ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address)->Scale;
            rows.Add((dist, $"  [{obj.ObjectKind,-12}] BaseId=0x{obj.BaseId:X} ({obj.BaseId}) dist={dist,7:F2} scale={scale,5:F2}  '{name}'"));
        }
        rows.Sort((a, b) => a.Dist.CompareTo(b.Dist));

        Plugin.Log.Info($"=== ObjectTable: {rows.Count} objects ===");
        foreach (var (_, line) in rows) Plugin.Log.Info(line);
    }
#endif
}
