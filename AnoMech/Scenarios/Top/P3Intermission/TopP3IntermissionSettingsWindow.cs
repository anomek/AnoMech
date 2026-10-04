using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P3Intermission;

public sealed class TopP3IntermissionSettingsWindow
{
    public TopP3IntermissionStateOverrides Overrides { get; } = new();

    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] HandLabels = ["Random", "N / SE / SW", "NE / S / NW"];
    private static readonly string[] DebuffLabels = ["Random", "Spread", "Stack", "No debuff"];

    // Solo, the player's own pick sits in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.Hands = null;
            if (solo) Overrides.Debuff.Mine = null;
        }

        if (SettingsGrid.Begin("##p3intermission"))
        {
            SettingsGrid.Row("First hands:");
            var hands = Overrides.Hands is { } h ? (int)h + 1 : 0;
            SettingsGrid.ItemWidth(140);
            if (ImGui.Combo("##p3ihands", ref hands, HandLabels, HandLabels.Length))
                Overrides.Hands = hands == 0 ? null : (FirstHands)(hands - 1);

            if (solo) DrawPlayerRows();
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.Debuff.Clear();
        if (SettingsGrid.Begin("##p3intermissionplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##p3iseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Debuffs set:", Overrides.Debuff);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        SettingsGrid.PlayerRow("debuff:");
        var debuff = Overrides.Debuff.Effective(editingSeat) is { } d ? (int)d + 1 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##p3idebuff", ref debuff, DebuffLabels, DebuffLabels.Length))
            Overrides.Debuff.Set(editingSeat, debuff == 0 ? null : (SniperDebuff)(debuff - 1));
    }
}
