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
            DrawIfritCorner();
            DrawSearingWind();
            DrawFlamingCrush();
            DrawUltimaHp();
            SettingsGrid.End();
        }
    }

    private void DrawIfritCorner()
    {
        var v = Overrides.IfritCorner;
        SettingsGrid.Row("Ifrit:");
        if (ImGui.RadioButton("Random##ifritcorner", v == IfritCorner.Random)) Overrides.IfritCorner = IfritCorner.Random;
        ImGui.SameLine();
        if (ImGui.RadioButton("South-east##ifritcorner", v == IfritCorner.SouthEast)) Overrides.IfritCorner = IfritCorner.SouthEast;
        ImGui.SameLine();
        if (ImGui.RadioButton("South-west##ifritcorner", v == IfritCorner.SouthWest)) Overrides.IfritCorner = IfritCorner.SouthWest;
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

    private void DrawUltimaHp()
    {
        var v = Overrides.UltimaPushedBelowHalf;
        SettingsGrid.Row("Ultima HP after Homing Lasers:");
        if (ImGui.RadioButton("Above 50%##ultimahp", !v)) Overrides.UltimaPushedBelowHalf = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("Pushed below 50%##ultimahp", v)) Overrides.UltimaPushedBelowHalf = true;
    }

    private void ResetAll()
    {
        Overrides.IfritCorner = IfritCorner.Random;
        Overrides.SearingWindOnPlayer = false;
        Overrides.FlamingCrushOnPlayer = false;
        Overrides.UltimaPushedBelowHalf = false;
    }
}
