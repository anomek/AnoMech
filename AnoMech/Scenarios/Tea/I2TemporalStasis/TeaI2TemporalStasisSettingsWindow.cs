using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Tea.I2TemporalStasis;

public sealed class TeaI2TemporalStasisSettingsWindow
{
    public TeaI2TemporalStasisStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] SideLabels = ["Random", "West", "East"];
    private static readonly string[] DebuffLabels = ["Random", "Nothing", "Aggravated Assault", "House Arrest", "Restraining Order"];

    // Solo, the player's own row sits in this panel; a host assigns seats from the Multiplayer window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.BruteJusticeEast = null;
            if (solo) Overrides.Debuff.Mine = null;
        }

        if (!SettingsGrid.Begin("##teai2")) return;
        SettingsGrid.Row("Brute Justice on:");
        var side = Overrides.BruteJusticeEast is { } east ? east ? 2 : 1 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teai2side", ref side, SideLabels, SideLabels.Length))
            Overrides.BruteJusticeEast = side == 0 ? null : side == 2;

        if (solo) DrawPlayerRows();
        SettingsGrid.End();
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.Debuff.Clear();
        if (SettingsGrid.Begin("##teai2players"))
        {
            editingSeat = SettingsGrid.SeatRow("##teai2seat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Debuffs set:", Overrides.Debuff);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        SettingsGrid.Row(PerRole.SeatsActive ? "Debuff:" : "Your debuff:");
        var index = Overrides.Debuff.Effective(editingSeat) is { } debuff ? (int)debuff + 1 : 0;
        SettingsGrid.ItemWidth(180);
        if (ImGui.Combo("##teai2debuff", ref index, DebuffLabels, DebuffLabels.Length))
            Overrides.Debuff.Set(editingSeat, index == 0 ? null : (StasisDebuff)(index - 1));
    }
}
