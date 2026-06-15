using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P3KefkaSays;

// ImGui panel rendered in the main window's "Scenario config" pane when this
// scenario is active. Owns the StateOverrides instance and writes user choices into
// it. See UmadP2ForsakenSettingsWindow for the canonical shape.
public sealed class UmadP3KefkaSaysSettingsWindow
{
    public UmadP3KefkaSaysStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##umadp3kefkasays"))
        {
#if DEBUG
            DrawFirstBlizzard();
            DrawFirstLightning();
            DrawFirstBlizzardOffset();
            DrawInferno();
            DrawTsunami();
#endif
            SettingsGrid.End();
        }
    }

#if DEBUG
    private void DrawFirstBlizzard()
    {
        var v = Overrides.FirstBlizzardReal;
        SettingsGrid.Row("1st Blizzard:");
        if (ImGui.RadioButton("Auto##firstblizz", v == null))  Overrides.FirstBlizzardReal = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Real##firstblizz", v == true))  Overrides.FirstBlizzardReal = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("Fake##firstblizz", v == false)) Overrides.FirstBlizzardReal = false;
    }

    private void DrawFirstLightning()
    {
        var v = Overrides.FirstLightningReal;
        SettingsGrid.Row("1st Lightning:");
        if (ImGui.RadioButton("Auto##firstlight", v == null))  Overrides.FirstLightningReal = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Real##firstlight", v == true))  Overrides.FirstLightningReal = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("Fake##firstlight", v == false)) Overrides.FirstLightningReal = false;
    }

    private void DrawFirstBlizzardOffset()
    {
        var v = Overrides.FirstBlizzardOffset;
        SettingsGrid.Row("1st Blizz Offset:");
        if (ImGui.RadioButton("Auto##firstoffset", v == null)) Overrides.FirstBlizzardOffset = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("0##firstoffset", v == 0))       Overrides.FirstBlizzardOffset = 0;
        ImGui.SameLine();
        if (ImGui.RadioButton("1##firstoffset", v == 1))       Overrides.FirstBlizzardOffset = 1;
    }

    private void DrawInferno()
    {
        var v = Overrides.InfernoReal;
        SettingsGrid.Row("Chaos Fire:");
        if (ImGui.RadioButton("Auto##inferno", v == null))  Overrides.InfernoReal = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Real##inferno", v == true))  Overrides.InfernoReal = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("Fake##inferno", v == false)) Overrides.InfernoReal = false;
    }

    private void DrawTsunami()
    {
        var v = Overrides.TsunamiReal;
        SettingsGrid.Row("Tsunami:");
        if (ImGui.RadioButton("Auto##tsunami", v == null))  Overrides.TsunamiReal = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Real##tsunami", v == true))  Overrides.TsunamiReal = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("Fake##tsunami", v == false)) Overrides.TsunamiReal = false;
    }
#endif

    private void ResetAll()
    {
#if DEBUG
        Overrides.FirstBlizzardReal = null;
        Overrides.FirstLightningReal = null;
        Overrides.FirstBlizzardOffset = null;
        Overrides.InfernoReal = null;
        Overrides.TsunamiReal = null;
#endif
    }
}
