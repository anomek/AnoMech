using System;
using Dalamud.Game.Command;
using Dalamud.Game.DutyState;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.Native;
using AnoMech.Multiplayer;
using AnoMech.Core.UserActions;
using AnoMech.Windows;
using AnoMech.Pointers;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace AnoMech;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFlyTextGui FlyText { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IJobGauges JobGauges { get; private set; } = null!;
    [PluginService] internal static ITitleScreenMenu TitleScreenMenu { get; private set; } = null!;

    private const string CommandName = "/anomech";
    private const string CommandAlias = "/ano";
    private bool commandsRegistered;

    public Configuration Configuration { get; init; }
    internal static Configuration Config { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("AnoMech");
    public Game Game { get; }
    public MultiplayerManager Multiplayer { get; } = new();
    internal static MultiplayerManager MultiplayerInstance { get; private set; } = null!;
    // SimObjects reach engine singletons through these statics (mirrors the
    // Plugin.* PluginService pattern).
    internal static Game GameInstance { get; private set; } = null!;
    // Session-lifetime, hooked once per load; SimPlayer is the sole writer of their flags.
    internal static LocalPlayerInputHooks PlayerInputHooks { get; private set; } = null!;
    // Optional, detached module: resolves the player's own actions client-side.
    internal static UserActions UserActions { get; private set; } = null!;
    internal static LogManager LogManager { get; private set; } = null!;
    private ConfigWindow ConfigWindow { get; init; }
    // Static so MultiplayerManager can read the host's current selection.
    internal static MainWindow MainWindow { get; private set; } = null!;
    internal MultiplayerWindow MultiplayerWindow { get; init; }
    internal RunningSimWindow RunningSimWindow { get; init; }
    internal Offline.OfflineSession? Offline { get; private set; }
    private Dalamud.Interface.IReadOnlyTitleScreenMenuEntry? offlineTitleEntry;
#if DEBUG
    private DamageDebugWindow DamageDebugWindow { get; init; }
#endif

    public Plugin()
    {
        // First, so every subsequent construction step's own logging is captured from the start.
        Core.DiagnosticLog.Initialize();
        try
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config = Configuration;

            LogManager = new LogManager();
            if (Config.EnableEventLogging) LogManager.Open();

            PlayerInputHooks = new LocalPlayerInputHooks(GameInterop);
            Game = new Game();
            GameInstance = Game;
            MultiplayerInstance = Multiplayer;
            UserActions = new UserActions(PlayerInputHooks);
            if (Config.EnableUserActions) UserActions.Enable();
            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this);
            MultiplayerWindow = new MultiplayerWindow(this);
            RunningSimWindow = new RunningSimWindow(this);

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(MainWindow.ScenarioPanel);
            WindowSystem.AddWindow(MultiplayerWindow);
            WindowSystem.AddWindow(RunningSimWindow);
#if DEBUG
            DamageDebugWindow = new DamageDebugWindow(this);
            WindowSystem.AddWindow(DamageDebugWindow);
#endif

            if (Config.OpenSimMenuOnInn && ZoneSession.IsInInn())
                MainWindow.IsOpen = true;

            StartOffline();

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open AnoMech. Subcommands: config, mp, start, reset, leave, offline"
            });
            CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
            {
                HelpMessage = "Alias for /anomech"
            });
            commandsRegistered = true;

            PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
            Framework.Update += OnFrameworkUpdate;

            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
            ClientState.TerritoryChanged += OnTerritoryChanged;
            ClientState.Logout += OnLogout;
            DutyState.DutyStarted += OnDutyStarted;
            DutyState.DutyWiped += OnDutyWiped;
            DutyState.DutyCompleted += OnDutyCompleted;

            // Initialize Pointers
            CharacterManagerPointers.Initialize();
            EventFrameworkPointers.Initialize();
            EventObjectManagerPointers.Initialize();
            EventObjectPointers.Initialize();
            GameMainPointers.Initialize();
            ModelContainerPointers.Initialize();
            PacketDispatcherPointers.Initialize();
            RsfPointers.Initialize();
            StatusManagerPointers.Initialize();
            TimelineContainerPointers.Initialize();
            VfxContainerPointers.Initialize();
            VfxDataPointers.Initialize();

            Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
            // The diagnostic log's own header is written before any hook exists, so it alone
            // doesn't prove a load succeeded.
            Core.DiagnosticLog.Info("[Plugin] constructed OK -- all hooks and pointers initialized.");
        }
        catch (Exception)
        {
            // Dalamud never disposes an instance whose constructor threw; the log writer's open
            // handle would then block every later load from rotating the active log.
            Log.Warning("[Plugin] Load failed during construction -- tearing down partial state.");
            try
            {
                Dispose();
            }
            catch (Exception teardown)
            {
                Log.Warning($"[Plugin] Partial teardown after failed load threw: {teardown.Message}");
            }
            throw;
        }
    }

    // Null-tolerant throughout: the constructor's failure path calls this on a half-built
    // instance, where anything past the throwing step was never assigned.
    public void Dispose()
    {
        Offline?.CloseGameIfStarted();
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        ClientState.Logout -= OnLogout;
        DutyState.DutyStarted -= OnDutyStarted;
        DutyState.DutyWiped -= OnDutyWiped;
        DutyState.DutyCompleted -= OnDutyCompleted;

        WindowSystem.RemoveAllWindows();

        Core.Native.TimelineDebug.Shutdown();
        Core.Native.VfxSpawnLog.Dispose();
        Multiplayer.Dispose();
        Game?.Dispose();
        if (offlineTitleEntry != null) TitleScreenMenu.RemoveEntry(offlineTitleEntry);
        Offline?.Dispose();
        UserActions?.Dispose();
        // After Game.Dispose so World.Dispose → SimPlayer.Despawn can still clear
        // the lock flags through the hooks before they're torn down.
        PlayerInputHooks?.Dispose();
        LogManager?.Dispose();
        ConfigWindow?.Dispose();
        MainWindow?.Dispose();
        MultiplayerWindow?.Dispose();
#if DEBUG
        DamageDebugWindow?.Dispose();
#endif

        if (commandsRegistered)
        {
            CommandManager.RemoveHandler(CommandName);
            CommandManager.RemoveHandler(CommandAlias);
        }

        // Last, so it captures every other subsystem's teardown logging before the DLL unloads.
        Core.DiagnosticLog.Shutdown();
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        // FrameDeltaTime, not framework.UpdateDelta: UpdateDelta is wall-clock
        // truncated to whole ms, so summing it drifts. FrameDeltaTime is the
        // full-precision delta the game ticks its own animations with.
        var fw = CSFramework.Instance();
        if (fw == null) return;
        // First and on its own: the guard must run when Game.Tick is paused or throwing.
        try { ZoneSession.TickGuard(); }
        catch (Exception e) { Core.DiagnosticLog.Warn($"[Plugin] ZoneSession.TickGuard threw: {e}"); }
        // Both ticks reach code driven by whatever a relay sent; neither may take the frame
        // pump down.
        try { Game.Tick(fw->FrameDeltaTime); }
        catch (Exception e) { Core.DiagnosticLog.Warn($"[Plugin] Game.Tick threw: {e}"); }
        try { UserActions.Tick(fw->FrameDeltaTime); }
        catch (Exception e) { Core.DiagnosticLog.Warn($"[Plugin] UserActions.Tick threw: {e}"); }
        try { Multiplayer.Tick(fw->FrameDeltaTime); }
        catch (Exception e) { Core.DiagnosticLog.Warn($"[Plugin] Multiplayer.Tick threw: {e}"); }
        try { Offline?.Tick(); }
        catch (Exception e) { Core.DiagnosticLog.Warn($"[Plugin] Offline.Tick threw: {e}"); }
    }

    // Offline mode is optional: whatever goes wrong setting it up must not cost the plugin its load.
    private void StartOffline()
    {
        try
        {
            Offline = new Offline.OfflineSession();
            WindowSystem.AddWindow(Offline.ConfigWindow);
            var icon = TextureProvider.GetFromManifestResource(typeof(Plugin).Assembly, "AnoMech.OfflineIcon.png");
            offlineTitleEntry = TitleScreenMenu.AddEntry("AnoMech offline mode", icon, Offline.OpenFromTitle);
        }
        catch (Exception e)
        {
            Log.Warning($"[Plugin] Offline mode is unavailable: {e.Message}");
        }
    }

    private void OnTerritoryChanged(uint territory)
    {
        ZoneSession.NoteTerritoryChanged(territory);
        var row = DataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territory);
        var isInn = row?.TerritoryIntendedUse.RowId == 2; // TerritoryIntendedUse.Inn
        if (!isInn)
        {
            var name = row?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            LogManager.LogEnterInstance(territory, name);
        }

        if (!isInn)
        {
            MainWindow.IsOpen = false;
            return;
        }
        if (Config.OpenSimMenuOnInn)
            MainWindow.IsOpen = true;
    }

    private void OnLogout(int type, int code) => ZoneSession.NoteLogout(type, code);

    private void OnDutyStarted(IDutyStateEventArgs args)
        => LogManager.LogCombatStart(args.TerritoryType.RowId);

    private void OnDutyWiped(IDutyStateEventArgs args)
        => LogManager.LogCombatEnd(args.TerritoryType.RowId, wipe: true);

    private void OnDutyCompleted(IDutyStateEventArgs args)
        => LogManager.LogCombatEnd(args.TerritoryType.RowId, wipe: false);

    private void OnCommand(string command, string args)
    {
        switch (args.Trim())
        {
            case "config":
                ConfigWindow.Toggle();
                break;
            case "mp":
            case "multiplayer":
                MultiplayerWindow.Toggle();
                break;
            case "start":
                StartSelectedScenario(solo: false);
                break;
            case "start solo":
                StartSelectedScenario(solo: true);
                break;
            case "reset":
                ResetScenario();
                break;
            case "leave":
                LeaveInstance();
                break;
            case "offline":
                Offline?.ConfigWindow.Toggle();
                break;
            default:
                MainWindow.Toggle();
                break;
        }
    }

    internal string? StartRefusal(bool solo)
    {
        if (MainWindow.SelectedScenario is not { } scenario) return "No scenario selected.";
        if (solo && !scenario.SupportsSolo) return $"{scenario.Name} does not support Solo mode.";
        // Starting here bypasses MultiplayerManager: a host would run the fight without a
        // StartMessage, a peer would start a second independent simulation.
        if (scenario.SupportsMultiplayer && Multiplayer.IsConnected)
            return "Connected to a multiplayer session -- use Start in the Multiplayer window instead.";
        if (Game.StartWaitingOn is { } waiting) return $"Waiting for {waiting} to settle before starting...";
        // A settle only delays the start; Game.RunScenario waits it out.
        if (ZoneSession.StartBlockedReason(out var settling) is { } blocked && settling == null)
            return $"Cannot start: {blocked}.";
        if (!solo && !MainWindow.HasStartableStrat()) return "No strat available for this region yet.";
        return null;
    }

    internal void StartSelectedScenario(bool solo)
    {
        if (StartRefusal(solo) is { } refusal)
        {
            Log.Warning(refusal);
            return;
        }
        Game.RunScenario(MainWindow.SelectedScenario!, MainWindow.SelectedRoleOverride, solo ? null : MainWindow.SelectedStrat, MainWindow.SelectedWaymark);
    }

    internal void ResetScenario()
    {
        // A peer's own Game.Reset() would only clear their local view.
        if (Multiplayer.IsConnected && !Multiplayer.IsHost)
            Multiplayer.RequestReset();
        else
            Game.Reset();
    }

    internal void LeaveInstance()
    {
        if (!Game.World.Map.IsInInstance) return;
        // A peer's own Game.Leave() would leave the host simulating for a torn-down world.
        if (Multiplayer.IsConnected && !Multiplayer.IsHost)
            Multiplayer.RequestLeaveInstance();
        else
        {
            Game.Leave();
            // A prior Reset consumed Tick()'s one-shot end trigger (see NotifyLeftInstance).
            Multiplayer.NotifyLeftInstance();
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
