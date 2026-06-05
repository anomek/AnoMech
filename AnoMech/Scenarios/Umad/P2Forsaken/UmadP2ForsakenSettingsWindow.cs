using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P2Forsaken;

// ImGui panel rendered in the main window's "Scenario config" pane when this
// scenario is active. Owns the StateOverrides instance and writes user choices
// into it. See TopP5DeltaSettingsWindow for the canonical shape.
public sealed class UmadP2ForsakenSettingsWindow
{
    public UmadP2ForsakenStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##umadp2forsaken"))
        {
            DrawFirstEndAttack();
            SettingsGrid.End();
        }
    }

    private void DrawFirstEndAttack()
    {
        var v = Overrides.FirstEndAttack;
        SettingsGrid.Row("First End:");
        if (ImGui.RadioButton("Auto##firstend",   v == null))                 Overrides.FirstEndAttack = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Future##firstend", v == EndAttack.FuturesEnd)) Overrides.FirstEndAttack = EndAttack.FuturesEnd;
        ImGui.SameLine();
        if (ImGui.RadioButton("Past##firstend",   v == EndAttack.PastsEnd))   Overrides.FirstEndAttack = EndAttack.PastsEnd;
    }

    private void ResetAll() => Overrides.FirstEndAttack = null;
}
