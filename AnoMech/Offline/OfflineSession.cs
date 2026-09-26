using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using AnoMech.Core;
using AnoMech.Core.Map;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AnoMech.Offline;

internal enum OfflinePhase { Idle, Starting, InWorld, Leaving, Failed }

// Nothing here hooks or writes game memory until the user starts offline mode from the title screen.
internal sealed unsafe class OfflineSession : IDisposable
{
    // LogoutCallbackInterface.LogoutParams.Code of a normal logout.
    private const int NormalLogoutCode = 10000;
    // AgentLobby.LobbyUpdateStage: the title screen, and the two stages a logout passes through.
    private const byte TitleStage = 1, LogoutStage = 0x12, LogoutEndStage = 0x13;
    // AgentLobby.LobbyUIStage of the idle title screen. Start moves it on at once, while the title
    // menu is still fading out, and the character select it heads for leaves lobby objects only a
    // real login cleans up.
    internal const byte TitleUiStage = 9;
    // AgentLobby.ReceiveEvent value of the title menu's Exit.
    private const int TitleExitEvent = 12;
    private static readonly TimeSpan ReturnTimeout = TimeSpan.FromSeconds(60);
    // A real logout's saves are issued as it happens and written asynchronously; they are long done
    // by then, and offline mode's file guard would refuse one still in flight.
    private const long SaveSettleMilliseconds = 10_000;
    internal const string LoginPrecaution = "As a precaution, the game closes at once if anything tries to log in or reach a server while offline mode is active, "
                                            + "for example a plugin that logs in on its own, so no login can happen with offline mode's changes in place.";

    internal static OfflineSession? Current { get; private set; }
    // Process-wide rather than this instance's phase: the game stays in the offline world across
    // plugin reloads.
    internal static bool InWorld => OfflineProcessState.InWorld;

    public OfflineStore Store { get; }
    public OfflineConfigWindow ConfigWindow { get; }
    public OfflinePhase Phase { get; private set; }
    public string Status { get; private set; } = "";
    public bool Busy => Phase is OfflinePhase.Starting or OfflinePhase.Leaving;
    public OfflineLoadout? Loadout { get; private set; }
    public bool UseConfig { get; private set; }
    public IReadOnlyList<string> OtherPlugins { get; private set; } = [];

    private List<OfflineLocalCharacter>? characters;
    private List<OfflineAppearance>? appearances;
    private int unusable;
    private (OfflineLoadout? Loadout, string Error)? selection;
    private OfflineBootstrap? bootstrap;
    private bool confirmClose;
    private bool returnRequested;
    private bool exitAfterReturn;
    private string? lastRefusal;
    private string? notice;
    private string? closedLastTime;
    private string? leaveReason;
    private bool ownLogout;
    private long leavingSince;
    private bool configWindowHidden;

    public OfflineSession()
    {
        Store = new OfflineStore(Plugin.PluginInterface.ConfigDirectory.FullName);
        ConfigWindow = new OfflineConfigWindow(this);
        Current = this;
        closedLastTime = Store.TakeCloseRecord();
        if (closedLastTime != null) DiagnosticLog.Info($"[Offline] {closedLastTime}");

        Plugin.ClientState.Logout += OnLogout;

        if (OfflineProcessState.Tainted) Resume();
    }

    public void Dispose()
    {
        Plugin.ClientState.Logout -= OnLogout;
        bootstrap?.Dispose();
        if (Current == this) Current = null;
    }

    // The game's own logout, from its menus or anything else: the title screen is on its way, so
    // offline mode only has to put things back once it arrives. Raised before the game's own
    // logout handler runs.
    private void OnLogout(int type, int code)
    {
        if (Phase is not (OfflinePhase.Starting or OfflinePhase.InWorld))
        {
            if (!OfflineProcessState.Tainted) OfflineProcessState.LastLogout = Environment.TickCount64;
            return;
        }
        DiagnosticLog.Warn($"[Offline] The game logged out of the offline world (type {type}, code {code}).");
        if (bootstrap?.Snapshot?.SharedMacrosChanged() == true)
        {
            CloseGame("shared macros were edited offline, and offline mode can't put them back");
            return;
        }
        BeginLeaving($"the game logged out (code {code}).", ownLogout: false);
    }

    // A load into a process where offline mode already began, which unloading normally prevents by
    // closing the game: the guards come back either way, and the world only if a local player is there.
    private void Resume()
    {
        OfflineTrace.Begin();
        bootstrap = new OfflineBootstrap(this);
        var armed = bootstrap.RearmGuards(out var why);
        var control = Control.Instance();
        if (OfflineProcessState.InWorld && armed && control != null && control->LocalPlayer != null)
        {
            Phase = OfflinePhase.InWorld;
            DiagnosticLog.Info("[Offline] Resumed the offline world after a plugin reload.");
            return;
        }
        OfflineProcessState.InWorld = false;
        Phase = OfflinePhase.Failed;
        Status = armed
            ? "Offline mode ended in an earlier plugin load and can't put the game back. Close the game and start it again."
            : $"Offline mode couldn't resume after a plugin reload ({why}). Close the game and start it again.";
        DiagnosticLog.Warn($"[Offline] {Status}");
    }

    // The game's own files, read when first needed and again whenever the choices are shown. Only
    // characters with a combat gearset can go offline.
    public void Refresh()
    {
        var all = OfflineGameFiles.ScanCharacters();
        characters = all.Where(c => OfflineLoadouts.CombatGearsets(c).Count > 0).ToList();
        unusable = all.Count - characters.Count;
        appearances = OfflineGameFiles.ScanAppearances();
        OtherPlugins = Plugin.PluginInterface.InstalledPlugins
                             .Where(p => p.IsLoaded && p.InternalName != Plugin.PluginInterface.InternalName)
                             .Select(p => p.Name)
                             .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                             .ToList();
        SelectionChanged();
    }

    public IReadOnlyList<OfflineLocalCharacter> Characters
    {
        get
        {
            if (characters == null) Refresh();
            return characters!;
        }
    }

    public int UnusableCharacters
    {
        get
        {
            if (characters == null) Refresh();
            return unusable;
        }
    }

    public IReadOnlyList<OfflineAppearance> Appearances
    {
        get
        {
            if (appearances == null) Refresh();
            return appearances!;
        }
    }

    public OfflineLocalCharacter? SelectedCharacter
        => Characters.FirstOrDefault(c => c.Key == Store.SelectedKey) ?? Characters.FirstOrDefault();

    public OfflineLoadout? SelectedLoadout(out string error)
    {
        selection ??= SelectedCharacter is { } character
            ? (Resolve(character, out var why), why)
            : (null, UnusableCharacters > 0
                ? "None of the characters on this PC has a combat gearset offline mode can read. Log in once normally and save a gearset for a combat job."
                : $"No characters were found in {OfflineGameFiles.UserFolder()}. Log in once normally so the game saves one.");
        error = selection.Value.Error;
        return selection.Value.Loadout;
    }

    private OfflineLoadout? Resolve(OfflineLocalCharacter character, out string error)
        => OfflineLoadouts.Resolve(character, Store.ChoiceFor(character), Appearances, OfflineGameFiles.InnRooms(), out error);

    public string? UnavailableReason()
    {
        if (Phase == OfflinePhase.InWorld) return null;
        if (OfflineProcessState.Tainted) return "Offline mode couldn't put this game session back. Restart the game to start it again.";
        if (Plugin.ClientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer != null) return "Offline mode starts from the title screen.";
        if (!AtTitleScreen()) return "Go back to the title screen to start offline mode.";
        if (AgentLobby.Instance() is var lobby && lobby != null && lobby->LobbyUIStage != TitleUiStage)
            return "Wait until the title screen is idle, with none of its menus open.";
        if (SaveSettleSeconds() is > 0 and var wait) return $"The game is still saving your last session; offline mode can start in {wait} s.";
        if (OfflineGameFiles.FileModulesBusy() is { } busy) return busy;
        return SelectedLoadout(out var error) == null ? error : null;
    }

    private static int SaveSettleSeconds()
    {
        var last = OfflineProcessState.LastLogout;
        if (last == 0) return 0;
        var remaining = SaveSettleMilliseconds - (Environment.TickCount64 - last);
        return remaining > 0 ? (int)Math.Ceiling(remaining / 1000.0) : 0;
    }

    private static bool AtTitleScreen()
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null) return false;
        var title = manager->GetAddonByName("_TitleMenu");
        return title != null && title->IsVisible;
    }

    public void SelectionChanged()
    {
        lastRefusal = null;
        selection = null;
    }

    public void Start()
    {
        if (Phase != OfflinePhase.Idle) return;
        if (UnavailableReason() is { } reason)
        {
            lastRefusal = reason;
            return;
        }
        Loadout = Resolve(SelectedCharacter!, out _);
        if (Loadout == null) return;
        UseConfig = Store.UseConfig;

        DiagnosticLog.Info($"[Offline] Starting: character {Loadout.Character.Key}, {Loadout.Summary}, inn territory {Loadout.Inn.TerritoryId}, configuration {(UseConfig ? "on" : "off")}.");
        lastRefusal = null;
        notice = null;
        closedLastTime = null;
        exitAfterReturn = false;
        bootstrap?.Dispose();
        bootstrap = new OfflineBootstrap(this);
        Phase = OfflinePhase.Starting;
        Status = "Checking the game...";
    }

    // The game's own Log Out and Exit Game, or the Return to title button.
    public void RequestReturn(bool exitAfterwards)
    {
        returnRequested = true;
        exitAfterReturn = exitAfterwards;
    }

    public void Tick()
    {
        if (OfflineProcessState.Tainted && LoginReached() is { } login)
        {
            CloseGameForLogin(login);
            return;
        }
        if (returnRequested)
        {
            returnRequested = false;
            if (Phase == OfflinePhase.InWorld && LeaveBlockedReason() == null) Leave(null);
            else exitAfterReturn = false;
        }
        if (exitAfterReturn && Phase == OfflinePhase.Idle) ExitFromTitle();
        if (Phase == OfflinePhase.InWorld) bootstrap?.Menus.Apply();

        if (Phase == OfflinePhase.Leaving)
        {
            TickLeaving();
            return;
        }
        if (Phase != OfflinePhase.Starting || bootstrap == null) return;

        OfflineBootstrap.Result result;
        string status;
        try
        {
            result = bootstrap.Step(out status);
        }
        catch (Exception e)
        {
            DiagnosticLog.Warn($"[Offline] Bootstrap threw: {e}");
            Fail($"Offline mode hit an error ({e.GetType().Name}: {e.Message}).");
            return;
        }
        // A logout raised inside the step has settled the phase already.
        if (Phase != OfflinePhase.Starting) return;

        switch (result)
        {
            case OfflineBootstrap.Result.Working:
                Status = status;
                break;
            case OfflineBootstrap.Result.Done:
                Phase = OfflinePhase.InWorld;
                OfflineProcessState.InWorld = true;
                Status = "";
                DiagnosticLog.Info("[Offline] In the offline world.");
                Plugin.MainWindow.IsOpen = true;
                // Out of the way in the world, where the main window has the controls.
                configWindowHidden = ConfigWindow.IsOpen;
                ConfigWindow.IsOpen = false;
                break;
            case OfflineBootstrap.Result.Failed:
                Fail(status);
                break;
        }
    }

    // Before the bootstrap touches the game, a failure is a refusal the user can fix and retry;
    // after, offline mode backs out through the title screen.
    private void Fail(string reason)
    {
        if (bootstrap is { ChangedGameState: false })
        {
            bootstrap.Dispose();
            bootstrap = null;
            Phase = OfflinePhase.Idle;
            Status = "";
            lastRefusal = reason;
            DiagnosticLog.Warn($"[Offline] Start refused: {reason}");
            OfflineTrace.End();
            return;
        }
        DiagnosticLog.Warn($"[Offline] Failed: {reason}");
        Leave(reason + (DiagnosticLog.LogDirectory is { } logs ? $" Details are in {logs}." : ""));
    }

    internal string? LeaveBlockedReason()
    {
        if (Plugin.GameInstance?.World.Map.IsInInstance ?? false) return "Leave the scenario first.";
        if (ZoneSession.GuardArmed) return "The scenario is still winding down; try again in a moment.";
        if (Plugin.Condition[ConditionFlag.BetweenAreas] || Plugin.Condition[ConditionFlag.BetweenAreas51]) return "Wait for the loading screen to finish.";
        return null;
    }

    // Ends offline mode through the game's own logout handler, as a server's reply to a logout
    // would: the game tears the world down and brings the title screen back itself.
    private void Leave(string? reason)
    {
        if (bootstrap?.Snapshot?.SharedMacrosChanged() == true)
        {
            CloseGame("shared macros were edited offline, and offline mode can't put them back");
            return;
        }
        BeginLeaving(reason, ownLogout: true);
        if (bootstrap is not { EnteredGameUi: true }) return;
        var lobby = AgentLobby.Instance();
        if (lobby == null)
        {
            CloseGame("the game's lobby is missing");
            return;
        }
        if (lobby->LobbyUpdateStage is LogoutStage or LogoutEndStage) return;
        var logout = new LogoutCallbackInterface.LogoutParams { Type = 0, Code = NormalLogoutCode };
        OfflineTrace.Write("[Offline] Logging out of the offline world.");
        lobby->LogoutCallbackInterface.OnLogout(&logout);
    }

    private void BeginLeaving(string? reason, bool ownLogout)
    {
        leaveReason = reason;
        this.ownLogout = ownLogout;
        bootstrap?.Menus.Restore();
        OfflineProcessState.InWorld = false;
        Phase = OfflinePhase.Leaving;
        Status = "Returning to the title screen...";
        leavingSince = Stopwatch.GetTimestamp();
        OfflineTrace.Write(OfflineProbe.Describe(reason == null ? "returning to the title screen" : $"returning to the title screen: {reason}"));
    }

    // The guards stay armed until the title screen is back and everything is put back, so nothing
    // the logout does can reach a file, and no login can begin in between.
    private void TickLeaving()
    {
        var lobby = AgentLobby.Instance();
        var control = Control.Instance();
        var atTitle = AtTitleScreen() && lobby != null && lobby->LobbyUpdateStage == TitleStage && control != null && control->LocalPlayer == null;
        if (atTitle || bootstrap is not { EnteredGameUi: true })
        {
            FinishLeaving();
            return;
        }
        if (ownLogout && Stopwatch.GetElapsedTime(leavingSince) > ReturnTimeout)
            CloseGame("the game didn't return to the title screen");
    }

    private void FinishLeaving()
    {
        var current = bootstrap;
        if (current?.Snapshot is not { } snapshot)
        {
            CloseGame("the title screen's state couldn't be put back (it was lost in a plugin reload)");
            return;
        }
        string problem;
        bool restored;
        try
        {
            current.ClearReZoning();
            restored = snapshot.Restore(out problem);
        }
        catch (Exception e)
        {
            restored = false;
            problem = $"{e.GetType().Name}: {e.Message}";
        }
        if (!restored)
        {
            CloseGame($"the title screen's state couldn't be put back ({problem})");
            return;
        }
        current.Dispose();
        bootstrap = null;
        Loadout = null;
        OfflineProcessState.InWorld = false;
        OfflineProcessState.Tainted = false;
        Phase = OfflinePhase.Idle;
        Status = "";
        notice = leaveReason == null
            ? "Back at the title screen. Everything offline mode changed was put back."
            : $"Offline mode stopped: {leaveReason} Everything it changed was put back.";
        OfflineTrace.Write(OfflineProbe.Describe("back at the title screen"));
        DiagnosticLog.Info($"[Offline] {notice}");
        OfflineTrace.End();
        if (configWindowHidden && !exitAfterReturn) ConfigWindow.IsOpen = true;
        configWindowHidden = false;
    }

    // The game's Exit Game, finished by the title menu's own Exit once the title is up: it closes the
    // title, fades out, and the lobby exits the game from a stage that is no longer the title's.
    private void ExitFromTitle()
    {
        var lobby = AgentLobby.Instance();
        if (lobby == null || !TitleIdle(lobby)) return;
        exitAfterReturn = false;
        OfflineTrace.Write("[Offline] Exiting the game from the title screen.");
        var result = new AtkValue();
        var exit = new AtkValue();
        exit.SetInt(TitleExitEvent);
        lobby->AgentInterface.ReceiveEvent(&result, &exit, 1, 0);
    }

    // As a player sees it before picking Exit: the title menu up with no fade over it.
    private static bool TitleIdle(AgentLobby* lobby)
    {
        if (!AtTitleScreen() || lobby->LobbyUIStage != TitleUiStage || lobby->LobbyUpdateStage != TitleStage) return false;
        var fade = RaptureAtkUnitManager.Instance()->GetAddonByName("FadeMiddle");
        return fade == null || !fade->IsVisible;
    }

    // First thing when the plugin unloads. Whatever runs after this plugin is gone, the game's own
    // shutdown included, would run without the file guard, and the logout that puts the game back
    // takes frames an unload doesn't have.
    public void CloseGameIfStarted()
    {
        if (OfflineProcessState.Tainted) CloseGame("AnoMech is unloading");
    }

    // The fallback whenever the game can't be put back: nothing in the offline world is worth
    // saving, and the process ends before anything unguarded can run.
    internal static void CloseGame(string why, bool explainNextTime = true)
    {
        if (!why.EndsWith('.')) why += ".";
        if (explainNextTime) OfflineStore.RecordClose($"AnoMech closed the game ({DateTime.Now:g}): {why}");
        try
        {
            DiagnosticLog.Info($"[Offline] Closing the game: {why}");
            DiagnosticLog.Shutdown();
        }
        catch
        {
            // Closing the game is all that is left to do.
        }
        TerminateProcess(GetCurrentProcess(), 0);
    }

    internal static void CloseGameForLogin(string what)
    {
        OfflineTrace.Write($"[Offline] {what} while offline mode was active.");
        CloseGame($"{what} while offline mode was active. {LoginPrecaution}");
    }

    // OfflineConnectionGuard stops the game before it reaches a server; these are what a login
    // reaches even so.
    private static string? LoginReached()
    {
        if (OfflineProbe.ZoneClient() != 0 || OfflineProbe.ChatClient() != 0) return "the game opened a server connection";
        var lobby = AgentLobby.Instance();
        if (lobby != null && (lobby->IsLoggedIn || lobby->IsLoggedIntoZone)) return "the game logged in";
        return Plugin.ClientState.IsLoggedIn ? "Dalamud reported a login" : null;
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(nint process, uint exitCode);

    // Only at the title screen or in the offline world; normal play never sees it.
    public bool ShowsControls => !Plugin.ClientState.IsLoggedIn || OfflineProcessState.Tainted;

    public void OpenFromTitle() => ConfigWindow.IsOpen = true;

    public void DrawMainWindowBar()
    {
        if (!ShowsControls) return;

        if (ImGui.Button("Offline config")) ConfigWindow.Toggle();
        ImGui.SameLine();
        DrawControls();
        ImGui.Separator();
    }

    public void DrawControls(bool withSummary = true)
    {
        switch (Phase)
        {
            case OfflinePhase.Idle:
                DrawIdle(withSummary);
                break;
            case OfflinePhase.Starting:
            case OfflinePhase.Leaving:
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(ImGuiColors.DalamudYellow, Status);
                break;
            case OfflinePhase.InWorld:
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Offline mode - {Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? Loadout?.Name}");
                ImGui.SameLine();
                if (bootstrap?.Snapshot != null) DrawLeaveButton();
                else DrawCloseButton();
                break;
            case OfflinePhase.Failed:
                DrawCloseButton();
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 520f * ImGuiHelpers.GlobalScale);
                ImGui.TextColored(ImGuiColors.DalamudRed, Status);
                ImGui.PopTextWrapPos();
                break;
        }
    }

    private void DrawIdle(bool withSummary)
    {
        if (UnavailableReason() is { } reason)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(reason);
        }
        else
        {
            if (ImGui.Button("Enter offline inn")) Start();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Experimental. Loads an inn room without connecting to a server, so you can practice scenarios while logged out.\n"
                                 + "Returning to the title screen puts everything back.\n"
                                 + "If anything tries to log in or reach a server meanwhile, the game closes as a precaution.");
            if (withSummary && SelectedLoadout(out _) is { } loadout)
            {
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled($"{loadout.Name}: {loadout.Summary}");
            }
        }
        if (lastRefusal != null || notice != null || closedLastTime != null)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 520f * ImGuiHelpers.GlobalScale);
            if (closedLastTime != null) ImGui.TextColored(ImGuiColors.DalamudOrange, closedLastTime);
            if (lastRefusal != null) ImGui.TextColored(ImGuiColors.DalamudOrange, lastRefusal);
            if (notice != null) ImGui.TextColored(ImGuiColors.HealerGreen, notice);
            ImGui.PopTextWrapPos();
        }
    }

    private void DrawLeaveButton()
    {
        var blocked = LeaveBlockedReason();
        ImGui.BeginDisabled(blocked != null);
        if (ImGui.Button("Return to title")) RequestReturn(exitAfterwards: false);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(blocked ?? "Logs out of the offline world and puts everything offline mode changed back,\n"
                                        + "so you can log in normally or start offline mode again.");
    }

    private void DrawCloseButton()
    {
        if (!confirmClose)
        {
            if (ImGui.Button("Close the game")) confirmClose = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Offline mode can't put this game session back, so it can only be closed.");
            return;
        }
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
        if (ImGui.Button("Close the game now")) CloseGame("Close the game", explainNextTime: false);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        if (ImGui.Button("Cancel##close")) confirmClose = false;
    }
}
