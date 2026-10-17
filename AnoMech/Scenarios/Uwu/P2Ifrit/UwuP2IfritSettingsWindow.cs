using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Uwu.P2Ifrit;

public class UwuP2IfritSettingsWindow
{
    public UwuP2IfritStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.SearingWindOnPlayer = SearingWindChoice.Random;
        }

        if (SettingsGrid.Begin("##uwup2ifrit"))
        {
            DrawSearingWind();
            SettingsGrid.End();
        }
    }

    private void DrawSearingWind()
    {
        var v = Overrides.SearingWindOnPlayer;
        SettingsGrid.Row("Searing Wind (Healer Only):");
        if (ImGui.RadioButton("Random##searingwind", v == SearingWindChoice.Random)) Overrides.SearingWindOnPlayer = SearingWindChoice.Random;
        ImGui.SameLine();
        if (ImGui.RadioButton("Me: 1st + 3rd Howl##searingwind", v == SearingWindChoice.FirstAndThirdHowl)) Overrides.SearingWindOnPlayer = SearingWindChoice.FirstAndThirdHowl;
        ImGui.SameLine();
        if (ImGui.RadioButton("Me: 2nd Howl##searingwind", v == SearingWindChoice.SecondHowl)) Overrides.SearingWindOnPlayer = SearingWindChoice.SecondHowl;
    }
}
