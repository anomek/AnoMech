using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Uwu.UltimateAnnihilation;

public class UltimateAnnihilationSettingsWindow
{
    public UltimateAnnihilationStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto"))
        {
            ResetAll();
        }

        if (SettingsGrid.Begin("##ultimateannihilation"))
        {
            DrawSearingWind();
            DrawFlamingCrush();
            SettingsGrid.End();
        }
    }

    private void DrawSearingWind()
    {
        var v = Overrides.SearingWindOnPlayer;
        SettingsGrid.Row("Searing Wind (Healer Only):");
        if (ImGui.RadioButton("Random##searingwind", !v)) Overrides.SearingWindOnPlayer = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("Me##searingwind", v)) Overrides.SearingWindOnPlayer = true;
    }

    private void DrawFlamingCrush()
    {
        var v = Overrides.FlamingCrushOnPlayer;
        SettingsGrid.Row("Flaming Crush (DPS Only):");
        if (ImGui.RadioButton("Random##flamingcrush", !v)) Overrides.FlamingCrushOnPlayer = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("Me##flamingcrush", v)) Overrides.FlamingCrushOnPlayer = true;
    }

    private void ResetAll()
    {
        Overrides.SearingWindOnPlayer = false;
        Overrides.FlamingCrushOnPlayer = false;
    }
}
