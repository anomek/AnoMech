using System;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Fru.P3UltimateRelativity;

public sealed class FruP3UltimateRelativitySettingsWindow
{
    public FruP3UltimateRelativityStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    // Solo, the player's own fire sits in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##frup3relativity"))
        {
            if (solo) DrawFire();
            var v = Overrides.DpsIce;
            SettingsGrid.Row("Dark Blizzard:");
            if (ImGui.RadioButton("Auto##urice", v == null)) Overrides.DpsIce = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("Long DPS##urice", v == true)) Overrides.DpsIce = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("Short support##urice", v == false)) Overrides.DpsIce = false;
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.Fire.Clear();
        if (SettingsGrid.Begin("##frup3relativityplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##urseat", editingSeat);
            DrawFire();
            SettingsGrid.ForcedRecapRow("Fire set:", Overrides.Fire);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    // Solo's own pick only: a host's seat assignments are kept apart.
    private void ResetAll()
    {
        Overrides.DpsIce = null;
        Overrides.Fire.Mine = null;
    }

    private void DrawFire()
    {
        var v = Overrides.Fire.Effective(editingSeat);
        SettingsGrid.PlayerRow("fire:");
        if (ImGui.RadioButton("Auto##urfire", v == null)) Overrides.Fire.Set(editingSeat, null);
        foreach (var fire in Enum.GetValues<RelativityFire>())
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{fire}##urfire", v == fire)) Overrides.Fire.Set(editingSeat, fire);
        }
    }
}
