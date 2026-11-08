using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

// While the main window is collapsed for a run: the party's latest changes, docked under its title
// bar, since the game itself doesn't show them until the run ends. Clicks go through to the game,
// except while an alert offers the way back to the inn.
internal sealed class PartyStripWindow : Window
{
    private const int ChangesShown = 3;
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.AlwaysAutoResize;

    private readonly MainWindow mainWindow;

    internal PartyStripWindow(MainWindow mainWindow)
        : base("###AnoMechPartyStrip")
    {
        this.mainWindow = mainWindow;
        Flags = BaseFlags | ImGuiWindowFlags.NoInputs;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
        DisableWindowSounds = true;
    }

    public override void PreOpenCheck()
    {
        IsOpen = mainWindow.IsOpen && mainWindow.IsActuallyCollapsed
                 && (MainWindow.PartyChanges().Count > 0 || MainWindow.HasPartyAlert());
    }

    public override void PreDraw()
    {
        Flags = MainWindow.HasPartyAlert() ? BaseFlags : BaseFlags | ImGuiWindowFlags.NoInputs;
        Position = mainWindow.TitleBarBottomLeft;
        PositionCondition = ImGuiCond.Always;
        var width = mainWindow.TitleBarWidth / ImGuiHelpers.GlobalScale;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(width, 0f),
            MaximumSize = new Vector2(width, float.MaxValue),
        };
    }

    public override void Draw()
    {
        if (MainWindow.HasPartyAlert())
        {
            MainWindow.DrawPartyAlerts();
            MainWindow.PushSemanticColors(MainWindow.StopColor);
            if (ImGui.Button("Return to inn###party-strip-leave", new Vector2(ImGui.GetContentRegionAvail().X, 0f)))
                mainWindow.ReturnToInn();
            MainWindow.PopSemanticColors();
        }
        ImGui.PushStyleColor(ImGuiCol.Text, MainWindow.PartyChangeColor);
        foreach (var change in MainWindow.PartyChanges().TakeLast(ChangesShown))
            ImGui.TextWrapped($"{change.At:HH:mm}  {change.Text}");
        ImGui.PopStyleColor();
    }
}
