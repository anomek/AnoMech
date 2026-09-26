using System;
using System.Linq;
using System.Numerics;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

// The session controls while a multiplayer session is in the sim, where the Multiplayer window
// is hidden; MainWindow keeps Start, Stop and Leave. Every button calls the same methods the
// Multiplayer window uses.
public sealed class RunningSimWindow : Window
{
    private readonly Plugin plugin;

    public RunningSimWindow(Plugin plugin) : base("Multiplayer###AnoMechRunningSim")
    {
        this.plugin = plugin;
        Flags |= ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        IsOpen = false;
    }

    public override void PreOpenCheck()
    {
        IsOpen = plugin.Game.World.Map.IsInInstance && plugin.Multiplayer.SessionCode != null;
    }

    public override void Draw()
    {
        var mp = plugin.Multiplayer;
        if (!mp.Session.Started)
        {
            MainWindow.PushSemanticColors(MainWindow.StartColor);
            plugin.MultiplayerWindow.DrawStartButton();
            MainWindow.PopSemanticColors();
            ImGui.SameLine();
        }

        MainWindow.PushSemanticColors(MainWindow.StopColor);
        plugin.MultiplayerWindow.DrawLeaveSessionButton();
        MainWindow.PopSemanticColors();

        DrawRoster();
#if DEBUG
        DrawRelayUsage();
#endif
    }

    // The Multiplayer window is hidden while a sim runs, so the host needs a way to remove
    // someone from here.
    private void DrawRoster()
    {
        var mp = plugin.Multiplayer;
        if (!mp.IsHost || !ImGui.CollapsingHeader("Players")) return;
        var others = mp.Session.Names.Keys.Where(id => id != mp.MyPeerId).ToList();
        if (others.Count == 0)
        {
            ImGui.TextDisabled("Nobody else is connected.");
            return;
        }
        foreach (var id in others)
        {
            ImGui.PushID(id.ToString());
            var seat = mp.Session.ClaimedBy.FirstOrDefault(kv => kv.Value == id);
            var where = mp.Session.ClaimedBy.ContainsValue(id) ? SettingsGrid.RoleLabel(seat.Key) : "no role";
            ImGui.TextUnformatted($"{mp.Session.NameOf(id)} ({where})");
            ImGui.SameLine(200);
            MultiplayerWindow.DrawKickBanButtons(mp, id);
            ImGui.PopID();
        }
    }

#if DEBUG
    // What this client is spending against the relay's per-connection caps, plus the traffic
    // either way. Debug readout: the relay enforces these, the plugin only reports them.
    private void DrawRelayUsage()
    {
        if (!ImGui.CollapsingHeader("Relay usage (debug)")) return;
        RelayStats.ObservePeers(plugin.Multiplayer.Session.Names.Count);
        var s = RelayStats.Current;

        if (ImGui.BeginTable("##relayusage", 3, ImGuiTableFlags.SizingFixedFit))
        {
            Header();
            Capped("Messages/sec", s.MessagesPerSecond, RelayStats.MaxMessagesPerSecond, s.MaxMessagesPerSecond, v => v.ToString());
            Capped("Sent/sec", s.SentBytesPerSecond, RelayStats.MaxBytesPerSecond, s.MaxSentBytesPerSecond, Bytes);
            Capped("Message size", s.LastMessageBytes, RelayStats.MaxMessageBytes, s.LargestMessageBytes, Bytes);
            Capped("Players in session", s.Peers, RelayStats.MaxPeersPerSession, s.MaxPeers, v => v.ToString());
            Row("Received/sec", Bytes(s.ReceivedBytesPerSecond), Bytes(s.MaxReceivedBytesPerSecond));
            Row("Sent total", Bytes(s.TotalSentBytes), "");
            Row("Received total", Bytes(s.TotalReceivedBytes), "");
            ImGui.EndTable();
        }
        ImGui.TextDisabled("Caps are the relay's defaults; one started with other flags differs.");
    }

    private static void Header()
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextDisabled("Value");
        ImGui.TableSetColumnIndex(1);
        ImGui.TextDisabled("Now / cap");
        ImGui.TableSetColumnIndex(2);
        ImGui.TextDisabled("Max this session");
    }

    private static void Row(string label, string now, string max)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(now);
        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(max);
    }

    // Amber past half the cap (the relay's own [NEAR-LIMIT] point), red at it.
    private static void Capped(string label, long now, long cap, long max, Func<long, string> format)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        var fraction = cap <= 0 ? 0d : (double)now / cap;
        var color = fraction >= 1d ? new Vector4(1f, 0.4f, 0.4f, 1f)
            : fraction >= 0.5d ? new Vector4(1f, 0.85f, 0.3f, 1f)
            : new Vector4(0.8f, 0.8f, 0.8f, 1f);
        ImGui.TextColored(color, $"{format(now)} / {format(cap)}");
        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(format(max));
    }

    private static string Bytes(long value)
        => value >= 1024 * 1024 ? $"{value / (1024f * 1024f):F2} MB"
            : value >= 1024 ? $"{value / 1024f:F1} KB"
            : $"{value} B";
#endif
}
