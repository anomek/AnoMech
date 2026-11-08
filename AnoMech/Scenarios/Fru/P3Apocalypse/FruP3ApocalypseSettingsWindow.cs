using System;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Fru.P3Apocalypse;

public sealed class FruP3ApocalypseSettingsWindow
{
    private static readonly string[] Spokes = ["N/S", "NE/SW", "E/W", "SE/NW"];

    public FruP3ApocalypseStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    // Solo, the player's own Dark Water sits in this panel; a host assigns seats from the
    // Multiplayer window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##frup3apocalypse"))
        {
            if (solo) DrawWater();
            DrawStart();
            DrawRotation();
            DrawSpiritTaker();
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.DarkWater.Clear();
        if (SettingsGrid.Begin("##frup3apocalypseplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##apocseat", editingSeat);
            DrawWater();
            SettingsGrid.ForcedRecapRow("Dark Water set:", Overrides.DarkWater);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    // Solo's own pick only: a host's seat assignments are kept apart.
    private void ResetAll()
    {
        Overrides.Rotation = null;
        Overrides.Clockwise = null;
        Overrides.SpiritTakerTarget = null;
        Overrides.DarkWater.Mine = null;
    }

    private void DrawWater()
    {
        var v = Overrides.DarkWater.Effective(editingSeat);
        SettingsGrid.PlayerRow("Dark Water:");
        if (ImGui.RadioButton("Auto##apocwater", v == null)) Overrides.DarkWater.Set(editingSeat, null);
        foreach (var water in Enum.GetValues<ApocalypseWater>())
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{water}##apocwater", v == water)) Overrides.DarkWater.Set(editingSeat, water);
        }
    }

    private void DrawStart()
    {
        SettingsGrid.Row("First lights:");
        if (ImGui.RadioButton("Auto##apocstart", Overrides.Rotation == null)) Overrides.Rotation = null;
        for (var i = 0; i < Spokes.Length; i++)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{Spokes[i]}##apocstart", Overrides.Rotation == i)) Overrides.Rotation = i;
        }
    }

    private void DrawRotation()
    {
        var v = Overrides.Clockwise;
        SettingsGrid.Row("Rotation:");
        if (ImGui.RadioButton("Auto##apocrot", v == null)) Overrides.Clockwise = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("CW##apocrot", v == true)) Overrides.Clockwise = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("CCW##apocrot", v == false)) Overrides.Clockwise = false;
    }

    private void DrawSpiritTaker()
    {
        SettingsGrid.Row("Spirit Taker:");
        if (ImGui.RadioButton("Auto##apocspirit", Overrides.SpiritTakerTarget == null)) Overrides.SpiritTakerTarget = null;
        foreach (var role in PerRole.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{SettingsGrid.RoleLabel(role)}##apocspirit", Overrides.SpiritTakerTarget == role))
                Overrides.SpiritTakerTarget = role;
        }
    }
}
