using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AnoMech.Windows;

// Changes Game.EventTimeScale live, so a scenario can be sped up or slowed down mid-run.
// Only event scheduling is affected; cast bars and animations run at real time (see Game.Tick).
internal sealed class SpeedControl
{
    // Applied on Enter or focus loss, so half-typed values like "0" never reach the timeline.
    private float typed = 1f;
    private bool typing;

    public void Draw(Core.Game.Game game, string id)
    {
        ImGui.PushID(id);
        ImGui.TextUnformatted("Speed:");
        for (int x = 1; x <= 4; x++)
        {
            ImGui.SameLine();
            var active = MathF.Abs(game.EventTimeScale - x) < 0.01f;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            if (ImGui.Button($"x{x}")) game.EventTimeScale = x;
            if (active) ImGui.PopStyleColor();
        }
        ImGui.SameLine();
        if (!typing) typed = game.EventTimeScale;
        ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
        ImGui.InputFloat("x##speed-value", ref typed, 0f, 0f, "%.2f");
        typing = ImGui.IsItemActive();
        if (ImGui.IsItemDeactivatedAfterEdit()) game.EventTimeScale = Math.Clamp(typed, 0.1f, 10f);
        if (MathF.Abs(game.EventTimeScale - 1f) > 0.01f)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Bots may die: they still run at normal speed.");
        ImGui.PopID();
    }
}
