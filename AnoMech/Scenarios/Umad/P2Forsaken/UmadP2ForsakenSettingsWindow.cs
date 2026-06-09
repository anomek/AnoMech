using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P2Forsaken;

// ImGui panel rendered in the main window's "Scenario config" pane when this
// scenario is active. Owns the StateOverrides instance and writes user choices
// into it. See TopP5DeltaSettingsWindow for the canonical shape.
public sealed class UmadP2ForsakenSettingsWindow
{
    private static readonly (string Label, PartyRole Role)[] SupportRoles =
    [
        ("MT", PartyRole.MainTank), ("OT", PartyRole.OffTank),
        ("H1", PartyRole.RegenHealer), ("H2", PartyRole.ShieldHealer),
    ];

    private static readonly (string Label, PartyRole Role)[] DpsRoles =
    [
        ("M1", PartyRole.MeleeDpsA), ("M2", PartyRole.MeleeDpsB),
        ("R1", PartyRole.PhysRangedDps), ("R2", PartyRole.CasterDps),
    ];

    public UmadP2ForsakenStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##umadp2forsaken"))
        {
#if DEBUG
            DrawFirstEndAttack();
            DrawNewNorth();
#endif
            DrawSupportLockon();
            DrawSupportStack();
            DrawDpsStack();
            SettingsGrid.End();
        }
    }

#if DEBUG
    private void DrawFirstEndAttack()
    {
        var v = Overrides.FirstEndAttack;
        SettingsGrid.Row("First End:");
        if (ImGui.RadioButton("Auto##firstend",   v == null))                 Overrides.FirstEndAttack = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Future##firstend", v == EndAttack.FuturesEnd)) Overrides.FirstEndAttack = EndAttack.FuturesEnd;
        ImGui.SameLine();
        if (ImGui.RadioButton("Past##firstend",   v == EndAttack.PastsEnd))   Overrides.FirstEndAttack = EndAttack.PastsEnd;
    }

    private void DrawNewNorth()
    {
        var v = Overrides.NewNorth;
        SettingsGrid.Row("New North:");
        if (ImGui.RadioButton("Auto##newnorth", v == null)) Overrides.NewNorth = null;
        foreach (var d in Direction.All)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}##newnorth", v == d)) Overrides.NewNorth = d;
        }
    }
#endif

    private void DrawSupportLockon()
    {
        var v = Overrides.SupportLockon;
        SettingsGrid.Row("Supports:");
        if (ImGui.RadioButton("Auto##supportlockon",    v == null))                   Overrides.SupportLockon = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Chariot##supportlockon", v == LockonId.ForsakenChariot)) Overrides.SupportLockon = LockonId.ForsakenChariot;
        ImGui.SameLine();
        if (ImGui.RadioButton("Cone##supportlockon",    v == LockonId.ForsakenCone))    Overrides.SupportLockon = LockonId.ForsakenCone;
    }

    private void DrawSupportStack()
    {
        var v = Overrides.SupportStackRole;
        SettingsGrid.Row("Support Stack:");
        if (ImGui.RadioButton("Auto##supportstack", v == null)) Overrides.SupportStackRole = null;
        foreach (var (label, role) in SupportRoles)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{label}##supportstack", v == role)) Overrides.SupportStackRole = role;
        }
    }

    private void DrawDpsStack()
    {
        var v = Overrides.DpsStackRole;
        SettingsGrid.Row("Dps Stack:");
        if (ImGui.RadioButton("Auto##dpsstack", v == null)) Overrides.DpsStackRole = null;
        foreach (var (label, role) in DpsRoles)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{label}##dpsstack", v == role)) Overrides.DpsStackRole = role;
        }
    }

    private void ResetAll()
    {
#if DEBUG
        Overrides.FirstEndAttack = null;
        Overrides.NewNorth = null;
#endif
        Overrides.SupportLockon = null;
        Overrides.SupportStackRole = null;
        Overrides.DpsStackRole = null;
    }
}
