using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Uwu.P3TitanGaols;

public sealed class UwuP3TitanGaolsSettingsWindow
{
    public UwuP3TitanGaolsStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        var always = Overrides.PlayerAlwaysInFirstGaols;
        if (ImGui.Checkbox("Always one of the three first gaols##titangaols", ref always))
            Overrides.PlayerAlwaysInFirstGaols = always;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("The main tank is never gaoled, so this does nothing when you play main tank.");

        ImGui.TextUnformatted("Titan jumps to:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##titangaolsjump", Overrides.FirstJump == null)) Overrides.FirstJump = null;
        foreach (var d in Direction.Cardinal)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}##titangaolsjump", Overrides.FirstJump == d)) Overrides.FirstJump = d;
        }
    }
}
