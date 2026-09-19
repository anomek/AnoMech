using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Fru.DiamondDust;

public sealed partial class FruDiamondDustScenario
{
    private static readonly string[] KickLabels = ["Random", "Axe Kick (out)", "Scythe Kick (in)"];

    partial void DrawPatternSettings()
    {
        if (ImGui.Button("Auto")) PatternSettings.Reset();
        if (SettingsGrid.Begin("##dd-pattern"))
        {
            SettingsGrid.Row("Opening kick:");
            ImGui.SetNextItemWidth(300);
            ImGui.Combo("##dd-kick", ref PatternSettings.Kick, KickLabels, KickLabels.Length);
            SettingsGrid.End();
        }
        ImGui.TextDisabled("Applies on the next Start. Bots use NAUR partner swaps.");
    }

}
