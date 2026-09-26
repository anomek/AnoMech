using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P5Sigma;

public sealed class TopP5SigmaSettingsWindow
{
    public TopP5SigmaStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    // Solo, the player's own picks sit in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            ResetFight();
            if (solo) ResetMine();
        }
        if (SettingsGrid.Begin("##p5sigma"))
        {
#if DEBUG
            DrawNewNorthA();
#endif
            DrawCloseFar();
            DrawTowerNorthFlip();
#if DEBUG
            DrawNewNorthB();
#endif
            DrawSpinnerRotation();
            DrawOmegaFForm();
            if (solo)
            {
                DrawHelloWorld();
                DrawDynamis();
            }
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) ResetPerPlayer();
        if (SettingsGrid.Begin("##p5sigmaplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##sigmaseat", editingSeat);
            DrawHelloWorld();
            DrawDynamis();
            SettingsGrid.ForcedRecapRow("Hello World set:", Overrides.HelloWorld);
            SettingsGrid.ForcedRecapRow("Dynamis set:", Overrides.Dynamis);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void ResetFight()
    {
#if DEBUG
        Overrides.NewNorthA = null;
        Overrides.NewNorthB = null;
#endif
        Overrides.CloseFarTether = null;
        Overrides.TowerNorthFlip = null;
        Overrides.SpinnerRotation = null;
        Overrides.OmegaFForm = null;
    }

    private void ResetPerPlayer()
    {
        Overrides.HelloWorld.Clear();
        Overrides.Dynamis.Clear();
    }

    // Solo's own picks only: a host's seat assignments are kept apart.
    private void ResetMine()
    {
        Overrides.HelloWorld.Mine = null;
        Overrides.Dynamis.Mine = null;
    }

#if DEBUG
    private void DrawNewNorthA()
    {
        SettingsGrid.Row("New north (A — sigma resolve):");
        if (ImGui.RadioButton("Auto##northA", Overrides.NewNorthA == null)) Overrides.NewNorthA = null;
        foreach (var d in Direction.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}##northA", Overrides.NewNorthA == d)) Overrides.NewNorthA = d;
        }
    }
#endif

    private void DrawCloseFar()
    {
        var v = Overrides.CloseFarTether;
        SettingsGrid.Row("Tether range:");
        if (ImGui.RadioButton("Auto##cf",  v == null))            Overrides.CloseFarTether = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Mid##cf", v == GlitchType.Mid))  Overrides.CloseFarTether = GlitchType.Mid;
        ImGui.SameLine();
        if (ImGui.RadioButton("Far##cf",   v == GlitchType.Far))    Overrides.CloseFarTether = GlitchType.Far;
    }

    private void DrawTowerNorthFlip()
    {
        var v = Overrides.TowerNorthFlip;
        SettingsGrid.Row("Tower-north flip:");
        if (ImGui.RadioButton("Auto##flip", v == null))  Overrides.TowerNorthFlip = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##flip",  v == true))  Overrides.TowerNorthFlip = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("No##flip",   v == false)) Overrides.TowerNorthFlip = false;
    }

#if DEBUG
    private void DrawNewNorthB()
    {
        SettingsGrid.Row("New north (B — second half):");
        if (ImGui.RadioButton("Auto##northB", Overrides.NewNorthB == null)) Overrides.NewNorthB = null;
        foreach (var d in Direction.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}##northB", Overrides.NewNorthB == d)) Overrides.NewNorthB = d;
        }
    }
#endif

    private void DrawSpinnerRotation()
    {
        var v = Overrides.SpinnerRotation;
        SettingsGrid.Row("Spinner rotation:");
        if (ImGui.RadioButton("Auto##spin", v == null))                       Overrides.SpinnerRotation = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("CW##spin",   v == Rotation.Clockwise))         Overrides.SpinnerRotation = Rotation.Clockwise;
        ImGui.SameLine();
        if (ImGui.RadioButton("CCW##spin",  v == Rotation.CounterClockwise))  Overrides.SpinnerRotation = Rotation.CounterClockwise;
    }

    private void DrawOmegaFForm()
    {
        var v = Overrides.OmegaFForm;
        SettingsGrid.Row("Omega-F form:");
        if (ImGui.RadioButton("Auto##form",       v == null))                  Overrides.OmegaFForm = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Leg blades##form", v == OmegaAttack.Legs))  Overrides.OmegaFForm = OmegaAttack.Legs;
        ImGui.SameLine();
        if (ImGui.RadioButton("Staff##form",      v == OmegaAttack.Staff))      Overrides.OmegaFForm = OmegaAttack.Staff;
    }

    private void DrawHelloWorld()
    {
        var h = Overrides.HelloWorld.Effective(editingSeat);
        SettingsGrid.PlayerRow("Hello World:");
        if (ImGui.RadioButton("Auto##hw", h == null)) Overrides.HelloWorld.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Near##hw", h == HelloWorldOption.Near)) Overrides.HelloWorld.Set(editingSeat, HelloWorldOption.Near);
        ImGui.SameLine();
        if (ImGui.RadioButton("Far##hw",  h == HelloWorldOption.Far))  Overrides.HelloWorld.Set(editingSeat, HelloWorldOption.Far);
        ImGui.SameLine();
        if (ImGui.RadioButton("None##hw", h == HelloWorldOption.No))   Overrides.HelloWorld.Set(editingSeat, HelloWorldOption.No);
    }

    private void DrawDynamis()
    {
        var d = Overrides.Dynamis.Effective(editingSeat);
        SettingsGrid.PlayerRow("start with Dynamis:");
        if (ImGui.RadioButton("Auto##dyn", d == null))  Overrides.Dynamis.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##dyn",  d == true))  Overrides.Dynamis.Set(editingSeat, true);
        ImGui.SameLine();
        if (ImGui.RadioButton("No##dyn",   d == false)) Overrides.Dynamis.Set(editingSeat, false);
    }
}
