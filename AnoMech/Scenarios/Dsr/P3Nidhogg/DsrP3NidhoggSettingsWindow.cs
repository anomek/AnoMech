using System;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Dsr.P3Nidhogg;

public sealed class DsrP3NidhoggSettingsWindow
{
    public DsrP3NidhoggStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##dsrp3nidhogg"))
        {
            SettingsGrid.FightOnlyNote();
            SettingsGrid.Row("Arrows on:");
            DrawChoices("arrows", ["1 & 2", "3", "2 & 3", "1", "1 & 3", "2"], [0, 1, 2, 3, 4, 5], Overrides.ArrowPattern, v => Overrides.ArrowPattern = v);
            SettingsGrid.Row("First wheels:");
            DrawChoices("wheel1", ["Out first", "In first"], [true, false], Overrides.FirstOutFirst, v => Overrides.FirstOutFirst = v);
            SettingsGrid.Row("Second wheels:");
            DrawChoices("wheel2", ["Out first", "In first"], [true, false], Overrides.SecondOutFirst, v => Overrides.SecondOutFirst = v);
            SettingsGrid.Row("Tower counts:");
            DrawChoices("towers", ["2-2-2-2", "1-1-2-4", "1-1-3-3", "1-2-2-3"], [0, 1, 2, 3], Overrides.CounterTowerShape, v => Overrides.CounterTowerShape = v);
            SettingsGrid.End();
        }
    }

    private void ResetAll()
    {
        Overrides.ArrowPattern = null;
        Overrides.FirstOutFirst = null;
        Overrides.SecondOutFirst = null;
        Overrides.CounterTowerShape = null;
    }

    private static void DrawChoices<T>(string id, string[] labels, T[] values, T? current, Action<T?> set) where T : struct
    {
        if (ImGui.RadioButton($"Auto##{id}", current == null)) set(null);
        for (var i = 0; i < labels.Length; i++)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{labels[i]}##{id}{i}", current is { } c && c.Equals(values[i]))) set(values[i]);
        }
    }
}
