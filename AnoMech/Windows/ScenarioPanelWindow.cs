using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

internal sealed unsafe class ScenarioPanelWindow : Window
{
    private readonly MainWindow mainWindow;

    internal bool RequestedOpen { get; private set; } = true;
    internal float NaturalHeight { get; private set; }

    internal ScenarioPanelWindow(MainWindow mainWindow)
        : base("###AnoMechScenarioPanel")
    {
        this.mainWindow = mainWindow;
        Flags = ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoBringToFrontOnFocus
            | ImGuiWindowFlags.NoNavFocus
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
        DisableWindowSounds = true;
    }

    internal void ToggleRequested() => RequestedOpen = !RequestedOpen;

    internal void Close() => RequestedOpen = false;

    public override void PreOpenCheck()
    {
        IsOpen = RequestedOpen && mainWindow.IsOpen && !mainWindow.IsActuallyCollapsed
                 && !mainWindow.ShowingMultiplayerSetup && !mainWindow.ShowingOfflineSetup;
    }

    public override void PreDraw()
    {
        var style = ImGui.GetStyle();
        var background = *ImGui.GetStyleColorVec4(ImGuiCol.WindowBg);
        var panelTint = *ImGui.GetStyleColorVec4(ImGuiCol.Header);
        var panelBackground = Vector4.Lerp(background, panelTint, 0.10f);
        panelBackground.W = background.W;

        var border = Vector4.Lerp(
            *ImGui.GetStyleColorVec4(ImGuiCol.Border),
            *ImGui.GetStyleColorVec4(ImGuiCol.ButtonHovered),
            0.12f);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, panelBackground);
        ImGui.PushStyleColor(ImGuiCol.Border, border);
        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowBorderSize,
            MathF.Max(style.WindowBorderSize, 1f * ImGuiHelpers.GlobalScale));

        var width = mainWindow.ScenarioPanelWindowWidth();
        var height = MathF.Max(mainWindow.ScenarioPanelHeight, NaturalHeight);
        var logicalWidth = width / ImGuiHelpers.GlobalScale;
        var logicalHeight = height / ImGuiHelpers.GlobalScale;
        var anchor = mainWindow.ScenarioPanelAnchor;
        Position = new Vector2(anchor.X - width + ImGui.GetStyle().WindowBorderSize, anchor.Y);
        PositionCondition = ImGuiCond.Always;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(logicalWidth, logicalHeight),
            MaximumSize = new Vector2(logicalWidth, logicalHeight)
        };
    }

    public override void Draw()
    {
        mainWindow.DrawScenariosPanel();
        NaturalHeight = ImGui.GetCursorPosY() + ImGui.GetStyle().WindowPadding.Y;
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
    }
}
