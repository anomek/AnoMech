using System;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Multiplayer;
using AnoMech.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.DutyState;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
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
    [PluginService] internal static IToastGui ToastGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static ITitleScreenMenu TitleScreenMenu { get; private set; } = null!;

    private const string CommandName = "/anomech";
    private const string CommandAlias = "/ano";
    private bool commandsRegistered;

    internal static Configuration Config { get; private set; } = null!;
    internal static Game Game { get; private set; } = null!;
    internal static PracticeLoop Practice { get; private set; } = null!;
    internal static MultiplayerManager Multiplayer { get; private set; } = null!;
    internal RunController Runs { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("AnoMech");

    // Plugin-lifetime singletons, static like the PluginServices above so sim code reaches them
    // without a Plugin reference.
    internal static LogManager LogManager { get; private set; } = null!;
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private FailureBanner? FailureBanner { get; init; }
    internal Offline.OfflineSession? Offline { get; private set; }
    private Dalamud.Interface.IReadOnlyTitleScreenMenuEntry? offlineTitleEntry;
#if DEBUG
    private DamageDebugWindow DamageDebugWindow { get; init; }
#endif

    private OpcodeUpdater? opcodeUpdater;

    public Plugin()
    {
        // First, so every subsequent construction step's own logging is captured from the start.
        DiagnosticLog.Initialize();
        try
        {
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            LogManager = new LogManager();
            if (Config.EnableEventLogging) LogManager.Open();

            GameNatives.Install();
            opcodeUpdater = new OpcodeUpdater();
            Game = new Game();
            Practice = new PracticeLoop(Game);
            Multiplayer = new MultiplayerManager();
            Runs = new RunController(Game, Multiplayer);
            if (Config.EnableUserActions) Natives.UserActions.Enable();
            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this);

            windowSystem.AddWindow(ConfigWindow);
            windowSystem.AddWindow(MainWindow);
            windowSystem.AddWindow(MainWindow.ScenarioPanel);
            windowSystem.AddWindow(MainWindow.PartyPanel);
            FailureBanner = new FailureBanner();
            windowSystem.AddWindow(FailureBanner);
#if DEBUG
            DamageDebugWindow = new DamageDebugWindow(this);
            windowSystem.AddWindow(DamageDebugWindow);
#endif

            if (Config.OpenSimMenuOnInn && ZoneControl.IsInInn())
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

            PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            Framework.Update += OnFrameworkUpdate;

            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
            ClientState.TerritoryChanged += OnTerritoryChanged;
            ClientState.Logout += OnLogout;
            DutyState.DutyStarted += OnDutyStarted;
            DutyState.DutyWiped += OnDutyWiped;
            DutyState.DutyCompleted += OnDutyCompleted;

            // Initialize Pointers
            EventObjectManagerPointers.Initialize();
            GameMainPointers.Initialize();
            ModelContainerPointers.Initialize();
            PacketDispatcherPointers.Initialize();
            ResolvedStringPointers.Initialize();

            Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
            // The diagnostic log's own header is written before any hook exists, so it alone
            // doesn't prove a load succeeded.
            DiagnosticLog.Info("[Plugin] constructed OK -- all hooks and pointers initialized.");
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
        try
        {
            DisposeSubsystems();
        }
        finally
        {
            // Dalamud disposes this plugin's hooks once Dispose returns or throws, so a run must be
            // stopped or lifted by then even when a step above threw. A no-op after a clean Game.Dispose.
            ZoneControl.Current?.Dispose();
        }

        // Last, so it captures every other subsystem's teardown logging before the DLL unloads.
        DiagnosticLog.Shutdown();
    }

    private void DisposeSubsystems()
    {
        Offline?.CloseGameIfStarted();
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        ClientState.Logout -= OnLogout;
        DutyState.DutyStarted -= OnDutyStarted;
        DutyState.DutyWiped -= OnDutyWiped;
        DutyState.DutyCompleted -= OnDutyCompleted;

        windowSystem.RemoveAllWindows();

        TimelineDebug.Shutdown();
        Multiplayer?.Dispose();
        Game?.Dispose();
        if (offlineTitleEntry != null) TitleScreenMenu.RemoveEntry(offlineTitleEntry);
        Offline?.Dispose();
        Natives.Dispose();
        opcodeUpdater?.Dispose();
        LogManager?.Dispose();
        ConfigWindow?.Dispose();
        MainWindow?.Dispose();
        FailureBanner?.Dispose();
#if DEBUG
        DamageDebugWindow?.Dispose();
#endif

        if (commandsRegistered)
        {
            CommandManager.RemoveHandler(CommandName);
            CommandManager.RemoveHandler(CommandAlias);
        }
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        // FrameDeltaTime, not framework.UpdateDelta: UpdateDelta is wall-clock
        // truncated to whole ms, so summing it drifts. FrameDeltaTime is the
        // full-precision delta the game ticks its own animations with.
        var fw = CSFramework.Instance();
        if (fw == null) return;
        // First: the zone guard must run when Game.Tick is paused or throwing.
        Natives.Tick(fw->FrameDeltaTime);
        // Both ticks reach code driven by whatever a relay sent; neither may take the frame
        // pump down.
        try { Runs.Tick(); }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] Runs.Tick threw: {e}"); }
        try { Game.Tick(fw->FrameDeltaTime); }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] Game.Tick threw: {e}"); }
#if DEBUG
        try { DamageDebugWindow.Instance?.Tick(fw->FrameDeltaTime); }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] DamageDebugWindow.Tick threw: {e}"); }
#endif
        try
        {
            // Every frame, not just while the settings are drawn: peers read what the host has
            // selected whether or not its window is open.
            if (Multiplayer.SessionCode != null)
            {
                Multiplayer.PublishSelectedScenario(MainWindow.SelectedScenario);
                Multiplayer.PublishScenarioSettings(MainWindow.SelectedScenario);
            }
            Multiplayer.Tick(fw->FrameDeltaTime);
        }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] Multiplayer.Tick threw: {e}"); }
        try { Offline?.Tick(); }
        catch (Exception e) { DiagnosticLog.Warn($"[Plugin] Offline.Tick threw: {e}"); }
    }

    // Offline mode is optional: whatever goes wrong setting it up must not cost the plugin its load.
    private void StartOffline()
    {
        try
        {
            Offline = new Offline.OfflineSession(() => MainWindow.IsOpen = true);
            var icon = TextureProvider.GetFromManifestResource(typeof(Plugin).Assembly, "AnoMech.OfflineIcon.png");
            offlineTitleEntry = TitleScreenMenu.AddEntry("AnoMech offline mode", icon, () => MainWindow.IsOpen = true);
        }
        catch (Exception e)
        {
            Log.Warning($"[Plugin] Offline mode is unavailable: {e.Message}");
        }
    }

    private void OnTerritoryChanged(uint territory)
    {
        ZoneControl.NoteTerritoryChanged(territory);
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

    private void OnLogout(int type, int code) => ZoneControl.NoteLogout(type, code);

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
                MainWindow.OpenMultiplayer();
                break;
            case "start":
                Runs.Start(MainWindow.Selection, solo: false);
                break;
            case "start solo":
                Runs.Start(MainWindow.Selection, solo: true);
                break;
            case "reset":
                Runs.Reset();
                break;
            case "leave":
                Runs.Leave();
                break;
            case "offline":
                MainWindow.IsOpen = true;
                break;
            default:
                MainWindow.Toggle();
                break;
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();

    public void OpenSettings()
    {
        ConfigWindow.IsOpen = true;
        ConfigWindow.BringToFront();
    }
}
