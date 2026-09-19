using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Fru.DarklitDragonsong;

public sealed partial class FruDarklitDragonsongScenario
{
    partial void DrawSomberSettings()
    {
        ImGui.Checkbox("Player takes both Somber Dance hits", ref PlayerTakesSomberDance);
        ImGui.TextDisabled("Requires a tank role. Otherwise, a tank bot takes both hits.");
        ImGui.TextDisabled("Applies on the next Start.");
    }
}
