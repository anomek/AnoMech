using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public sealed class TopP2PartySynergySettingsWindow
{
    public TopP2PartySynergyStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##partysynergy"))
        {
#if DEBUG
            DrawNewNorthA();
            DrawNewNorthB();
#endif
            DrawGlitch();
            DrawAttackM();
            DrawAttackF();
            SettingsGrid.End();
        }
    }

    private void ResetAll()
    {
#if DEBUG
        Overrides.NewNorthA = null;
        Overrides.NewNorthB = null;
#endif
        Overrides.Glitch    = null;
        Overrides.AttackM   = null;
        Overrides.AttackF   = null;
    }

#if DEBUG
    private void DrawNewNorthA()
    {
        SettingsGrid.Row("New north (A):");
        if (ImGui.RadioButton("Auto##northA", Overrides.NewNorthA == null)) Overrides.NewNorthA = null;
        foreach (var d in Direction.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}##northA", Overrides.NewNorthA == d)) Overrides.NewNorthA = d;
        }
    }

    private void DrawNewNorthB()
    {
        SettingsGrid.Row("New north (B):");
        if (ImGui.RadioButton("Auto##northB", Overrides.NewNorthB == null)) Overrides.NewNorthB = null;
        foreach (var d in Direction.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}##northB", Overrides.NewNorthB == d)) Overrides.NewNorthB = d;
        }
    }
#endif

    private void DrawGlitch()
    {
        var v = Overrides.Glitch;
        SettingsGrid.Row("Glitch:");
        if (ImGui.RadioButton("Auto##glitch", v == null))           Overrides.Glitch = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Mid##glitch",  v == GlitchType.Mid)) Overrides.Glitch = GlitchType.Mid;
        ImGui.SameLine();
        if (ImGui.RadioButton("Far##glitch",  v == GlitchType.Far)) Overrides.Glitch = GlitchType.Far;
    }

    private void DrawAttackM()
    {
        var v = Overrides.AttackM;
        SettingsGrid.Row("Omega-M form:");
        if (ImGui.RadioButton("Auto##atkM",   v == null))               Overrides.AttackM = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Sword##atkM",  v == OmegaAttack.Sword))  Overrides.AttackM = OmegaAttack.Sword;
        ImGui.SameLine();
        if (ImGui.RadioButton("Shield##atkM", v == OmegaAttack.Shield)) Overrides.AttackM = OmegaAttack.Shield;
    }

    private void DrawAttackF()
    {
        var v = Overrides.AttackF;
        SettingsGrid.Row("Omega-F form:");
        if (ImGui.RadioButton("Auto##atkF",  v == null))              Overrides.AttackF = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Staff##atkF", v == OmegaAttack.Staff)) Overrides.AttackF = OmegaAttack.Staff;
        ImGui.SameLine();
        if (ImGui.RadioButton("Legs##atkF",  v == OmegaAttack.Legs))  Overrides.AttackF = OmegaAttack.Legs;
    }
}
