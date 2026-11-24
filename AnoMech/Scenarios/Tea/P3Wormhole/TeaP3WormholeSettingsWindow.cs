using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Tea.P3Wormhole;

public sealed class TeaP3WormholeSettingsWindow
{
    public TeaP3WormholeStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] BruteJusticeLabels = ["Random", "NW (4/1)", "NE (3/2)"];
    private static readonly string[] PuddleLabels = ["Random", "WNW + ESE", "WSW + ENE"];
    private static readonly string[] NumberLabels = ["Random", "1", "2", "3", "4", "5", "6", "7", "8"];
    private static readonly string[] YesNoLabels = ["Random", "Yes", "No"];

    // Solo, the player's own rows sit in this panel; a host assigns seats from the Multiplayer window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.BruteJusticeNorthWest = null;
            Overrides.PuddlesMirrored = null;
            if (solo)
            {
                Overrides.Number.Mine = null;
                Overrides.Enumeration.Mine = null;
            }
        }

        if (!SettingsGrid.Begin("##teap3")) return;
        SettingsGrid.Row("Wormhole Brute Justice:");
        var side = Overrides.BruteJusticeNorthWest is { } northWest ? northWest ? 1 : 2 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap3bj", ref side, BruteJusticeLabels, BruteJusticeLabels.Length))
            Overrides.BruteJusticeNorthWest = side == 0 ? null : side == 1;

        SettingsGrid.Row("Wormholes:");
        var puddles = Overrides.PuddlesMirrored is { } mirrored ? mirrored ? 2 : 1 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap3puddles", ref puddles, PuddleLabels, PuddleLabels.Length))
            Overrides.PuddlesMirrored = puddles == 0 ? null : puddles == 2;

        if (solo) DrawPlayerRows();
        SettingsGrid.End();
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.Number.Clear();
            Overrides.Enumeration.Clear();
        }
        if (SettingsGrid.Begin("##teap3players"))
        {
            editingSeat = SettingsGrid.SeatRow("##teap3seat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Numbers set:", Overrides.Number);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var you = PerRole.SeatsActive ? "" : "Your ";

        SettingsGrid.Row($"{you}Wormhole number:");
        var number = Overrides.Number.Effective(editingSeat) ?? 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap3number", ref number, NumberLabels, NumberLabels.Length))
            Overrides.Number.Set(editingSeat, number == 0 ? null : number);

        SettingsGrid.Row($"{you}Enumeration (DPS):");
        var enumeration = Overrides.Enumeration.Effective(editingSeat) is { } yes ? yes ? 1 : 2 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap3enumeration", ref enumeration, YesNoLabels, YesNoLabels.Length))
            Overrides.Enumeration.Set(editingSeat, enumeration == 0 ? null : enumeration == 1);
    }
}
