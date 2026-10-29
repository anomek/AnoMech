using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

public sealed class FruP4DarklitDragonsongSettingsWindow
{
    private static readonly string[] Shapes = ["Bowtie", "Box", "Hourglass"];

    public FruP4DarklitDragonsongStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    // Solo, the player's own chain and Dark Water sit in this panel; a host assigns seats from the
    // Multiplayer window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##frup4darklit"))
        {
            if (solo) DrawPlayerRows();

            SettingsGrid.Row("Chains:");
            if (ImGui.RadioButton("Auto##dlchains", Overrides.Shape == null)) Overrides.Shape = null;
            for (var i = 0; i < Shapes.Length; i++)
            {
                ImGui.SameLine();
                if (ImGui.RadioButton($"{Shapes[i]}##dlchains", Overrides.Shape == i)) Overrides.Shape = i;
            }

            var v = Overrides.EastWing;
            SettingsGrid.Row("Hallowed Wings:");
            if (ImGui.RadioButton("Auto##dlwing", v == null)) Overrides.EastWing = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("East##dlwing", v == true)) Overrides.EastWing = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("West##dlwing", v == false)) Overrides.EastWing = false;

            SettingsGrid.Row("Spirit Taker:");
            if (ImGui.RadioButton("Auto##dlspirit", Overrides.SpiritTakerTarget == null)) Overrides.SpiritTakerTarget = null;
            foreach (var role in PerRole.All)
            {
                ImGui.SameLine();
                if (ImGui.RadioButton($"{SettingsGrid.RoleLabel(role)}##dlspirit", Overrides.SpiritTakerTarget == role))
                    Overrides.SpiritTakerTarget = role;
            }
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.Chained.Clear();
            Overrides.DarkWater.Clear();
        }
        if (SettingsGrid.Begin("##frup4darklitplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##dlseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Chains set:", Overrides.Chained);
            SettingsGrid.ForcedRecapRow("Dark Water set:", Overrides.DarkWater);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var chained = Overrides.Chained.Effective(editingSeat);
        SettingsGrid.PlayerRow("chain:");
        if (ImGui.RadioButton("Auto##dlchain", chained == null)) Overrides.Chained.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Chained##dlchain", chained == true)) Overrides.Chained.Set(editingSeat, true);
        ImGui.SameLine();
        if (ImGui.RadioButton("Free##dlchain", chained == false)) Overrides.Chained.Set(editingSeat, false);

        var water = Overrides.DarkWater.Effective(editingSeat);
        SettingsGrid.PlayerRow("Dark Water:");
        if (ImGui.RadioButton("Auto##dlwater", water == null)) Overrides.DarkWater.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##dlwater", water == true)) Overrides.DarkWater.Set(editingSeat, true);
        ImGui.SameLine();
        if (ImGui.RadioButton("No##dlwater", water == false)) Overrides.DarkWater.Set(editingSeat, false);
    }

    // Solo's own picks only: a host's seat assignments are kept apart.
    private void ResetAll()
    {
        Overrides.Shape = null;
        Overrides.EastWing = null;
        Overrides.SpiritTakerTarget = null;
        Overrides.Chained.Mine = null;
        Overrides.DarkWater.Mine = null;
    }
}
