using System;
using AnoMech.Core.SimObjects;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P1TeleTrouncing;

public sealed class UmadP1TeleTrouncingSettingsWindow
{
    public UmadP1TeleTrouncingStateOverrides Overrides { get; } = new();

#if DEBUG
    // Index-aligned with PropBeatMode and ArrowSoakMode.
    private static readonly string[] BeatModeLabels = ["ActorControl 413", "PlayAnimation", "SetSharedTimelineState"];
    private static readonly string[] SoakModeLabels = ["ActorControl 106", "SetSharedTimelineState", "Despawn"];
#endif

    // fireAppear/fireWindUp: fire the statue beats right now (null while no run is active), so
    // the props can be exercised without waiting out the timeline each time.
    public void Draw(Action? fireAppear = null, Action? fireWindUp = null)
    {
        if (ImGui.Button("Auto")) ResetAll();
        if (SettingsGrid.Begin("##p1teletrouncing"))
        {
            SettingsGrid.FightOnlyNote();
            DrawDifferentArrows();
            TriStateRow("Gaze:", "gaze", "Inverted", "Normal", Overrides.GazeInverted, v => Overrides.GazeInverted = v);
            TriStateRow("Fire:", "fire", "Stack", "Spread", Overrides.FireIsStack, v => Overrides.FireIsStack = v);
            TriStateRow("Fire orb:", "firelie", "Lie", "Truth", Overrides.FireIsLie, v => Overrides.FireIsLie = v);
            TriStateRow("Thunder orb:", "thunlie", "Lie", "Truth", Overrides.ThunderIsLie, v => Overrides.ThunderIsLie = v);
            TriStateRow("Thunder flip:", "thunflip", "Flipped", "Normal", Overrides.ThunderOrientationFlipped, v => Overrides.ThunderOrientationFlipped = v);
            DrawThunderOffset();
#if DEBUG
            DrawPropKnobs();
            DrawPropBeatRow(fireAppear, fireWindUp);
            DrawArrowRows();
            DrawHazeRow();
#endif
            SettingsGrid.End();
        }
    }

#if DEBUG
    private void DrawPropBeatRow(Action? fireAppear, Action? fireWindUp)
    {
        SettingsGrid.Row("Statue beats (debug):");
        var modeIdx = (int)Overrides.PropsBeatMode;
        SettingsGrid.ItemWidth(170);
        if (ImGui.Combo("##propbeatmode", ref modeIdx, BeatModeLabels, BeatModeLabels.Length))
            Overrides.PropsBeatMode = (PropBeatMode)modeIdx;
        ImGui.SameLine();
        ImGui.BeginDisabled(fireAppear == null);
        if (ImGui.Button("Appear now##propappear")) fireAppear?.Invoke();
        ImGui.SameLine();
        if (ImGui.Button("Wind-up now##propwindup")) fireWindUp?.Invoke();
        ImGui.EndDisabled();
    }

    private void DrawArrowRows()
    {
        SettingsGrid.Row("Arrow soak (debug):");
        var soakIdx = (int)Overrides.ArrowSoak;
        SettingsGrid.ItemWidth(170);
        if (ImGui.Combo("##arrowsoak", ref soakIdx, SoakModeLabels, SoakModeLabels.Length))
            Overrides.ArrowSoak = (ArrowSoakMode)soakIdx;
    }
#endif

    private void ResetAll()
    {
        Overrides.ArrowSoak = ArrowSoakMode.SetSharedTimelineState;
        Overrides.DpsGetsDifferent = null;
        Overrides.GazeInverted = null;
        Overrides.FireIsStack = null;
        Overrides.FireIsLie = null;
        Overrides.ThunderIsLie = null;
        Overrides.ThunderOrientationFlipped = null;
        Overrides.ThunderRealOffset = null;
        Overrides.PropsBindDirector = true;
        Overrides.PropsForceActive = false;
        Overrides.PropsStaticVfxTest = false;
        Overrides.PropsBeatMode = PropBeatMode.ActorControl;
        Overrides.HoldHaze = false;
        UmadZone.SuppressP1Scenery = false;
    }

#if DEBUG
    private void DrawHazeRow()
    {
        SettingsGrid.Row("Zone (debug):");
        var haze = Overrides.HoldHaze;
        if (ImGui.Checkbox("Hold P1 haze##holdhaze", ref haze)) Overrides.HoldHaze = haze;
        ImGui.SameLine();
        var scenery = UmadZone.SuppressP1Scenery;
        if (ImGui.Checkbox("Deactivate scenery SGs##scenerysg", ref scenery)) UmadZone.SuppressP1Scenery = scenery;
    }

    private void DrawPropKnobs()
    {
        SettingsGrid.Row("Statue props (debug):");
        var bind = Overrides.PropsBindDirector;
        if (ImGui.Checkbox("Bind to director##propbind", ref bind)) Overrides.PropsBindDirector = bind;
        ImGui.SameLine();
        var force = Overrides.PropsForceActive;
        if (ImGui.Checkbox("Force SG active##propforce", ref force)) Overrides.PropsForceActive = force;
        ImGui.SameLine();
        var direct = Overrides.PropsStaticVfxTest;
        if (ImGui.Checkbox("Direct VFX test##propvfx", ref direct)) Overrides.PropsStaticVfxTest = direct;
    }
#endif

    private void DrawDifferentArrows()
    {
        var d = Overrides.DpsGetsDifferent;
        SettingsGrid.Row("Different arrows:");
        if (ImGui.RadioButton("Auto##diffarrows", d == null)) Overrides.DpsGetsDifferent = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("DPS##diffarrows", d == true)) Overrides.DpsGetsDifferent = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("Support##diffarrows", d == false)) Overrides.DpsGetsDifferent = false;
    }

    // Auto / <trueLabel> / <falseLabel> tri-state row over a nullable-bool override
    // (null = Auto/random). Mirrors UmadP4KefkaSaysSettingsWindow.RealFakeRow.
    private static void TriStateRow(string label, string id, string trueLabel, string falseLabel, bool? value, Action<bool?> set)
    {
        SettingsGrid.Row(label);
        if (ImGui.RadioButton($"Auto##{id}", value == null)) set(null);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{trueLabel}##{id}", value == true)) set(true);
        ImGui.SameLine();
        if (ImGui.RadioButton($"{falseLabel}##{id}", value == false)) set(false);
    }

    private void DrawThunderOffset()
    {
        var v = Overrides.ThunderRealOffset;
        SettingsGrid.Row("Thunder reals:");
        if (ImGui.RadioButton("Auto##thunoff", v == null)) Overrides.ThunderRealOffset = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("Slots 0,2##thunoff", v == 0)) Overrides.ThunderRealOffset = 0;
        ImGui.SameLine();
        if (ImGui.RadioButton("Slots 1,3##thunoff", v == 1)) Overrides.ThunderRealOffset = 1;
    }
}
