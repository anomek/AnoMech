using System;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P5Exaflares;

// Settings panel for the P5 exaflares scenario: pick or randomize each side's column order.
// Mirrors TopP5DeltaSettingsWindow (SettingsGrid two-column layout, "Auto" reset).
public sealed class UmadP5ExaflaresSettingsWindow
{
    public UmadP5ExaflaresStateOverrides Overrides { get; } = new();

    // Index-aligned with the ExaFlareOrder enum (Random = 0, then the six fixed orders).
    private static readonly string[] OrderLabels =
    [
        "Random",
        "1/4 - 2/5 - 3/6",
        "1/4 - 3/6 - 2/5",
        "2/5 - 1/4 - 3/6",
        "2/5 - 3/6 - 1/4",
        "3/6 - 1/4 - 2/5",
        "3/6 - 2/5 - 1/4",
    ];

    public void Draw()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.LeftOrder = ExaFlareOrder.Random;
            Overrides.RightOrder = ExaFlareOrder.Random;
        }

        if (SettingsGrid.Begin("##p5exaflares"))
        {
            DrawOrderRow("Left order:", "##leftorder", Overrides.LeftOrder, v => Overrides.LeftOrder = v);
            DrawOrderRow("Right order:", "##rightorder", Overrides.RightOrder, v => Overrides.RightOrder = v);
            SettingsGrid.End();
        }
    }

    private static void DrawOrderRow(string label, string id, ExaFlareOrder current, Action<ExaFlareOrder> set)
    {
        SettingsGrid.Row(label);
        var idx = (int)current;
        ImGui.SetNextItemWidth(180);
        if (ImGui.Combo(id, ref idx, OrderLabels, OrderLabels.Length))
            set((ExaFlareOrder)idx);
    }
}
