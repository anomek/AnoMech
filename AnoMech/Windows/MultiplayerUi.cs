using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Multiplayer;
using AnoMech.Network;
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
    private string joinCode = "";
    private string? embeddedError;
    // Shown until the code is edited.
    private (string Code, string Message)? codeError;

    internal MultiplayerUi(Plugin plugin)
    {
        this.plugin = plugin;
        mp = plugin.Multiplayer;
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
        var serverProblem = DrawServer();
        var canConnect = serverProblem == null;
        var relayUrl = plugin.Configuration.ActiveRelayUrl();
        var group = RelayGroups.Current;
        ImGui.Spacing();

        const string hostLabel = "Host";
        const string joinLabel = "Join";
        var buttonWidth = MathF.Max(80f * scale,
            MathF.Max(ImGui.CalcTextSize(hostLabel).X, ImGui.CalcTextSize(joinLabel).X) + ImGui.GetStyle().FramePadding.X * 2);
        ImGui.BeginDisabled(!canConnect);
        if (ImGui.Button(hostLabel, new Vector2(buttonWidth, 0)))
        {
            UseCharacterName();
            embeddedError = null;
            codeError = null;
            mp.HostSession(group is null
                ? [relayUrl]
                : RelayGroups.HostOrder(group, plugin.Configuration.HostRelayUrl, RelayPings.Of).Select(m => RelayGroups.DialUrl(group, m, relayUrl)).ToList());
        }
        if (serverProblem != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(serverProblem);
        // The code only matters for joining: a hosted session gets one from the server.
        var code = joinCode.Trim().ToUpperInvariant();
        var codeProblem = code.Length == 0 ? "Type the session code first."
            : RelayWire.IsValidCode(code) ? null
            : code.Length != RelayWire.CodeLength ? $"A session code is {RelayWire.CodeLength} characters."
            : "Session codes never contain 0, O, 1 or I. Check it for typos.";
        ImGui.BeginDisabled(codeProblem != null);
        var join = ImGui.Button(joinLabel, new Vector2(buttonWidth, 0));
        ImGui.EndDisabled();
        if ((serverProblem ?? codeProblem) is { } joinBlocked && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(joinBlocked);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f * scale);
        join |= ImGui.InputTextWithHint("##joincode", "Session code", ref joinCode, 16,
            ImGuiInputTextFlags.CharsUppercase | ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.EndDisabled();
        if (join && canConnect)
        {
            // The server has listed every member of its group, so a code naming none of them is mistyped.
            var problem = codeProblem ?? (group is not null && RelayGroups.Confirmed && group.Member(RelayWire.CodeServer(code)) is null
                ? $"No server has a session {code}. Check the code for typos."
                : null);
            codeError = problem is null ? null : (code, problem);
            if (problem is null)
            {
                UseCharacterName();
                embeddedError = null;
                mp.JoinSession(RelayGroups.JoinUrl(group, code, relayUrl), code);
            }
        }

        if (codeError is { } shown && shown.Code == code)
            ImGui.TextColored(BadColor, shown.Message);
        else if (mp.ConnectionError is { } err)
            ImGui.TextColored(BadColor, $"Connection failed: {err}");
        else if (mp.SessionEndReason is { } endReason)
            ImGui.TextColored(WarnColor, endReason);
    }

    internal const string RelayReadmeUrl = "https://github.com/anomek/AnoMech/blob/master/AnoMech.Relay.Host/README.md";
    private static readonly Vector4 LinkColor = new(0.45f, 0.7f, 1f, 1f);

    internal static void DrawLink(string label, string url)
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

    // The server Host and Join use, as picked in Settings. Returns why it can't be used yet.
    private string? DrawServer()
    {
        var config = plugin.Configuration;
        var url = config.ActiveRelayUrl();
        var valid = IsPlausibleRelayUrl(url);
        var (state, needsPassword) = valid ? RelayServerStatus.Of(url) : default;
        var group = valid ? RelayGroups.Current : null;
        if (group is not null) RelayPings.Measure(url, group);

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Server");
        ImGui.SameLine();
        var detailX = ImGui.GetCursorPosX();
        if (valid)
        {
            ImGui.TextUnformatted(config.UseCustomRelay ? url : "Default server");
            ImGui.SameLine();
            DrawServerStatus(state);
        }
        else ImGui.TextColored(WarnColor, "No address set");
        ImGui.SameLine();
        if (ImGui.Button("Change...")) plugin.OpenSettings();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Choose the server in Settings.");
        ImGui.SetCursorPosX(detailX);
        if (group is not null) DrawHostTarget(group);
        else ImGui.TextDisabled(config.UseCustomRelay ? "Custom server" : DefaultRelay.Location);

        if (!valid) return "Enter your server's address in Settings first.";
        if (!needsPassword || config.TokenForRelay(url).Length != 0) return null;
        const string passwordMissing = "This server needs a password. Enter it in Settings.";
        ImGui.TextColored(WarnColor, passwordMissing);
        return passwordMissing;
    }

    // Where a session hosted now would run.
    private void DrawHostTarget(RelayGroupInfo group)
    {
        var chosen = plugin.Configuration.HostRelayUrl;
        var target = RelayGroups.HostOrder(group, chosen, RelayPings.Of)[0];
        ImGui.TextDisabled(RelayGroups.SameServer(target.Url, chosen) ? $"{target.Location}{PingSuffix(target.Url)} (picked in Settings)"
            : RelayPings.Of(target.Url) is not null ? $"Closest: {target.Location}{PingSuffix(target.Url)}"
            : RelayPings.Measuring ? "Finding the closest server..."
            : target.Location);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Sessions you host run on this server. Players who join are connected to it automatically.\n" +
                             "Pick a different server in Settings.");
    }

    internal static string PingSuffix(string url) => RelayPings.Of(url) is { } ms ? $" · {ms:F0} ms" : "";

    internal static void DrawServerStatus(RelayServerStatus.Reachability state)
    {
        switch (state)
        {
            case RelayServerStatus.Reachability.Online:
                ImGui.TextColored(GoodColor, "● Online");
                break;
            case RelayServerStatus.Reachability.Unreachable:
                ImGui.TextColored(BadColor, "● Can't reach");
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("No answer from this server. Check the address, or try again later.");
                break;
            default:
                ImGui.TextColored(MutedColor, "● Checking...");
                break;
        }
    }

    // Fails fast on a blank/garbled field, not reachability. A bare "host:port" parses as an
    // absolute URI on its own (scheme "host"), so "://" is checked first.
    internal static bool IsPlausibleRelayUrl(string url)
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
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(plugin.Configuration.UseCustomRelay
                    ? "Copy the session code. Others join with it and your server's address."
                    : "Copy the session code. Anyone on the default server can join with it.");
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
            tooltip = (RelayGroups.LocationOf(mp.RelayUrl) is { } location ? $"Connected to the server in {location}" : "Connected to the server")
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
        if (!mp.IsHost)
        {
            var lines = mp.Session.ScenarioSettings;
            ImGui.TextDisabled(lines.Count == 0 ? "Set by the host -- everything random." : "Set by the host:");
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
