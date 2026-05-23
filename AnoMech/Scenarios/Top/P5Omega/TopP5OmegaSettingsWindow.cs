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
        DrawExtraDynamis();
        DrawHelloWorldOrder();
        DrawHelloWorldType();
        DrawForceButtons();
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
        Overrides.ExtraDynamis = null;
        Overrides.HelloWorldOrder = HelloWorldOrderOption.Auto;
        Overrides.HelloWorldType = HelloWorldTypeOption.Auto;
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

    private void DrawExtraDynamis()
    {
        var v = Overrides.ExtraDynamis;
        ImGui.TextUnformatted("Extra dynamis stack:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##dyn", v == null))  Overrides.ExtraDynamis = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("No##dyn",   v == false)) Overrides.ExtraDynamis = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##dyn",  v == true))  Overrides.ExtraDynamis = true;
    }

    private void DrawHelloWorldOrder()
    {
        var v = Overrides.HelloWorldOrder;
        ImGui.TextUnformatted("Hello World order:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##hwo",   v == HelloWorldOrderOption.Auto))   Overrides.HelloWorldOrder = HelloWorldOrderOption.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("Any##hwo",    v == HelloWorldOrderOption.Any))    Overrides.HelloWorldOrder = HelloWorldOrderOption.Any;
        ImGui.SameLine();
        if (ImGui.RadioButton("First##hwo",  v == HelloWorldOrderOption.First))  Overrides.HelloWorldOrder = HelloWorldOrderOption.First;
        ImGui.SameLine();
        if (ImGui.RadioButton("Second##hwo", v == HelloWorldOrderOption.Second)) Overrides.HelloWorldOrder = HelloWorldOrderOption.Second;
        ImGui.SameLine();
        if (ImGui.RadioButton("None##hwo",   v == HelloWorldOrderOption.None))   Overrides.HelloWorldOrder = HelloWorldOrderOption.None;
    }

    private void DrawHelloWorldType()
    {
        var v = Overrides.HelloWorldType;
        ImGui.TextUnformatted("Hello World type:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto##hwt", v == HelloWorldTypeOption.Auto)) Overrides.HelloWorldType = HelloWorldTypeOption.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("Near##hwt", v == HelloWorldTypeOption.Near)) Overrides.HelloWorldType = HelloWorldTypeOption.Near;
        ImGui.SameLine();
        if (ImGui.RadioButton("Far##hwt",  v == HelloWorldTypeOption.Far))  Overrides.HelloWorldType = HelloWorldTypeOption.Far;
    }

    private void DrawForceButtons()
    {
        if (ImGui.Button("Force take monitor"))
        {
            Overrides.ExtraDynamis = true;
            Overrides.HelloWorldOrder = HelloWorldOrderOption.Second;
        }
        ImGui.SameLine();
        if (ImGui.Button("Force take tether"))
        {
            Overrides.ExtraDynamis = true;
            Overrides.HelloWorldOrder = HelloWorldOrderOption.First;
        }
    }
}
