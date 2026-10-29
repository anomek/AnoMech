using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using AnoMech.Core;
using AnoMech.Core.Native.Implementations;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
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
    // Each unload takes a frame or two; Dalamud never turns off on its own a plugin that is in more
    // than one collection.
    private static readonly TimeSpan TurnOffTimeout = TimeSpan.FromSeconds(20);
    // A real logout's saves are issued as it happens and written asynchronously; they are long done
    // by then, and offline mode's file guard would refuse one still in flight.
    private const long SaveSettleMilliseconds = 10_000;
    internal const string LoginPrecaution = "As a precaution, the game closes if anything tries to log in or reach a server while offline mode is active.";
    // Dalamud.Boot hands this exception code to the crash handler, which ends the game and launches it
    // again through Dalamud.Injector with the launcher's arguments: Dalamud's own Restart game.
    private const uint DalamudRestartCode = 0x12345678;

    internal static OfflineSession? Current { get; private set; }
    // Process-wide rather than this instance's phase: the game stays in the offline world across
    // plugin reloads.
    internal static bool InWorld => OfflineProcessState.InWorld;

    public OfflineStore Store { get; }
    public OfflinePanel Panel { get; }
    public OfflinePhase Phase { get; private set; }
    public string Status { get; private set; } = "";
    public bool Busy => Phase is OfflinePhase.Starting or OfflinePhase.Leaving;
    public OfflineLoadout? Loadout { get; private set; }
    public bool UseConfig { get; private set; }
    public string? LastRefusal => lastRefusal;
    public string? Notice => notice;
    public string? ClosedLastTime => closedLastTime;
    public bool StillConnected => OfflineConnectionGuard.OpenServerConnection() != null;
    // Display names of the blacklisted plugins starting turns off.
    public IReadOnlyList<string> TurnsOff { get; private set; } = [];

    private readonly Action openMainWindow;
    private List<OfflineLocalCharacter>? characters;
    private List<OfflineAppearance>? appearances;
    private int unusable;
    private (OfflineLoadout? Loadout, string Error)? selection;
    private OfflineBootstrap? bootstrap;
    private bool returnRequested;
    private bool exitAfterReturn;
    private string? lastRefusal;
    private string? notice;
    private string? closedLastTime;
    private string? leaveReason;
    private bool ownLogout;
    private long leavingSince;
    private long turningOffSince;

    public OfflineSession(Action openMainWindow)
    {
        this.openMainWindow = openMainWindow;
        Store = new OfflineStore(Plugin.PluginInterface.ConfigDirectory.FullName);
        Panel = new OfflinePanel(this);
        Current = this;
        closedLastTime = Store.TakeCloseRecord();
        if (closedLastTime != null) DiagnosticLog.Info($"[Offline] {closedLastTime}");

        Plugin.ClientState.Logout += OnLogout;
        TurnsOff = OfflinePlugins.Loaded();
        Plugin.PluginInterface.ActivePluginsChanged += OnPluginsChanged;

        if (OfflineProcessState.Tainted) Resume();
    }

    public void Dispose()
    {
        Plugin.ClientState.Logout -= OnLogout;
        Plugin.PluginInterface.ActivePluginsChanged -= OnPluginsChanged;
        bootstrap?.Dispose();
        // Unloaded while still starting; a tainted game was closed before this.
        if (!OfflineProcessState.Tainted) OfflinePlugins.TurnBackOn();
        if (Current == this) Current = null;
    }

    private void OnPluginsChanged(IActivePluginsChangedEventArgs args) => TurnsOff = OfflinePlugins.Loaded();

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
            ? "Offline mode ended in an earlier plugin load and can't put the game back. Restart the game."
            : $"Offline mode couldn't resume after a plugin reload ({why}). Restart the game.";
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
                ? "No character on this PC has a combat gearset. Log in once and save one for a combat job."
                : $"No characters found in {OfflineGameFiles.UserFolder()}. Log in once so the game saves one.");
        error = selection.Value.Error;
        return selection.Value.Loadout;
    }

    private OfflineLoadout? Resolve(OfflineLocalCharacter character, out string error)
        => OfflineLoadouts.Resolve(character, Store.ChoiceFor(character), Appearances, OfflineGameFiles.InnRooms(), out error);

    public string? UnavailableReason()
    {
        if (Phase == OfflinePhase.InWorld) return null;
        if (OfflineProcessState.Tainted) return "Offline mode couldn't put this game session back. Restart the game to start it again.";
        if (!OfflinePlugins.CanHoldAutoUpdate) return "Offline mode can't hold off this Dalamud version's plugin auto-update.";
        if (Plugin.ClientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer != null) return "Offline mode starts from the title screen.";
        if (!AtTitleScreen()) return "Go back to the title screen to start offline mode.";
        if (AgentLobby.Instance() is var lobby && lobby != null && lobby->LobbyUIStage != TitleUiStage)
            return "Wait until the title screen is idle, with none of its menus open.";
        if (SaveSettleSeconds() is > 0 and var wait) return $"The game is still saving your last session; ready in {wait} s.";
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

        // From here, so turning the blacklisted plugins off is in the offline log too.
        OfflineTrace.Begin();
        DiagnosticLog.Info($"[Offline] Starting: character {Loadout.Character.Key}, {Loadout.Summary}, inn territory {Loadout.Inn.TerritoryId}, configuration {(UseConfig ? "on" : "off")}.");
        lastRefusal = null;
        notice = null;
        closedLastTime = null;
        exitAfterReturn = false;
        OfflinePlugins.TurnOff();
        turningOffSince = Stopwatch.GetTimestamp();
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
        // From the zone-in until the logout, as a zone connection would.
        if (Phase == OfflinePhase.InWorld || Phase == OfflinePhase.Starting && bootstrap is { ZoneEntered: true }) bootstrap?.TickWeather();
        if (Phase != OfflinePhase.Idle) OfflinePlugins.HoldAutoUpdate();

        if (Phase == OfflinePhase.Leaving)
        {
            TickLeaving();
            return;
        }
        if (Phase != OfflinePhase.Starting || bootstrap == null) return;
        // The blacklisted plugins are gone before the bootstrap touches the game.
        if (!bootstrap.ChangedGameState && OfflinePlugins.Loaded() is { Count: > 0 } loaded)
        {
            var names = string.Join(", ", loaded);
            if (Stopwatch.GetElapsedTime(turningOffSince) > TurnOffTimeout)
                Fail($"{names} didn't turn off. Turn {(loaded.Count == 1 ? "it" : "them")} off in the plugin installer and try again.");
            else Status = $"Turning off {names}...";
            return;
        }

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
                openMainWindow();
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
            OfflinePlugins.TurnBackOn();
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
        if (Plugin.Game?.World.Map.IsInInstance ?? false) return "Leave the scenario first.";
        if (ZoneControl.GuardArmed) return "The scenario is still winding down; try again in a moment.";
        if (Plugin.Condition[ConditionFlag.BetweenAreas] || Plugin.Condition[ConditionFlag.BetweenAreas51]) return "Wait for the loading screen to finish.";
        return null;
    }

    // Returning puts back the title screen as it was when offline mode started, which a plugin
    // reload forgets.
    internal string? ReturnBlockedReason()
        => bootstrap?.Snapshot == null ? "A plugin reload lost the title screen's state; restart the game instead." : LeaveBlockedReason();

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
        // Not before an exit, which would only load them to shut them down.
        if (!exitAfterReturn) OfflinePlugins.TurnBackOn();
        Phase = OfflinePhase.Idle;
        Status = "";
        notice = leaveReason == null
            ? "Back at the title screen with everything put back."
            : $"Offline mode stopped: {leaveReason} Everything was put back.";
        OfflineTrace.Write(OfflineProbe.Describe("back at the title screen"));
        DiagnosticLog.Info($"[Offline] {notice}");
        OfflineTrace.End();
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

    // A new process, so nothing offline mode did outlives it. Without a crash handler to take the
    // exception the game only closes.
    internal static void RestartGame()
    {
        try
        {
            DiagnosticLog.Info("[Offline] Restarting the game.");
            DiagnosticLog.Shutdown();
        }
        catch
        {
            // Restarting is all that is left to do.
        }
        try
        {
            RaiseException(DalamudRestartCode, 0, 0, 0);
        }
        catch
        {
            // Nothing restarted the game.
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
        if (OfflineConnectionGuard.OpenServerConnection() is { } server) return $"the game had a server connection open ({server})";
        var lobby = AgentLobby.Instance();
        if (lobby != null && (lobby->IsLoggedIn || lobby->IsLoggedIntoZone)) return "the game logged in";
        return Plugin.ClientState.IsLoggedIn ? "Dalamud reported a login" : null;
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(nint process, uint exitCode);

    [DllImport("kernel32.dll")]
    private static extern void RaiseException(uint code, uint flags, uint argumentCount, nint arguments);

    // Only at the title screen or in the offline world; normal play never sees it.
    public bool ShowsControls => !Plugin.ClientState.IsLoggedIn || OfflineProcessState.Tainted;

    // The main window is offline mode's setup screen until the offline world is up.
    public bool ShowsSetup => ShowsControls && Phase is OfflinePhase.Idle or OfflinePhase.Starting;
}
