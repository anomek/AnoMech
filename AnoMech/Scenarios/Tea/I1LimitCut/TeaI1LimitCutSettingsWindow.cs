using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Tea.I1LimitCut;

public sealed class TeaI1LimitCutSettingsWindow
{
    public TeaI1LimitCutStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] AxisLabels = ["Random", "N/S", "NE/SW", "E/W", "SE/NW"];
    private static readonly string[] NumberLabels = ["Random", "1", "2", "3", "4", "5", "6", "7", "8"];

    // Solo, the player's own number sits in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.FirstBlasterAxis = null;
            if (solo) Overrides.Number.Mine = null;
        }

        if (SettingsGrid.Begin("##tealimitcut"))
        {
            SettingsGrid.Row("First Hawk Blasters:");
            var axis = (Overrides.FirstBlasterAxis ?? -1) + 1;
            SettingsGrid.ItemWidth(140);
            if (ImGui.Combo("##tealcaxis", ref axis, AxisLabels, AxisLabels.Length))
                Overrides.FirstBlasterAxis = axis == 0 ? null : axis - 1;

            if (solo) DrawPlayerRows();
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.Number.Clear();
        if (SettingsGrid.Begin("##tealimitcutplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##tealcseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Numbers set:", Overrides.Number);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        SettingsGrid.Row($"{(PerRole.SeatsActive ? "" : "Your ")}number:");
        var number = Overrides.Number.Effective(editingSeat) ?? 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##tealcnumber", ref number, NumberLabels, NumberLabels.Length))
            Overrides.Number.Set(editingSeat, number == 0 ? null : number);
    }
}
