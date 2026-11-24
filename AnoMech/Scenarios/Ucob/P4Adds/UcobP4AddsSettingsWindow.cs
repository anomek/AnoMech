using System;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Ucob.P4Adds;

public sealed class UcobP4AddsSettingsWindow
{
    private static readonly (string Label, PartyRole Role)[] Dps =
    [
        ("M1", PartyRole.MeleeDpsA), ("M2", PartyRole.MeleeDpsB),
        ("R1", PartyRole.PhysRangedDps), ("R2", PartyRole.CasterDps),
    ];

    public UcobP4AddsStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        ImGui.SameLine();
        var simplified = Overrides.SimplifiedQuotes;
        if (ImGui.Checkbox("Simplified Nael quotes", ref simplified)) Overrides.SimplifiedQuotes = simplified;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Replace each quote with its moves, e.g. \"In > Spread > Stack\".");

        if (SettingsGrid.Begin("##ucobp4adds"))
        {
            DrawQuote("First quote:", "q1", Overrides.FirstQuote, q => Overrides.FirstQuote = q);
            DrawQuote("Second quote:", "q2", Overrides.SecondQuote, q => Overrides.SecondQuote = q);
            DrawUntargeted("Hatch 1 skips:", "h1", Overrides.FirstHatchUntargeted, r => Overrides.FirstHatchUntargeted = r);
            DrawUntargeted("Hatch 2 skips:", "h2", Overrides.SecondHatchUntargeted, r => Overrides.SecondHatchUntargeted = r);
            SettingsGrid.End();
        }
    }

    private void ResetAll()
    {
        Overrides.FirstQuote = null;
        Overrides.SecondQuote = null;
        Overrides.FirstHatchUntargeted = null;
        Overrides.SecondHatchUntargeted = null;
    }

    private static void DrawQuote(string label, string id, NaelQuote? current, Action<NaelQuote?> set)
    {
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"Auto##{id}", current == null)) set(null);
        foreach (var quote in NaelQuote.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{quote.Name}##{id}", current == quote)) set(quote);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(quote.Text);
        }
    }

    private static void DrawUntargeted(string label, string id, PartyRole? current, Action<PartyRole?> set)
    {
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"Auto##{id}", current == null)) set(null);
        foreach (var (name, role) in Dps)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{name}##{id}", current == role)) set(role);
        }
    }
}
