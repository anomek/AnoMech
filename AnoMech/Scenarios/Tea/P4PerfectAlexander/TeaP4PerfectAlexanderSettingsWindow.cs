using System.Linq;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Tea.P4PerfectAlexander;

public sealed class TeaP4PerfectAlexanderSettingsWindow
{
    public TeaP4PerfectAlexanderStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    private static readonly string[] PracticeLabels = TeaP4PerfectAlexanderPractice.All.Select(e => e.Label).ToArray();
    private static readonly string[] OrdainedLabels = ["Random", "Motion first", "Stillness first"];
    private static readonly string[] OpticalLabels = ["Random", "Spread first", "Stack first"];
    private static readonly string[] SafeCloneLabels = ["Random", "Second Alexander", "Third Alexander"];
    private static readonly string[] RequirementLabels = ["Random", "Motion", "Stillness"];
    private static readonly string[] BetaOpticalLabels = ["Random", "Stack", "Spread"];
    private static readonly string[] DonutLabels = ["Random", "East", "South", "West"];
    private static readonly int[] DonutClones = [0, 1, 3];
    private static readonly string[] FinalWordLabels = ["Random", "Light beacon", "Light follower", "Dark beacon", "Dark follower"];
    private static readonly string[] AlphaLabels = ["Random", "Shared Sentence", "Defamation", "Aggravated Assault", "Nothing"];
    private static readonly string[] BetaLabels =
        ["Random", "Light, far tether", "Dark, far tether", "Light, near tether", "Dark, near tether", "Light, shared", "Dark, no tether",
         "Light beacon", "Dark beacon"];
    private static readonly string[] YesNoLabels = ["Random", "Yes", "No"];

    // Solo, the player's own rows sit in this panel; a host assigns seats from the Multiplayer window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            Overrides.FinalWordMotionFirst = null;
            Overrides.OpticalSpreadFirst = null;
            Overrides.AlphaSafeClone = null;
            Overrides.AlphaFirstMotion = null;
            Overrides.AlphaSecondMotion = null;
            Overrides.BetaOpticalStack = null;
            Overrides.BetaOpticalClone = null;
            Overrides.BetaDonutClone = null;
            if (solo)
            {
                Overrides.FinalWord.Mine = null;
                Overrides.OpticalStack.Mine = null;
                Overrides.Alpha.Mine = null;
                Overrides.Beta.Mine = null;
            }
        }

        if (!SettingsGrid.Begin("##teap4")) return;
        SettingsGrid.Row("Practice:");
        var practice = (int)Overrides.Practice;
        SettingsGrid.ItemWidth(220);
        if (ImGui.Combo("##teap4practice", ref practice, PracticeLabels, PracticeLabels.Length) && practice >= 0 && practice < PracticeLabels.Length)
            Overrides.Practice = (P4Practice)practice;
        SettingsGrid.Row("");
        ImGui.TextDisabled(TeaP4PerfectAlexanderPractice.Of(Overrides.Practice).Covers);

        Overrides.FinalWordMotionFirst = BoolRow("The Final Word:", "##teap4ordained", OrdainedLabels, Overrides.FinalWordMotionFirst);
        Overrides.OpticalSpreadFirst = BoolRow("Optical Sight:", "##teap4optical", OpticalLabels, Overrides.OpticalSpreadFirst);

        SettingsGrid.Row("Fate Calibration α safe:");
        var safe = Overrides.AlphaSafeClone ?? 0;
        SettingsGrid.ItemWidth(160);
        if (ImGui.Combo("##teap4safe", ref safe, SafeCloneLabels, SafeCloneLabels.Length))
            Overrides.AlphaSafeClone = safe == 0 ? null : safe;
        Overrides.AlphaFirstMotion = BoolRow("Fate Calibration α first:", "##teap4alpha1", RequirementLabels, Overrides.AlphaFirstMotion);
        Overrides.AlphaSecondMotion = BoolRow("Fate Calibration α second:", "##teap4alpha2", RequirementLabels, Overrides.AlphaSecondMotion);
        Overrides.BetaOpticalStack = BoolRow("Fate Calibration β Optical:", "##teap4betaoptical", BetaOpticalLabels, Overrides.BetaOpticalStack);

        SettingsGrid.Row("Fate Calibration β Optical on:");
        var optical = Overrides.BetaOpticalClone ?? 0;
        SettingsGrid.ItemWidth(160);
        if (ImGui.Combo("##teap4betaclone", ref optical, SafeCloneLabels, SafeCloneLabels.Length))
            Overrides.BetaOpticalClone = optical == 0 ? null : optical;

        SettingsGrid.Row("Radiant Sacrament:");
        var donut = Overrides.BetaDonutClone is { } clone ? System.Array.IndexOf(DonutClones, clone) + 1 : 0;
        SettingsGrid.ItemWidth(160);
        if (ImGui.Combo("##teap4donut", ref donut, DonutLabels, DonutLabels.Length))
            Overrides.BetaDonutClone = donut == 0 ? null : DonutClones[donut - 1];

        if (solo) DrawPlayerRows();
        SettingsGrid.End();
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.FinalWord.Clear();
            Overrides.OpticalStack.Clear();
            Overrides.Alpha.Clear();
            Overrides.Beta.Clear();
        }
        if (SettingsGrid.Begin("##teap4players"))
        {
            editingSeat = SettingsGrid.SeatRow("##teap4seat", editingSeat);
            DrawPlayerRows();
            SettingsGrid.ForcedRecapRow("Final Word debuffs set:", Overrides.FinalWord);
            SettingsGrid.ForcedRecapRow("Fate Calibration α set:", Overrides.Alpha);
            SettingsGrid.ForcedRecapRow("Fate Calibration β set:", Overrides.Beta);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
    }

    private void DrawPlayerRows()
    {
        var you = PerRole.SeatsActive ? "" : "Your ";
        EnumRow($"{you}Final Word debuff:", "##teap4fw", FinalWordLabels, Overrides.FinalWord);
        YesNoRow($"{you}Optical Sight stack:", "##teap4stack", Overrides.OpticalStack);
        EnumRow($"{you}Fate Calibration α:", "##teap4alpha", AlphaLabels, Overrides.Alpha);
        EnumRow($"{you}Fate Calibration β:", "##teap4beta", BetaLabels, Overrides.Beta);
    }

    private void EnumRow<T>(string label, string id, string[] labels, PerRoleSetting<T> setting) where T : struct, System.Enum
    {
        SettingsGrid.Row(label);
        var index = setting.Effective(editingSeat) is { } value ? System.Convert.ToInt32(value) + 1 : 0;
        SettingsGrid.ItemWidth(180);
        if (ImGui.Combo(id, ref index, labels, labels.Length))
            setting.Set(editingSeat, index == 0 ? null : (T)System.Enum.ToObject(typeof(T), index - 1));
    }

    private void YesNoRow(string label, string id, PerRoleSetting<bool> setting)
    {
        SettingsGrid.Row(label);
        var index = setting.Effective(editingSeat) is { } yes ? yes ? 1 : 2 : 0;
        SettingsGrid.ItemWidth(140);
        if (ImGui.Combo(id, ref index, YesNoLabels, YesNoLabels.Length))
            setting.Set(editingSeat, index == 0 ? null : index == 1);
    }

    // Labels: random, the true value, the false one.
    private static bool? BoolRow(string label, string id, string[] labels, bool? value)
    {
        SettingsGrid.Row(label);
        var index = value is { } v ? v ? 1 : 2 : 0;
        SettingsGrid.ItemWidth(160);
        if (ImGui.Combo(id, ref index, labels, labels.Length)) return index == 0 ? null : index == 1;
        return value;
    }
}
