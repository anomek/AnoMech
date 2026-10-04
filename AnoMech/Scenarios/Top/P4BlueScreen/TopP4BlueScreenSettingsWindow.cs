using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P4BlueScreen;

public sealed class TopP4BlueScreenSettingsWindow
{
    public TopP4BlueScreenStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.FirstStacks = null;
            Overrides.SecondStacks = null;
            Overrides.ThirdStacks = null;
            Overrides.MeleeLimitBreakBy = null;
            Overrides.MeleeLimitBreakTiming = null;
        }
        if (SettingsGrid.Begin("##p4bluescreen"))
        {
            Overrides.FirstStacks = StacksRow("Wave Cannon 1 stacks:", "##p4stacks1", Overrides.FirstStacks);
            Overrides.SecondStacks = StacksRow("Wave Cannon 2 stacks:", "##p4stacks2", Overrides.SecondStacks);
            Overrides.ThirdStacks = StacksRow("Wave Cannon 3 stacks:", "##p4stacks3", Overrides.ThirdStacks);
            SettingsGrid.Row("Melee LB3:");
            var melee = Overrides.MeleeLimitBreakBy;
            if (ImGui.RadioButton("Auto (NIN = MNK > DRG > VPR > SAM > RPR)##p4lb", melee == null)) Overrides.MeleeLimitBreakBy = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("M1##p4lb", melee == PartyRole.MeleeDpsA)) Overrides.MeleeLimitBreakBy = PartyRole.MeleeDpsA;
            ImGui.SameLine();
            if (ImGui.RadioButton("M2##p4lb", melee == PartyRole.MeleeDpsB)) Overrides.MeleeLimitBreakBy = PartyRole.MeleeDpsB;
            SettingsGrid.Row("Melee LB3 timing:");
            var timing = Overrides.MeleeLimitBreakTiming;
            if (ImGui.RadioButton("Phase start (guide)##p4lbtime", timing is null or LimitBreakTiming.PhaseStart)) Overrides.MeleeLimitBreakTiming = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("During Blue Screen##p4lbtime", timing == LimitBreakTiming.BlueScreen)) Overrides.MeleeLimitBreakTiming = LimitBreakTiming.BlueScreen;
            ImGui.SameLine();
            if (ImGui.RadioButton("Off##p4lbtime", timing == LimitBreakTiming.Off)) Overrides.MeleeLimitBreakTiming = LimitBreakTiming.Off;
            SettingsGrid.End();
        }
    }

    private static StackSplit? StacksRow(string label, string id, StackSplit? value)
    {
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"Auto{id}", value == null)) value = null;
        ImGui.SameLine();
        if (ImGui.RadioButton($"One per side{id}", value == StackSplit.OnePerSide)) value = StackSplit.OnePerSide;
        ImGui.SameLine();
        if (ImGui.RadioButton($"Both on one side (flex){id}", value == StackSplit.OneSide)) value = StackSplit.OneSide;
        return value;
    }
}
