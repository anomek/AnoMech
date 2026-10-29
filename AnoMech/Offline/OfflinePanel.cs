using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnoMech.Scenarios;
using AnoMech.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;

namespace AnoMech.Offline;

// The offline parts of the main window: the setup screen it shows at the title screen, and the
// strip above the scenarios in the offline world.
internal sealed class OfflinePanel(OfflineSession session)
{
    private const float FieldWidth = 260f;
    private const int JobsInLabel = 5;
    private const string BuiltInLook = "Default Midlander";
    private const string LeaveMenu = "offline-leave-menu";
    private const string RestartLabel = "Restart game", ReturnLabel = "Return to title";

    private readonly ConditionalWeakTable<OfflineLocalCharacter, string> jobLists = new();
    private int lastSetupFrame = -2;

    private OfflineStore Store => session.Store;

    public void DrawSetup()
    {
        // Read again whenever the screen comes back: a session since may have saved new gearsets.
        var frame = ImGui.GetFrameCount();
        if (frame - lastSetupFrame > 1) session.Refresh();
        lastSetupFrame = frame;

        ImGui.TextUnformatted("Offline mode");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Practice in an inn without logging in, as one of the characters saved on this PC. "
                                   + $"Nothing is written to the game's files.\n{OfflineSession.LoginPrecaution}");
        ImGui.Separator();
        if (Store.LoadProblem is { } problem) Wrapped(MultiplayerUi.BadColor, problem);

        ImGui.BeginDisabled(session.Busy);
        DrawChoices();
        ImGui.EndDisabled();

        ImGui.Spacing();
        DrawEnter();
        if (session.ClosedLastTime is { } closed) Wrapped(MultiplayerUi.WarnColor, closed);
        if (session.LastRefusal is { } refusal) Wrapped(MultiplayerUi.WarnColor, refusal);
        if (session.Notice is { } notice) Wrapped(MultiplayerUi.GoodColor, notice);
    }

    public void DrawStrip()
    {
        if (!session.ShowsControls) return;
        switch (session.Phase)
        {
            case OfflinePhase.InWorld:
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(MultiplayerUi.GoodColor, "●");
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"No server connection. {OfflineSession.LoginPrecaution}");
                ImGui.SameLine();
                ImGui.TextUnformatted($"Offline · {Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? session.Loadout?.Name}");
                ImGui.SameLine();
                DrawLeave();
                break;
            case OfflinePhase.Leaving:
                ImGui.TextUnformatted(session.Status);
                break;
            case OfflinePhase.Failed:
                Wrapped(MultiplayerUi.BadColor, session.Status);
                MainWindow.PushSemanticColors(MainWindow.StartColor);
                if (ImGui.Button($"{RestartLabel}###offline-restart")) OfflineSession.RestartGame();
                MainWindow.PopSemanticColors();
                break;
            default:
                return;
        }
        ImGui.Separator();
    }

    private void DrawChoices()
    {
        if (!SettingsGrid.Begin("##offline-setup")) return;
        var selected = session.SelectedCharacter;
        SettingsGrid.Row("Character:");
        SettingsGrid.ItemWidth(FieldWidth);
        if (ImGui.BeginCombo("##offline-character", selected != null ? CharacterLabel(selected) : "None"))
        {
            foreach (var character in session.Characters)
            {
                if (ImGui.Selectable($"{CharacterLabel(character)}##{character.Key}", character.Key == selected?.Key))
                {
                    Store.Select(character.Key);
                    session.SelectionChanged();
                }
            }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Characters played on this PC with a combat gearset, most recent first.");
        ImGui.SameLine();
        if (ImGuiComponents.IconButton("offline-refresh", FontAwesomeIcon.Sync)) session.Refresh();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Read this PC's characters and appearances again.");
        if (session.UnusableCharacters is > 0 and var hidden)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"+{hidden}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(hidden == 1 ? "1 more character has no combat gearset." : $"{hidden} more characters have no combat gearset.");
        }

        if (selected == null)
        {
            SettingsGrid.End();
            return;
        }

        var choice = Store.ChoiceFor(selected);
        var set = OfflineLoadouts.ChosenGearset(selected, choice);
        SettingsGrid.Row("Gearset:");
        SettingsGrid.ItemWidth(FieldWidth);
        if (ImGui.BeginCombo("##offline-gearset", set != null ? GearsetLabel(set) : "None"))
        {
            foreach (var gearset in OfflineLoadouts.CombatGearsets(selected))
            {
                if (ImGui.Selectable(GearsetLabel(gearset), gearset.Id == set?.Id))
                {
                    choice.GearsetId = gearset.Id;
                    Changed(selected);
                }
            }
            ImGui.EndCombo();
        }

        SettingsGrid.Row("Name:");
        var name = choice.Name;
        SettingsGrid.ItemWidth(FieldWidth);
        if (ImGui.InputTextWithHint("##offline-name", OfflineLoadouts.DefaultName, ref name, OfflineLoadouts.MaxNameLength))
        {
            choice.Name = name;
            session.SelectionChanged();
        }
        if (ImGui.IsItemDeactivatedAfterEdit()) Changed(selected);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("The game doesn't keep character names on this PC.");

        var appearances = session.Appearances;
        var appearance = OfflineLoadouts.ChosenAppearance(choice, appearances);
        SettingsGrid.Row("Appearance:");
        SettingsGrid.ItemWidth(FieldWidth);
        if (ImGui.BeginCombo("##offline-appearance", appearance != null ? OfflineLoadouts.Describe(appearance) : BuiltInLook))
        {
            if (ImGui.Selectable(BuiltInLook, appearance == null))
            {
                choice.Appearance = OfflineLoadouts.BuiltInAppearance;
                Changed(selected);
            }
            foreach (var saved in appearances)
            {
                if (ImGui.Selectable($"{OfflineLoadouts.Describe(saved)}##{saved.File}", saved.File == appearance?.File))
                {
                    choice.Appearance = saved.File;
                    Changed(selected);
                }
            }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(appearances.Count == 0
                ? "Save appearance data from the character creator to look like your character."
                : "Appearance data saved from the character creator. The game doesn't keep your character's own look on this PC.");

        SettingsGrid.Row("");
        var useConfig = Store.UseConfig;
        if (ImGui.Checkbox("Use my settings##offline-config", ref useConfig))
        {
            Store.UseConfig = useConfig;
            session.SelectionChanged();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("This character's hotbars, keybinds, HUD layout and settings, read without changing them.");
        SettingsGrid.End();
    }

    // Like a scenario's Start; under it, what is missing or what you'll play.
    private void DrawEnter()
    {
        var starting = session.Phase == OfflinePhase.Starting;
        var reason = starting ? null : session.UnavailableReason();
        ImGui.BeginDisabled(starting || reason != null);
        MainWindow.PushSemanticColors(MainWindow.StartColor);
        var enter = ImGui.Button("Enter inn###offline-enter", new Vector2(140f, 32f) * ImGuiHelpers.GlobalScale);
        MainWindow.PopSemanticColors();
        ImGui.EndDisabled();
        ImGui.PushTextWrapPos(0f);
        if (starting) ImGui.TextUnformatted(session.Status);
        else if ((reason ?? session.SelectedLoadout(out _)?.Summary) is { } hint) ImGui.TextDisabled(hint);
        if (!starting && session.StillConnected) ImGui.TextDisabled("Closes the game's data-center connection first.");
        if (!starting && session.TurnsOff.Count > 0) ImGui.TextDisabled($"Turns off {string.Join(", ", session.TurnsOff)} until you leave.");
        ImGui.PopTextWrapPos();
        if (enter) session.Start();
    }

    private void DrawLeave()
    {
        MainWindow.PushSemanticColors(MainWindow.StopColor);
        if (ImGui.Button("Leave###offline-leave")) ImGui.OpenPopup(LeaveMenu);
        MainWindow.PopSemanticColors();
        if (!ImGui.BeginPopup(LeaveMenu)) return;

        var width = new Vector2(MathF.Max(ImGui.CalcTextSize(RestartLabel).X, ImGui.CalcTextSize(ReturnLabel).X) + ImGui.GetStyle().FramePadding.X * 2f, 0f);
        MainWindow.PushSemanticColors(MainWindow.StartColor);
        var restart = ImGui.Button(RestartLabel, width);
        MainWindow.PopSemanticColors();
        ImGui.SameLine();
        ImGui.TextDisabled("Recommended: exactly as if the sim never ran");

        var blocked = session.ReturnBlockedReason();
        ImGui.BeginDisabled(blocked != null);
        if (ImGui.Button(ReturnLabel, width))
        {
            session.RequestReturn(exitAfterwards: false);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        if (blocked != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(blocked);
        ImGui.SameLine();
        ImGui.TextDisabled("Reverts every known memory change to vanilla");
        ImGui.EndPopup();

        if (restart) OfflineSession.RestartGame();
    }

    // Setting a character up also keeps it selected, rather than whichever was played last.
    private void Changed(OfflineLocalCharacter character)
    {
        Store.Select(character.Key);
        session.SelectionChanged();
    }

    private static void Wrapped(Vector4 color, string text)
    {
        ImGui.PushTextWrapPos(0f);
        ImGui.TextColored(color, text);
        ImGui.PopTextWrapPos();
    }

    // The game keeps no names on this PC, so an unnamed character goes by when it was last played.
    private string CharacterLabel(OfflineLocalCharacter character)
    {
        var name = Store.NameOf(character).Trim();
        var jobs = jobLists.GetValue(character, JobList);
        return $"{(name.Length > 0 ? name : $"Played {character.LastPlayed.ToLocalTime():g}")} · {jobs}";
    }

    private static string JobList(OfflineLocalCharacter character)
    {
        var jobs = OfflineLoadouts.CombatGearsets(character).Select(g => OfflineLoadouts.JobAbbreviation(g.ClassJobId)).Distinct().ToList();
        return string.Join(" ", jobs.Take(JobsInLabel)) + (jobs.Count > JobsInLabel ? $" +{jobs.Count - JobsInLabel}" : "");
    }

    private static string GearsetLabel(OfflineGearset set)
        => $"{set.Id + 1}. {set.Name} · {OfflineLoadouts.JobAbbreviation(set.ClassJobId)} i{set.ItemLevel}";
}
