using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P3Monitors;

public sealed class TopP3MonitorsSettingsWindow
{
    public TopP3MonitorsStateOverrides Overrides { get; } = new();

    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] BossSideLabels = ["Random", "Left (west)", "Right (east)"];
    private static readonly string[] MonitorLabels = ["Random", "Yes", "No"];

    // Solo, the player's own pick sits in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.BossSide = null;
            if (solo) Overrides.Monitor.Mine = null;
        }

        if (SettingsGrid.Begin("##p3monitors"))
        {
            SettingsGrid.Row("Omega's monitor:");
            var side = Overrides.BossSide is { } cleave ? (int)cleave + 1 : 0;
            SettingsGrid.ItemWidth(140);
            if (ImGui.Combo("##mnboss", ref side, BossSideLabels, BossSideLabels.Length))
                Overrides.BossSide = side == 0 ? null : (MonitorCleave)(side - 1);

            if (solo) DrawPlayerRows();
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.Monitor.Clear();
        if (SettingsGrid.Begin("##p3monitorsplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##mnseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Monitors set:", Overrides.Monitor);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var whose = PerRole.SeatsActive ? "" : "Your ";
        SettingsGrid.Row($"{whose}monitor:");
        var monitor = Overrides.Monitor.Effective(editingSeat) switch { true => 1, false => 2, null => 0 };
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##mnmonitor", ref monitor, MonitorLabels, MonitorLabels.Length))
            Overrides.Monitor.Set(editingSeat, monitor switch { 1 => true, 2 => false, _ => null });
    }
}
