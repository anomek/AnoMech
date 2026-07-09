using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Uwu.UltimatePredation;

public class UltimatePredationSettingsWindow
{
    public UltimatePredationStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto"))
        {
            ResetAll();
        }

        if (SettingsGrid.Begin("##ultimatepredation"))
        {
            DrawCenterDodge();
            SettingsGrid.End();
        }
    }

    private void DrawCenterDodge()
    {
        var v = Overrides.CenterDodge;
        SettingsGrid.Row("Boss Positions:");
        if (ImGui.RadioButton("Random##centerdodge", v == null)) Overrides.CenterDodge = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Center Dodge##centerdodge", v == true)) Overrides.CenterDodge = true;
    }

    private void ResetAll()
    {
        Overrides.CenterDodge = null;
    }
}
