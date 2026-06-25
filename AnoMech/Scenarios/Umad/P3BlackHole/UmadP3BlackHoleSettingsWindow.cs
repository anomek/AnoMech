using Dalamud.Bindings.ImGui;
using static AnoMech.Scenarios.Umad.UmadConstants;

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
        if (SettingsGrid.Begin("##umadp3blackhole"))
        {
            DrawLineNumber();
            DrawAccretion();
#if DEBUG
            DrawFirstSlap();
            DrawFirstSlapTarget();
#endif
            SettingsGrid.End();
        }
    }

    // Forces the player into the slot carrying that line number (Auto = random).
    private void DrawLineNumber()
    {
        var v = Overrides.LineNumber;
        SettingsGrid.Row("Line:");
        if (ImGui.RadioButton("Auto##line",   v == null)) Overrides.LineNumber = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("First##line",  v == 1))    Overrides.LineNumber = 1;
        ImGui.SameLine();
        if (ImGui.RadioButton("Second##line", v == 2))    Overrides.LineNumber = 2;
        ImGui.SameLine();
        if (ImGui.RadioButton("Third##line",  v == 3))    Overrides.LineNumber = 3;
    }

    // Auto = random; Yes is ignored for tanks and third-in-line (they never get Accretion).
    private void DrawAccretion()
    {
        var v = Overrides.Accretion;
        SettingsGrid.Row("Accretion:");
        if (ImGui.RadioButton("Auto##accretion", v == null))  Overrides.Accretion = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##accretion",  v == true))  Overrides.Accretion = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("No##accretion",   v == false)) Overrides.Accretion = false;
    }

#if DEBUG
    private void DrawFirstSlap()
    {
        var v = Overrides.FirstSlap;
        SettingsGrid.Row("1st Slap:");
        if (ImGui.RadioButton("Auto##firstslap",  v == null))                     Overrides.FirstSlap = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Left##firstslap",  v == ActionId.SlapHappy_Left))  Overrides.FirstSlap = ActionId.SlapHappy_Left;
        ImGui.SameLine();
        if (ImGui.RadioButton("Right##firstslap", v == ActionId.SlapHappy_Right)) Overrides.FirstSlap = ActionId.SlapHappy_Right;
    }

    private void DrawFirstSlapTarget()
    {
        var v = Overrides.FirstSlapAllOnPlayer;
        SettingsGrid.Row("1st Slap Target:");
        if (ImGui.RadioButton("Auto##firstslaptarget",   v == null)) Overrides.FirstSlapAllOnPlayer = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Player##firstslaptarget", v == true)) Overrides.FirstSlapAllOnPlayer = true;
    }
#endif

    private void ResetAll()
    {
        Overrides.LineNumber = null;
        Overrides.Accretion = null;
#if DEBUG
        Overrides.FirstSlap = null;
        Overrides.FirstSlapAllOnPlayer = null;
#endif
    }
}