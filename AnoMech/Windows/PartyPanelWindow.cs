using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using static AnoMech.Windows.MultiplayerUi;

namespace AnoMech.Windows;

// Seats and host moderation for a multiplayer session, docked to the main window's right edge.
internal sealed unsafe class PartyPanelWindow : Window
{
    private const string ConfirmPopupId = "##mpconfirm";

    private readonly MainWindow mainWindow;
    private readonly MultiplayerManager mp;
    // Kick/Ban picked from a seat menu, confirmed in a popup at the panel's root.
    private (Guid Peer, bool Ban)? pendingConfirm;
    private bool openConfirm;

    internal bool RequestedOpen { get; private set; } = true;

    internal PartyPanelWindow(MainWindow mainWindow, MultiplayerManager mp)
        : base("###AnoMechPartyPanel")
    {
        this.mainWindow = mainWindow;
        this.mp = mp;
        Flags = ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNavFocus
            | ImGuiWindowFlags.AlwaysAutoResize;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
        DisableWindowSounds = true;
    }

    internal void ToggleRequested() => RequestedOpen = !RequestedOpen;

    public override void PreOpenCheck()
    {
        IsOpen = RequestedOpen && mp.InSession && mainWindow.IsOpen && !mainWindow.IsActuallyCollapsed
                 && !mainWindow.ShowingMultiplayerSetup;
    }

    public override void PreDraw()
    {
        var background = *ImGui.GetStyleColorVec4(ImGuiCol.WindowBg);
        var panelBackground = Vector4.Lerp(background, *ImGui.GetStyleColorVec4(ImGuiCol.Header), 0.10f);
        panelBackground.W = background.W;
        ImGui.PushStyleColor(ImGuiCol.WindowBg, panelBackground);

        var anchor = mainWindow.RightPanelAnchor;
        Position = new Vector2(anchor.X - ImGui.GetStyle().WindowBorderSize, anchor.Y);
        PositionCondition = ImGuiCond.Always;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220f, mainWindow.ScenarioPanelHeight / ImGuiHelpers.GlobalScale),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void PostDraw() => ImGui.PopStyleColor();

    public override void Draw()
    {
        ImGui.TextUnformatted("Party");
        ImGui.Separator();
        DrawSeats();
        DrawLobby();
        DrawBanned();

        ImGui.Separator();
        // Locked once Started: the choreography only makes sense replayed from a fresh Start.
        var botControlled = mp.DebugBotControlled;
        ImGui.BeginDisabled(mp.Session.Started);
        if (ImGui.Checkbox("Debug AI bot", ref botControlled))
            mp.SetDebugBotControlled(botControlled);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Testing aid -- your seat is driven locally by the same AI a bot in that " +
                             "seat would use. Entirely client-side. Locked once the fight starts.");
#if DEBUG
        RelayUsageReadout.Draw(mp);
#endif
        DrawConfirmPopup();
    }

    private void DrawSeats()
    {
        var myMismatchVsHost = !mp.IsHost && mp.IsVersionMismatched(mp.Session.HostId);
        if (!ImGui.BeginTable("##seats", 3, ImGuiTableFlags.SizingFixedFit)) return;
        ImGui.TableSetupColumn("role");
        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("action");
        foreach (var role in SettingsGrid.Seats)
        {
            var claimed = mp.Session.ClaimedBy.TryGetValue(role, out var peerId);
            var mine = claimed && peerId == mp.MyPeerId;
            ImGui.PushID((int)role);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(SettingsGrid.RoleLabel(role));

            ImGui.TableNextColumn();
            if (!claimed) ImGui.TextColored(MutedColor, "bot");
            else if (mine) ImGui.TextUnformatted($"{mp.Session.NameOf(peerId)} (you)");
            else DrawPeerName(peerId);

            ImGui.TableNextColumn();
            if (mine)
            {
                ImGui.BeginDisabled(mp.Session.Started);
                if (SeatButton("Release")) mp.ReleaseRole();
                ImGui.EndDisabled();
            }
            else if (!claimed)
            {
                ImGui.BeginDisabled(mp.Session.Started || myMismatchVsHost);
                if (SeatButton("Take")) mp.ClaimRole(role);
                ImGui.EndDisabled();
            }
            else if (mp.IsHost)
            {
                DrawPeerMenu(peerId, role);
            }
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    // Names has everyone who said Hello, seated or not.
    private void DrawLobby()
    {
        var unseated = mp.Session.Names.Keys
            .Where(id => id != mp.MyPeerId && !mp.Session.ClaimedBy.ContainsValue(id))
            .ToList();
        if (unseated.Count == 0) return;
        ImGui.Spacing();
        ImGui.TextDisabled("No seat yet");
        foreach (var id in unseated)
        {
            ImGui.PushID(id.ToString());
            ImGui.Bullet();
            ImGui.SameLine();
            DrawPeerName(id);
            if (mp.IsHost)
            {
                ImGui.SameLine();
                DrawPeerMenu(id, null);
            }
            ImGui.PopID();
        }
    }

    // A ban lasts the session.
    private void DrawBanned()
    {
        if (!mp.IsHost || mp.BannedPeers.Count == 0) return;
        ImGui.Spacing();
        if (!ImGui.CollapsingHeader($"Banned ({mp.BannedPeers.Count})##banned")) return;
        foreach (var (id, name) in mp.BannedPeers.ToList())
        {
            ImGui.PushID(id.ToString());
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.TextUnformatted(name);
            ImGui.SameLine();
            if (ImGui.SmallButton("Unban")) mp.UnbanPeer(id);
            ImGui.PopID();
        }
    }

    private void DrawPeerName(Guid peerId)
    {
        // The host doesn't ping itself, so its row tracks time since its last broadcast.
        var isHostRow = peerId == mp.Session.HostId;
        var stale = isHostRow ? mp.IsHostStale : mp.IsPeerStale(peerId);
        if (isHostRow) DrawHostStatusDot(stale, mp.SecondsSinceHostMessage);
        else DrawStatusDot(mp.GetPeerStatus(peerId), stale);
        ImGui.SameLine();

        var mismatched = mp.IsVersionMismatched(peerId);
        var name = mp.Session.NameOf(peerId) + (isHostRow ? " (host)" : "");
        if (mismatched) ImGui.TextColored(WarnColor, $"{name} ⚠");
        else if (stale) ImGui.TextColored(BadColor, name);
        else ImGui.TextUnformatted(name);
        if (mismatched && ImGui.IsItemHovered())
        {
            var theirs = mp.Session.Builds.GetValueOrDefault(peerId);
            ImGui.SetTooltip($"Different plugin build than yours -- everyone needs to match before starting.\nYours: {PluginBuildInfo.Version} ({PluginBuildInfo.ShortChecksum})\nTheirs: {theirs?.Version ?? "?"} ({theirs?.ShortChecksum ?? "?"})");
        }
    }

    // SmallButton sized to the widest seat action, so the column lines up.
    private static bool SeatButton(string label)
    {
        var width = ImGui.CalcTextSize("Release").X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 0f));
        var clicked = ImGui.Button(label, new Vector2(width, 0f));
        ImGui.PopStyleVar();
        return clicked;
    }

    private void DrawPeerMenu(Guid peerId, PartyRole? seat)
    {
        if (SeatButton("...")) ImGui.OpenPopup("##peermenu");
        if (!ImGui.BeginPopup("##peermenu")) return;
        ImGui.TextDisabled(mp.Session.NameOf(peerId));
        ImGui.BeginDisabled(mp.Session.Started);
        if (ImGui.BeginMenu("Move to seat"))
        {
            foreach (var role in SettingsGrid.Seats)
            {
                var label = SettingsGrid.RoleLabel(role)
                            + (mp.Session.ClaimedBy.TryGetValue(role, out var holder) && holder != peerId ? $" ({mp.Session.NameOf(holder)})" : "");
                if (ImGui.MenuItem(label, "", seat == role)) mp.AssignRole(peerId, role);
            }
            ImGui.EndMenu();
        }
        if (seat != null && ImGui.MenuItem("Unseat")) mp.UnassignRole(peerId);
        ImGui.EndDisabled();
        ImGui.Separator();
        if (ImGui.MenuItem("Kick...")) Confirm(peerId, ban: false);
        if (ImGui.MenuItem("Ban...")) Confirm(peerId, ban: true);
        ImGui.EndPopup();
    }

    private void Confirm(Guid peerId, bool ban)
    {
        pendingConfirm = (peerId, ban);
        openConfirm = true;
    }

    private void DrawConfirmPopup()
    {
        if (openConfirm)
        {
            ImGui.OpenPopup(ConfirmPopupId);
            openConfirm = false;
        }
        if (!ImGui.BeginPopup(ConfirmPopupId)) return;
        if (pendingConfirm is not { } confirm)
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }
        var who = mp.Session.NameOf(confirm.Peer);
        ImGui.TextUnformatted(confirm.Ban ? $"Ban {who}?" : $"Kick {who}?");
        ImGui.TextDisabled(confirm.Ban
            ? "They stay out of this session until you unban them."
            : "They can come back with the session code. Mid-fight it ends the run.");
        MainWindow.PushSemanticColors(MainWindow.StopColor);
        if (ImGui.Button(confirm.Ban ? "Ban them" : "Kick them"))
        {
            if (confirm.Ban) mp.BanPeer(confirm.Peer);
            else mp.KickPeer(confirm.Peer);
            pendingConfirm = null;
            ImGui.CloseCurrentPopup();
        }
        MainWindow.PopSemanticColors();
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            pendingConfirm = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    // Time since last broadcast, not latency, so no "fair" band.
    private static void DrawHostStatusDot(bool stale, float secondsSince)
    {
        ImGui.TextColored(stale ? BadColor : GoodColor, "●");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(stale
                ? $"No message from the host in {secondsSince:F0}s -- likely disconnected."
                : $"Host -- last message {secondsSince:F0}s ago.");
    }

    // Grey for "no number yet", so the dot never reads as a suspicious 0ms.
    private static void DrawStatusDot(PeerStatusEntry? status, bool stale)
    {
        Vector4 color;
        string tooltip;
        if (stale)
        {
            color = BadColor;
            tooltip = "No message received in a while -- likely disconnected.";
        }
        else if (status is not { } s)
        {
            color = MutedColor;
            tooltip = "Connected -- waiting for a status update...";
        }
        else if (s.LatencyMs is not { } ms)
        {
            color = MutedColor;
            tooltip = "Connected -- measuring ping...";
        }
        else
        {
            color = ms < 100f ? GoodColor : ms <= 300f ? new Vector4(0.95f, 0.85f, 0.3f, 1f) : BadColor;
            tooltip = $"Ping: {ms:F0}ms ({(ms < 100f ? "good" : ms <= 300f ? "fair" : "poor")})";
        }
        if (status is { } shown)
            tooltip += $"\nLast message: {shown.SecondsSinceLastSeen:F0}s ago";

        ImGui.TextColored(color, "●");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
    }
}
