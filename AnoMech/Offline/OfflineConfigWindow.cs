using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace AnoMech.Offline;

internal sealed class OfflineConfigWindow : Window
{
    private const float LabelWidth = 90f;
    private const float FieldWidth = 340f;
    private const int JobsInLabel = 5;
    private const string BuiltInLook = "Default Midlander";

    private readonly OfflineSession session;
    private readonly ConditionalWeakTable<OfflineLocalCharacter, string> jobLists = new();

    public OfflineConfigWindow(OfflineSession session) : base("AnoMech Offline config###AnoMechOfflineConfig")
    {
        this.session = session;
        Flags |= ImGuiWindowFlags.AlwaysAutoResize;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(440, 0), MaximumSize = new Vector2(720, float.MaxValue) };
    }

    private OfflineStore Store => session.Store;

    public override void OnOpen() => session.Refresh();

    public override void Draw()
    {
        ImGui.PushTextWrapPos(0f);
        ImGui.TextUnformatted("Practice scenarios without logging in.");
        ImGui.TextDisabled("Offline mode starts from the title screen with one of your characters' gearsets, and its hotbars, keybinds, HUD layout "
                           + "and settings as saved on this PC. Nothing can write to the game's folder while it runs, and returning to the title screen "
                           + "puts everything back, so logging in afterwards is exactly as if offline mode never ran. Anything changed offline is forgotten.");
        ImGui.TextDisabled(OfflineSession.LoginPrecaution);
        if (Store.LoadProblem is { } problem) ImGui.TextColored(ImGuiColors.DalamudRed, problem);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();

        ImGui.BeginDisabled(session.Busy);
        DrawChoices();
        ImGui.EndDisabled();

        if (session.OtherPlugins.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(ImGuiColors.DalamudOrange, session.OtherPlugins.Count == 1 ? "1 other plugin stays loaded offline." : $"{session.OtherPlugins.Count} other plugins stay loaded offline.");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("They see the offline character and may keep data about it in their own settings, which offline mode can't put back.\n"
                                 + "Nothing they do can change the game's own files while offline mode runs,\n"
                                 + "and if one tries to log in or reach a server, the game closes as a precaution.\n\n"
                                 + string.Join("\n", session.OtherPlugins));
        }

        if (session.ShowsControls)
        {
            ImGui.Spacing();
            ImGui.Separator();
            session.DrawControls(withSummary: false);
        }
    }

    private void DrawChoices()
    {
        var selected = session.SelectedCharacter;
        Label("Character");
        ImGui.SetNextItemWidth(FieldWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##offlinecharacter", selected != null ? CharacterLabel(selected) : "None available"))
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
            ImGui.SetTooltip("The characters this PC has played with a combat gearset, most recent first.");
        ImGui.SameLine();
        if (ImGui.Button("Refresh")) session.Refresh();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Read the characters and saved appearances from this PC again.");
        if (session.UnusableCharacters is > 0 and var hidden)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled($"(+{hidden} without one)");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(hidden == 1
                    ? "1 other character on this PC has no combat gearset offline mode can read."
                    : $"{hidden} other characters on this PC have no combat gearset offline mode can read.");
        }

        if (selected == null)
        {
            session.SelectedLoadout(out var none);
            ImGui.PushTextWrapPos(0f);
            ImGui.TextColored(ImGuiColors.DalamudOrange, none);
            ImGui.PopTextWrapPos();
            return;
        }

        var choice = Store.ChoiceFor(selected);
        var set = OfflineLoadouts.ChosenGearset(selected, choice);
        Label("Gearset");
        ImGui.SetNextItemWidth(FieldWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##offlinegearset", set != null ? GearsetLabel(selected, set) : "No gearsets for a combat job"))
        {
            foreach (var gearset in OfflineLoadouts.CombatGearsets(selected))
            {
                if (ImGui.Selectable(GearsetLabel(selected, gearset), gearset.Id == set?.Id))
                {
                    choice.GearsetId = gearset.Id;
                    Changed(selected);
                }
            }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("The job, gear, materia, glamour and dyes of one of this character's gearsets.");

        Label("Name");
        var name = choice.Name;
        ImGui.SetNextItemWidth(FieldWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint("##offlinename", OfflineLoadouts.DefaultName, ref name, OfflineLoadouts.MaxNameLength))
        {
            choice.Name = name;
            session.SelectionChanged();
        }
        if (ImGui.IsItemDeactivatedAfterEdit()) Changed(selected);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("The name your character shows offline. The game doesn't keep character names on this PC.");

        var appearances = session.Appearances;
        var appearance = OfflineLoadouts.ChosenAppearance(choice, appearances);
        Label("Appearance");
        ImGui.SetNextItemWidth(FieldWidth * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##offlineappearance", appearance != null ? OfflineLoadouts.Describe(appearance) : BuiltInLook))
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
                ? "No appearance data is saved on this PC. Save some from the character creator or an aesthetician to look like your character."
                : "Appearance data saved from the character creator or an aesthetician. The game doesn't keep your character's own look on this PC.");

        var useConfig = Store.UseConfig;
        if (ImGui.Checkbox("Use my hotbars, keybinds, HUD layout and settings", ref useConfig))
        {
            Store.UseConfig = useConfig;
            session.SelectionChanged();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Loads this character's own files into the offline session without changing them.\nTurn off to start with the game's defaults.");

        ImGui.Spacing();
        var loadout = session.SelectedLoadout(out var error);
        ImGui.PushTextWrapPos(0f);
        if (loadout == null) ImGui.TextColored(ImGuiColors.DalamudOrange, error);
        else ImGui.TextUnformatted($"{loadout.Name}: {loadout.Summary}");
        if (session.Phase == OfflinePhase.InWorld)
            ImGui.TextDisabled("Changes apply the next time you enter offline mode.");
        ImGui.PopTextWrapPos();
    }

    // Setting a character up also keeps it selected, rather than whichever was played last.
    private void Changed(OfflineLocalCharacter character)
    {
        Store.Select(character.Key);
        session.SelectionChanged();
    }

    private static void Label(string text)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);
        ImGui.SameLine(LabelWidth * ImGuiHelpers.GlobalScale);
    }

    private string CharacterLabel(OfflineLocalCharacter character)
    {
        var name = Store.NameOf(character).Trim();
        var played = $"{character.LastPlayed.ToLocalTime():g}";
        var jobs = jobLists.GetValue(character, JobList);
        return name.Length > 0 ? $"{name}, last played {played}: {jobs}" : $"Last played {played}: {jobs}";
    }

    private static string JobList(OfflineLocalCharacter character)
    {
        var jobs = OfflineLoadouts.CombatGearsets(character).Select(g => OfflineLoadouts.JobAbbreviation(g.ClassJobId)).Distinct().ToList();
        if (jobs.Count == 0) return "no usable gearsets";
        return string.Join(" ", jobs.Take(JobsInLabel)) + (jobs.Count > JobsInLabel ? $" +{jobs.Count - JobsInLabel}" : "");
    }

    private static string GearsetLabel(OfflineLocalCharacter character, OfflineGearset set)
        => $"{set.Id + 1}: {set.Name} ({OfflineLoadouts.JobAbbreviation(set.ClassJobId)}, i{set.ItemLevel}{(set.Id == character.CurrentGearset ? ", last worn" : "")})";
}
