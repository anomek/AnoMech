using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios;

// Two-column "Label : options" layout for scenario config panels. SizingFixedFit
// makes column 0 fit the widest label, so every option group in column 1 starts at
// the same x. See TopP5DeltaSettingsWindow for the canonical usage.
internal static class SettingsGrid
{
    public static bool Begin(string id) =>
        ImGui.BeginTable(id, 2, ImGuiTableFlags.SizingFixedFit);

    // Begin a new option row: writes the label in column 0 and leaves the cursor
    // in column 1, ready for the option widgets.
    public static void Row(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();   // vertically center label against the radio buttons
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
    }

    public static void End() => ImGui.EndTable();
}
