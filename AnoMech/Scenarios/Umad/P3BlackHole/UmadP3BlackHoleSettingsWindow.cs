using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// ImGui panel rendered in the main window's "Scenario config" pane when this
// scenario is active. Owns the StateOverrides instance and writes user choices into
// it. See UmadP4KefkaSaysSettingsWindow for the canonical shape.
public sealed class UmadP3BlackHoleSettingsWindow
{
    public UmadP3BlackHoleStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        // TODO: one row per override field, in timeline order.
    }

    private void ResetAll()
    {
        // TODO: reset every Overrides property to null / Auto.
    }
}
