using System;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P4KefkaSays;

// ImGui panel rendered in the main window's "Scenario config" pane when this
// scenario is active. Owns the StateOverrides instance and writes user choices into
// it. See UmadP2ForsakenSettingsWindow for the canonical shape.
public sealed class UmadP4KefkaSaysSettingsWindow
{
    public UmadP4KefkaSaysStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##umadp4kefkasays"))
        {
#if DEBUG
            DrawFirstBlizzard();
            DrawFirstLightning();
            DrawFirstBlizzardOffset();
#endif
            RealFakeRow("Exdeath 1:", "ex1", Overrides.ExdeathCast1Real, v => Overrides.ExdeathCast1Real = v);
            DrawExdeath2();
            RealFakeRow("Exdeath 3:", "ex3", Overrides.ExdeathCast3Real, v => Overrides.ExdeathCast3Real = v);
            RealFakeRow("Exdeath 4:", "ex4", Overrides.ExdeathCast4Real, v => Overrides.ExdeathCast4Real = v);
            RealFakeRow("Chaos 1:",   "chaos1", Overrides.ChaosCast1Real, v => Overrides.ChaosCast1Real = v);
            RealFakeRow("Chaos 2:",   "chaos2", Overrides.ChaosCast2Real, v => Overrides.ChaosCast2Real = v);
            SettingsGrid.End();
        }
    }

    // Auto / Real / Fake tri-state row writing a nullable-bool override
    // (null = Auto/random, true = Real, false = Fake).
    private static void RealFakeRow(string label, string id, bool? value, Action<bool?> set)
    {
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"Auto##{id}", value == null))  set(null);
        ImGui.SameLine();
        if (ImGui.RadioButton($"Real##{id}", value == true))  set(true);
        ImGui.SameLine();
        if (ImGui.RadioButton($"Fake##{id}", value == false)) set(false);
    }

    // Exdeath 2 gets a fourth choice: mirror cast 1's resolution (Wave2True = !Wave1True).
    private void DrawExdeath2()
    {
        var v = Overrides.ExdeathCast2;
        SettingsGrid.Row("Exdeath 2:");
        if (ImGui.RadioButton("Auto##ex2", v == ExdeathCast2Mode.Auto)) Overrides.ExdeathCast2 = ExdeathCast2Mode.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("Real##ex2", v == ExdeathCast2Mode.Real)) Overrides.ExdeathCast2 = ExdeathCast2Mode.Real;
        ImGui.SameLine();
        if (ImGui.RadioButton("Fake##ex2", v == ExdeathCast2Mode.Fake)) Overrides.ExdeathCast2 = ExdeathCast2Mode.Fake;
        ImGui.SameLine();
        if (ImGui.RadioButton("Opposite to 1##ex2", v == ExdeathCast2Mode.OppositeTo1)) Overrides.ExdeathCast2 = ExdeathCast2Mode.OppositeTo1;
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
#endif

    private void ResetAll()
    {
#if DEBUG
        Overrides.FirstBlizzardReal = null;
        Overrides.FirstLightningReal = null;
        Overrides.FirstBlizzardOffset = null;
#endif
        Overrides.ExdeathCast1Real = null;
        Overrides.ExdeathCast2 = ExdeathCast2Mode.Auto;
        Overrides.ExdeathCast3Real = null;
        Overrides.ExdeathCast4Real = null;
        Overrides.ChaosCast1Real = null;
        Overrides.ChaosCast2Real = null;
    }
}
