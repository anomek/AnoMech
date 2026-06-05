using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using AnoMech.Core.Map;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;

namespace AnoMech.Windows;

public unsafe class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private bool _leftPanelOpen = true;
    internal IScenario? SelectedScenario => _selectedScenario;
    private IScenario? _selectedScenario;

    internal PartyRole? SelectedRoleOverride => _roleOverride;
    private PartyRole? _roleOverride;

    // Index 0 = Auto (null override); indices 1..8 map to (PartyRole)(idx - 1).
    // Labels are the canonical raid role abbreviations: MT/OT tanks, H1/H2 healers
    // (H1 = regen), M1/M2 melee DPS, R1/R2 ranged DPS (R1 = phys).
    private static readonly string[] RoleLabels =
        ["Auto", "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];

#if DEBUG
    private readonly DebugMenu debugMenu;
#endif

    // <Version> from AnoMech.csproj flows into the assembly version; surface it in the
    // title bar. Use a ### id so the window identity stays "MainWindow" across versions.
    private static string TitleWithVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        var version = v is null ? "" : $" v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        return $"AnoMech{version}###MainWindow";
    }

    public MainWindow(Plugin plugin)
        : base(TitleWithVersion())
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 80),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Flags |= ImGuiWindowFlags.AlwaysAutoResize;

        this.plugin = plugin;
        IsOpen = false;

        // Small gear in the title bar opens the settings window (same toggle as /anomech config).
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => plugin.ToggleConfigUi(),
            ShowTooltip = () => ImGui.SetTooltip("Settings"),
        });
#if DEBUG
        debugMenu = new DebugMenu(plugin);
#endif
    }

    public void Dispose() { }

    private bool _wasInInstance;

    // While the fake-zone instance is loaded, pin the window open and uncollapsible
    // so the user can always reach Reset/Leave/God-mode without re-opening it.
    public override void PreOpenCheck()
    {
        var inInstance = plugin.Game.World.Map.IsInInstance;
        if (inInstance)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            Flags |= ImGuiWindowFlags.NoCollapse;
            if (!_wasInInstance)
            {
                Collapsed = false;
                CollapsedCondition = ImGuiCond.Always;
            }
        }
        else
        {
            ShowCloseButton = true;
            RespectCloseHotkey = true;
            Flags &= ~ImGuiWindowFlags.NoCollapse;
            if (_wasInInstance)
                CollapsedCondition = ImGuiCond.FirstUseEver;
        }
        _wasInInstance = inInstance;
    }

    public override void Draw()
    {
        var leftWidth = _leftPanelOpen ? ScenarioPanelWidth() : 30f;

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

    // Size the left panel to the widest scenario label so names never clip as scenarios are added.
    private float ScenarioPanelWidth()
    {
        var style = ImGui.GetStyle();
        var widest = 0f;
        foreach (var scenario in plugin.Game.Scenarios)
            widest = Math.Max(widest, ImGui.CalcTextSize(scenario.Name).X);
        var measured = widest + style.FramePadding.X * 2 + style.CellPadding.X * 2;
        return Math.Max(180f, measured);
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

        DrawRoleSelector();

        var inInn = ZoneSession.IsInInn();
        var busy = ZoneSession.IsPlayerBusy();
        var canStart = inInn && !busy;
        ImGui.BeginDisabled(!canStart);
        if (ImGui.Button("Start")) game.RunScenario(_selectedScenario, _roleOverride);
        ImGui.EndDisabled();
        if (!canStart && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(!inInn
                ? "Scenarios can only be started from an inn."
                : "Cannot start while you are busy (cutscene, NPC event, crafting, trading, zoning, etc.).");
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset")) game.Reset();
        if (game.World.Map.IsInInstance)
        {
            ImGui.SameLine();
            if (ImGui.Button("Leave")) game.Leave();
        }

        if (_selectedScenario.SupportsSolo)
        {
            ImGui.BeginDisabled(!canStart);
            if (ImGui.Button("Start Solo")) game.RunScenario(_selectedScenario, _roleOverride, solo: true);
            ImGui.EndDisabled();
            if (!canStart && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(!inInn
                    ? "Scenarios can only be started from an inn."
                    : "Cannot start while you are busy (cutscene, NPC event, crafting, trading, zoning, etc.).");
            }
        }

        var god = game.GodMode;
        if (ImGui.Checkbox("God mode", ref god)) game.GodMode = god;

#if DEBUG
        debugMenu.DrawSpeedControl();
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
            debugMenu.DrawDebugContent();
        }
#endif
    }

    private void DrawRoleSelector()
    {
        var idx = _roleOverride is { } role ? (int)role + 1 : 0;
        ImGui.TextUnformatted("Select your Role:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        if (ImGui.Combo("##role", ref idx, RoleLabels, RoleLabels.Length))
            _roleOverride = idx == 0 ? null : (PartyRole)(idx - 1);
    }

    private void DrawLocationHint()
    {
        if (ZoneSession.IsInInn()) return;
        ImGui.TextDisabled("Scenarios only run in an inn");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Scenarios can only be started from an inn — return to one to run a scenario.");
    }
}
