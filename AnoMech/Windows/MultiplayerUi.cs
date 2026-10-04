using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

namespace AnoMech.Windows;

// The multiplayer parts of the main window: the setup screen it switches to, the session strip
// under the scenario header, and the session's Start button and scenario settings.
internal sealed class MultiplayerUi
{
    internal static readonly Vector4 GoodColor = new(0.4f, 0.9f, 0.4f, 1f);
    internal static readonly Vector4 WarnColor = new(1f, 0.7f, 0.3f, 1f);
    internal static readonly Vector4 BadColor = new(1f, 0.4f, 0.4f, 1f);
    internal static readonly Vector4 MutedColor = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 CodeColor = new(1f, 0.85f, 0.3f, 1f);
    private const string LockedWhileRunning = "Locked while the fight is running -- changes apply to the next start.";

    private readonly Plugin plugin;
    private readonly MultiplayerManager mp;
    private string relayUrl;
    private string relayToken;
    private string joinCode = "";
    private string? embeddedError;
    // null = unknown (not checked yet, or the relay is unreachable), treated as "no token
    // needed". Re-checked whenever relayUrl changes.
    private bool? relayRequiresToken;
    private string? relayInfoCheckedForUrl;

    internal MultiplayerUi(Plugin plugin)
    {
        this.plugin = plugin;
        mp = plugin.Multiplayer;
        relayUrl = plugin.Configuration.RelayServerUrl;
        relayToken = plugin.Configuration.TokenForRelay(relayUrl);
    }

    // ObjectTable.LocalPlayer is main-thread-only, so the name is read at Host/Join time.
    private void UseCharacterName()
    {
        if (Plugin.ObjectTable.LocalPlayer?.Name.TextValue is { Length: > 0 } name)
            mp.DisplayName = name;
    }

    // ---- Setup screen ---------------------------------------------------------------------

    internal void DrawSetupScreen(Action back)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (ImGui.Button("‹ Back")) back();
        ImGui.SameLine();
        ImGui.TextUnformatted("Multiplayer");
        ImGui.Separator();

        // TODO: hidden until EmbeddedRelay is backed by a real server.
        if (false)
        {
            if (ImGui.Button("Run embedded server"))
            {
                UseCharacterName();
                embeddedError = mp.HostEmbeddedSession(plugin.Configuration.EmbeddedRelayPort);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Run the server on this PC and host a session on it.");
            if (embeddedError != null) ImGui.TextColored(BadColor, embeddedError);

            ImGui.Spacing();
            ImGui.Separator();
        }
        ImGui.TextDisabled("Connect to a server");
        DrawServerFields(out var canConnect);

        const string hostLabel = "Host";
        const string joinLabel = "Join";
        var buttonWidth = MathF.Max(80f * scale,
            MathF.Max(ImGui.CalcTextSize(hostLabel).X, ImGui.CalcTextSize(joinLabel).X) + ImGui.GetStyle().FramePadding.X * 2);
        ImGui.BeginDisabled(!canConnect);
        if (ImGui.Button(hostLabel, new Vector2(buttonWidth, 0)))
        {
            UseCharacterName();
            embeddedError = null;
            mp.HostSession(relayUrl.Trim());
        }
        // The code only matters for joining: a hosted session gets one from the server.
        var hasCode = !string.IsNullOrWhiteSpace(joinCode);
        ImGui.BeginDisabled(!hasCode);
        var join = ImGui.Button(joinLabel, new Vector2(buttonWidth, 0));
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f * scale);
        join |= ImGui.InputTextWithHint("##joincode", "Session code", ref joinCode, 16,
            ImGuiInputTextFlags.CharsUppercase | ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.EndDisabled();
        if (join && canConnect && hasCode)
        {
            UseCharacterName();
            embeddedError = null;
            mp.JoinSession(relayUrl.Trim(), joinCode);
        }

        if (mp.ConnectionError is { } err)
            ImGui.TextColored(BadColor, $"Connection failed: {err}");
        else if (mp.SessionEndReason is { } endReason)
            ImGui.TextColored(WarnColor, endReason);

        ImGui.Spacing();
        DrawLink("How to run a server", RelayReadmeUrl);
    }

    private const string RelayReadmeUrl = "https://github.com/anomek/AnoMech/blob/master/AnoMech.Relay.Host/README.md";
    private static readonly Vector4 LinkColor = new(0.45f, 0.7f, 1f, 1f);

    private static void DrawLink(string label, string url)
    {
        ImGui.TextColored(LinkColor, label);
        if (!ImGui.IsItemHovered()) return;
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, max.Y), max, ImGui.GetColorU32(LinkColor));
        ImGui.SetTooltip(url);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) Util.OpenLink(url);
    }

    private void DrawServerFields(out bool canConnect)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetNextItemWidth(240f * scale);
        if (ImGui.InputTextWithHint("##relayUrl", "relay.example.com:7890", ref relayUrl, 256))
        {
            // Shows the password saved for this relay, if any. Hidden rather than cleared while
            // the address points elsewhere, so a typo doesn't lose it.
            relayToken = plugin.Configuration.TokenForRelay(relayUrl);
            plugin.Configuration.RelayServerUrl = relayUrl;
            plugin.Configuration.Save();
        }
        var validUrl = IsPlausibleRelayUrl(relayUrl);
        if (!validUrl && !string.IsNullOrWhiteSpace(relayUrl))
            ImGui.TextColored(BadColor, "Doesn't look like a valid server address.");
        // Re-checked once per distinct valid URL; unreachable/old-relay failures default to
        // "no token needed".
        else if (validUrl && relayInfoCheckedForUrl != relayUrl)
        {
            relayInfoCheckedForUrl = relayUrl;
            relayRequiresToken = null;
            var urlSnapshot = relayUrl;
            _ = RelayClient.FetchInfoAsync(urlSnapshot).ContinueWith(t =>
            {
                if (t.Result is { } info)
                    Plugin.Framework.Run(() => { if (relayInfoCheckedForUrl == urlSnapshot) relayRequiresToken = info.RequiresToken; });
            });
        }

        if (relayRequiresToken == true)
        {
            ImGui.SetNextItemWidth(240f * scale);
            if (ImGui.InputTextWithHint("##relayToken", "Server password", ref relayToken, 128, ImGuiInputTextFlags.Password))
            {
                plugin.Configuration.RelayAccessToken = relayToken;
                plugin.Configuration.RelayTokenOrigin = Configuration.OriginOf(relayUrl);
                plugin.Configuration.Save();
            }
        }

        canConnect = validUrl && !(relayRequiresToken == true && string.IsNullOrEmpty(relayToken));
    }

    // Fails fast on a blank/garbled field, not reachability. A bare "host:port" parses as an
    // absolute URI on its own (scheme "host"), so "://" is checked first.
    private static bool IsPlausibleRelayUrl(string url)
    {
        var trimmed = url.Trim();
        if (trimmed.Length == 0) return false;
        if (trimmed.Contains("://", StringComparison.Ordinal))
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var explicitUri)
                && explicitUri.Scheme is "ws" or "wss" or "http" or "https";
        return Uri.TryCreate($"ws://{trimmed}", UriKind.Absolute, out var probe) && !string.IsNullOrEmpty(probe.Host);
    }

    // ---- Session strip --------------------------------------------------------------------

    internal void DrawSessionStrip(PartyPanelWindow partyPanel)
    {
        ImGui.AlignTextToFramePadding();
        DrawConnectionDot();
        ImGui.SameLine();

        var joining = !mp.IsHost && !mp.EverHeardFromHost;
        if (mp.IsHostConnecting || joining)
        {
            ImGui.TextUnformatted(mp.IsHostConnecting ? "Starting session..." : $"Joining {mp.SessionCode}...");
            ImGui.SameLine();
            if (StripButton("Cancel")) LeaveSession();
            return;
        }

        if (mp.IsHost)
        {
            ImGui.TextUnformatted("Hosting");
            ImGui.SameLine();
            ImGui.TextColored(CodeColor, mp.SessionCode);
            ImGui.SameLine();
            if (StripButton("Copy")) ImGui.SetClipboardText(mp.SessionCode);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copy the session code. Others join with it and your server's address.");
        }
        else
        {
            ImGui.TextUnformatted($"Joined {mp.SessionCode} · host {mp.Session.NameOf(mp.Session.HostId)}");
        }

        ImGui.SameLine();
        if (StripButton($"Party {mp.Session.Names.Count}/8###partyToggle")) partyPanel.ToggleRequested();

        if (mp.Embedded.IsRunning)
        {
            ImGui.SameLine();
            ImGui.TextColored(GoodColor, $"Server :{mp.Embedded.Port}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Running the server on this PC (port {mp.Embedded.Port}). It stops when you leave.");
        }

        ImGui.SameLine();
        MainWindow.PushSemanticColors(MainWindow.StopColor);
        if (StripButton("Disconnect###leave-session")) LeaveSession();
        MainWindow.PopSemanticColors();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(mp.IsHost
                ? "End the session for everyone and go back to solo play."
                : "Leave the session and go back to solo play. Mid-fight this ends the run for everyone.");
    }

    // One fixed height for every strip button, whatever styling each pushes.
    private static bool StripButton(string label) =>
        ImGui.Button(label, new Vector2(0f, ImGui.GetFrameHeight()));

    private void DrawConnectionDot()
    {
        Vector4 color;
        string tooltip;
        if (mp.IsConnected)
        {
            color = mp.IsEncrypted ? GoodColor : WarnColor;
            tooltip = "Connected to the server"
                      + (mp.IsEncrypted ? " (encrypted)."
                         : mp.FellBackToUnencrypted ? " -- NOT encrypted, this server doesn't support wss://."
                         : " -- NOT encrypted.");
            if (!mp.SupportsCompression) tooltip += "\nThis server doesn't support compression.";
        }
        else if (mp.IsReconnecting)
        {
            color = WarnColor;
            tooltip = $"Reconnecting to the server... (attempt {mp.ReconnectAttempt})";
        }
        else if (mp.IsHostConnecting || (!mp.IsHost && !mp.EverHeardFromHost))
        {
            color = MutedColor;
            tooltip = "Connecting...";
        }
        else
        {
            color = BadColor;
            tooltip = "Not connected to the server.";
        }
        if (!mp.IsConnected && mp.ConnectionError is { } err) tooltip += $"\nLast error: {err}";

        ImGui.TextColored(color, "●");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
    }

    internal void LeaveSession()
    {
        mp.LeaveSession();
        // Leave() assumes a zone was entered.
        if (plugin.Game.World.Map.IsInInstance) plugin.Game.Leave();
    }

    // ---- Start ----------------------------------------------------------------------------

    internal void DrawStartButton(Vector2 size)
    {
        if (!mp.IsHost)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Waiting for host###start", size);
            ImGui.EndDisabled();
            return;
        }

        var blocked = StartBlockedReason();
        ImGui.BeginDisabled(blocked != null);
        MainWindow.PushSemanticColors(MainWindow.StartColor);
        if (ImGui.Button($"{(mp.StartWaitingOn != null || mp.IsStartCheckPending ? "Waiting to start..." : "Start")}###start", size))
            mp.StartScenario();
        MainWindow.PopSemanticColors();
        ImGui.EndDisabled();
        if (blocked != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(blocked);
    }

    // Warnings and errors, between the session strip and Start.
    internal void DrawSessionMessages()
    {
        if (mp.IsHostConnecting || (!mp.IsHost && !mp.EverHeardFromHost)) return;

        if (!mp.IsConnected)
        {
            ImGui.TextColored(mp.IsReconnecting ? WarnColor : BadColor, mp.IsReconnecting
                ? $"⚠ Reconnecting to the server... (attempt {mp.ReconnectAttempt})"
                : "⚠ Not connected to the server.");
            if (mp.ConnectionError is { } err) ImGui.TextColored(BadColor, $"Last error: {err}");
        }

        // Shown with both checksums rather than as a silently rejected Claim.
        if (!mp.IsHost && mp.IsVersionMismatched(mp.Session.HostId))
        {
            var hostBuild = mp.Session.Builds.GetValueOrDefault(mp.Session.HostId);
            ImGui.TextColored(WarnColor, "⚠ Different AnoMech build than the host -- update before taking a seat.");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Yours: {PluginBuildInfo.Version} ({PluginBuildInfo.ShortChecksum})\nHost: {hostBuild?.Version ?? "?"} ({hostBuild?.ShortChecksum ?? "?"})");
        }

        if (mp.IsHost)
        {
            if (mp.IsStartCheckPending)
                ImGui.TextColored(CodeColor, "Checking everyone's ready...");
            else if (mp.StartWaitingOn is { } waitingOn)
                ImGui.TextColored(CodeColor, $"Waiting for {waitingOn} to settle...");
            else if (mp.StartCheckFailureReason is { } startFail)
                ImGui.TextColored(BadColor, startFail);
            else if (mp.IsConnected && StartBlockedReason() is { } blocked && !mp.Session.Started)
                ImGui.TextColored(WarnColor, $"⚠ {blocked}");
            else if (mp.RunEndReason is { } runEnd)
                ImGui.TextColored(WarnColor, $"Last run ended: {runEnd}");
        }
        else if (mp.RunEndReason is { } runEnd)
        {
            ImGui.TextColored(WarnColor, $"Last run ended: {runEnd}");
        }
    }

    private string? StartBlockedReason()
    {
        if (!mp.IsConnected) return "Not connected to the server.";
        if (Plugin.MainWindow.SelectedScenario is not { SupportsMultiplayer: true } scenario)
            return "Select a multiplayer-supported scenario.";
        if (!Plugin.MainWindow.HasStartableStrat()) return "No strat available for the selected scenario/region.";
        var conflicts = scenario.SettingsConflicts;
        if (conflicts.Count > 0)
            return $"The fight can't produce these settings together:\n{string.Join("\n", conflicts)}";
        if (mp.Session.ClaimedBy.Values.Any(mp.IsVersionMismatched))
            return "A player is on a different plugin build -- everyone needs to match.";
        if (mp.MyClaimedRole == null) return "Take a seat in the Party panel first.";
        // A connected person without a seat would be left behind at Start.
        var seated = mp.Session.ClaimedBy.Values.ToHashSet();
        if (mp.Session.Names.Keys.FirstOrDefault(id => !seated.Contains(id)) is var unseated && unseated != Guid.Empty)
            return $"{mp.Session.NameOf(unseated)} hasn't taken a seat.";
        return null;
    }

    // ---- Scenario settings ----------------------------------------------------------------

    // The host edits the scenario as in solo play; everyone else sees what the host forced.
    // Speed is deliberately absent: a session always runs at 1x.
    internal void DrawScenarioSettings(IScenario scenario)
    {
        if (scenario.HasLocalSettings) scenario.DrawLocalSettings(mp.MyClaimedRole);
        if (!mp.IsHost)
        {
            var lines = mp.Session.ScenarioSettings;
            var setBy = scenario.HasLocalSettings ? "Other settings are handled by the host" : "Set by the host";
            ImGui.TextDisabled(lines.Count == 0 ? $"{setBy} -- everything random." : $"{setBy}:");
            foreach (var line in lines) ImGui.BulletText(line);
            return;
        }
        if (!scenario.SupportsMultiplayer)
        {
            ImGui.TextDisabled("This scenario doesn't support multiplayer.");
            return;
        }
        ImGui.BeginGroup();
        ImGui.BeginDisabled(mp.Session.Started);
        scenario.DrawSettings();
        ImGui.EndDisabled();
        ImGui.EndGroup();
        if (mp.Session.Started && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(LockedWhileRunning);
        DrawAssignMechanicsButton(scenario, mp.Session.Started, LockedWhileRunning);
        scenario.DrawMultiplayerSettings();
    }

    private const string MechanicsPopupId = "Mechanics###AnoMechAssignMechanics";

    // The mechanic a given player carries (a number, an Accretion, a tether) is a different
    // question from the fight-wide rolls, so it gets its own dialog.
    private static void DrawAssignMechanicsButton(IScenario scenario, bool locked, string lockedReason)
    {
        if (!scenario.HasPerPlayerSettings) return;
        ImGui.BeginDisabled(locked);
        if (ImGui.Button("Assign specific mechanics to players")) ImGui.OpenPopup(MechanicsPopupId);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(locked
                ? lockedReason
                : $"Choose which mechanic each player gets in {scenario.Name}. Anyone left on Auto gets the fight's own roll.");

        if (!ImGui.BeginPopup(MechanicsPopupId)) return;
        ImGui.TextUnformatted($"{scenario.Name}: mechanics per player");
        ImGui.TextDisabled(PerRole.SeatsActive
            ? "Pick a seat, then what that player gets. Anyone left on Auto gets the fight's own roll."
            : "Yours only. Anything left on Auto gets the fight's own roll.");
        ImGui.Separator();
        scenario.DrawPerPlayerSettings();
        ImGui.Separator();
        if (ImGui.Button("Done")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }
}
