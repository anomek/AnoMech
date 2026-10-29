using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using AnoMech.Core.Map;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using SessionStatus = AnoMech.Core.Game.SessionStatus;
using PracticeLoop = AnoMech.Core.Game.PracticeLoop;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using static AnoMech.Core.Game.Game;
using AnoMech.Core.Native.Implementations;

namespace AnoMech.Windows;

public unsafe class MainWindow : Window, IDisposable
{
    private const float ScenarioButtonExtraPadding = 6f;
    private const float SetupDropdownWidth = 180f;

    internal static readonly Vector4 StartColor = new(0.18f, 0.40f, 0.24f, 0.92f);
    internal static readonly Vector4 StopColor = new(0.45f, 0.14f, 0.16f, 0.92f);
    // A live run's buttons: restarting or stopping it throws the attempt away, so neither leads.
    private static readonly Vector4 RestartLiveColor = new(0.26f, 0.36f, 0.29f, 0.80f);
    private static readonly Vector4 StopRunColor = new(0.30f, 0.30f, 0.33f, 0.92f);
    private static readonly Vector4 RunningColor = new(0.35f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 MistakeColor = new(1f, 0.65f, 0.25f, 1f);
    private static readonly Vector4 FailedColor = new(1f, 0.38f, 0.35f, 1f);
    // Fixed rather than theme-derived: Dalamud's default ButtonActive is red, which reads as
    // "stop"/error next to the semantic Start/Stop buttons.
    private static readonly Vector4 SelectionColor = new(0.30f, 0.55f, 0.85f, 1f);
    private static readonly Vector4 SelectionHoveredColor = new(0.28f, 0.50f, 0.78f, 0.92f);
    private static readonly Vector4 SelectionActiveColor = new(0.22f, 0.42f, 0.68f, 1f);
    private static readonly Vector4 HoverColor = new(0.24f, 0.39f, 0.60f, 0.88f);

    private readonly Plugin plugin;
    private readonly TitleBarButton autoCollapseButton;
    private IZone? _openZone;
    internal ScenarioPanelWindow ScenarioPanel { get; }
    internal PartyPanelWindow PartyPanel { get; }
    private readonly MultiplayerUi multiplayerUi;
    internal Vector2 ScenarioPanelAnchor { get; private set; }
    internal Vector2 RightPanelAnchor { get; private set; }
    internal float ScenarioPanelHeight { get; private set; }
    internal bool IsActuallyCollapsed { get; private set; }
    private float _windowChromeHeight;
    internal IScenario? SelectedScenario => _selectedScenario;
    private IScenario? _selectedScenario;

    internal ScenarioSelection? Selection => _selectedScenario is { } scenario
        ? new ScenarioSelection(scenario, HasStartableStrat() ? _selectedStrat : null, _selectedWaymark, SelectedSeed, _roleOverride)
        : null;

    private PartyRole? _roleOverride;
    private bool _soloMode;

    // Index into the selected scenario's AiStrats; reset to the first strat whenever the
    // selected scenario changes. -1 when a grouped scenario's selected region has no strats
    // (Start is then gated off).
    private int _selectedStrat;

    // Index into the selected zone's WaymarkPresets. Remembered per zone so switching between
    // scenarios in the same encounter keeps the chosen layout.
    private int _selectedWaymark;
    private readonly Dictionary<IZone, int> _waymarkMemory = new();

    // Seed for the next Start: empty rolls a fresh one each time, a number replays it every time.
#if DEBUG
    private int? SelectedSeed => int.TryParse(_seedText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed) ? seed : null;
    private string _seedText = "";
#else
    private int? SelectedSeed => null;
#endif

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

    // <Version> from AnoMech.csproj flows into the assembly version; surface it in the
    // title bar. Use a ### id so the window identity stays "MainWindow" across versions.
    private static readonly string TitleText = TitleWithVersion();

    private static string TitleWithVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        var version = v is null ? "" : $" v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        return $"AnoMech{version}";
    }

    // The title bar still shows while the window is collapsed for the run.
    private static string Title(bool failed) => $"{TitleText}{(failed ? " — Failed" : "")}###MainWindow";

    public MainWindow(Plugin plugin)
        : base(Title(failed: false))
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
        PartyPanel = new PartyPanelWindow(this, Plugin.Multiplayer);
        multiplayerUi = new MultiplayerUi(plugin);
        IsOpen = false;
        RestoreSelectedScenario();

        autoCollapseButton = new TitleBarButton
        {
            Icon = FontAwesomeIcon.CompressAlt,
            IconOffset = new Vector2(1f, 1f),
            Priority = 2,
            Click = _ =>
            {
                Plugin.Config.AutoCollapseWhileRunning = !Plugin.Config.AutoCollapseWhileRunning;
                Plugin.Config.Save();
            },
            ShowTooltip = () => ImGui.SetTooltip(
                $"Auto-collapse while running: {(Plugin.Config.AutoCollapseWhileRunning ? "On" : "Off")}"),
        };
        TitleBarButtons.Add(autoCollapseButton);

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

    private bool _wasInInstance;
    private bool _wasScenarioActive;
    private SessionStatus _lastStatus;
    private bool _clearCollapsedRequest;

    // In a session the run counts from the start handshake, before Game has one.
    private bool RunActive => Plugin.Multiplayer.SessionCode != null ? Plugin.Multiplayer.IsRunning : Plugin.Game.Status >= SessionStatus.Running;

    private bool InSession => Plugin.Multiplayer.InSession;
    private bool IsGuest => Plugin.Multiplayer.SessionCode != null && !Plugin.Multiplayer.IsHost;

    // The whole window becomes the multiplayer setup screen until a session starts.
    private bool _showMultiplayerSetup;
    internal bool ShowingMultiplayerSetup => _showMultiplayerSetup && !InSession;
    // At the title screen the whole window is offline mode's setup screen.
    internal bool ShowingOfflineSetup => plugin.Offline is { ShowsSetup: true };
    private bool _wasInSession;

    internal void OpenMultiplayer()
    {
        IsOpen = true;
        if (!InSession) _showMultiplayerSetup = true;
    }

    // A session that ended on someone else's terms reopens the setup screen, where its reason
    // is shown; leaving on our own returns to the scenario view.
    private void TrackSessionEnd()
    {
        var inSession = InSession;
        if (inSession) _showMultiplayerSetup = false;
        else if (_wasInSession && Plugin.Multiplayer.SessionEndReason != null) _showMultiplayerSetup = true;
        _wasInSession = inSession;
    }

    // A guest runs whatever the host picked, so its window shows that scenario.
    private void FollowHostScenario()
    {
        if (!IsGuest || Plugin.Multiplayer.TryResolveScenario() is not { } hostScenario) return;
        if (_selectedScenario != hostScenario) SelectScenario(hostScenario, persist: false);
    }

    // The window is collapsible while a scenario runs, and expands again when the run ends.
    // Outside a run, fake-zone sessions keep it expanded so the next action is visible.
    public override void PreOpenCheck()
    {
        TrackSessionEnd();
        FollowHostScenario();
        if (_clearCollapsedRequest)
        {
            Collapsed = null;
            CollapsedCondition = ImGuiCond.None;
            _clearCollapsedRequest = false;
        }

        var scenarioActive = RunActive;
        var status = Plugin.Game.Status;
        WindowName = Title(Plugin.Game.LastRun is { } lastRun && IsFailed(lastRun.Status));
        if (scenarioActive && !_wasScenarioActive)
        {
            if (Plugin.Config.AutoCollapseWhileRunning)
                RequestCollapsed(true);
        }
        // Not on a failure the run plays on from: expanding mid-fight gets in the way.
        if (status >= SessionStatus.Frozen && status != _lastStatus)
        {
            RequestCollapsed(false);
        }
        else if (!scenarioActive && _wasScenarioActive)
        {
            RequestCollapsed(false);
        }

        var inInstance = Plugin.Game.World.Map.IsInInstance;
        if (inInstance)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            if (scenarioActive)
                Flags &= ~ImGuiWindowFlags.NoCollapse;
            else
                Flags |= ImGuiWindowFlags.NoCollapse;
            if (!_wasInInstance)
            {
                ScenarioPanel.Close();
                if (!scenarioActive)
                    RequestCollapsed(false);
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

        _wasScenarioActive = scenarioActive;
        _lastStatus = status;
        _wasInInstance = inInstance;
    }

    private void RequestCollapsed(bool collapsed)
    {
        Collapsed = collapsed;
        CollapsedCondition = ImGuiCond.Always;
        _clearCollapsedRequest = true;
    }

    public override void PreDraw()
    {
        autoCollapseButton.IconColor = Plugin.Config.AutoCollapseWhileRunning
            ? StyleColor(ImGuiCol.Text)
            : StyleColor(ImGuiCol.TextDisabled);

        var uiScale = ImGuiHelpers.GlobalScale;
        var minimumHeight = 80f;
        if (ScenarioPanel.RequestedOpen && ScenarioPanel.NaturalHeight > 0f && !ShowingOfflineSetup)
            minimumHeight = Math.Max(minimumHeight, (_windowChromeHeight + ScenarioPanel.NaturalHeight) / uiScale);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220f, minimumHeight),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void PostDraw()
    {
        var window = ImGuiP.FindWindowByName(WindowName);
        IsActuallyCollapsed = !window.IsNull && window.Collapsed;
    }

    public override void Draw()
    {
        var windowPos = ImGui.GetWindowPos();
        var contentTop = windowPos.Y + ImGui.GetFrameHeight();
        ScenarioPanelAnchor = new Vector2(windowPos.X, contentTop);
        _windowChromeHeight = contentTop - windowPos.Y;
        ScenarioPanelHeight = windowPos.Y + ImGui.GetWindowSize().Y - contentTop;
        RightPanelAnchor = new Vector2(windowPos.X + ImGui.GetWindowSize().X, contentTop);
        if (plugin.Offline is { ShowsSetup: true } offline)
        {
            offline.Panel.DrawSetup();
            return;
        }
        plugin.Offline?.Panel.DrawStrip();
        if (ShowingMultiplayerSetup)
            multiplayerUi.DrawSetupScreen(() => _showMultiplayerSetup = false);
        else
            DrawMainContent();
    }

    // Size the left panel to the widest scenario label so names never clip as scenarios are added.
    internal float ScenarioPanelWindowWidth()
    {
        var style = ImGui.GetStyle();
        var widest = 0f;
        foreach (var zone in Plugin.Game.Catalog.Zones)
        {
            widest = Math.Max(widest, ImGui.CalcTextSize(zone.Name).X + style.IndentSpacing);
            foreach (var phase in Plugin.Game.Catalog.PhasesOf(zone))
                foreach (var scenario in Plugin.Game.Catalog.ScenariosOf(phase))
                    widest = Math.Max(widest, ImGui.CalcTextSize(scenario.DisplayName()).X + style.IndentSpacing);
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
        // Switching scenarios inside a loaded zone skips the zone reload, so another zone's
        // scenario would run on the wrong territory.
        var lockedZone = Plugin.Game.World.Map.IsInInstance ? _selectedScenario?.Phase.Zone : null;
        ImGui.TextUnformatted(lockedZone?.Name ?? "Scenarios");
        ImGui.Separator();

        if (lockedZone != null)
        {
            var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
            DrawZoneScenarios(lockedZone, right);
            return;
        }
        foreach (var zone in Plugin.Game.Catalog.Zones)
        {
            var shouldOpen = _openZone == zone;
            ImGui.SetNextItemOpen(shouldOpen, ImGuiCond.Always);
            var containsSelection = ZoneContains(zone, _selectedScenario);
            var headerColor = BlendColor(
                StyleColor(ImGuiCol.WindowBg),
                StyleColor(ImGuiCol.Header),
                0.72f);
            ImGui.PushStyleColor(ImGuiCol.Header,
                containsSelection ? BlendColor(headerColor, SelectionColor, 0.58f) : headerColor);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, containsSelection ? SelectionHoveredColor : HoverColor);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, SelectionActiveColor);
            var open = ImGui.CollapsingHeader($"{zone.Name}###scenario-zone-{zone.GetType().FullName}");
            ImGui.PopStyleColor(3);
            if (containsSelection) DrawSelectionAccent();
            var sectionRight = ImGui.GetItemRectMax().X;
            if (open != shouldOpen) _openZone = open ? zone : null;
            if (!open) continue;
            ImGui.Indent();
            DrawZoneScenarios(zone, sectionRight);
            ImGui.Unindent();
        }
    }

    private void DrawZoneScenarios(IZone zone, float sectionRight)
    {
        var inSession = InSession;
        var guest = IsGuest;
        var buttonPadding = ImGui.GetStyle().FramePadding;
        buttonPadding.X += ScenarioButtonExtraPadding * ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, buttonPadding);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0f, 0.5f));
        foreach (var phase in Plugin.Game.Catalog.PhasesOf(zone))
            foreach (var scenario in Plugin.Game.Catalog.ScenariosOf(phase))
            {
                var selected = _selectedScenario == scenario;
                var mpUnsupported = inSession && !scenario.SupportsMultiplayer;
                if (selected) PushSelectedScenarioStyle();
                else PushScenarioHoverStyle();
                // Zone-qualified: two zones can hold same-named scenarios (UMAD and UCOB
                // both have a P5 "Exaflares"), and a shared ImGui id makes the second
                // button unclickable.
                ImGui.PushID(scenario.FullName());
                var buttonWidth = sectionRight - ImGui.GetCursorScreenPos().X;
                ImGui.BeginDisabled(mpUnsupported || guest);
                var clicked = ImGui.Button(scenario.DisplayName(), new Vector2(buttonWidth, 0));
                ImGui.EndDisabled();
                if ((mpUnsupported || guest) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(guest ? "The host picks the scenario." : "This scenario doesn't support multiplayer.");
                if (selected)
                {
                    DrawSelectionAccent();
                    ImGui.PopStyleColor(3);
                }
                else
                {
                    ImGui.PopStyleColor(2);
                }
                ImGui.PopID();
                if (clicked) SelectScenario(scenario);
            }
        ImGui.PopStyleVar(2);
    }

    private void RestoreSelectedScenario()
    {
        if (string.IsNullOrEmpty(Plugin.Config.LastSelectedScenario)) return;
        foreach (var scenario in Plugin.Game.Catalog.Scenarios)
        {
            if (!string.Equals(
                    scenario.FullName(),
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
            Plugin.Config.LastSelectedScenario = scenario.FullName();
            Plugin.Config.Save();
        }
    }

    private const string MpDisabledReason = "Disabled in a multiplayer session.";

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
        var inSession = InSession;
        if (_selectedScenario == null)
        {
            DrawScenarioPanelToggle();
            ImGui.SameLine();
            ImGui.TextDisabled("Select a scenario");
            if (inSession) DrawSession();
            else DrawMultiplayerButton();
            return;
        }

        var game = Plugin.Game;
        var mpGuest = IsGuest;
        ReconcileSetup(inSession, mpGuest);
#if DEBUG
        if (inSession) game.EventTimeScale = 1f;
#endif

        DrawScenarioHeader(game);
        if (inSession) DrawSession();
        DrawPrimaryActions(game, inSession);
        if (!inSession) DrawSoloOption(game);
        DrawLocationHint();
        DrawRunOptions(game, Plugin.Practice, inSession);

        ImGui.Spacing();
        DrawSections(_selectedScenario, inSession, mpGuest);
    }

    // Every frame, Setup expanded or not: both Start buttons read these.
    // In a session the role comes from the Party panel seat, and only the host's region/strat
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

#if DEBUG
    private void DrawSeedControl()
    {
        ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("Seed##run-seed", "random", ref _seedText, 16, ImGuiInputTextFlags.CharsDecimal);
        if (_seedText.Length > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear##run-seed")) _seedText = "";
        }

        // The world keeps its last run's Rng until the next start reseeds it.
        var lastText = Plugin.Game.World.Rng.Seed.ToString(CultureInfo.InvariantCulture);
        ImGui.TextUnformatted($"Last seed: {lastText}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Reuse##last-seed")) _seedText = lastText;
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy##last-seed")) ImGui.SetClipboardText(lastText);
        ImGui.Separator();
    }

    // In a session MultiplayerManager owns DebugBotControl.Enabled per run, so route through its
    // sticky toggle (as the Party panel does); solo nothing else writes the flag.
    private void DrawBotControlsPlayer(bool inSession)
    {
        var mp = Plugin.Multiplayer;
        if (inSession)
        {
            var botControlled = mp.DebugBotControlled;
            ImGui.BeginDisabled(mp.Session.Started);
            if (ImGui.Checkbox("Bot controls player##bot-controls-player", ref botControlled))
                mp.SetDebugBotControlled(botControlled);
            ImGui.EndDisabled();
        }
        else
        {
            var enabled = DebugBotControl.Enabled;
            if (ImGui.Checkbox("Bot controls player##bot-controls-player", ref enabled))
                DebugBotControl.Enabled = enabled;
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(inSession
                ? "Your seat is driven by the strat's AI. Locked once the fight starts."
                : "Your character is driven by the strat's AI, like a bot in your role.");
        ImGui.Separator();
    }
#endif

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

    // Mirrors the visibility rules of DrawStratSelector and DrawWaymarkSelector.
    private static bool HasStratChoice(IScenario scenario) =>
        StratGroups(scenario).Count > 0 || scenario.AiStrats.Count > 1;

    private static bool HasWaymarkChoice(IScenario scenario) =>
        scenario.Phase.Zone.WaymarkPresets.Count > 1;

    private void DrawSections(IScenario scenario, bool inSession, bool mpGuest)
    {
        // In a session the seat comes from the Party panel, so Setup can be left with nothing.
        var hasSetup = !inSession || HasStratChoice(scenario) || HasWaymarkChoice(scenario);
        if (hasSetup)
        {
            if (ImGui.TreeNodeEx("Setup###scenario-setup-v3",
                    ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.FramePadding))
            {
                if (SettingsGrid.Begin("##scenario-setup-grid"))
                {
                    if (!inSession) DrawRoleSelector();
                    DrawStratSelector(mpGuest);
                    DrawWaymarkSelector();
                    SettingsGrid.End();
                }
                ImGui.TreePop();
            }
            ImGui.Spacing();
        }

        if (ImGui.TreeNodeEx("Scenario settings###scenario-config-v3",
                ImGuiTreeNodeFlags.FramePadding))
        {
            if (inSession)
                multiplayerUi.DrawScenarioSettings(scenario);
            else
            {
                scenario.DrawSettings();
                scenario.DrawMultiplayerSettings();
            }
            ImGui.TreePop();
        }

#if DEBUG
        ImGui.Spacing();
        if (ImGui.TreeNodeEx("Debug###debug-v3",
                ImGuiTreeNodeFlags.FramePadding))
        {
            DrawSeedControl();
            DrawBotControlsPlayer(inSession);
            ImGui.BeginDisabled(inSession);
            ImGui.BeginGroup();
            debugMenu.DrawSpeedControl();
            ImGui.EndGroup();
            ImGui.EndDisabled();
            if (inSession && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(MpDisabledReason);
            debugMenu.DrawDebugContent();
            ImGui.TreePop();
        }
#endif
    }

    private void DrawScenarioHeader(AnoMech.Core.Game.Game game)
    {
        var uiScale = ImGuiHelpers.GlobalScale;
        var contentStart = ImGui.GetCursorScreenPos();
        var scenario = _selectedScenario!;

        DrawScenarioPanelToggle();
        ImGui.SameLine();
        ImGui.TextDisabled($"{scenario.Phase.Zone.Name} —");
        ImGui.SameLine(0f, 4f * uiScale);
        ImGui.TextUnformatted(scenario.DisplayName());
        DrawRunStatus(game);

        var dividerColor = StyleColor(ImGuiCol.TextDisabled);
        dividerColor.W *= 0.35f;
        var dividerY = contentStart.Y + ImGui.GetFrameHeight();
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(ImGui.GetWindowPos().X, dividerY),
            new Vector2(ImGui.GetWindowPos().X + ImGui.GetWindowSize().X, dividerY),
            ImGui.GetColorU32(dividerColor),
            1f * uiScale);
        ImGui.SetCursorScreenPos(new Vector2(
            contentStart.X,
            dividerY + ImGui.GetStyle().ItemSpacing.Y));
    }

    // Left-aligned after the name: a right-aligned widget isn't always given the room to draw.
    private void DrawRunStatus(AnoMech.Core.Game.Game game)
    {
        var (label, color) = RunStatus(game);
        ImGui.SameLine(0f, 8f * ImGuiHelpers.GlobalScale);
        ImGui.TextColored(color, $"· {label}");
        if (game.LastRun is not { Issues.Count: > 0 } run || !ImGui.IsItemHovered()) return;
        ImGui.BeginTooltip();
        foreach (var issue in run.Issues)
            ImGui.TextUnformatted($"{issue.Time,6:F1}s  {issue.Description}");
        ImGui.EndTooltip();
    }

    // The live run's state, else how the selected scenario's last run ended.
    private (string Label, Vector4 Color) RunStatus(AnoMech.Core.Game.Game game)
    {
        var idle = ("Idle", StyleColor(ImGuiCol.TextDisabled));
        if (game.LastRun is not { } run) return RunActive ? ("Running", RunningColor) : idle;
        if (run.Ended && run.Scenario != _selectedScenario) return idle;
        var status = run.Status;
        if (IsFailed(status)) return ("Failed", FailedColor);
        return status switch
        {
            SessionStatus.Succeeded => ("Cleared", RunningColor),
            SessionStatus.CompletedWithMistake => ("Cleared with mistakes", MistakeColor),
            _ when run.Ended => ("Stopped", StyleColor(ImGuiCol.TextDisabled)),
            SessionStatus.RunningWithMistake => ("Running with mistakes", MistakeColor),
            _ => ("Running", RunningColor),
        };
    }

    private static bool IsFailed(SessionStatus status)
        => status is SessionStatus.Failed or SessionStatus.Frozen or SessionStatus.CompletedFailed;

    // A run still playing, as opposed to one frozen or finished and only left standing.
    private static bool RunLive(AnoMech.Core.Game.Game game)
        => game.Status is SessionStatus.Running or SessionStatus.RunningWithMistake or SessionStatus.Failed;

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

    private void DrawPrimaryActions(AnoMech.Core.Game.Game game, bool inSession)
    {
        var uiScale = ImGuiHelpers.GlobalScale;
        var actionSize = new Vector2(140f * uiScale, 32f * uiScale);
        if (!inSession)
            DrawStartButton(game, actionSize);
        else if (RunActive)
        {
            if (DrawSemanticButton("Stop", actionSize, StopColor))
                plugin.Runs.Reset();
        }
        else
            multiplayerUi.DrawStartButton(actionSize, Selection);

        if (!inSession && RunLive(game))
        {
            ImGui.SameLine();
            if (DrawSemanticButton("Stop###stop", actionSize, StopRunColor))
                plugin.Runs.Reset();
        }
        else if (game.World.Map.IsInInstance)
        {
            ImGui.SameLine();
            if (DrawSemanticButton(inSession ? "Return to inn###leave-instance" : "Leave###leave-instance", actionSize, StopColor))
                plugin.Runs.Leave();
            if (inSession && ImGui.IsItemHovered())
                ImGui.SetTooltip("Takes the whole party back to the inn. Everyone stays in the session.");
        }
        else if (!inSession && !RunActive)
        {
            DrawMultiplayerButton(actionSize);
        }
    }

    private void DrawSession()
    {
        multiplayerUi.DrawSessionStrip(PartyPanel);
        multiplayerUi.DrawSessionMessages(Selection);
    }

    // Sessions start from the inn, like any scenario.
    private void DrawMultiplayerButton(Vector2 size = default)
    {
#if DEBUG
        if (!ZoneControl.IsInInn() || Plugin.Game.World.Map.IsInInstance) return;
        ImGui.SameLine();
        if (ImGui.Button("Multiplayer", size)) _showMultiplayerSetup = true;
#endif
    }

    private bool SoloSelected => _selectedScenario is { SupportsSolo: true } && _soloMode;

    // A start replaces whatever run is standing, so it needs no Stop first.
    private void DrawStartButton(AnoMech.Core.Game.Game game, Vector2 size)
    {
        var solo = SoloSelected;
        var selection = Selection;
        var refusal = plugin.Runs.StartRefusal(selection, solo);
        var restart = game.ActiveScenario is { } active && active == _selectedScenario;
        var label = plugin.Runs.StartWaitingOn != null ? "Waiting to start..." : restart ? "Restart" : "Start";
        ImGui.BeginDisabled(refusal != null);
        if (DrawSemanticButton($"{label}###start", size, restart && RunLive(game) ? RestartLiveColor : StartColor))
            plugin.Runs.Start(selection, solo);
        ImGui.EndDisabled();
        if (refusal != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(refusal);
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
        ImGui.PushStyleColor(ImGuiCol.Button, BlendColor(StyleColor(ImGuiCol.Button), SelectionColor, 0.78f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, SelectionHoveredColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, SelectionActiveColor);
    }

    private static void PushScenarioHoverStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, HoverColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, SelectionActiveColor);
    }

    private static void DrawSelectionAccent()
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var accentWidth = 3f * ImGuiHelpers.GlobalScale;
        ImGui.GetWindowDrawList().AddRectFilled(
            min,
            new Vector2(min.X + accentWidth, max.Y),
            ImGui.GetColorU32(SelectionColor));
    }

    private bool ZoneContains(IZone zone, IScenario? scenario) =>
        scenario != null && Plugin.Game.Catalog.PhasesOf(zone).Any(p => Plugin.Game.Catalog.ScenariosOf(p).Contains(scenario));

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
        var running = RunLive(game);
        ImGui.BeginDisabled(running);
        ImGui.Checkbox("Solo", ref _soloMode);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(running
                ? "Stop the current scenario before changing the run mode."
                : "Run without simulated party members or party AI.");
    }

    // God mode and auto-restart are disabled while a session is being set up or is live; forced
    // off, not just disabled, so a stale value can't apply.
    private static void DrawRunOptions(AnoMech.Core.Game.Game game, PracticeLoop practice, bool mpActive)
    {
        ImGui.Spacing();
        if (mpActive) game.GodMode = false;
        ImGui.BeginDisabled(mpActive);
        var god = game.GodMode;
        if (ImGui.Checkbox("God mode", ref god)) game.GodMode = god;
        ImGui.EndDisabled();
        if (mpActive && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(MpDisabledReason);
        ImGui.SameLine();
        // A host rerunning on its own would desync the session, so this is solo-only.
        if (mpActive) practice.AutoRestart = false;
        ImGui.BeginDisabled(mpActive);
        var autoRestart = practice.AutoRestart;
        if (ImGui.Checkbox("Auto-restart", ref autoRestart)) practice.AutoRestart = autoRestart;
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(mpActive
                ? MpDisabledReason
                : "Restart the same scenario immediately after a successful run. A failed run turns this back off.");
        ImGui.SameLine();
        ImGui.TextDisabled($"Streak: {practice.Streak}");
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
            if (Plugin.Game.World.Map.IsInInstance)
                Plugin.Game.World.PlaceWaymarks(presets[_selectedWaymark].Markers);
        }
    }

    private void DrawRoleSelector()
    {
        var idx = _roleOverride is { } role ? (int)role + 1 : 0;
        SettingsGrid.Row("Role:");
        ImGui.SetNextItemWidth(SetupDropdownWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##role", ref idx, RoleLabels, RoleLabels.Length))
            _roleOverride = idx == 0 ? null : (PartyRole)(idx - 1);
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
            ImGui.SetTooltip("Only the host's selection is used in multiplayer.");
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

    // Grouped scenarios need a real strat in the active region.
    private bool HasStartableStrat()
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
        if (ZoneControl.IsInInn()) return;
        ImGui.TextDisabled("Scenarios only run in an inn");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Scenarios can only be started from an inn — return to one to run a scenario.");
    }
}
