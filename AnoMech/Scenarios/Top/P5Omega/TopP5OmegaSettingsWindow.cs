using System;
using AnoMech.Scenarios.Top;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios;

public sealed class TopP5OmegaSettingsWindow
{
    public TopP5OmegaStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) ResetAll();
        DrawAttack("First F attack:",  "1f", OmegaAttack.Legs,   "Legs",   OmegaAttack.Staff,  "Staff",
                   () => Overrides.FirstFAttack,  v => Overrides.FirstFAttack  = v);
        DrawAttack("First M attack:",  "1m", OmegaAttack.Sword,  "Sword",  OmegaAttack.Shield, "Shield",
                   () => Overrides.FirstMAttack,  v => Overrides.FirstMAttack  = v);
        DrawAttack("Second F attack:", "2f", OmegaAttack.Legs,   "Legs",   OmegaAttack.Staff,  "Staff",
                   () => Overrides.SecondFAttack, v => Overrides.SecondFAttack = v);
        DrawAttack("Second M attack:", "2m", OmegaAttack.Sword,  "Sword",  OmegaAttack.Shield, "Shield",
                   () => Overrides.SecondMAttack, v => Overrides.SecondMAttack = v);
        DrawWaveCannon();
        DrawMonitorSide();
        DrawBeetleSpawn();
    }

    private void ResetAll()
    {
        Overrides.FirstFAttack = null;
        Overrides.FirstMAttack = null;
        Overrides.SecondFAttack = null;
        Overrides.SecondMAttack = null;
        Overrides.FirstWaveCannonFront = null;
        Overrides.MonitorSide = null;
        Overrides.BettleSpawnDirection = null;
    }

    private static void DrawAttack(string label, string suffix,
                                   OmegaAttack optionA, string nameA,
                                   OmegaAttack optionB, string nameB,
                                   Func<OmegaAttack?> get, Action<OmegaAttack?> set)
    {
        var v = get();
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        if (ImGui.RadioButton($"Auto##{suffix}",    v == null))     set(null);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{nameA}##{suffix}", v == optionA))  set(optionA);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{nameB}##{suffix}", v == optionB))  set(optionB);
    }

    private void DrawWaveCannon()
    {
        var v = Overrides.FirstWaveCannonFront;
        ImGui.TextUnformatted("Diffuse Wave Cannon:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##wc",       v == null))  Overrides.FirstWaveCannonFront = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Horizontal##wc", v == false)) Overrides.FirstWaveCannonFront = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("Vertical##wc",   v == true))  Overrides.FirstWaveCannonFront = true;
    }

    private void DrawMonitorSide()
    {
        var v = Overrides.MonitorSide;
        ImGui.TextUnformatted("Monitor side:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##mon",  v == null))               Overrides.MonitorSide = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Left##mon",  v == MonitorSide.Left))   Overrides.MonitorSide = MonitorSide.Left;
        ImGui.SameLine();
        if (ImGui.RadioButton("Right##mon", v == MonitorSide.Right))  Overrides.MonitorSide = MonitorSide.Right;
    }

    private void DrawBeetleSpawn()
    {
        ImGui.TextUnformatted("Beetle spawn:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##beetle", Overrides.BettleSpawnDirection == null)) Overrides.BettleSpawnDirection = null;
        foreach (var d in EightWayDirection.Cardinal)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name}##beetle", Overrides.BettleSpawnDirection == d)) Overrides.BettleSpawnDirection = d;
        }
    }
}
