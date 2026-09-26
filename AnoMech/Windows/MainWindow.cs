using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using AnoMech.Core.Map;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;
using static AnoMech.Core.Game.Game;

namespace AnoMech.Windows;

public unsafe class MainWindow : Window, IDisposable
{
    private const float ScenarioButtonExtraPadding = 6f;
    private const float SetupDropdownWidth = 180f;
    private const float MinimumSectionsHeight = 120f;

    internal static readonly Vector4 StartColor = new(0.18f, 0.40f, 0.24f, 0.92f);
    internal static readonly Vector4 StopColor = new(0.45f, 0.14f, 0.16f, 0.92f);
    private static readonly Vector4 RunningColor = new(0.35f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 PausedColor = new(1f, 0.65f, 0.25f, 1f);

    private readonly Plugin plugin;
    private IZone? _openZone;
    internal ScenarioPanelWindow ScenarioPanel { get; }
    internal Vector2 ScenarioPanelAnchor { get; private set; }
    internal float ScenarioPanelHeight { get; private set; }
    internal bool IsActuallyCollapsed { get; private set; }
    private float _windowChromeHeight;
    private Vector2? _windowPos;
    private bool _onMainViewport;
    private Vector2 _sectionsSize;
    internal IScenario? SelectedScenario => _selectedScenario;
    private IScenario? _selectedScenario;

    internal PartyRole? SelectedRoleOverride => _roleOverride;
    private PartyRole? _roleOverride;
    private bool _soloMode;

    // Index into the selected scenario's AiStrats; reset to the first strat whenever the
    // selected scenario changes. Passed to RunScenario as selectedAi on a (non-solo) Start.
    // -1 when a grouped scenario's selected region has no strats (Start is then gated off).
    internal int SelectedStrat => _selectedStrat;
    private int _selectedStrat;

    // Index into the selected zone's WaymarkPresets. Remembered per zone so switching between
    // scenarios in the same encounter keeps the chosen layout.
    internal int SelectedWaymark => _selectedWaymark;
    private int _selectedWaymark;
    private readonly Dictionary<IZone, int> _waymarkMemory = new();

    // The region/group label currently selected in the strat picker, for scenarios that
    // declare StratGroups. Null until a grouped scenario is drawn (then it snaps to the
    // first group); stays null for ungrouped scenarios. Filters AiStrats under the buttons.
    private string? _selectedStratGroup;

    // The last region picked per grouped scenario, restored on a switch back to it.
    private readonly Dictionary<IScenario, string> _stratGroupMemory = new();

    // Index 0 = Auto (null override); indices 1..8 map to (PartyRole)(idx - 1).
    // Labels are the canonical raid role abbreviations: MT/OT tanks, H1/H2 healers
    // (H1 = regen), M1/M2 melee DPS, R1/R2 ranged DPS (R1 = phys).
    private static readonly string[] RoleLabels =
        ["Auto", "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];

#if DEBUG
    private readonly DebugMenu debugMenu;
#endif

    // Version plus the build checksum the multiplayer handshake compares; the ### id keeps the
    // window identity stable across versions.
    private static string TitleWithVersion()
        => $"AnoMech v{PluginBuildInfo.Version} ({PluginBuildInfo.ShortChecksum})###MainWindow";

    public MainWindow(Plugin plugin)
        : base(TitleWithVersion())
    {
        var uiScale = ImGuiHelpers.GlobalScale;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 80) * uiScale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Flags |= ImGuiWindowFlags.AlwaysAutoResize;

        this.plugin = plugin;
        ScenarioPanel = new ScenarioPanelWindow(this);
        IsOpen = false;
        RestoreSelectedScenario();

        // Global tools live in the title bar so the scenario header stays focused on the
        // selected scenario. Higher priority places Multiplayer to the left of Settings.
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Users,
            IconOffset = new Vector2(2f, 1f) * uiScale,
            Priority = 1,
            Click = _ => plugin.MultiplayerWindow.Toggle(),
            ShowTooltip = () => ImGui.SetTooltip(plugin.Multiplayer.IsConnected
                ? "Multiplayer (connected)"
                : "Multiplayer"),
        });

        // Small gear opens the settings window (same toggle as /anomech config).
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new Vector2(2f, 1f) * uiScale,
            Priority = 0,
            Click = _ => plugin.ToggleConfigUi(),
            ShowTooltip = () => ImGui.SetTooltip("Settings"),
        });
#if DEBUG
        debugMenu = new DebugMenu(plugin);
#endif
    }

    public void Dispose()
    {
#if DEBUG
        debugMenu.Dispose();
#endif
    }

    // Hidden while the instance is loaded (RunningSimWindow covers Start/Reset/Leave) and
    // reopened afterwards only if we were the one who closed it.
    private bool hiddenByUs;

    public override void PreOpenCheck()
    {
        if (plugin.Game.World.Map.IsInInstance)
        {
            if (IsOpen) hiddenByUs = true;
            IsOpen = false;
        }
        else if (hiddenByUs)
        {
            hiddenByUs = false;
            IsOpen = true;
        }
    }

    public override void PreDraw()
    {
        var uiScale = ImGuiHelpers.GlobalScale;
        var minimumHeight = 80f;
        if (ScenarioPanel.RequestedOpen && ScenarioPanel.NaturalHeight > 0f)
            minimumHeight = Math.Max(minimumHeight, (_windowChromeHeight + ScenarioPanel.NaturalHeight) / uiScale);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220f, minimumHeight),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        KeepScenarioPanelOnScreen();
    }

    // The scenario panel docks to this window's left edge, so a window opened or dropped too close
    // to the game window's left edge moves right until the panel fits. Not while the mouse is held
    // (a drag), nor outside the game window (multi-monitor windows).
    private void KeepScenarioPanelOnScreen()
    {
        Position = null;
        if (_windowPos is not { } pos || !_onMainViewport || !ScenarioPanel.RequestedOpen || IsActuallyCollapsed
            || ImGui.IsMouseDown(ImGuiMouseButton.Left))
            return;
        var minimumX = ImGui.GetMainViewport().WorkPos.X + ScenarioPanelWindowWidth() - ImGui.GetStyle().WindowBorderSize;
        if (pos.X >= minimumX) return;
        Position = new Vector2(minimumX, pos.Y);
        PositionCondition = ImGuiCond.Always;
    }

    public override void PostDraw()
    {
        var window = ImGuiP.FindWindowByName(WindowName);
        IsActuallyCollapsed = !window.IsNull && window.Collapsed;
    }

    public override void Draw()
    {
        var windowPos = ImGui.GetWindowPos();
        _windowPos = windowPos;
        _onMainViewport = ImGui.GetWindowViewport().ID == ImGui.GetMainViewport().ID;
        var contentTop = windowPos.Y + ImGui.GetFrameHeight();
        ScenarioPanelAnchor = new Vector2(windowPos.X, contentTop);
        _windowChromeHeight = contentTop - windowPos.Y;
        ScenarioPanelHeight = windowPos.Y + ImGui.GetWindowSize().Y - contentTop;
        DrawMainContent();
    }

    // Size the left panel to the widest scenario label so names never clip as scenarios are added.
    internal float ScenarioPanelWindowWidth()
    {
        var style = ImGui.GetStyle();
        var widest = 0f;
        foreach (var zone in plugin.Game.Zones)
        {
            widest = Math.Max(widest, ImGui.CalcTextSize(zone.Name).X + style.IndentSpacing);
            foreach (var phase in plugin.Game.PhasesOf(zone))
                foreach (var scenario in plugin.Game.ScenariosOf(phase))
                    widest = Math.Max(widest, ImGui.CalcTextSize(DisplayName(scenario)).X + style.IndentSpacing);
        }
        var measured = widest
            + style.FramePadding.X * 2
            + style.CellPadding.X * 2
            + style.ScrollbarSize
            + ScenarioButtonExtraPadding * 2 * ImGuiHelpers.GlobalScale;
        var contentWidth = Math.Max(180f * ImGuiHelpers.GlobalScale, measured);
        return contentWidth + style.WindowPadding.X * 2;
    }

    internal void DrawScenariosPanel()
    {
        ImGui.TextUnformatted("Scenarios");
        ImGui.Separator();

        var mpWindowOpen = plugin.MultiplayerWindow.IsOpen;
        var mpConnected = plugin.Multiplayer.IsConnected;
        foreach (var zone in plugin.Game.Zones)
        {
            var shouldOpen = _openZone == zone;
            ImGui.SetNextItemOpen(shouldOpen, ImGuiCond.Always);
            var headerColor = BlendColor(
                StyleColor(ImGuiCol.WindowBg),
                StyleColor(ImGuiCol.Header),
                0.72f);
            ImGui.PushStyleColor(ImGuiCol.Header, headerColor);
            var open = ImGui.CollapsingHeader($"{zone.Name}###scenario-zone-{zone.GetType().FullName}");
            ImGui.PopStyleColor();
            var sectionRight = ImGui.GetItemRectMax().X;
            if (open != shouldOpen) _openZone = open ? zone : null;
            if (!open) continue;
            ImGui.Indent();
            var buttonPadding = ImGui.GetStyle().FramePadding;
            buttonPadding.X += ScenarioButtonExtraPadding * ImGuiHelpers.GlobalScale;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, buttonPadding);
            ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0f, 0.5f));
            foreach (var phase in plugin.Game.PhasesOf(zone))
                foreach (var scenario in plugin.Game.ScenariosOf(phase))
                {
                    var selected = _selectedScenario == scenario;
                    var mpUnsupported = (mpWindowOpen || mpConnected) && !scenario.SupportsMultiplayer;
                    if (selected) PushSelectedScenarioStyle();
                    // Zone-qualified: two zones can hold same-named scenarios (UMAD and UCOB
                    // both have a P5 "Exaflares"), and a shared ImGui id makes the second
                    // button unclickable.
                    ImGui.PushID(FullName(scenario));
                    var buttonWidth = sectionRight - ImGui.GetCursorScreenPos().X;
                    ImGui.BeginDisabled(mpUnsupported);
                    var clicked = ImGui.Button(DisplayName(scenario), new Vector2(buttonWidth, 0));
                    ImGui.EndDisabled();
                    if (mpUnsupported && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip($"This scenario doesn't support multiplayer. {MpDisabledReason(mpWindowOpen, mpConnected)}");
                    if (selected)
                    {
                        var min = ImGui.GetItemRectMin();
                        var max = ImGui.GetItemRectMax();
                        var accentWidth = 3f * ImGuiHelpers.GlobalScale;
                        ImGui.GetWindowDrawList().AddRectFilled(
                            min,
                            new Vector2(min.X + accentWidth, max.Y),
                            ImGui.GetColorU32(ImGuiCol.ButtonActive));
                        ImGui.PopStyleColor(3);
                    }
                    ImGui.PopID();
                    if (clicked) SelectScenario(scenario);
                }
            ImGui.PopStyleVar(2);
            ImGui.Unindent();
        }
    }

    private void RestoreSelectedScenario()
    {
        if (string.IsNullOrEmpty(Plugin.Config.LastSelectedScenario)) return;
        foreach (var scenario in plugin.Game.Scenarios)
        {
            if (!string.Equals(
                    FullName(scenario),
                    Plugin.Config.LastSelectedScenario,
                    StringComparison.Ordinal))
                continue;
            SelectScenario(scenario, persist: false);
            return;
        }
    }

    // Select a scenario and reset its per-scenario UI state (strat, waymark, remembered region).
    private void SelectScenario(IScenario scenario, bool persist = true)
    {
        if (_selectedScenario is { } previousScenario)
            _waymarkMemory[previousScenario.Phase.Zone] = _selectedWaymark;

        _selectedScenario = scenario;
        _openZone = scenario.Phase.Zone;
        _soloMode = false;
        _selectedStrat = 0;
        _selectedWaymark = _waymarkMemory.GetValueOrDefault(scenario.Phase.Zone);
        if (_selectedWaymark < 0 || _selectedWaymark >= scenario.Phase.Zone.WaymarkPresets.Count)
            _selectedWaymark = 0;
        // Restore the last region picked for this scenario; ReconcileStrat snaps null to its first region.
        _selectedStratGroup = _stratGroupMemory.GetValueOrDefault(scenario);
        ReconcileStrat();

        if (persist)
        {
            Plugin.Config.LastSelectedScenario = FullName(scenario);
            Plugin.Config.Save();
        }
    }

    // Shared wording for every control disabled by the Multiplayer window or a live session.
    private static string MpDisabledReason(bool windowOpen, bool connected) => (windowOpen, connected) switch
    {
        (true, true) => "Disabled: the Multiplayer window is open and you're connected to a multiplayer session.",
        (true, false) => "Disabled while the Multiplayer window is open.",
        (false, true) => "Disabled while connected to a multiplayer session.",
        _ => "",
    };

    // Distinct, ordered region labels from the strats' IScenarioAi.Group; empty = ungrouped.
    private static IReadOnlyList<string> StratGroups(IScenario scenario)
    {
        var groups = new List<string>();
        foreach (var ai in scenario.AiStrats)
            if (ai.Group is { } g && !groups.Contains(g)) groups.Add(g);
        return groups;
    }

    private void DrawMainContent()
    {
        if (_selectedScenario == null)
        {
            DrawScenarioPanelToggle();
            ImGui.SameLine();
            ImGui.TextDisabled("Select a scenario");
            return;
        }

        var game = plugin.Game;
        var mpWindowOpen = plugin.MultiplayerWindow.IsOpen;
        var mpConnected = plugin.Multiplayer.IsConnected;
        var mpActive = mpWindowOpen || mpConnected;
        var mpGuest = mpConnected && !plugin.Multiplayer.IsHost;
        ReconcileSetup(mpConnected, mpGuest);
#if DEBUG
        if (mpActive) game.EventTimeScale = 1f;
#endif

        DrawScenarioHeader(game);
        DrawPrimaryActions(game);
        DrawSoloOption(game);
        DrawLocationHint();
        DrawRunOptions(game, mpWindowOpen, mpConnected);

        // The sections scroll rather than push the window past the bottom of the game window.
        // The child takes last frame's measured size: one sized "remaining" would collapse inside
        // an auto-resize window.
        ImGui.Spacing();
        var style = ImGui.GetStyle();
        var viewport = ImGui.GetWindowViewport();
        var room = viewport.WorkPos.Y + viewport.WorkSize.Y - ImGui.GetCursorScreenPos().Y - style.WindowPadding.Y;
        var height = Math.Min(_sectionsSize.Y, Math.Max(room, MinimumSectionsHeight * ImGuiHelpers.GlobalScale));
        var scrolling = _sectionsSize.Y > height;
        ImGui.BeginChild("##sections", new Vector2(_sectionsSize.X + (scrolling ? style.ScrollbarSize : 0f), height), false, ImGuiWindowFlags.None);
        DrawSections(_selectedScenario, mpWindowOpen, mpConnected, mpGuest);
        // The size ImGui's own auto-fit uses: a nested table (SettingsGrid) reports its width
        // only here and clamps CursorMaxPos to its outer rect, so a group would under-measure.
        _sectionsSize = ImGuiP.GetCurrentWindow().ContentSizeIdeal;
        ImGui.EndChild();
    }

    // Every frame, Setup expanded or not: Start and the Multiplayer window's Start read these.
    // Once connected the role comes from the Multiplayer claim, and only the host's region/strat
    // is broadcast and run; each is reset, not just disabled, so a stale pick can't apply.
    private void ReconcileSetup(bool mpConnected, bool mpGuest)
    {
        if (mpConnected) _roleOverride = null;
        if (mpGuest)
        {
            _selectedStrat = 0;
            _selectedStratGroup = null;
        }
        ReconcileStrat();
    }

    // _selectedStrat stays an absolute index into AiStrats (what RunScenario consumes): for a
    // grouped scenario, a strat of the selected region (its first when the pick isn't in it, -1
    // when it has none); otherwise in range.
    private void ReconcileStrat()
    {
        if (_selectedScenario is not { } scenario) return;
        var strats = scenario.AiStrats;
        var groups = StratGroups(scenario);
        if (groups.Count == 0)
        {
            if (strats.Count > 1) _selectedStrat = Math.Clamp(_selectedStrat, 0, strats.Count - 1);
            return;
        }
        if (!GroupsContain(groups, _selectedStratGroup)) _selectedStratGroup = groups[0];
        var inRegion = RegionStrats(strats);
        if (inRegion.Count == 0) _selectedStrat = -1;
        else if (!inRegion.Contains(_selectedStrat)) _selectedStrat = inRegion[0];
    }

    private List<int> RegionStrats(IReadOnlyList<IScenarioAi> strats)
    {
        var inRegion = new List<int>();
        for (var i = 0; i < strats.Count; i++)
            if (strats[i].Group == _selectedStratGroup) inRegion.Add(i);
        return inRegion;
    }

    private void DrawSections(IScenario scenario, bool mpWindowOpen, bool mpConnected, bool mpGuest)
    {
        var mpActive = mpWindowOpen || mpConnected;
        if (ImGui.TreeNodeEx("Setup###scenario-setup-v3",
                ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.FramePadding))
        {
            if (SettingsGrid.Begin("##scenario-setup-grid"))
            {
                DrawRoleSelector(mpConnected);
                DrawStratSelector(mpGuest);
                DrawWaymarkSelector();
                SettingsGrid.End();
            }
            ImGui.TreePop();
        }

        ImGui.Spacing();
        if (ImGui.TreeNodeEx("Scenario settings###scenario-config-v3",
                ImGuiTreeNodeFlags.FramePadding))
        {
            if (mpConnected)
            {
                ImGui.TextDisabled(plugin.Multiplayer.IsHost
                    ? "Configured in the Multiplayer window while hosting."
                    : "The host configures the scenario -- see the Multiplayer window.");
            }
            else
            {
                // DrawMultiplayerSettings only matters in multiplayer, so it stays outside the
                // disabled block.
                ImGui.BeginGroup();
                ImGui.BeginDisabled(mpActive);
                scenario.DrawSettings();
                ImGui.EndDisabled();
                ImGui.EndGroup();
                if (mpActive && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(MpDisabledReason(mpWindowOpen, mpConnected));
                // Solo keeps its own copy: these are the player's own mechanics, not only a
                // host's assignment.
                MultiplayerWindow.DrawAssignMechanicsButton(scenario, mpActive,
                                                            MpDisabledReason(mpWindowOpen, mpConnected));
                scenario.DrawMultiplayerSettings();
            }
            ImGui.TreePop();
        }

#if DEBUG
        ImGui.Spacing();
        if (ImGui.TreeNodeEx("Debug###debug-v3",
                ImGuiTreeNodeFlags.FramePadding))
        {
            ImGui.BeginDisabled(mpActive);
            ImGui.BeginGroup();
            debugMenu.DrawSpeedControl();
            ImGui.EndGroup();
            ImGui.EndDisabled();
            if (mpActive && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(MpDisabledReason(mpWindowOpen, mpConnected));
            debugMenu.DrawDebugContent();
            ImGui.TreePop();
        }
#endif
    }

    private void DrawScenarioHeader(AnoMech.Core.Game.Game game)
    {
        var uiScale = ImGuiHelpers.GlobalScale;
        var contentStart = ImGui.GetCursorScreenPos();
        var contentWidth = ImGui.GetContentRegionAvail().X;
        var rowHeight = ImGui.GetFrameHeight();
        var scenario = _selectedScenario!;

        var (statusLabel, statusColor) = Status(game.Paused, game.IsScenarioActive);

        DrawScenarioPanelToggle();
        ImGui.SameLine();
        ImGui.TextDisabled($"{scenario.Phase.Zone.Name} —");
        ImGui.SameLine(0f, 4f * uiScale);
        ImGui.TextUnformatted(DisplayName(scenario));

        var minimumStatusX = ImGui.GetItemRectMax().X + 12f * uiScale;
        // Room for the widest status, or an auto-sized window whose widest row is this one would
        // leave the overlay no space and it would not be drawn.
        ImGui.SameLine(0f, 12f * uiScale);
        ImGui.Dummy(new Vector2(StatusOverlayWidth(), rowHeight));
        DrawStatusOverlay(
            statusLabel,
            statusColor,
            contentStart.X + contentWidth,
            minimumStatusX,
            contentStart.Y,
            rowHeight);

        var dividerColor = StyleColor(ImGuiCol.TextDisabled);
        dividerColor.W *= 0.35f;
        var dividerY = contentStart.Y + rowHeight;
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(ImGui.GetWindowPos().X, dividerY),
            new Vector2(ImGui.GetWindowPos().X + ImGui.GetWindowSize().X, dividerY),
            ImGui.GetColorU32(dividerColor),
            1f * uiScale);
        ImGui.SetCursorScreenPos(new Vector2(
            contentStart.X,
            dividerY + ImGui.GetStyle().ItemSpacing.Y));
    }

    private void DrawScenarioPanelToggle()
    {
        if (DrawQuietIconButton(
                "scenario-panel-toggle",
                FontAwesomeIcon.Columns,
                active: ScenarioPanel.RequestedOpen))
            ScenarioPanel.ToggleRequested();
    }

    private static bool DrawQuietIconButton(string id, FontAwesomeIcon icon, bool active = false)
    {
        var button = StyleColor(ImGuiCol.Button);
        if (active)
        {
            var activeColor = StyleColor(ImGuiCol.ButtonActive);
            button = BlendColor(button, activeColor, 0.72f);
            button.W = MathF.Max(button.W, activeColor.W * 0.9f);
        }
        else
        {
            button.W *= 0.42f;
        }
        var hovered = StyleColor(ImGuiCol.ButtonHovered);
        if (active)
            hovered = BlendColor(hovered, StyleColor(ImGuiCol.ButtonActive), 0.55f);
        else
            hovered.W *= 0.78f;
        ImGui.PushStyleColor(ImGuiCol.Button, button);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hovered);
        if (active)
        {
            var border = AdjustColor(StyleColor(ImGuiCol.ButtonActive), 1.18f);
            border.W = 0.9f;
            ImGui.PushStyleColor(ImGuiCol.Border, border);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f * ImGuiHelpers.GlobalScale);
        }
        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            new Vector2(5f, 2f) * ImGuiHelpers.GlobalScale);
        var clicked = ImGuiComponents.IconButton(id, icon);
        ImGui.PopStyleVar(active ? 2 : 1);
        ImGui.PopStyleColor(active ? 3 : 2);
        return clicked;
    }

    private void DrawPrimaryActions(AnoMech.Core.Game.Game game)
    {
        var uiScale = ImGuiHelpers.GlobalScale;
        var actionSize = new Vector2(140f * uiScale, 32f * uiScale);
        if (game.IsScenarioActive)
        {
            if (DrawSemanticButton("Stop", actionSize, StopColor))
                plugin.ResetScenario();
        }
        else
            DrawStartButton(actionSize);

        if (game.World.Map.IsInInstance)
        {
            ImGui.SameLine();
            if (DrawSemanticButton("Leave", actionSize, StopColor))
                plugin.LeaveInstance();
        }
    }

    private bool SoloSelected => _selectedScenario is { SupportsSolo: true } && _soloMode;

    private void DrawStartButton(Vector2 size)
    {
        var solo = SoloSelected;
        var refusal = plugin.StartRefusal(solo);
        ImGui.BeginDisabled(refusal != null);
        if (DrawSemanticButton($"{(plugin.Game.StartWaitingOn != null ? "Waiting to start..." : "Start")}###start", size, StartColor))
            plugin.StartSelectedScenario(solo);
        ImGui.EndDisabled();
        if (refusal != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(refusal);
    }

    // RunningSimWindow's compact controls. Start stays up while a run is active: it restarts.
    internal void DrawSoloStartButton()
    {
        if (_selectedScenario != null) DrawStartButton(Vector2.Zero);
    }

    // Stop, plus Leave while in-instance; a connected peer's clicks route through the host. Stop
    // stays up with no run active: it also cancels a start still waiting to settle.
    internal void DrawStopLeaveButtons()
    {
        if (DrawSemanticButton("Stop", Vector2.Zero, StopColor))
            plugin.ResetScenario();
        if (!plugin.Game.World.Map.IsInInstance) return;
        ImGui.SameLine();
        if (DrawSemanticButton("Leave", Vector2.Zero, StopColor))
            plugin.LeaveInstance();
    }

    // The header's status dot and label, drawn inline.
    internal static void DrawStatus(bool paused, bool active)
    {
        var (label, color) = Status(paused, active);
        var uiScale = ImGuiHelpers.GlobalScale;
        var radius = 3f * uiScale;
        var lineHeight = ImGui.GetTextLineHeight();
        var start = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddCircleFilled(
            new Vector2(start.X + radius, start.Y + lineHeight * 0.5f),
            radius,
            ImGui.GetColorU32(color));
        ImGui.Dummy(new Vector2(radius * 2f, lineHeight));
        ImGui.SameLine(0f, 4f * uiScale);
        ImGui.TextColored(color, label);
    }

    private static (string Label, Vector4 Color) Status(bool paused, bool active) =>
        paused ? ("Paused", PausedColor)
        : active ? ("Running", RunningColor)
        : ("Idle", StyleColor(ImGuiCol.TextDisabled));

    // DrawStatusOverlay's dot, gap and widest label.
    private static float StatusOverlayWidth()
    {
        var widestLabel = MathF.Max(
            ImGui.CalcTextSize(Status(true, false).Label).X,
            MathF.Max(ImGui.CalcTextSize(Status(false, true).Label).X, ImGui.CalcTextSize(Status(false, false).Label).X));
        return 10f * ImGuiHelpers.GlobalScale + widestLabel;
    }

    private static void DrawStatusOverlay(
        string label,
        Vector4 color,
        float right,
        float minimumX,
        float top,
        float height)
    {
        var uiScale = ImGuiHelpers.GlobalScale;
        var radius = 3f * uiScale;
        var diameter = radius * 2f;
        var gap = 4f * uiScale;
        var textSize = ImGui.CalcTextSize(label);
        var fullWidth = diameter + gap + textSize.X;
        var x = right - fullWidth;
        var drawLabel = x >= minimumX;
        if (!drawLabel)
            x = right - diameter;
        if (x < minimumX) return;

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(
            new Vector2(x + radius, top + height * 0.5f),
            radius,
            ImGui.GetColorU32(color));
        if (drawLabel)
            drawList.AddText(
                new Vector2(x + diameter + gap, top + (height - textSize.Y) * 0.5f),
                ImGui.GetColorU32(color),
                label);
    }

    private static bool DrawSemanticButton(string label, Vector2 size, Vector4 color)
    {
        PushSemanticColors(color);
        var clicked = ImGui.Button(label, size);
        PopSemanticColors();
        return clicked;
    }

    internal static void PushSemanticColors(Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, AdjustColor(color, 1.16f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, AdjustColor(color, 0.84f));
    }

    internal static void PopSemanticColors() => ImGui.PopStyleColor(3);

    private static void PushSelectedScenarioStyle()
    {
        var button = StyleColor(ImGuiCol.Button);
        var hovered = StyleColor(ImGuiCol.ButtonHovered);
        var active = StyleColor(ImGuiCol.ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.Button, BlendColor(button, active, 0.52f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, BlendColor(hovered, active, 0.62f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
    }

    private static Vector4 StyleColor(ImGuiCol color) => *ImGui.GetStyleColorVec4(color);

    private static Vector4 BlendColor(Vector4 from, Vector4 to, float amount) =>
        Vector4.Lerp(from, to, amount);

    private static Vector4 AdjustColor(Vector4 color, float factor) => new(
        Math.Clamp(color.X * factor, 0f, 1f),
        Math.Clamp(color.Y * factor, 0f, 1f),
        Math.Clamp(color.Z * factor, 0f, 1f),
        color.W);

    private void DrawSoloOption(AnoMech.Core.Game.Game game)
    {
        if (!_selectedScenario!.SupportsSolo) return;
        ImGui.BeginDisabled(game.IsScenarioActive);
        ImGui.Checkbox("Solo", ref _soloMode);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(game.IsScenarioActive
                ? "Stop the current scenario before changing the run mode."
                : "Run without simulated party members or party AI.");
    }

    // God mode and auto-restart are disabled while a session is being set up or is live; forced
    // off, not just disabled, so a stale value can't apply.
    private static void DrawRunOptions(AnoMech.Core.Game.Game game, bool mpWindowOpen, bool mpConnected)
    {
        var mpActive = mpWindowOpen || mpConnected;
        ImGui.Spacing();
        if (mpActive) game.GodMode = false;
        ImGui.BeginDisabled(mpActive);
        var god = game.GodMode;
        if (ImGui.Checkbox("God mode", ref god)) game.GodMode = god;
        ImGui.EndDisabled();
        if (mpActive && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(MpDisabledReason(mpWindowOpen, mpConnected));
        ImGui.SameLine();
        // A host rerunning on its own would desync the session, so this is solo-only.
        if (mpActive) game.AutoRestart = false;
        ImGui.BeginDisabled(mpActive);
        var autoRestart = game.AutoRestart;
        if (ImGui.Checkbox("Auto-restart", ref autoRestart)) game.AutoRestart = autoRestart;
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(mpActive
                ? MpDisabledReason(mpWindowOpen, mpConnected)
                : "Restart the same scenario immediately after a successful run. A death turns this back off.");
        ImGui.SameLine();
        ImGui.TextDisabled($"Streak: {game.MechanicStreak}");
    }

    // Drawn below the strat picker for scenarios that declare WaymarkPresets. _selectedWaymark
    // is the index passed to RunScenario on Start; changing it while a scenario is loaded
    // re-places the markers immediately (same live-feedback loop as the position readout).
    private void DrawWaymarkSelector()
    {
        if (_selectedScenario is null) return;
        var presets = _selectedScenario.Phase.Zone.WaymarkPresets;
        if (presets.Count <= 1) return;
        if (_selectedWaymark < 0 || _selectedWaymark >= presets.Count) _selectedWaymark = 0;

        var labels = new string[presets.Count];
        for (var i = 0; i < presets.Count; i++) labels[i] = presets[i].Name;

        SettingsGrid.Row("Waymarks:");
        ImGui.SetNextItemWidth(SetupDropdownWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##waymarks", ref _selectedWaymark, labels, labels.Length))
        {
            _waymarkMemory[_selectedScenario.Phase.Zone] = _selectedWaymark;
            if (plugin.Game.World.Map.IsInInstance)
                plugin.Game.World.PlaceWaymarks(presets[_selectedWaymark].Markers);
        }
    }

    private void DrawRoleSelector(bool mpConnected)
    {
        var idx = _roleOverride is { } role ? (int)role + 1 : 0;
        SettingsGrid.Row("Role:");
        ImGui.SetNextItemWidth(SetupDropdownWidth * ImGuiHelpers.GlobalScale);
        ImGui.BeginDisabled(mpConnected);
        if (ImGui.Combo("##role", ref idx, RoleLabels, RoleLabels.Length))
            _roleOverride = idx == 0 ? null : (PartyRole)(idx - 1);
        ImGui.EndDisabled();
        if (mpConnected && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Role is claimed via the Multiplayer window instead. " + MpDisabledReason(false, true));
    }

    // Only meaningful when a scenario offers more than one strat; hidden otherwise.
    // When the scenario declares StratGroups, a region-button row is drawn above the
    // dropdown and the dropdown is filtered to the selected region.
    private void DrawStratSelector(bool mpGuest)
    {
        if (_selectedScenario is null) return;
        var strats = _selectedScenario.AiStrats;
        var groups = StratGroups(_selectedScenario);
        if (groups.Count == 0 && strats.Count <= 1) return;
        ImGui.BeginDisabled(mpGuest);
        if (groups.Count > 0)
            DrawGroupedStratSelector(strats, groups);
        else
        {
            var labels = new string[strats.Count];
            for (var i = 0; i < strats.Count; i++) labels[i] = strats[i].Name;
            SettingsGrid.Row("Strategy:");
            ImGui.SetNextItemWidth(SetupDropdownWidth * ImGuiHelpers.GlobalScale);
            ImGui.Combo("##strat", ref _selectedStrat, labels, labels.Length);
        }
        ImGui.EndDisabled();
        if (mpGuest && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Only the host's selection is used in multiplayer. " + MpDisabledReason(false, true));
    }

    // Region buttons + a region-filtered strat dropdown, over the pick ReconcileStrat keeps valid.
    private void DrawGroupedStratSelector(IReadOnlyList<IScenarioAi> strats, IReadOnlyList<string> groups)
    {
        SettingsGrid.Row("Region:");
        for (var i = 0; i < groups.Count; i++)
        {
            if (i > 0) ImGui.SameLine();
            var group = groups[i];
            var selected = _selectedStratGroup == group;
            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            ImGui.PushID($"region{i}");
            if (ImGui.Button(group))
            {
                _selectedStratGroup = group;
                _stratGroupMemory[_selectedScenario!] = group; // remember across scenario switches
                ReconcileStrat();
            }
            ImGui.PopID();
            if (selected) ImGui.PopStyleColor();
        }

        var filtered = RegionStrats(strats);
        SettingsGrid.Row("Strategy:");
        if (filtered.Count == 0)
        {
            ImGui.TextDisabled("(no strats for this region yet)");
            return;
        }

        var localIdx = filtered.IndexOf(_selectedStrat);
        var labels = new string[filtered.Count];
        for (var i = 0; i < filtered.Count; i++) labels[i] = strats[filtered[i]].Name;
        ImGui.SetNextItemWidth(SetupDropdownWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##strat", ref localIdx, labels, labels.Length))
            _selectedStrat = filtered[localIdx];
    }

    // Grouped scenarios need a real strat in the active region. Also the host's pre-broadcast
    // check in MultiplayerManager.StartScenario.
    internal bool HasStartableStrat()
    {
        if (_selectedScenario is not { } scenario) return false;
        if (StratGroups(scenario).Count == 0) return true;
        var strats = scenario.AiStrats;
        return _selectedStrat >= 0 && _selectedStrat < strats.Count
            && strats[_selectedStrat].Group == _selectedStratGroup;
    }

    private static bool GroupsContain(IReadOnlyList<string> groups, string? group)
    {
        if (group is null) return false;
        for (var i = 0; i < groups.Count; i++)
            if (groups[i] == group) return true;
        return false;
    }

    private void DrawLocationHint()
    {
        if (ZoneSession.IsInInn()) return;
        ImGui.TextDisabled("Scenarios only run in an inn");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Scenarios can only be started from an inn — return to one to run a scenario.");
    }
}
