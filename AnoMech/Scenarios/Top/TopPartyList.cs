using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace AnoMech.Scenarios.Top;

// The player's own P1 party list, one per seat: In Line flexers sort the people they swap for above
// themselves, healers keep the game's order.
public static class TopPartyList
{
    private const string PopupId = "Your party list###AnoMechTopPartyList";

    private static readonly Dictionary<PartyRole, List<PartyRole>> Custom = new();

    public static IReadOnlyList<PartyRole> Order(PartyRole self)
        => Custom.TryGetValue(self, out var order) ? order : Default(self);

    public static IReadOnlyList<PartyRole> Default(PartyRole self)
    {
        var above = TopLightParties.FlexesFor(self);
        return [.. above, self, .. Enum.GetValues<PartyRole>().Where(role => role != self && !above.Contains(role))];
    }

    public static void Draw(PartyRole? self)
    {
        ImGui.TextUnformatted("Your party list:");
        ImGui.SameLine();
        if (self is not { } me)
        {
            ImGui.TextDisabled("take a seat to set it");
            return;
        }
        var order = Order(me);
        ImGui.TextUnformatted(string.Join("  ", order.Select(role => role == me ? "You" : SettingsGrid.RoleLabel(role))));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Your party list in the sim, top to bottom. Only yours: everyone sets their own.");
        ImGui.SameLine();
        if (ImGui.SmallButton("Edit##toppartylist")) ImGui.OpenPopup(PopupId);
        if (!Custom.ContainsKey(me))
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(default)");
        }
        DrawEditor(me);
    }

    private static void DrawEditor(PartyRole me)
    {
        if (!ImGui.BeginPopup(PopupId)) return;
        ImGui.TextUnformatted($"Your party list as {SettingsGrid.RoleLabel(me)}, top to bottom");
        ImGui.TextDisabled("Drag a row or use the arrows. For In Line, put the people you swap for above you.");
        ImGui.Separator();

        var order = Order(me).ToList();
        (int From, int To)? move = null;
        for (var i = 0; i < order.Count; i++)
        {
            var role = order[i];
            ImGui.PushID((int)role);
            ImGui.BeginDisabled(i == 0);
            if (ImGuiComponents.IconButton("up", FontAwesomeIcon.ArrowUp)) move = (i, i - 1);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(i == order.Count - 1);
            if (ImGuiComponents.IconButton("down", FontAwesomeIcon.ArrowDown)) move = (i, i + 1);
            ImGui.EndDisabled();
            ImGui.SameLine();
            var name = role == me ? "You" : PartyPresets.Standard[(int)role].Name;
            ImGui.Selectable($"{i + 1}.  {SettingsGrid.RoleLabel(role),-3} {name}###row", role == me);
            if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
            {
                var target = i + (ImGui.GetMouseDragDelta(ImGuiMouseButton.Left).Y < 0f ? -1 : 1);
                if (target >= 0 && target < order.Count)
                {
                    move = (i, target);
                    ImGui.ResetMouseDragDelta();
                }
            }
            ImGui.PopID();
        }
        if (move is { } m) Move(me, order, m.From, m.To);

        ImGui.Separator();
        ImGui.BeginDisabled(!Custom.ContainsKey(me));
        if (ImGui.Button("Default")) Custom.Remove(me);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.TextDisabled(TopLightParties.FlexesFor(me).Count == 0
            ? "The game's own order."
            : "The people you swap for above you.");
        ImGui.SameLine();
        if (ImGui.Button("Done")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private static void Move(PartyRole me, List<PartyRole> order, int from, int to)
    {
        (order[from], order[to]) = (order[to], order[from]);
        if (order.SequenceEqual(Default(me))) Custom.Remove(me);
        else Custom[me] = order;
    }
}
