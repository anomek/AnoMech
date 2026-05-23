using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P5Sigma;

public sealed class TopP5SigmaSettingsWindow
{
    public TopP5SigmaStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        DrawNewNorthA();
        DrawCloseFar();
        DrawTowerNorthFlip();
        DrawNewNorthB();
        DrawSpinnerRotation();
        DrawOmegaFForm();
        DrawHelloWorld();
        DrawDynamis();
    }

    private void ResetAll()
    {
        Overrides.NewNorthA = null;
        Overrides.CloseFarTether = null;
        Overrides.TowerNorthFlip = null;
        Overrides.NewNorthB = null;
        Overrides.SpinnerRotation = null;
        Overrides.OmegaFForm = null;
        Overrides.HelloWorld = HelloWorldOption.Auto;
        Overrides.Dynamis = null;
    }

    private void DrawNewNorthA()
    {
        ImGui.TextUnformatted("New north (A — sigma resolve):");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##northA", Overrides.NewNorthA == null)) Overrides.NewNorthA = null;
        foreach (var d in EightWayDirection.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name}##northA", Overrides.NewNorthA == d)) Overrides.NewNorthA = d;
        }
    }

    private void DrawCloseFar()
    {
        var v = Overrides.CloseFarTether;
        ImGui.TextUnformatted("Tether range:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##cf",  v == null))            Overrides.CloseFarTether = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Mid##cf", v == GlitchType.Mid))  Overrides.CloseFarTether = GlitchType.Mid;
        ImGui.SameLine();
        if (ImGui.RadioButton("Far##cf",   v == GlitchType.Far))    Overrides.CloseFarTether = GlitchType.Far;
    }

    private void DrawTowerNorthFlip()
    {
        var v = Overrides.TowerNorthFlip;
        ImGui.TextUnformatted("Tower-north flip:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##flip", v == null))  Overrides.TowerNorthFlip = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##flip",  v == true))  Overrides.TowerNorthFlip = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("No##flip",   v == false)) Overrides.TowerNorthFlip = false;
    }

    private void DrawNewNorthB()
    {
        ImGui.TextUnformatted("New north (B — second half):");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##northB", Overrides.NewNorthB == null)) Overrides.NewNorthB = null;
        foreach (var d in EightWayDirection.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name}##northB", Overrides.NewNorthB == d)) Overrides.NewNorthB = d;
        }
    }

    private void DrawSpinnerRotation()
    {
        var v = Overrides.SpinnerRotation;
        ImGui.TextUnformatted("Spinner rotation:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##spin", v == null))                       Overrides.SpinnerRotation = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("CW##spin",   v == Rotation.Clockwise))         Overrides.SpinnerRotation = Rotation.Clockwise;
        ImGui.SameLine();
        if (ImGui.RadioButton("CCW##spin",  v == Rotation.CounterClockwise))  Overrides.SpinnerRotation = Rotation.CounterClockwise;
    }

    private void DrawOmegaFForm()
    {
        var v = Overrides.OmegaFForm;
        ImGui.TextUnformatted("Omega-F form:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##form",       v == null))                  Overrides.OmegaFForm = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Leg blades##form", v == OmegaAttack.Legs))  Overrides.OmegaFForm = OmegaAttack.Legs;
        ImGui.SameLine();
        if (ImGui.RadioButton("Staff##form",      v == OmegaAttack.Staff))      Overrides.OmegaFForm = OmegaAttack.Staff;
    }

    private void DrawHelloWorld()
    {
        var h = Overrides.HelloWorld;
        ImGui.TextUnformatted("Hello World:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##hw", h == HelloWorldOption.Auto)) Overrides.HelloWorld = HelloWorldOption.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("Near##hw", h == HelloWorldOption.Near)) Overrides.HelloWorld = HelloWorldOption.Near;
        ImGui.SameLine();
        if (ImGui.RadioButton("Far##hw",  h == HelloWorldOption.Far))  Overrides.HelloWorld = HelloWorldOption.Far;
        ImGui.SameLine();
        if (ImGui.RadioButton("None##hw", h == HelloWorldOption.No))   Overrides.HelloWorld = HelloWorldOption.No;
    }

    private void DrawDynamis()
    {
        var d = Overrides.Dynamis;
        ImGui.TextUnformatted("Start with Dynamis:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##dyn", d == null))  Overrides.Dynamis = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##dyn",  d == true))  Overrides.Dynamis = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("No##dyn",   d == false)) Overrides.Dynamis = false;
    }
}
