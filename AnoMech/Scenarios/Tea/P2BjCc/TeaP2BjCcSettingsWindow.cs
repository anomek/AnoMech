using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.P2BjCc;

public sealed class TeaP2BjCcSettingsWindow
{
    public TeaP2BjCcStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] NisiGroupLabels = ["Random", "Supports", "DPS"];
    private static readonly string[] HealerLabels = ["Random", "H1", "H2"];
    private static readonly string[] AxisLabels = ["Random", "N/S", "NE/SW", "E/W", "SE/NW"];
    private static readonly string[] TankLabels = ["Random", "MT", "OT"];
    private static readonly string[] LetterLabels = ["Random", "α (blue)", "β (orange)", "γ (purple)", "δ (green)"];
    private static readonly string[] YesNoLabels = ["Random", "Yes", "No"];

    // Solo, the player's own rows sit in this panel; a host assigns seats from the Multiplayer window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.NisiOnDps = null;
            Overrides.FirstWaterHealer = null;
            Overrides.ChakramAxis = null;
            Overrides.IceTank = null;
            if (solo)
            {
                Overrides.Nisi.Mine = null;
                Overrides.Judgment.Mine = null;
                Overrides.FirstLightning.Mine = null;
                Overrides.Enumeration.Mine = null;
            }
        }

        if (!SettingsGrid.Begin("##teap2")) return;
        SettingsGrid.Row("Judgment Nisi on:");
        var group = Overrides.NisiOnDps is { } dps ? dps ? 2 : 1 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap2nisigroup", ref group, NisiGroupLabels, NisiGroupLabels.Length))
            Overrides.NisiOnDps = group == 0 ? null : group == 2;

        SettingsGrid.Row("First Compressed Water:");
        var water = Overrides.FirstWaterHealer switch { PartyRole.RegenHealer => 1, PartyRole.ShieldHealer => 2, _ => 0 };
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap2water", ref water, HealerLabels, HealerLabels.Length))
            Overrides.FirstWaterHealer = water switch { 1 => PartyRole.RegenHealer, 2 => PartyRole.ShieldHealer, _ => null };

        SettingsGrid.Row("Steam Chakrams:");
        var axis = (Overrides.ChakramAxis ?? -1) + 1;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap2axis", ref axis, AxisLabels, AxisLabels.Length))
            Overrides.ChakramAxis = axis == 0 ? null : axis - 1;

        SettingsGrid.Row("Ice on:");
        var ice = Overrides.IceTank switch { PartyRole.MainTank => 1, PartyRole.OffTank => 2, _ => 0 };
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap2ice", ref ice, TankLabels, TankLabels.Length))
            Overrides.IceTank = ice switch { 1 => PartyRole.MainTank, 2 => PartyRole.OffTank, _ => null };

        if (solo) DrawPlayerRows();
        SettingsGrid.End();
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.Nisi.Clear();
            Overrides.Judgment.Clear();
            Overrides.FirstLightning.Clear();
            Overrides.Enumeration.Clear();
        }
        if (SettingsGrid.Begin("##teap2players"))
        {
            editingSeat = SettingsGrid.SeatRow("##teap2seat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Nisis set:", Overrides.Nisi);
            SettingsGrid.ForcedRecapRow("Final Judgments set:", Overrides.Judgment);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var you = PerRole.SeatsActive ? "" : "Your ";
        LetterRow($"{you}starting nisi:", "##teap2nisi", Overrides.Nisi);
        LetterRow($"{you}Final Judgment:", "##teap2judgment", Overrides.Judgment);
        YesNoRow($"{you}first lightning (DPS):", "##teap2lightning", Overrides.FirstLightning);
        YesNoRow($"{you}Enumeration (DPS):", "##teap2enumeration", Overrides.Enumeration);
    }

    private void LetterRow(string label, string id, PerRoleSetting<NisiLetter> setting)
    {
        SettingsGrid.Row(label);
        var index = setting.Effective(editingSeat) is { } letter ? (int)letter + 1 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo(id, ref index, LetterLabels, LetterLabels.Length))
            setting.Set(editingSeat, index == 0 ? null : (NisiLetter)(index - 1));
    }

    private void YesNoRow(string label, string id, PerRoleSetting<bool> setting)
    {
        SettingsGrid.Row(label);
        var index = setting.Effective(editingSeat) is { } yes ? yes ? 1 : 2 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo(id, ref index, YesNoLabels, YesNoLabels.Length))
            setting.Set(editingSeat, index == 0 ? null : index == 1);
    }
}
