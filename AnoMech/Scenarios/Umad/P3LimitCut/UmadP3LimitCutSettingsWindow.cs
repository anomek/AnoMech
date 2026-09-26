using System;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P3BlackHole;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P3LimitCut;

public sealed class UmadP3LimitCutSettingsWindow
{
    public UmadP3LimitCutStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] SpotLabels = ["Random", "S", "SE", "E", "NE", "N", "NW", "W", "SW"];
    private static readonly string[] OrderLabels = ["Random", "Clockwise", "Counter-clockwise"];
    private static readonly string[] NumberLabels = ["Random", "1", "2", "3", "4", "5", "6", "7", "8"];
    private static readonly string[] WindLabels = ["Random", "Headwind", "Tailwind"];
    private static readonly string[] BossSpotLabels = ["Random", "NE", "SE", "SW", "NW"];
    private static readonly string[] LimitBreakLabels = ["Auto (bots, unless you tank)", "Always", "Never"];
    private static readonly int[] BossSpotValues = [3, 1, 7, 5];
    private static readonly string[] BaitLabels =
        ["Phys ranged (default)", "Main tank", "Off tank", "Regen healer", "Shield healer", "Melee A", "Melee B", "Caster"];
    private static readonly PartyRole[] BaitRoles =
    [
        PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.CasterDps,
    ];

    // Solo, the player's own picks sit in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            ResetAll();
            if (solo) ResetMine();
        }

        if (SettingsGrid.Begin("##p3limitcut"))
        {
            SettingsGrid.Row("First clone:");
            var spot = (Overrides.StartSpot ?? -1) + 1;
            SettingsGrid.ItemWidth(140);
            if (ImGui.Combo("##lcstartspot", ref spot, SpotLabels, SpotLabels.Length))
                Overrides.StartSpot = spot == 0 ? null : spot - 1;

            SettingsGrid.Row("Clone order:");
            var order = Overrides.Clockwise switch { true => 1, false => 2, null => 0 };
            SettingsGrid.ItemWidth(160);
            if (ImGui.Combo("##lcorder", ref order, OrderLabels, OrderLabels.Length))
                Overrides.Clockwise = order switch { 1 => true, 2 => false, _ => null };

            SettingsGrid.Row("Bosses held at:");
            var boss = Overrides.BossSpot is { } b ? Array.IndexOf(BossSpotValues, b) + 1 : 0;
            SettingsGrid.ItemWidth(140);
            if (ImGui.Combo("##lcbossspot", ref boss, BossSpotLabels, BossSpotLabels.Length))
                Overrides.BossSpot = boss == 0 ? null : BossSpotValues[boss - 1];

            SettingsGrid.Row("Umbra Smash bait:");
            var bait = Overrides.BaitRole is { } r ? Array.IndexOf(BaitRoles, r) + 1 : 0;
            SettingsGrid.ItemWidth(180);
            if (ImGui.Combo("##lcbait", ref bait, BaitLabels, BaitLabels.Length))
                Overrides.BaitRole = bait == 0 ? null : BaitRoles[bait - 1];

            SettingsGrid.Row("Bot tank LB3:");
            var lb = Overrides.BotTankLimitBreak switch { true => 1, false => 2, null => 0 };
            SettingsGrid.ItemWidth(200);
            if (ImGui.Combo("##lclb", ref lb, LimitBreakLabels, LimitBreakLabels.Length))
                Overrides.BotTankLimitBreak = lb switch { 1 => true, 2 => false, _ => null };

            if (solo) DrawPlayerRows();
            SettingsGrid.End();
        }
        // The Thunder III plan is drawn by UmadP3LimitCutScenario.DrawMultiplayerSettings so it
        // stays editable while the Multiplayer window is open, as Black Hole's does.
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) ResetPerPlayer();
        if (SettingsGrid.Begin("##p3limitcutplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##lcseat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Numbers set:", Overrides.Number);
            SettingsGrid.ForcedRecapRow("Winds set:", Overrides.Wind);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var whose = PerRole.SeatsActive ? "" : "Your ";

        SettingsGrid.Row($"{whose}number:");
        var number = Overrides.Number.Effective(editingSeat) ?? 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##lcnumber", ref number, NumberLabels, NumberLabels.Length))
            Overrides.Number.Set(editingSeat, number == 0 ? null : number);

        SettingsGrid.Row($"{whose}wind:");
        var wind = Overrides.Wind.Effective(editingSeat) switch { Wind.Headwind => 1, Wind.Tailwind => 2, _ => 0 };
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo("##lcwind", ref wind, WindLabels, WindLabels.Length))
            Overrides.Wind.Set(editingSeat, wind switch { 1 => Wind.Headwind, 2 => Wind.Tailwind, _ => null });
    }

    private void ResetAll()
    {
        Overrides.StartSpot = null;
        Overrides.Clockwise = null;
        Overrides.BossSpot = null;
        Overrides.BaitRole = null;
        Overrides.BotTankLimitBreak = null;
        Overrides.ThunderPlan = ThunderIIIAssignment.MtInvulnsBoth;
    }

    private void ResetPerPlayer()
    {
        Overrides.Number.Clear();
        Overrides.Wind.Clear();
    }

    // Solo's own picks only: a host's seat assignments are kept apart.
    private void ResetMine()
    {
        Overrides.Number.Mine = null;
        Overrides.Wind.Mine = null;
    }

    // Black Hole's planner for the one Thunder III set here (the two hits at ~38.6s and ~41.6s):
    // which tank stands on Exdeath, with an invuln or a swap. Only the host's pick is read.
    public void DrawThunderIIIPlan()
    {
        ImGui.Separator();
        var mpGuest = Plugin.MultiplayerInstance is { IsConnected: true, IsHost: false };
        ImGui.TextUnformatted("Thunder III plan (planning tank bots will follow):");
        ImGui.BeginDisabled(mpGuest);
        ImGui.TextUnformatted("After the charges (~38.6s):");
        ImGui.SameLine();
        DrawThunderIIIOption("MT invulns both##lcthunder", ThunderIIIAssignment.MtInvulnsBoth);
        ImGui.SameLine();
        DrawThunderIIIOption("OT invulns both##lcthunder", ThunderIIIAssignment.OtInvulnsBoth);
        ImGui.SameLine();
        DrawThunderIIIOption("Share, MT first##lcthunder", ThunderIIIAssignment.ShareMtFirst);
        ImGui.SameLine();
        DrawThunderIIIOption("Share, OT first##lcthunder", ThunderIIIAssignment.ShareOtFirst);
        ImGui.EndDisabled();
        if (mpGuest && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Only the host's plan is used in multiplayer.");
    }

    private void DrawThunderIIIOption(string label, ThunderIIIAssignment option)
    {
        if (ImGui.RadioButton(label, Overrides.ThunderPlan == option)) Overrides.ThunderPlan = option;
    }
}
