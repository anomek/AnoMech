using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Fru.CrystallizeTime;

public sealed partial class FruCrystallizeTimeScenario
{
    private static readonly string[] PlayerPatternLabels =
        ["Random", "RED+ICE", "RED+AERO", "BLUE+DARK", "BLUE+ICE / BLUE / YELLOW stack"];

    partial void DrawPatternSettings()
    {
        if (ImGui.Button("Auto")) SelectedPlayerPattern = CrystallizeTimePlayerPattern.Random;
        if (SettingsGrid.Begin("##ct-pattern"))
        {
            SettingsGrid.Row("Your debuffs:");
            var selected = (int)SelectedPlayerPattern;
            ImGui.SetNextItemWidth(300);
            if (ImGui.Combo("##ct-player-pattern", ref selected, PlayerPatternLabels, PlayerPatternLabels.Length))
                SelectedPlayerPattern = (CrystallizeTimePlayerPattern)selected;
            SettingsGrid.End();
        }
        if (SelectedPlayerPattern == CrystallizeTimePlayerPattern.BlueStack)
            ImGui.TextWrapped("Randomly assigns blue ice, water, or eruption.");
        ImGui.TextDisabled("Applies on the next Start. Bots use NA priority.");
    }
}
