using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using AnoMech.Integrations.Splatoon;

namespace AnoMech.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("AnoMech Settings###AnoMechConfig")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(380, 420) * ImGuiHelpers.GlobalScale;
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var onInn = configuration.OpenSimMenuOnInn;
        if (ImGui.Checkbox("Open Sim Menu when entering Inn", ref onInn))
        {
            configuration.OpenSimMenuOnInn = onInn;
            configuration.Save();
        }

        var suppressBgm = configuration.SuppressBgm;
        if (ImGui.Checkbox("Suppress scenario BGM", ref suppressBgm))
        {
            configuration.SuppressBgm = suppressBgm;
            configuration.Save();
        }

        var resultMarks = configuration.EnableMechanicResultMarks;
        if (ImGui.Checkbox("Show mechanic success/failure marks", ref resultMarks))
        {
            configuration.EnableMechanicResultMarks = resultMarks;
            configuration.Save();
        }

        var userActions = configuration.EnableUserActions;
        if (ImGui.Checkbox("Resolve your own actions", ref userActions))
        {
            configuration.EnableUserActions = userActions;
            configuration.Save();
            if (userActions) Plugin.UserActions.Enable();
            else Plugin.UserActions.Disable();
        }

        if (configuration.EnableUserActions)
        {
            var threshold = configuration.CastInterruptThreshold;
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputFloat("Slidecast window (s)", ref threshold, 0.05f, 0.1f, "%.2f"))
            {
                configuration.CastInterruptThreshold = Math.Clamp(threshold, 0f, 5f);
                configuration.Save();
            }
        }

        ImGui.Separator();
        DrawSplatoonCompat();
        ImGui.Separator();

        var logging = configuration.EnableEventLogging;
        if (ImGui.Checkbox("Enable event logging", ref logging))
        {
            configuration.EnableEventLogging = logging;
            configuration.Save();
            if (logging) Plugin.LogManager.Open();
            else Plugin.LogManager.Close();
        }
        ImGui.SameLine();
        if (ImGui.Button("Open logs folder"))
            Plugin.LogManager.OpenLogsFolder();

#if DEBUG
        ImGui.Separator();

        var safeMode = configuration.SafeMode;
        if (ImGui.Checkbox("Safe mode (debug)", ref safeMode))
        {
            configuration.SafeMode = safeMode;
            configuration.Save();
        }
        if (safeMode)
            ImGui.TextWrapped(
                "Safe mode cuts you off from server traffic while in the sim zone. " +
                "You won't see players joining or leaving the party, ready checks, " +
                "or duty pops.");
        else
            ImGui.TextWrapped(
                "Safe mode off — server packets reach the engine, so you'll see " +
                "party updates, ready checks, and duty pops. It's easier to break " +
                "the sim zone this way. You still can't send anything to the server " +
                "while in the instance.");
#endif
    }

    private void DrawSplatoonCompat()
    {
        var compat = configuration.SplatoonCompat;
        if (ImGui.Checkbox("Splatoon compatibility", ref compat))
        {
            configuration.SplatoonCompat = compat;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "While a scenario runs, flags you as in combat and in duty and sets the fight's\n" +
                "phase scene, so Splatoon layouts and scripts for the fight activate and reset\n" +
                "between pulls. Also lets Splatoon see the sim's arena map effects.");

        ImGui.SameLine();
        if (SplatoonCompat.IsSplatoonLoaded())
            ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), "Splatoon loaded");
        else
            ImGui.TextDisabled("Splatoon not loaded");

        if (!configuration.SplatoonCompat) return;

        var sceneOverride = configuration.SplatoonSceneOverride;
        ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Scene override", ref sceneOverride))
        {
            configuration.SplatoonSceneOverride = Math.Clamp(sceneOverride, SplatoonCompat.SceneOverrideOff, byte.MaxValue);
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "-1 uses each phase's own scene. Set a value to force Splatoon's Controller.Scene\n" +
                "for phases whose scene isn't known yet.");

        if (Plugin.SplatoonCompat.IsHoldingEncounter)
            ImGui.TextDisabled($"Active: in combat, scene {Plugin.SplatoonCompat.AppliedScene?.ToString() ?? "unchanged"}");
    }
}
