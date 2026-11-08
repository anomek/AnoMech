using System;
using System.Linq;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Fru.P4CrystallizeTime;

public sealed class FruP4CrystallizeTimeSettingsWindow
{
    private static readonly string[] DebuffLabels =
        ["Random", .. Enum.GetValues<CrystallizeDebuffChoice>().Select(FruP4CrystallizeTimeStateOverrides.Label)];

    public FruP4CrystallizeTimeStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    // Solo, the player's own debuffs sit in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##frup4crystallize"))
        {
            if (solo) DrawDebuffs();

            var slow = Overrides.SlowNorthwest;
            SettingsGrid.Row("Slowed hourglasses:");
            if (ImGui.RadioButton("Auto##ctslow", slow == null)) Overrides.SlowNorthwest = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("NW + SE##ctslow", slow == true)) Overrides.SlowNorthwest = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("NE + SW##ctslow", slow == false)) Overrides.SlowNorthwest = false;

            var corner = Overrides.Corner;
            SettingsGrid.Row("Last safe corner:");
            if (ImGui.RadioButton("Auto##ctcorner", corner == null)) Overrides.Corner = null;
            foreach (var choice in Enum.GetValues<CrystallizeCorner>())
            {
                ImGui.SameLine();
                if (ImGui.RadioButton($"{choice}##ctcorner", corner == choice)) Overrides.Corner = choice;
            }

            SettingsGrid.Row("Spirit Taker:");
            if (ImGui.RadioButton("Auto##ctspirit", Overrides.SpiritTakerTarget == null)) Overrides.SpiritTakerTarget = null;
            foreach (var role in PerRole.All)
            {
                ImGui.SameLine();
                if (ImGui.RadioButton($"{SettingsGrid.RoleLabel(role)}##ctspirit", Overrides.SpiritTakerTarget == role))
                    Overrides.SpiritTakerTarget = role;
            }
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) Overrides.PlayerDebuffs.Clear();
        if (SettingsGrid.Begin("##frup4crystallizeplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##ctseat", editingSeat);
            DrawDebuffs();
            SettingsGrid.ForcedRecapRow("Debuffs set:", Overrides.PlayerDebuffs);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawDebuffs()
    {
        SettingsGrid.Row($"{(PerRole.SeatsActive ? "" : "Your ")}debuffs:");
        var index = Overrides.PlayerDebuffs.Effective(editingSeat) is { } choice ? (int)choice + 1 : 0;
        SettingsGrid.ItemWidth(300);
        if (ImGui.Combo("##ctplayer", ref index, DebuffLabels, DebuffLabels.Length))
            Overrides.PlayerDebuffs.Set(editingSeat, index == 0 ? null : (CrystallizeDebuffChoice)(index - 1));
    }

    // Solo's own pick only: a host's seat assignments are kept apart.
    private void ResetAll()
    {
        Overrides.PlayerDebuffs.Mine = null;
        Overrides.SlowNorthwest = null;
        Overrides.Corner = null;
        Overrides.SpiritTakerTarget = null;
    }
}
