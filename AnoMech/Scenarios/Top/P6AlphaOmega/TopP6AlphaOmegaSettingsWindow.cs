using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

public sealed class TopP6AlphaOmegaSettingsWindow
{
    public TopP6AlphaOmegaStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    // Shared with the lobby summary, so peers read the host's picks as the host sees them. Auto
    // comes first where there is one; "" marks an Auto label worked out when drawn.
    private static readonly string[] PracticeLabels = TopP6AlphaOmegaPractice.All
        .Select(entry => entry.Start > 0f ? $"{entry.Label}  ({Clock(entry.Start)})" : entry.Label)
        .ToArray();
    private static readonly string[] ArrowLabels = ["Auto", "In first", "Out first"];
    private static readonly string[] InvulnLabels = ["OT first, MT second (NAUR)", "MT first, OT second"];
    private static readonly string[] TankOrderLabels = ["MT first", "OT first"];
    private static readonly string[] MeleeOrderLabels = ["M1 first + enrage (NAUR)", "M2 first + enrage"];
    private static readonly string[] HealerOrderLabels = ["H1 first", "H2 first"];
    private static readonly string[] MiddleHealerLabels = ["H1", "H2"];
    private static readonly string[] DiveSideLabels = ["Auto (NAUR: MT left, OT right)", "Left", "Right"];
    private static readonly string[] MeleeLabels =
        ["", "First at Cosmo Dive 2", "First + enrage", "Second at Cosmo Dive 2", "Second + enrage"];

    // Solo, the player's own picks sit in this panel; a host assigns seats from the Multiplayer
    // window. Only the settings the chosen practice reaches are shown.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            ResetAll();
            if (solo) ResetMine();
        }
        if (SettingsGrid.Begin("##p6alphaomega"))
        {
            DrawPractice();
            if (ReachesFirstArrow)
                Overrides.FirstArrowInFirst = ArrowRow("Cosmo Arrow 1:", "##arrow1", Overrides.FirstArrowInFirst);
            if (ReachesSecondArrow)
                Overrides.SecondArrowInFirst = ArrowRow("Cosmo Arrow 2:", "##arrow2", Overrides.SecondArrowInFirst);
            if (ReachesInvulnOrder)
                Overrides.MainTankInvulnsFirstWaveCannon = PairRow("Wave Cannon invulns:", "p6wc", InvulnLabels, Overrides.MainTankInvulnsFirstWaveCannon);
            if (ReachesTankLimitBreaks)
                Overrides.OffTankLimitBreaksFirst = PairRow("Tank LB3 order:", "p6tanklb", TankOrderLabels, Overrides.OffTankLimitBreaksFirst,
                    "Cosmo Memory and the second Magic Number go to the first tank, the first Magic Number to the other.");
            if (ReachesMeleeLimitBreaks)
                Overrides.MeleeDpsBLimitBreaksFirst = PairRow("Melee LB3 order:", "p6melee", MeleeOrderLabels, Overrides.MeleeDpsBLimitBreaksFirst);
            if (ReachesMagicNumbers)
                Overrides.ShieldHealerLimitBreaksFirst = PairRow("Healer LB3 order:", "p6healerlb", HealerOrderLabels, Overrides.ShieldHealerLimitBreaksFirst,
                    "The first Magic Number's healer LB3 goes to the first healer, the second's to the other.");
            if (ReachesCosmoMeteor)
                Overrides.ShieldHealerGoesMiddle = PairRow("Cosmo Meteor middle:", "p6middle", MiddleHealerLabels, Overrides.ShieldHealerGoesMiddle);
            if (solo) DrawPlayerRows(SoloRole(), everyRow: false);
            SettingsGrid.End();
        }
    }

    private void DrawPractice()
    {
        SettingsGrid.Row("Practice:");
        var index = (int)Overrides.Practice;
        FitWidth(PracticeLabels);
        if (ImGui.Combo("##p6practice", ref index, PracticeLabels, PracticeLabels.Length) && index >= 0 && index < PracticeLabels.Length)
            Overrides.Practice = (P6Practice)index;
        var entry = TopP6AlphaOmegaPractice.Of(Overrides.Practice);
        SettingsGrid.Row("");
        ImGui.TextDisabled(entry.Covers);
    }

    private bool Reaches(params P6Practice[] mechanics)
        => Overrides.Practice == P6Practice.WholePhase || mechanics.Contains(Overrides.Practice);

    private bool ReachesFirstArrow => Reaches(P6Practice.CosmoArrowOne);
    private bool ReachesSecondArrow => Reaches(P6Practice.CosmoArrowTwo);
    // The invuln order also picks which tank Reprisals each Cosmo Dive.
    private bool ReachesInvulnOrder => Reaches(P6Practice.CosmoArrowOne, P6Practice.WaveCannonOne, P6Practice.CosmoArrowTwo, P6Practice.CosmoDiveTwo);
    private bool ReachesMeleeLimitBreaks => Reaches(P6Practice.CosmoDiveTwo, P6Practice.CosmoMeteor, P6Practice.RunDynamis);
    private bool ReachesMagicNumbers => Reaches(P6Practice.MagicNumberOne, P6Practice.MagicNumberTwo);
    private bool ReachesTankLimitBreaks => Reaches(P6Practice.CosmoMemory, P6Practice.MagicNumberOne, P6Practice.MagicNumberTwo);
    private bool ReachesCosmoDive => Reaches(P6Practice.CosmoArrowOne, P6Practice.CosmoDiveTwo);
    private bool ReachesCosmoMeteor => Reaches(P6Practice.CosmoMeteor);

    private static string Clock(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    // As wide as the longest option, so the closed box never cuts one off.
    private static void FitWidth(string[] labels)
        => ImGui.SetNextItemWidth(labels.Max(label => ImGui.CalcTextSize(label).X) + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight());

    // Two options on one row; returns whether the second is picked.
    private static bool PairRow(string label, string id, string[] options, bool second, string? tooltip = null)
    {
        SettingsGrid.Row(label);
        ImGui.BeginGroup();
        if (ImGui.RadioButton($"{options[0]}##{id}", !second)) second = false;
        ImGui.SameLine();
        if (ImGui.RadioButton($"{options[1]}##{id}", second)) second = true;
        ImGui.EndGroup();
        if (tooltip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        return second;
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) ResetPerPlayer();
        if (SettingsGrid.Begin("##p6alphaomegaplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##p6seat", editingSeat);
            DrawPlayerRows(editingSeat, everyRow: true);
            RecapRow("Dive sides set:", Seats(Overrides.CosmoDiveSide, SideName));
            RecapRow("Melee LB3s set:", Seats(Overrides.MeleeDuty, MeleeName));
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    // The lobby's read-only lines: the rows the practice reaches, worded as the panel words them.
    public List<string> Summary()
    {
        var lines = new List<string> { $"Practice: {PracticeLabels[(int)Overrides.Practice]}" };
        if (ReachesFirstArrow && Overrides.FirstArrowInFirst is { } first) lines.Add($"Cosmo Arrow 1: {ArrowLabels[first ? 1 : 2]}");
        if (ReachesSecondArrow && Overrides.SecondArrowInFirst is { } second) lines.Add($"Cosmo Arrow 2: {ArrowLabels[second ? 1 : 2]}");
        if (ReachesInvulnOrder) lines.Add($"Wave Cannon invulns: {InvulnLabels[Overrides.MainTankInvulnsFirstWaveCannon ? 1 : 0]}");
        if (ReachesTankLimitBreaks) lines.Add($"Tank LB3 order: {TankOrderLabels[Overrides.OffTankLimitBreaksFirst ? 1 : 0]}");
        if (ReachesMeleeLimitBreaks) lines.Add($"Melee LB3 order: {MeleeOrderLabels[Overrides.MeleeDpsBLimitBreaksFirst ? 1 : 0]}");
        if (ReachesMagicNumbers) lines.Add($"Healer LB3 order: {HealerOrderLabels[Overrides.ShieldHealerLimitBreaksFirst ? 1 : 0]}");
        if (ReachesCosmoMeteor) lines.Add($"Cosmo Meteor middle: {MiddleHealerLabels[Overrides.ShieldHealerGoesMiddle ? 1 : 0]}");

        void SeatLine(string label, bool reached, string? seats)
        {
            if (reached && seats != null) lines.Add($"{label}: {seats}");
        }
        SeatLine("Cosmo Dive side", ReachesCosmoDive, Seats(Overrides.CosmoDiveSide, SideName));
        SeatLine("Melee LB3s", ReachesMeleeLimitBreaks, Seats(Overrides.MeleeDuty, MeleeName));
        return lines;
    }

    // "MT Left, OT Right" over the seats a setting forces; null when it forces none.
    private static string? Seats<T>(PerRoleSetting<T> setting, Func<T, string> name) where T : struct
    {
        var seats = SettingsGrid.Seats.Where(r => setting[r].HasValue)
            .Select(r => $"{SettingsGrid.RoleLabel(r)} {name(setting[r]!.Value)}")
            .ToList();
        return seats.Count == 0 ? null : string.Join(", ", seats);
    }

    private static void RecapRow(string label, string? seats)
    {
        if (!PerRole.SeatsActive || seats == null) return;
        SettingsGrid.Row(label);
        ImGui.TextDisabled(seats);
    }

    private static string SideName(DiveSide side) => DiveSideLabels[(int)side + 1];
    private static string MeleeName(MeleeLimitBreaks duty) => MeleeLabels[(int)duty + 1];

    private static PartyRole SoloRole()
        => Plugin.MainWindow.SelectedRoleOverride ?? PartyPresets.SkipRoleForJob(Natives.BattleCharas.LocalPlayer.ClassJob);

    // A limit break duty shows wherever an earlier practice start still depends on it: it decides
    // who comes into a later mechanic on Spark of Dynamis. The seat dialog shows every row, so a
    // conflict that refuses a start can always be fixed from it.
    private void DrawPlayerRows(PartyRole role, bool everyRow)
    {
        if (role.IsTank())
        {
            if (everyRow || ReachesCosmoDive)
            {
                SettingsGrid.PlayerRow("Cosmo Dive side:");
                var side = Overrides.CosmoDiveSide.Effective(role);
                if (ImGui.RadioButton($"{DiveSideLabels[0]}##p6side", side == null)) Overrides.CosmoDiveSide.Set(role, null);
                ImGui.SameLine();
                if (ImGui.RadioButton($"{DiveSideLabels[1]}##p6side", side == DiveSide.Left)) Overrides.CosmoDiveSide.Set(role, DiveSide.Left);
                ImGui.SameLine();
                if (ImGui.RadioButton($"{DiveSideLabels[2]}##p6side", side == DiveSide.Right)) Overrides.CosmoDiveSide.Set(role, DiveSide.Right);
            }
        }
        else if (role is PartyRole.MeleeDpsA or PartyRole.MeleeDpsB)
        {
            if (everyRow || ReachesMeleeLimitBreaks)
            {
                SettingsGrid.PlayerRow("melee LB3s:");
                var duty = Overrides.MeleeDuty.Effective(role) is { } d ? (int)d + 1 : 0;
                var labels = (string[])MeleeLabels.Clone();
                labels[0] = Overrides.MeleeDpsBLimitBreaksFirst ? "Auto (M2 first + enrage, M1 second)" : "Auto (M1 first + enrage, M2 second)";
                FitWidth(labels);
                if (ImGui.Combo("##p6meleelb", ref duty, labels, labels.Length))
                    Overrides.MeleeDuty.Set(role, duty == 0 ? null : (MeleeLimitBreaks)(duty - 1));
            }
        }
        else if (everyRow)
        {
            SettingsGrid.Row("");
            ImGui.TextDisabled("Nothing to set for this seat.");
        }
    }

    // The practice stays: Auto resets how the fight rolls, not which part of it runs.
    private void ResetAll()
    {
        Overrides.FirstArrowInFirst = null;
        Overrides.SecondArrowInFirst = null;
        Overrides.MainTankInvulnsFirstWaveCannon = false;
        Overrides.OffTankLimitBreaksFirst = false;
        Overrides.MeleeDpsBLimitBreaksFirst = false;
        Overrides.ShieldHealerLimitBreaksFirst = false;
        Overrides.ShieldHealerGoesMiddle = false;
    }

    private void ResetMine()
    {
        Overrides.CosmoDiveSide.Mine = null;
        Overrides.MeleeDuty.Mine = null;
    }

    private void ResetPerPlayer()
    {
        Overrides.CosmoDiveSide.Clear();
        Overrides.MeleeDuty.Clear();
    }

    private static bool? ArrowRow(string label, string id, bool? value)
    {
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"{ArrowLabels[0]}{id}", value == null)) value = null;
        ImGui.SameLine();
        if (ImGui.RadioButton($"{ArrowLabels[1]}{id}", value == true)) value = true;
        ImGui.SameLine();
        if (ImGui.RadioButton($"{ArrowLabels[2]}{id}", value == false)) value = false;
        return value;
    }
}
