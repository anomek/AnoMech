using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Tea.P1LivingLiquid;

public sealed class TeaP1LivingLiquidSettingsWindow
{
    public TeaP1LivingLiquidStateOverrides Overrides { get; } = new();

    private static readonly string[] OpenSideLabels = ["Random", "N", "NE", "E", "SE", "S", "SW", "W", "NW"];

    public void Draw()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.FirstTornadoesOpenSide = null;
            Overrides.SecondTornadoesOpenSide = null;
        }

        if (SettingsGrid.Begin("##tealivingliquid"))
        {
            Overrides.FirstTornadoesOpenSide = DrawOpenSide("First tornadoes open to:", "##teallfirst", Overrides.FirstTornadoesOpenSide);
            Overrides.SecondTornadoesOpenSide = DrawOpenSide("Second tornadoes open to:", "##teallsecond", Overrides.SecondTornadoesOpenSide);
            SettingsGrid.End();
        }
    }

    private static int? DrawOpenSide(string label, string id, int? degrees)
    {
        SettingsGrid.Row(label);
        var index = degrees is { } d ? d / 45 + 1 : 0;
        SettingsGrid.ItemWidth(140);
        return ImGui.Combo(id, ref index, OpenSideLabels, OpenSideLabels.Length)
            ? index == 0 ? null : (index - 1) * 45
            : degrees;
    }
}
