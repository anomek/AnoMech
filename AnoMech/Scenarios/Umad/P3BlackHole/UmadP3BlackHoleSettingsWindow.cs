using System;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// ImGui panel rendered in the main window's "Scenario config" pane when this scenario is
// active. Owns the StateOverrides instance and writes user choices into it.
public sealed class UmadP3BlackHoleSettingsWindow
{
    public UmadP3BlackHoleStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    // Solo, the player's own picks sit in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
#if !DEBUG
        if (!solo)
        {
            ImGui.TextDisabled("Everything here is per player -- use the button below.");
            return;
        }
#endif
        if (ImGui.Button("Auto"))
        {
#if DEBUG
            Overrides.FirstSlap = null;
#endif
            if (solo) ResetMine();
        }
        if (SettingsGrid.Begin("##umadp3blackhole"))
        {
            if (solo)
            {
                DrawLineNumber();
                DrawAccretion();
            }
#if DEBUG
            DrawFirstSlap();
            if (solo) DrawFirstSlapTarget();
#endif
            SettingsGrid.End();
        }
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) ResetPerPlayer();
        if (SettingsGrid.Begin("##umadp3blackholeplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##bhseat", editingSeat);
            DrawLineNumber();
            DrawAccretion();
#if DEBUG
            DrawFirstSlapTarget();
#endif
            SettingsGrid.ForcedRecapRow("Lines set:", Overrides.LineNumber);
            SettingsGrid.ForcedRecapRow("Accretion set:", Overrides.Accretion);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    // Forces the seat into the slot carrying that line number (Auto = the fight's own roll).
    private void DrawLineNumber()
    {
        var v = Overrides.LineNumber.Effective(editingSeat);
        SettingsGrid.PlayerRow("line:");
        if (ImGui.RadioButton("Auto##line",   v == null)) Overrides.LineNumber.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("First##line",  v == 1))    Overrides.LineNumber.Set(editingSeat, 1);
        ImGui.SameLine();
        if (ImGui.RadioButton("Second##line", v == 2))    Overrides.LineNumber.Set(editingSeat, 2);
        ImGui.SameLine();
        if (ImGui.RadioButton("Third##line",  v == 3))    Overrides.LineNumber.Set(editingSeat, 3);
    }

    // Yes is dropped for tanks and third-in-line, which never carry Accretion in the fight.
    private void DrawAccretion()
    {
        var v = Overrides.Accretion.Effective(editingSeat);
        SettingsGrid.PlayerRow("Accretion:");
        if (ImGui.RadioButton("Auto##accretion", v == null))  Overrides.Accretion.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##accretion",  v == true))  Overrides.Accretion.Set(editingSeat, true);
        ImGui.SameLine();
        if (ImGui.RadioButton("No##accretion",   v == false)) Overrides.Accretion.Set(editingSeat, false);
    }

#if DEBUG
    private void DrawFirstSlap()
    {
        var v = Overrides.FirstSlap;
        SettingsGrid.Row("1st Slap:");
        if (ImGui.RadioButton("Auto##firstslap",  v == null))                     Overrides.FirstSlap = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Left##firstslap",  v == ActionId.SlapHappy_Left))  Overrides.FirstSlap = ActionId.SlapHappy_Left;
        ImGui.SameLine();
        if (ImGui.RadioButton("Right##firstslap", v == ActionId.SlapHappy_Right)) Overrides.FirstSlap = ActionId.SlapHappy_Right;
    }

    private void DrawFirstSlapTarget()
    {
        var v = Overrides.FirstSlapAllOnMe.Effective(editingSeat);
        SettingsGrid.Row(PerRole.SeatsActive ? "1st Slap at:" : "1st Slap Target:");
        if (ImGui.RadioButton("Auto##firstslaptarget", v != true)) Overrides.FirstSlapAllOnMe.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{(PerRole.SeatsActive ? "This seat" : "Player")}##firstslaptarget", v == true))
            Overrides.FirstSlapAllOnMe.Set(editingSeat, true);
    }
#endif

    // Only matters when a tank slot is bot-driven. Two rows: the sim casts Thunder III twice.
    // Drawn from DrawMultiplayerSettings so it stays editable while Multiplayer is open.
    public void DrawThunderIIIPlan()
    {
        ImGui.Separator();
        // Only the host's plan is read.
        var mpGuest = Plugin.MultiplayerInstance is { IsConnected: true, IsHost: false };
        ImGui.TextUnformatted("Thunder III plan (planning tank bots will follow):");
        ImGui.BeginDisabled(mpGuest);
        DrawThunderIIIRow("##thunder1", "Set 1 (~42.6s):", Overrides.ThunderSet1, Overrides.ThunderSet2, v => Overrides.ThunderSet1 = v);
        DrawThunderIIIRow("##thunder2", "Set 2 (~83.9s):", Overrides.ThunderSet2, Overrides.ThunderSet1, v => Overrides.ThunderSet2 = v);
        ImGui.EndDisabled();
        if (mpGuest && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Only the host's plan is used in multiplayer.");
    }

    private static void DrawThunderIIIRow(string idSuffix, string label, ThunderIIIAssignment current, ThunderIIIAssignment otherSet, Action<ThunderIIIAssignment> set)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        DrawThunderIIIOption("MT invulns both" + idSuffix, ThunderIIIAssignment.MtInvulnsBoth, current, otherSet, set);
        ImGui.SameLine();
        DrawThunderIIIOption("OT invulns both" + idSuffix, ThunderIIIAssignment.OtInvulnsBoth, current, otherSet, set);
        ImGui.SameLine();
        DrawThunderIIIOption("Share, MT first" + idSuffix, ThunderIIIAssignment.ShareMtFirst, current, otherSet, set);
        ImGui.SameLine();
        DrawThunderIIIOption("Share, OT first" + idSuffix, ThunderIIIAssignment.ShareOtFirst, current, otherSet, set);
    }

    // Invuln and Share are separate pools: neither an invuln nor the big self-mit cooldowns
    // recover in the ~41s between sets.
    private static void DrawThunderIIIOption(string label, ThunderIIIAssignment option, ThunderIIIAssignment current, ThunderIIIAssignment otherSet, Action<ThunderIIIAssignment> set)
    {
        var isShare = option is ThunderIIIAssignment.ShareMtFirst or ThunderIIIAssignment.ShareOtFirst;
        var otherIsShare = otherSet is ThunderIIIAssignment.ShareMtFirst or ThunderIIIAssignment.ShareOtFirst;
        var blocked = isShare ? otherIsShare : option == otherSet;
        if (blocked) ImGui.BeginDisabled();
        if (ImGui.RadioButton(label, current == option) && !blocked) set(option);
        if (blocked)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(isShare
                    ? "Both tanks already shared the other set -- their own mitigation kit's big cooldowns (120s/90s) can't recover in ~41s, so neither can reach the required mitigation again."
                    : "That tank already invulns the other set -- no tank invuln recovers in ~41s, so they can't do both.");
        }
    }

    private void ResetPerPlayer()
    {
        Overrides.LineNumber.Clear();
        Overrides.Accretion.Clear();
        Overrides.FirstSlapAllOnMe.Clear();
    }

    // Solo's own picks only: a host's seat assignments are kept apart.
    private void ResetMine()
    {
        Overrides.LineNumber.Mine = null;
        Overrides.Accretion.Mine = null;
        Overrides.FirstSlapAllOnMe.Mine = null;
    }
}
