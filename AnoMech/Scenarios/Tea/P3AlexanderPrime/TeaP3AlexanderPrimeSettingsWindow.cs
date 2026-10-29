using System.Linq;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Tea.P3AlexanderPrime;

public sealed class TeaP3AlexanderPrimeSettingsWindow
{
    public TeaP3AlexanderPrimeStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] PracticeLabels = TeaP3AlexanderPrimePractice.All.Select(e => e.Label).ToArray();
    private static readonly string[] TankLabels = ["Random", "MT", "OT"];
    private static readonly string[] EdgeLabels = ["Random", "South", "North"];
    private static readonly string[] SpotLabels = ["Random", "NNE", "ENE", "ESE", "SSE", "SSW", "WSW", "WNW", "NNW"];
    private static readonly string[] BruteJusticeLabels = ["Random", "NW (4/1)", "NE (3/2)"];
    private static readonly string[] PuddleLabels = ["Random", "WNW + ESE", "WSW + ENE"];
    private static readonly string[] OrbLabels = ["Random", "Plasmasphere", "Judgment Crystal"];
    private static readonly string[] DebuffLabels = ["Random", "Restraining Order", "Shared Sentence", "Aggravated Assault"];
    private static readonly string[] NumberLabels = ["Random", "1", "2", "3", "4", "5", "6", "7", "8"];
    private static readonly string[] YesNoLabels = ["Random", "Yes", "No"];

    // Solo, the player's own rows sit in this panel; a host assigns seats from the Multiplayer window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.RestrainingOrderTank = null;
            Overrides.AlexanderPrimeNorth = null;
            Overrides.SacramentSpot = null;
            Overrides.BruteJusticeNorthWest = null;
            Overrides.PuddlesMirrored = null;
            if (solo)
            {
                Overrides.Orb.Mine = null;
                Overrides.Debuff.Mine = null;
                Overrides.Number.Mine = null;
                Overrides.Enumeration.Mine = null;
            }
        }

        if (!SettingsGrid.Begin("##teap3")) return;
        SettingsGrid.Row("Practice:");
        var practice = (int)Overrides.Practice;
        SettingsGrid.ItemWidth(220);
        if (ImGui.Combo("##teap3practice", ref practice, PracticeLabels, PracticeLabels.Length) && practice >= 0 && practice < PracticeLabels.Length)
            Overrides.Practice = (P3Practice)practice;
        SettingsGrid.Row("");
        ImGui.TextDisabled(TeaP3AlexanderPrimePractice.Of(Overrides.Practice).Covers);

        SettingsGrid.Row("Restraining Order tank:");
        var tank = Overrides.RestrainingOrderTank switch { PartyRole.MainTank => 1, PartyRole.OffTank => 2, _ => 0 };
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap3rotank", ref tank, TankLabels, TankLabels.Length))
            Overrides.RestrainingOrderTank = tank switch { 1 => PartyRole.MainTank, 2 => PartyRole.OffTank, _ => null };

        SettingsGrid.Row("Inception Alexander Prime:");
        var edge = Overrides.AlexanderPrimeNorth is { } north ? north ? 2 : 1 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap3edge", ref edge, EdgeLabels, EdgeLabels.Length))
            Overrides.AlexanderPrimeNorth = edge == 0 ? null : edge == 2;

        SettingsGrid.Row("Inception Sacrament:");
        var spot = (Overrides.SacramentSpot ?? -1) + 1;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap3spot", ref spot, SpotLabels, SpotLabels.Length))
            Overrides.SacramentSpot = spot == 0 ? null : spot - 1;

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
            Overrides.Orb.Clear();
            Overrides.Debuff.Clear();
            Overrides.Number.Clear();
            Overrides.Enumeration.Clear();
        }
        if (SettingsGrid.Begin("##teap3players"))
        {
            editingSeat = SettingsGrid.SeatRow("##teap3seat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Inception roles set:", Overrides.Orb);
            SettingsGrid.ForcedRecapRow("Numbers set:", Overrides.Number);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var you = PerRole.SeatsActive ? "" : "Your ";

        SettingsGrid.Row($"{you}Inception role:");
        var orb = Overrides.Orb.Effective(editingSeat) is { } tethered ? tethered ? 1 : 2 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap3orb", ref orb, OrbLabels, OrbLabels.Length))
            Overrides.Orb.Set(editingSeat, orb == 0 ? null : orb == 1);

        SettingsGrid.Row($"{you}Inception debuff (DPS):");
        var debuff = Overrides.Debuff.Effective(editingSeat) is { } held ? (int)held + 1 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##teap3debuff", ref debuff, DebuffLabels, DebuffLabels.Length))
            Overrides.Debuff.Set(editingSeat, debuff == 0 ? null : (InceptionDebuff)(debuff - 1));

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
