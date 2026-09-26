using System;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P5Omega;

public sealed class TopP5OmegaSettingsWindow
{
    public TopP5OmegaStateOverrides Overrides { get; } = new();

    // Which seat the per-player rows are showing. UI state only; never broadcast.
    private PartyRole editingSeat = PartyRole.MainTank;

    // Solo, the player's own picks sit in this panel; a host assigns seats from the Multiplayer
    // window.
    public void Draw()
    {
        var solo = !PerRole.SeatsActive;
        if (ImGui.Button("Auto"))
        {
            ResetFight();
            if (solo) ResetMine();
        }
        if (SettingsGrid.Begin("##p5omega"))
        {
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
            if (solo)
            {
                DrawExtraDynamis();
                DrawHelloWorldOrder();
                DrawHelloWorldType();
            }
            SettingsGrid.End();
        }
        if (solo) DrawForceButtons();
    }

    public void DrawPerPlayer()
    {
        if (ImGui.Button("Auto")) ResetPerPlayer();
        if (SettingsGrid.Begin("##p5omegaplayers"))
        {
            editingSeat = SettingsGrid.SeatRow("##omegaseat", editingSeat);
            DrawExtraDynamis();
            DrawHelloWorldOrder();
            DrawHelloWorldType();
            SettingsGrid.ForcedRecapRow("Hello World set:", Overrides.HelloWorldOrder);
            SettingsGrid.ForcedRecapRow("Extra dynamis set:", Overrides.ExtraDynamis);
            SettingsGrid.End();
        }
        SettingsGrid.ConflictRows(Overrides.Validate());
        DrawForceButtons();
    }

    private void ResetFight()
    {
        Overrides.FirstFAttack = null;
        Overrides.FirstMAttack = null;
        Overrides.SecondFAttack = null;
        Overrides.SecondMAttack = null;
        Overrides.FirstWaveCannonFront = null;
        Overrides.MonitorSide = null;
        Overrides.BettleSpawnDirection = null;
    }

    private void ResetPerPlayer()
    {
        Overrides.ExtraDynamis.Clear();
        Overrides.HelloWorldOrder.Clear();
        Overrides.HelloWorldType.Clear();
    }

    // Solo's own picks only: a host's seat assignments are kept apart.
    private void ResetMine()
    {
        Overrides.ExtraDynamis.Mine = null;
        Overrides.HelloWorldOrder.Mine = null;
        Overrides.HelloWorldType.Mine = null;
    }

    private static void DrawAttack(string label, string suffix,
                                   OmegaAttack optionA, string nameA,
                                   OmegaAttack optionB, string nameB,
                                   Func<OmegaAttack?> get, Action<OmegaAttack?> set)
    {
        var v = get();
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"Auto##{suffix}",    v == null))     set(null);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{nameA}##{suffix}", v == optionA))  set(optionA);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{nameB}##{suffix}", v == optionB))  set(optionB);
    }

    private void DrawWaveCannon()
    {
        var v = Overrides.FirstWaveCannonFront;
        SettingsGrid.Row("Diffuse Wave Cannon:");
        if (ImGui.RadioButton("Auto##wc",       v == null))  Overrides.FirstWaveCannonFront = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Horizontal##wc", v == false)) Overrides.FirstWaveCannonFront = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("Vertical##wc",   v == true))  Overrides.FirstWaveCannonFront = true;
    }

    private void DrawMonitorSide()
    {
        var v = Overrides.MonitorSide;
        SettingsGrid.Row("Monitor side:");
        if (ImGui.RadioButton("Auto##mon",  v == null))               Overrides.MonitorSide = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Left##mon",  v == MonitorSide.Left))   Overrides.MonitorSide = MonitorSide.Left;
        ImGui.SameLine();
        if (ImGui.RadioButton("Right##mon", v == MonitorSide.Right))  Overrides.MonitorSide = MonitorSide.Right;
    }

    private void DrawBeetleSpawn()
    {
        SettingsGrid.Row("Beetle spawn:");
        if (ImGui.RadioButton("Auto##beetle", Overrides.BettleSpawnDirection == null)) Overrides.BettleSpawnDirection = null;
        foreach (var d in Direction.Cardinal)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{d.Name()}##beetle", Overrides.BettleSpawnDirection == d)) Overrides.BettleSpawnDirection = d;
        }
    }

    private void DrawExtraDynamis()
    {
        var v = Overrides.ExtraDynamis.Effective(editingSeat);
        SettingsGrid.PlayerRow("extra dynamis stack:");
        if (ImGui.RadioButton("Auto##dyn", v == null))  Overrides.ExtraDynamis.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("No##dyn",   v == false)) Overrides.ExtraDynamis.Set(editingSeat, false);
        ImGui.SameLine();
        if (ImGui.RadioButton("Yes##dyn",  v == true))  Overrides.ExtraDynamis.Set(editingSeat, true);
    }

    private void DrawHelloWorldOrder()
    {
        var v = Overrides.HelloWorldOrder.Effective(editingSeat);
        SettingsGrid.PlayerRow("Hello World order:");
        if (ImGui.RadioButton("Auto##hwo",   v == null))                          SetOrder(null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Any##hwo",    v == HelloWorldOrderOption.Any))     SetOrder(HelloWorldOrderOption.Any);
        ImGui.SameLine();
        if (ImGui.RadioButton("First##hwo",  v == HelloWorldOrderOption.First))   SetOrder(HelloWorldOrderOption.First);
        ImGui.SameLine();
        if (ImGui.RadioButton("Second##hwo", v == HelloWorldOrderOption.Second))  SetOrder(HelloWorldOrderOption.Second);
        ImGui.SameLine();
        if (ImGui.RadioButton("None##hwo",   v == HelloWorldOrderOption.None))    SetOrder(HelloWorldOrderOption.None);
    }

    private void DrawHelloWorldType()
    {
        var v = Overrides.HelloWorldType.Effective(editingSeat);
        SettingsGrid.PlayerRow("Hello World type:");
        if (ImGui.RadioButton("Auto##hwt", v == null))                        Overrides.HelloWorldType.Set(editingSeat, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("Near##hwt", v == HelloWorldTypeOption.Near))   Overrides.HelloWorldType.Set(editingSeat, HelloWorldTypeOption.Near);
        ImGui.SameLine();
        if (ImGui.RadioButton("Far##hwt",  v == HelloWorldTypeOption.Far))    Overrides.HelloWorldType.Set(editingSeat, HelloWorldTypeOption.Far);
    }

    private void SetOrder(HelloWorldOrderOption? option) => Overrides.HelloWorldOrder.Set(editingSeat, option);

    private void DrawForceButtons()
    {
        var who = PerRole.SeatsActive ? $" ({SettingsGrid.RoleLabel(editingSeat)})" : "";
        if (ImGui.Button($"Force take monitor{who}"))
        {
            Overrides.ExtraDynamis.Set(editingSeat, true);
            SetOrder(HelloWorldOrderOption.Second);
        }
        ImGui.SameLine();
        if (ImGui.Button($"Force take tether{who}"))
        {
            Overrides.ExtraDynamis.Set(editingSeat, true);
            SetOrder(HelloWorldOrderOption.First);
        }
    }
}
