using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P1ProgramLoop;

public sealed class TopP1ProgramLoopSettingsWindow
{
    public TopP1ProgramLoopStateOverrides Overrides { get; } = new();

    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] NumberLabels = ["Random", "1", "2", "3", "4"];

    // Solo, the player's own pick sits in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        if (PerRole.SeatsActive) return;
        if (ImGui.Button("Auto")) Overrides.Number.Mine = null;
        if (SettingsGrid.Begin("##p1programloop"))
        {
            DrawPlayerRows();
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.Number.Clear();
        if (SettingsGrid.Begin("##p1programloopplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##plseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("In Line set:", Overrides.Number);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var whose = PerRole.SeatsActive ? "" : "Your ";
        SettingsGrid.Row($"{whose}In Line:");
        var number = Overrides.Number.Effective(editingSeat) ?? 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##plnumber", ref number, NumberLabels, NumberLabels.Length))
            Overrides.Number.Set(editingSeat, number == 0 ? null : number);
    }
}
