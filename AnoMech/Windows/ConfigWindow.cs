using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("AnoMech Settings###AnoMechConfig")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

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

        var showSpeed = configuration.ShowSpeedControl;
        if (ImGui.Checkbox("Show speed control", ref showSpeed))
        {
            configuration.ShowSpeedControl = showSpeed;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Adds the scenario speed buttons and a typed value under God mode in the main window.\nWarning: bots can die at other speeds, since they still run at normal speed.");

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

            var tankMitigation = configuration.EnableTankMitigation;
            if (ImGui.Checkbox("Require tank mitigation", ref tankMitigation))
            {
                configuration.EnableTankMitigation = tankMitigation;
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Some tankbusters (depending on scenario support) will check your mitigation.");
        }

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
}
