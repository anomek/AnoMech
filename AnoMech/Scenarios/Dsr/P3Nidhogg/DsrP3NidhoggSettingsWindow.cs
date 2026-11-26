using System;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Dsr.P3Nidhogg;

public sealed class DsrP3NidhoggSettingsWindow
{
    public DsrP3NidhoggStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            ResetAll();
            if (solo) ResetMine();
        }
        if (SettingsGrid.Begin("##dsrp3nidhogg"))
        {
            SettingsGrid.Row("Arrows on:");
            DrawChoices("arrows", ["1 & 2", "3", "2 & 3", "1", "1 & 3", "2"], [0, 1, 2, 3, 4, 5], Overrides.ArrowPattern, v => Overrides.ArrowPattern = v);
            SettingsGrid.Row("First wheels:");
            DrawChoices("wheel1", ["Out first", "In first"], [true, false], Overrides.FirstOutFirst, v => Overrides.FirstOutFirst = v);
            SettingsGrid.Row("Second wheels:");
            DrawChoices("wheel2", ["Out first", "In first"], [true, false], Overrides.SecondOutFirst, v => Overrides.SecondOutFirst = v);
            SettingsGrid.Row("Tower counts:");
            DrawChoices("towers", ["2-2-2-2", "1-1-2-4", "1-1-3-3", "1-2-2-3"], [0, 1, 2, 3], Overrides.CounterTowerShape, v => Overrides.CounterTowerShape = v);
            if (solo) DrawPlayerRows();
            SettingsGrid.End();
        }
        if (solo) SettingsGrid.ConflictRows(Overrides.Validate());
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) ResetPerPlayer();
        if (SettingsGrid.Begin("##dsrp3nidhoggplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##dsrp3nidhoggseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Numbers set:", Overrides.Order);
            SettingsGrid.ForcedRecapRow("Jumps set:", Overrides.Jump);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        SettingsGrid.PlayerRow("number:");
        DrawChoices("order", ["1", "2", "3"], [1, 2, 3], Overrides.Order.Effective(editingSeat), v => Overrides.Order.Set(editingSeat, v));
        SettingsGrid.PlayerRow("jump:");
        DrawChoices("jump", ["High Jump", "Up arrow", "Down arrow"], [JumpKind.High, JumpKind.Forward, JumpKind.Backward],
            Overrides.Jump.Effective(editingSeat), v => Overrides.Jump.Set(editingSeat, v));
    }

    private void ResetAll()
    {
        Overrides.ArrowPattern = null;
        Overrides.FirstOutFirst = null;
        Overrides.SecondOutFirst = null;
        Overrides.CounterTowerShape = null;
    }

    private void ResetPerPlayer()
    {
        Overrides.Order.Clear();
        Overrides.Jump.Clear();
    }

    // Solo's own picks only: a host's seat assignments are kept apart.
    private void ResetMine()
    {
        Overrides.Order.Mine = null;
        Overrides.Jump.Mine = null;
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
