using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AnoMech.SafetyTests.Fakes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.Network;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AnoMech.SafetyTests.Harness;

public enum HookRole { Send, Receive }

// Dalamud's hook backends; see the fake Hook<T>.
public enum HookBackend { Reloaded, SafetyHook }

internal interface IHookHost
{
    HookBackend Backend { get; }
    bool Refuses(HookRole role, bool enabling);
    void OnHookStateChanged(HookRole role, bool enabled, string by);
    void OnHookDisposed(HookRole role);
}

internal interface IFakeHook
{
    HookRole Role { get; }
    bool IsEnabled { get; }
    bool IsDisposed { get; }
    void ForceDisable(string by);
    void ForceDispose(string by);
}

// Environment.FailFast in the plugin. Unwinds to the harness; the death is recorded before it is
// thrown, so code that caught it could not bring the game back.
public sealed class GameTerminated : Exception
{
    public GameTerminated(string reason) : base(reason)
    {
    }
}

public sealed record ServerPacket(ushort Opcode, string What, double At, uint ClientTerritory, Vector3 ClientPosition);

internal sealed class CastState
{
    public required uint ActionId { get; init; }
    public required float Total { get; init; }
    public float Elapsed { get; set; }
}

// The game client, its server and Dalamud's plumbing, as the zone session sees them. The client
// state is split the way the game splits it: what the client has loaded and shows (LoadedTerritory,
// ClientPosition), what Dalamud reports (DalamudTerritory) and what the server believes
// (ServerTerritory, ServerPosition). Every packet the send path lets through is judged against the
// server's belief, which is how a leak shows up.
public sealed unsafe class VirtualGame : IDisposable, IHookHost
{
    private static VirtualGame? current;

    public static VirtualGame Current => current ?? throw new InvalidOperationException("No VirtualGame is running.");

    // ---- world -----------------------------------------------------------------------------

    public const uint InnTerritory = 177;
    public const uint OtherInnTerritory = 179;
    public const uint CityTerritory = 129;
    public const uint OtherCityTerritory = 132;
    public const uint DutyTerritory = 1122;
    public const uint OtherDutyTerritory = 1363;
    public const uint TerritoryWithoutDutyEntry = 1999;
    public const uint UnknownTerritory = 4999;
    public const uint DutyContentFinderCondition = 1005;
    public const uint OtherDutyContentFinderCondition = 1063;

    public static readonly Vector3 InnPosition = new(-2.21f, 0.02f, 3.13f);
    public static readonly Vector3 CityAetherytePosition = new(-84.03f, 18.00f, -1.84f);

    public const ushort HeartbeatOpcode = 0x02C3;
    public const ushort MovementOpcode = 0x01F2;
    public const ushort ActionRequestOpcode = 0x0305;
    public const ushort ChatOpcode = 0x0411;
    // Sent by the client once a zone finishes loading; the server only expects one it asked for.
    public const ushort PostLoadOpcode = 0x0187;
    public const ushort KeepAliveInboundOpcode = 0x0077;
    public const ushort InitZoneInboundOpcode = 0x0199;
    public const ushort ActorCastInboundOpcode = 0x0233;
    public const ushort CastCancelInboundOpcode = 0x0234;
    public const ushort ActorMoveInboundOpcode = 0x0341;

    public const uint TeleportActionId = 5;
    public const uint ReturnActionId = 6;
    public const float TeleportCastSeconds = 5f;
    public const double HeartbeatIntervalSeconds = 5;
    public const float ClientZoneLoadSeconds = 1f;
    // The largest jump between two consecutive packets the server takes as ordinary movement.
    public const float MaxServerStep = 3f;

    // ---- plumbing --------------------------------------------------------------------------

    internal FakeSigScanner SigScanner { get; }
    internal FakeGameInterop GameInterop { get; }
    internal FakeLog Log { get; }
    internal FakeAddonLifecycle AddonLifecycle { get; }
    internal FakeDataManager DataManager { get; }
    internal FakeClientState ClientState { get; }
    internal FakeObjectTable ObjectTable { get; }
    internal FakePlayerState PlayerState { get; }
    internal FakeCondition Condition { get; }
    internal FakeFramework Framework { get; }
    internal FakeConfig Config { get; } = new();

    public long Ticks { get; private set; } = TimeSpan.FromSeconds(1000).Ticks;
    public double Now => Ticks / (double)TimeSpan.TicksPerSecond;

    private readonly List<(long Due, long Order, Action Action, string What)> timers = new();
    private long timerOrder;
    private readonly List<Action> frameworkQueue = new();
    public int PendingTimers => timers.Count;
    public int PendingFrameworkTasks => frameworkQueue.Count;

    private readonly List<nint> allocations = new();
    internal nint ConditionsMemory { get; }
    internal nint GameMainMemory { get; }
    internal nint EventFrameworkMemory { get; }
    internal nint UIStateMemory { get; }
    internal nint EnvManagerMemory { get; }
    internal nint InventoryMemory { get; }
    internal nint AgentModuleMemory { get; }
    internal nint AgentMemory { get; }
    internal nint PacketDispatcherVTableMemory { get; }
    private readonly nint heartbeatSignatureMemory;
    private readonly nint dispatcherMemory;

    public const string HeartbeatSignature = "C7 44 24 ?? ?? ?? ?? ?? 48 F7 F1";
    public const string SendPacketSignature = "48 89 5C 24 ?? 48 89 74 24 ?? 4C 89 64 24 ?? 55 41 56 41 57 48 8B EC 48 83 EC 70";
    public static readonly nint SendFunctionAddress = (nint)0x7FF6_1000;
    public static readonly nint ReceiveFunctionAddress = (nint)0x7FF6_2000;

    // ---- the local player and other objects ------------------------------------------------

    private readonly Dictionary<int, nint> objects = new();
    private int nextObjectId = 1;
    private readonly FakeLocalPlayer localPlayer;
    public bool LocalPlayerPresent { get; set; } = true;
    internal FakeLocalPlayer? LocalPlayerVisible => LoggedIn && LocalPlayerPresent ? localPlayer : null;
    public Vector3 ClientPosition { get; set; } = InnPosition;
    public float ClientRotation { get; set; } = 1.2f;
    public byte PlayerLevel { get; set; } = 100;
    public uint ClassJobId { get; set; } = 19;
    internal CastState? Cast { get; private set; }
    public int DrawDisabledCount { get; private set; }

    // ---- client state ----------------------------------------------------------------------

    public bool LoggedIn { get; private set; } = true;
    public uint LoadedTerritory { get; private set; } = InnTerritory;
    public uint DalamudTerritory
    {
        get => ClientState.Stored;
        private set => ClientState.Stored = value;
    }
    public uint EventFrameworkTerritory { get; private set; } = InnTerritory;
    public uint? DirectorContent { get; private set; }
    public ushort SyncedLevel { get; private set; }
    private readonly HashSet<ConditionFlag> gameFlags = new();
    public IReadOnlyCollection<ConditionFlag> GameFlags => gameFlags;

    // ---- server ----------------------------------------------------------------------------

    public uint ServerTerritory { get; private set; } = InnTerritory;
    public Vector3 ServerPosition { get; private set; } = InnPosition;
    public List<ServerPacket> ServerReceived { get; } = new();
    private (double CompleteAt, uint Destination, Vector3 Position, string What)? serverZoneChange;
    public bool ServerHasPendingZoneChange => serverZoneChange != null;
    private double lastHeartbeatAt;
    private Vector3 lastSentPosition = InnPosition;

    // ---- hooks -----------------------------------------------------------------------------

    private IFakeHook? sendHook;
    private IFakeHook? receiveHook;
    private Func<nint, nint, nint, byte, byte>? sendDetour;
    private PacketDispatcher.Delegates.OnReceivePacket? receiveDetour;
    private readonly HashSet<(HookRole Role, bool Enabling)> refusedChanges = new();

    // ANOMECH_HOOK_BACKEND=safetyhook runs a whole suite with the other backend's failures.
    public HookBackend Backend { get; set; } =
        string.Equals(Environment.GetEnvironmentVariable("ANOMECH_HOOK_BACKEND"), "safetyhook", StringComparison.OrdinalIgnoreCase)
            ? HookBackend.SafetyHook
            : HookBackend.Reloaded;
    public bool SendFilterUp => sendHook is { IsEnabled: true };
    public bool ReceiveFilterUp => receiveHook is { IsEnabled: true };
    public bool HooksCreated => sendHook != null && receiveHook != null;
    public int SendHookDisableCount { get; private set; }
    public List<string> HookHistory { get; } = new();

    // ---- outcome ---------------------------------------------------------------------------

    public bool Dead { get; private set; }
    public string? DeathReason { get; private set; }
    public double? DiedAt { get; private set; }
    public List<string> Violations { get; } = new();
    // Known, documented exposures the plugin cannot close (see the README), kept apart so a test
    // can pin exactly which ones occurred.
    public List<string> AcceptedRisks { get; } = new();
    public bool PluginUnloaded { get; set; }
    public List<string> Timeline { get; } = new();
    public List<string> DiagnosticLines { get; } = new();
    public List<(uint Territory, double At)> NativeLoads { get; } = new();
    public List<(Vector3 Position, double At)> NativePositionWrites { get; } = new();
    public List<(uint Category, double At)> ActorControls { get; } = new();

    // Invariant judgement needs to know the session's own view of whether a start or a lift is
    // in progress; the driver provides it.
    internal Func<StartSpec, string?>? StartSafetyOracle { get; set; }
    internal Func<string?>? StuckOracle { get; set; }
    internal Func<string?>? ExtraInvariant { get; set; }
    public Action<uint>? TerritoryChangedHandler { get; set; }
    public Action<int, int>? LogoutHandler { get; set; }

    public VirtualGame(bool dalamudSyncsTerritory = false)
    {
        if (current != null) throw new InvalidOperationException("A VirtualGame is already running; dispose it first.");
        current = this;

        SigScanner = new FakeSigScanner(this);
        GameInterop = new FakeGameInterop(this);
        Log = new FakeLog(this);
        AddonLifecycle = new FakeAddonLifecycle();
        DataManager = new FakeDataManager(this);
        ClientState = dalamudSyncsTerritory ? new FakeClientStateSyncable(this) : new FakeClientState(this);
        ObjectTable = new FakeObjectTable(this);
        PlayerState = new FakePlayerState(this);
        Condition = new FakeCondition(this);
        Framework = new FakeFramework(this);

        ConditionsMemory = Alloc(sizeof(Conditions));
        GameMainMemory = Alloc(sizeof(GameMain));
        EventFrameworkMemory = Alloc(sizeof(EventFramework));
        UIStateMemory = Alloc(sizeof(UIState));
        EnvManagerMemory = Alloc(sizeof(EnvManager));
        InventoryMemory = Alloc(sizeof(InventoryManager));
        AgentModuleMemory = Alloc(64);
        AgentMemory = Alloc(64);
        PacketDispatcherVTableMemory = Alloc(sizeof(PacketDispatcher.PacketDispatcherVirtualTable));
        heartbeatSignatureMemory = Alloc(16);
        dispatcherMemory = Alloc(64);
        *(int*)(heartbeatSignatureMemory + 4) = HeartbeatOpcode;
        ((PacketDispatcher.PacketDispatcherVirtualTable*)PacketDispatcherVTableMemory)->OnReceivePacket = (delegate* unmanaged<PacketDispatcher*, uint, nint, void>)ReceiveFunctionAddress;
        var env = (EnvManager*)EnvManagerMemory;
        env->EnvScene = (EnvScene*)Alloc(sizeof(EnvScene));
        env->EnvSpace = (EnvSpace*)Alloc(sizeof(EnvSpace));

        DalamudTerritory = InnTerritory;
        ((GameMain*)GameMainMemory)->CurrentTerritoryTypeId = InnTerritory;

        var playerId = NewObject();
        localPlayer = new FakeLocalPlayer(this, playerId, objects[playerId]);
        ((Character*)objects[playerId])->Level = PlayerLevel;
        for (var i = 0; i < 3; i++) NewObject();

        BuildSheets();
        Config.ZoneDownOpcodes = [KeepAliveInboundOpcode];
        lastHeartbeatAt = Now;
    }

    static VirtualGame() => NativeLibrary.SetDllImportResolver(typeof(VirtualGame).Assembly, ResolveNativeLibrary);

    private static nint ResolveNativeLibrary(string name, System.Reflection.Assembly assembly, DllImportSearchPath? path)
        // The safety stop's message box must never block a headless test run: resolving user32 to
        // a module without MessageBoxW makes the call throw, which Die already tolerates.
        => name.StartsWith("user32", StringComparison.OrdinalIgnoreCase) ? NativeLibrary.GetMainProgramHandle() : 0;

    private nint Alloc(int size)
    {
        var p = (nint)NativeMemory.AllocZeroed((nuint)Math.Max(size, 16));
        allocations.Add(p);
        return p;
    }

    private int NewObject()
    {
        var id = nextObjectId++;
        var p = Alloc(sizeof(BattleChara) + 64);
        *(int*)p = id;
        objects[id] = p;
        return id;
    }

    internal IEnumerable<FakeObjectRef> ObjectAddresses()
        => objects.Where(kv => kv.Key != localPlayer.FakeId || LocalPlayerVisible != null).Select(kv => new FakeObjectRef(kv.Value)).ToList();

    public void Dispose()
    {
        foreach (var p in allocations) NativeMemory.Free((void*)p);
        allocations.Clear();
        if (current == this) current = null;
    }

    // ---- sheets ----------------------------------------------------------------------------

    private readonly Dictionary<Type, object> sheets = new();

    internal ExcelSheet<T> Sheet<T>() where T : struct
        => sheets.TryGetValue(typeof(T), out var s) ? (ExcelSheet<T>)s : new ExcelSheet<T>(new Dictionary<uint, T>());

    private void AddSheet<T>(Dictionary<uint, T> rows) where T : struct => sheets[typeof(T)] = new ExcelSheet<T>(rows);

    private void BuildSheets()
    {
        static TerritoryType Territory(uint intendedUse, uint cfc) => new()
        {
            TerritoryIntendedUse = new RowRef<TerritoryIntendedUse>(intendedUse),
            ContentFinderCondition = new RowRef<ContentFinderCondition>(cfc),
        };
        AddSheet(new Dictionary<uint, TerritoryType>
        {
            [InnTerritory] = Territory(2, 0),
            [OtherInnTerritory] = Territory(2, 0),
            [CityTerritory] = Territory(0, 0),
            [OtherCityTerritory] = Territory(0, 0),
            [DutyTerritory] = Territory(28, DutyContentFinderCondition),
            [OtherDutyTerritory] = Territory(28, OtherDutyContentFinderCondition),
            [TerritoryWithoutDutyEntry] = Territory(28, 0),
        });
        AddSheet(new Dictionary<uint, TerritoryIntendedUse> { [0] = default, [2] = default, [28] = default });
        AddSheet(new Dictionary<uint, ContentFinderCondition>
        {
            [DutyContentFinderCondition] = new() { Content = new RowRef(70), ClassJobLevelSync = 90, ItemLevelSync = 0, ItemLevelRequired = 365 },
            [OtherDutyContentFinderCondition] = new() { Content = new RowRef(99), ClassJobLevelSync = 100, ItemLevelSync = 0, ItemLevelRequired = 0 },
        });
        AddSheet(new Dictionary<uint, ClassJob> { [ClassJobId] = default, [1] = default, [24] = default });
        var grow = new Dictionary<uint, ParamGrow>();
        for (uint level = 1; level <= 100; level++) grow[level] = new ParamGrow { BaseSpeed = 400 + (int)level };
        AddSheet(grow);
    }

    // ---- clock, framework and deferred work ------------------------------------------------

    internal void AddTimer(int milliseconds, Action action, string what)
    {
        timers.Add((Ticks + TimeSpan.FromMilliseconds(milliseconds).Ticks, timerOrder++, action, what));
    }

    internal void QueueFrameworkTask(Action action) => frameworkQueue.Add(action);

    // Dalamud's scheduler runs what was queued before the drain started; work queued by a task
    // waits for the next frame. It walks a ConcurrentDictionary's keys, so a frame's tasks run in
    // no set order; set this to try them in random ones.
    public Random? FrameworkTaskOrder { get; set; }

    private void DrainFrameworkQueue()
    {
        var batch = frameworkQueue.ToList();
        frameworkQueue.Clear();
        if (FrameworkTaskOrder is { } order) batch = batch.OrderBy(_ => order.Next()).ToList();
        foreach (var task in batch)
        {
            if (Dead) return;
            RunPluginCode(task, "framework task");
        }
    }

    private void FireDueTimers()
    {
        while (!Dead)
        {
            var due = timers.Where(t => t.Due <= Ticks).OrderBy(t => t.Due).ThenBy(t => t.Order).FirstOrDefault();
            if (due.Action == null) return;
            timers.Remove(due);
            RunPluginCode(due.Action, due.What);
        }
    }

    // Plugin code runs through here so a safety stop anywhere ends the process for good.
    internal void RunPluginCode(Action action, string what)
    {
        if (Dead) return;
        try
        {
            action();
        }
        catch (GameTerminated)
        {
        }
    }

    // ---- natives the zone session calls ----------------------------------------------------

    public bool SendSignatureMissing { get; set; }

    internal nint ScanText(string signature)
    {
        if (signature == HeartbeatSignature) return heartbeatSignatureMemory;
        if (signature == SendPacketSignature && !SendSignatureMissing) return SendFunctionAddress;
        throw new KeyNotFoundException($"Signature not found: {signature}");
    }

    internal Hook<T> CreateHook<T>(nint address, T detour) where T : Delegate
    {
        if (address == SendFunctionAddress)
        {
            if (sendHook != null) Violation("a second hook was created on the send function");
            var realSend = typeof(VirtualGame).GetMethod(nameof(RealSend), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var original = (T)Delegate.CreateDelegate(typeof(T), this, realSend);
            var hook = new Hook<T>(address, detour, original, HookRole.Send, this);
            sendHook = hook;
            sendDetour = (Func<nint, nint, nint, byte, byte>)Delegate.CreateDelegate(typeof(Func<nint, nint, nint, byte, byte>), detour.Target, detour.Method);
            return hook;
        }
        if (address == ReceiveFunctionAddress)
        {
            if (receiveHook != null) Violation("a second hook was created on the receive function");
            var original = (T)(Delegate)(PacketDispatcher.Delegates.OnReceivePacket)RealReceive;
            var hook = new Hook<T>(address, detour, original, HookRole.Receive, this);
            receiveHook = hook;
            receiveDetour = (PacketDispatcher.Delegates.OnReceivePacket)(Delegate)detour;
            return hook;
        }
        throw new InvalidOperationException($"No function at 0x{address:X} to hook.");
    }

    // A native patch that fails (memory the backend cannot unprotect): the hook stays as it was.
    public void RefuseToEnableHook(HookRole role) => refusedChanges.Add((role, true));

    public void RefuseToDisableHook(HookRole role) => refusedChanges.Add((role, false));

    public void AllowHookChanges() => refusedChanges.Clear();

    bool IHookHost.Refuses(HookRole role, bool enabling)
    {
        if (!refusedChanges.Contains((role, enabling))) return false;
        Note($"hook {role} refused to {(enabling ? "enable" : "disable")} ({Backend})");
        return true;
    }

    void IHookHost.OnHookStateChanged(HookRole role, bool enabled, string by) => OnHookStateChanged(role, enabled, by);

    void IHookHost.OnHookDisposed(HookRole role) => OnHookDisposed(role);

    internal void OnHookStateChanged(HookRole role, bool enabled, string by)
    {
        HookHistory.Add($"{Now:F3} {role} {(enabled ? "on" : "off")} ({by})");
        Note($"hook {role} {(enabled ? "enabled" : "disabled")} by {by}");
        if (Dead)
        {
            Violation($"the {role} filter changed ({(enabled ? "on" : "off")}) after the game was stopped");
            return;
        }
        if (enabled)
        {
            if (role == HookRole.Receive && StartSafetyOracle?.Invoke(StartSpecNow()) is { } unsafeArm)
                Violation($"a stay's firewall was armed while {unsafeArm}");
            return;
        }
        if (role == HookRole.Send) SendHookDisableCount++;
        // Taken down from outside: the plugin's answer (a stop before anything leaves) is judged
        // at the frame's packets and invariants, not at this instant.
        if (by != "Disable") return;
        var diverged = Divergence();
        if (diverged.Count > 0)
            Violation($"the {role} filter went down while {string.Join("; ", diverged)}");
    }

    internal void OnHookDisposed(HookRole role) => Note($"hook {role} disposed");

    // The engine can refuse a write (a character mid-animation-lock, a server correction racing it).
    public bool IgnorePositionWrites { get; set; }
    public bool ThrowOnPositionWrite { get; set; }

    internal void NativeSetPosition(int id, float x, float y, float z)
    {
        if (id != localPlayer.FakeId) return;
        if (ThrowOnPositionWrite) throw new InvalidOperationException("injected position write failure");
        var p = new Vector3(x, y, z);
        NativePositionWrites.Add((p, Now));
        Note($"native: local player position set to {Fmt(p)}{(IgnorePositionWrites ? " (ignored)" : "")}");
        // A move with nothing holding it reaches the server; one to where the server already has the
        // character is harmless, any other is a jump the server sees.
        var known = LoadedTerritory == ServerTerritory && Vector3.Distance(p, ServerPosition) <= MaxServerStep;
        if (!SendFilterUp && !Dead && !known)
        {
            if (guardHasNotRunSinceExternalDisable) AcceptedRisks.Add($"{Now:F3}s the plugin moved the character to {Fmt(p)} in the frame after something else took the send filter down");
            else Violation($"the plugin moved the real character to {Fmt(p)} with the send filter down");
        }
        if (!IgnorePositionWrites) ClientPosition = p;
    }

    internal void NativeSetRotation(int id, float rotation)
    {
        if (id == localPlayer.FakeId) ClientRotation = rotation;
    }

    internal void NativeDisableDraw(int id) => DrawDisabledCount++;

    // A managed failure after the native load (the client already shows the new zone).
    public bool ThrowAfterNativeLoad { get; set; }

    internal void NativeSetEventFrameworkTerritory(ushort territory)
    {
        EventFrameworkTerritory = territory;
        if (ThrowAfterNativeLoad && !IsInn(territory)) throw new InvalidOperationException("injected failure after the native zone load");
    }

    public bool ThrowOnActorControl { get; set; }

    internal void NativeActorControl(uint entityId, uint category, uint arg1, uint arg2)
    {
        if (ThrowOnActorControl) throw new InvalidOperationException("injected ActorControl failure");
        ActorControls.Add((category, Now));
        Note($"native: actor control {category} on 0x{entityId:X}");
    }

    internal void NativeInitDirector(uint eventId, uint contentId)
    {
        DirectorContent = contentId;
        Note($"native: director 0x{eventId:X} initialized");
    }

    internal void NativeTerminateDirector(uint eventId)
    {
        DirectorContent = null;
        Note($"native: director 0x{eventId:X} terminated");
    }

    internal void NativeUpdateClassInfo(ushort syncedLevel) => SyncedLevel = syncedLevel;

    internal bool NativeDirectorUpdate(uint category)
    {
        NativeDirectorUpdates++;
        return true;
    }

    public int NativeMapEffects { get; private set; }
    public int NativeDirectorUpdates { get; private set; }

    internal bool NativeMapEffect(uint flags, byte index)
    {
        NativeMapEffects++;
        return true;
    }

    public Action<uint>? BeforeNativeLoad { get; set; }

    internal void NativeLoadZone(uint territory)
    {
        BeforeNativeLoad?.Invoke(territory);
        var fromInn = IsInn(LoadedTerritory);
        NativeLoads.Add((territory, Now));
        Note($"native: LoadZone({territory})");
        if (!IsInn(territory) && fromInn && StartSafetyOracle?.Invoke(StartSpecNow()) is { } unsafeStart)
            Violation($"a sim zone was loaded while {unsafeStart}");
        if (!IsInn(territory) && !SendFilterUp)
            Violation($"sim territory {territory} was loaded with the send filter down");
        LoadedTerritory = territory;
        ((GameMain*)GameMainMemory)->CurrentTerritoryTypeId = 0;
        var seconds = IsInn(territory) ? InnLoadSeconds : SimLoadSeconds;
        pendingLoad = (Now + seconds, territory, false);
    }

    // How long the engine takes to finish a client-side load: the game's own territory reads 0
    // until then, and the post-load packet goes out when it finishes.
    public double InnLoadSeconds { get; set; } = 0.6;
    public double SimLoadSeconds { get; set; } = 1.2;
    private (double DoneAt, uint Territory, bool ServerOrdered)? pendingLoad;
    private uint? serverAwaitsLoadOf;

    private void FinishPendingLoad()
    {
        if (pendingLoad is not { } load || load.DoneAt > Now) return;
        pendingLoad = null;
        ((GameMain*)GameMainMemory)->CurrentTerritoryTypeId = load.Territory;
        ClientSends(PostLoadOpcode, $"zone load finished ({load.Territory})");
    }

    public static bool IsInn(uint territory) => territory is InnTerritory or OtherInnTerritory;

    // ---- conditions ------------------------------------------------------------------------

    internal bool HasCondition(ConditionFlag flag)
    {
        var index = (int)flag;
        return index >= 0 && index < sizeof(Conditions) && ((byte*)ConditionsMemory)[index] != 0;
    }

    public void SetCondition(ConditionFlag flag, bool on)
    {
        if (on) gameFlags.Add(flag);
        else gameFlags.Remove(flag);
        ((byte*)ConditionsMemory)[(int)flag] = on ? (byte)1 : (byte)0;
    }

    // Occupied written by the plugin itself (ZoneSession.Revert), as opposed to the game's own.
    public bool PluginHoldsOccupied => ((Conditions*)ConditionsMemory)->Occupied && !gameFlags.Contains(ConditionFlag.Occupied);
    public bool PluginSetStatusAffliction => ((Conditions*)ConditionsMemory)->SufferingStatusAffliction && !gameFlags.Contains(ConditionFlag.SufferingStatusAffliction);

    public void SetPluginStatusAffliction() => ((Conditions*)ConditionsMemory)->SufferingStatusAffliction = true;

    public byte ActiveWeather => ((EnvManager*)EnvManagerMemory)->ActiveWeather;

    // ---- the network -----------------------------------------------------------------------

    // One outbound packet, through the hooked send function exactly as the game calls it.
    public void ClientSends(ushort opcode, string what)
    {
        if (Dead || !LoggedIn) return;
        var buffer = (byte*)NativeMemory.AllocZeroed(64);
        try
        {
            *(ushort*)buffer = opcode;
            lastWhat = what;
            if (sendHook is { IsEnabled: true }) sendDetour!(0x7FF0_0000, (nint)buffer, 0x7FF1_0000, 0);
            else RealSend(0x7FF0_0000, (nint)buffer, 0x7FF1_0000, 0);
        }
        finally
        {
            NativeMemory.Free(buffer);
        }
    }

    private string lastWhat = "";

    // The unhooked send: whatever gets here is on the wire.
    private byte RealSend(nint a1, nint a2, nint a3, byte a4)
    {
        var opcode = *(ushort*)a2;
        var packet = new ServerPacket(opcode, opcode == HeartbeatOpcode ? "heartbeat" : lastWhat, Now, LoadedTerritory, ClientPosition);
        ServerReceived.Add(packet);
        ServerJudges(packet);
        return 1;
    }

    private void ServerJudges(ServerPacket packet)
    {
        if (Dead)
        {
            Violation($"0x{packet.Opcode:X4} ({packet.What}) left a stopped client");
            return;
        }
        if (packet.Opcode == HeartbeatOpcode) return;
        if (guardHasNotRunSinceExternalDisable)
        {
            AcceptedRisks.Add($"{Now:F3}s 0x{packet.Opcode:X4} ({packet.What}) left in the frame after something else took the send filter down");
            return;
        }
        // Before or during a zone change the server ordered, the server expects the old state.
        if ((clientZoneLoad != null || initZoneInFlight != null) && packet.Opcode != PostLoadOpcode) return;
        if (packet.Opcode == PostLoadOpcode)
        {
            if (serverAwaitsLoadOf == packet.ClientTerritory)
            {
                serverAwaitsLoadOf = null;
                return;
            }
            var what = $"the server received a zone-load-finished for territory {packet.ClientTerritory} it never ordered";
            if (PluginUnloaded) AcceptedRisks.Add($"{Now:F3}s {what} (the lift on a mid-sim unload cannot wait for the inn load)");
            else Violation($"LEAK: {what}");
            return;
        }
        if (packet.ClientTerritory != ServerTerritory)
            Violation($"LEAK: 0x{packet.Opcode:X4} ({packet.What}) reached the server from territory {packet.ClientTerritory}; the server has the character in {ServerTerritory}");
        var step = Vector3.Distance(packet.ClientPosition, ServerPosition);
        if (!(step <= MaxServerStep))
            Violation($"LEAK: 0x{packet.Opcode:X4} ({packet.What}) reported {Fmt(packet.ClientPosition)}, {step:F1}y from where the server has the character ({Fmt(ServerPosition)})");
        if (DirectorContent != null)
            Violation($"LEAK: 0x{packet.Opcode:X4} ({packet.What}) left while a sim duty director was active");
        if (packet.Opcode == MovementOpcode && float.IsFinite(step))
        {
            ServerPosition = packet.ClientPosition;
            if (serverZoneChange is { } pending && pending.What.StartsWith("UseAction") && pending.CompleteAt > Now)
            {
                serverZoneChange = null;
                Note("server: the zone change cast was interrupted by movement");
            }
        }
        if (packet.Opcode == ActionRequestOpcode) ServerHandlesAction(packet.What);
    }

    private readonly Dictionary<int, Action> inbound = new();
    private int nextInbound;

    // One inbound packet, through the hooked receive function.
    public void ServerSends(ushort opcode, Action? onClient, string what)
    {
        if (Dead || !LoggedIn) return;
        var id = nextInbound++;
        inbound[id] = onClient ?? (() => { });
        var buffer = (byte*)NativeMemory.AllocZeroed(64);
        try
        {
            *(ushort*)(buffer + 2) = opcode;
            *(int*)(buffer + 8) = id;
            Note($"server sends 0x{opcode:X4} ({what})");
            if (receiveHook is { IsEnabled: true }) receiveDetour!((PacketDispatcher*)dispatcherMemory, 0x10000001, (nint)buffer);
            else RealReceive((PacketDispatcher*)dispatcherMemory, 0x10000001, (nint)buffer);
        }
        finally
        {
            inbound.Remove(id);
            NativeMemory.Free(buffer);
        }
    }

    public int InboundDelivered { get; private set; }

    private void RealReceive(PacketDispatcher* dispatcher, uint targetId, nint packet)
    {
        InboundDelivered++;
        var id = *(int*)(packet + 8);
        if (inbound.TryGetValue(id, out var apply)) apply();
    }

    // What the send detour does with one opcode, called the way the game calls it.
    public bool SendDetourPasses(ushort opcode)
    {
        if (sendDetour == null) throw new InvalidOperationException("The send function was never hooked.");
        var before = ServerReceived.Count;
        var buffer = (byte*)NativeMemory.AllocZeroed(64);
        try
        {
            *(ushort*)buffer = opcode;
            lastWhat = "probe";
            var result = sendDetour(0x7FF0_0000, (nint)buffer, 0x7FF1_0000, 0);
            if (result != 1) Violation($"the send detour returned {result} for 0x{opcode:X4}; the game treats anything but 1 as a failed send");
        }
        finally
        {
            NativeMemory.Free(buffer);
        }
        var passed = ServerReceived.Count > before;
        if (passed) ServerReceived.RemoveAt(ServerReceived.Count - 1);
        return passed;
    }

    public byte SendDetourWithNullPacket()
    {
        var before = ServerReceived.Count;
        var result = sendDetour!(0x7FF0_0000, 0, 0x7FF1_0000, 0);
        if (ServerReceived.Count > before) Violation("the send detour passed a null packet on");
        return result;
    }

    public bool ReceiveDetourPasses(ushort opcode)
    {
        if (receiveDetour == null) throw new InvalidOperationException("The receive function was never hooked.");
        var before = InboundDelivered;
        var buffer = (byte*)NativeMemory.AllocZeroed(64);
        try
        {
            *(ushort*)(buffer + 2) = opcode;
            *(int*)(buffer + 8) = -1;
            receiveDetour((PacketDispatcher*)dispatcherMemory, 0x10000001, (nint)buffer);
        }
        finally
        {
            NativeMemory.Free(buffer);
        }
        return InboundDelivered > before;
    }

    public void ReceiveDetourWithNullPacket() => receiveDetour!((PacketDispatcher*)dispatcherMemory, 0x10000001, 0);

    // ---- server-side game logic ------------------------------------------------------------

    public bool ServerAcceptsTeleport { get; set; } = true;
    public uint TeleportDestination { get; set; } = CityTerritory;

    private void ServerHandlesAction(string what)
    {
        if (!what.StartsWith("UseAction Teleport") && !what.StartsWith("UseAction Return")) return;
        if (!ServerAcceptsTeleport)
        {
            ServerSends(CastCancelInboundOpcode, () => CancelCast("the server refused it"), "cast refused");
            return;
        }
        serverZoneChange = (Now + TeleportCastSeconds, TeleportDestination, CityAetherytePosition, what);
        Note($"server: {what} cast started, zone change due at {Now + TeleportCastSeconds:F2}");
    }

    // A zone change the server decides on its own (a duty commencing, a GM move, a kick).
    public void ServerMovesCharacter(uint territory, Vector3 position, string why)
    {
        serverZoneChange = (Now, territory, position, why);
        var latency = ServerLatencySeconds;
        ServerLatencySeconds = 0;
        ServerTick();
        ServerLatencySeconds = latency;
    }

    // How long the server's zone-change packet takes to reach the client (a lag spike can make
    // it seconds).
    public double ServerLatencySeconds { get; set; }
    private (double ArrivesAt, uint Territory, Vector3 Position)? initZoneInFlight;
    public bool ZoneChangeInFlight => initZoneInFlight != null;

    private void ServerTick()
    {
        if (initZoneInFlight is { } flight && flight.ArrivesAt <= Now && !Dead)
        {
            initZoneInFlight = null;
            ServerSends(InitZoneInboundOpcode, () => ClientBeginsZoneChange(flight.Territory, flight.Position), $"InitZone {flight.Territory}");
        }
        if (serverZoneChange is not { } change || change.CompleteAt > Now || Dead) return;
        serverZoneChange = null;
        ServerTerritory = change.Destination;
        ServerPosition = change.Position;
        Note($"server: character moved to territory {change.Destination} ({change.What})");
        serverAwaitsLoadOf = change.Destination;
        initZoneInFlight = (Now + ServerLatencySeconds, change.Destination, change.Position);
        ServerTick();
    }

    // ---- client-side game logic ------------------------------------------------------------

    private (double DoneAt, uint Territory, Vector3 Position)? clientZoneLoad;
    public bool ClientZoning => clientZoneLoad != null;

    // Dalamud sets ClientState.TerritoryType, and raises TerritoryChanged only if that changed it,
    // when the client handles the ZoneInit packet (UIModule.HandlePacket), part-way into the
    // load. 0 is that moment, 1 the load's end.
    public double DalamudSeesZoneInitAt { get; set; }
    private (double At, uint Territory)? dalamudZoneInit;

    private void ClientBeginsZoneChange(uint territory, Vector3 position)
    {
        Cast = null;
        SetCondition(ConditionFlag.Casting, false);
        SetCondition(ConditionFlag.BetweenAreas, true);
        clientZoneLoad = (Now + ClientZoneLoadSeconds, territory, position);
        Note($"client: zoning to {territory}");
        dalamudZoneInit = (Now + DalamudSeesZoneInitAt * ClientZoneLoadSeconds, territory);
        if (DalamudSeesZoneInitAt <= 0) DalamudHandlesZoneInit();
    }

    private void DalamudHandlesZoneInit()
    {
        if (dalamudZoneInit is not { } init) return;
        dalamudZoneInit = null;
        if (DalamudTerritory == init.Territory) return;
        DalamudTerritory = init.Territory;
        Note($"dalamud: TerritoryType {init.Territory}");
        if (TerritoryChangedHandler is { } handler) RunPluginCode(() => handler(init.Territory), "TerritoryChanged");
    }

    private void ClientTick(float dt)
    {
        // The game holds BetweenAreas for the whole transition.
        if (clientZoneLoad != null) SetCondition(ConditionFlag.BetweenAreas, true);
        if (Cast is { } cast)
        {
            cast.Elapsed = Math.Min(cast.Total, cast.Elapsed + dt);
            if (cast.Elapsed >= cast.Total)
            {
                Cast = null;
                SetCondition(ConditionFlag.Casting, false);
                Note("client: cast bar finished");
            }
        }
        if (dalamudZoneInit is { } init && init.At <= Now && (clientZoneLoad is not { } loading || init.At < loading.DoneAt)) DalamudHandlesZoneInit();
        if (clientZoneLoad is { } load && load.DoneAt <= Now)
        {
            clientZoneLoad = null;
            SetCondition(ConditionFlag.BetweenAreas, false);
            LoadedTerritory = load.Territory;
            EventFrameworkTerritory = load.Territory;
            ((GameMain*)GameMainMemory)->CurrentTerritoryTypeId = load.Territory;
            ClientPosition = load.Position;
            lastSentPosition = load.Position;
            LastZoneInAt = Now;
            Note($"client: zone-in to {load.Territory} finished");
            ClientSends(PostLoadOpcode, $"zone load finished ({load.Territory})");
            DalamudHandlesZoneInit();
        }
    }

    public Action<ActionType, uint>? UseActionHandler { get; set; }

    // A hotbar press of Teleport/Return: the plugin's UseAction detour sees it, the client starts
    // its own cast bar at once, and the request goes out through the send path.
    public void PressZoneChangeAction(uint actionId = TeleportActionId)
    {
        if (Dead || !LoggedIn || LocalPlayerVisible == null || ClientZoning) return;
        if (UseActionHandler is { } handler) RunPluginCode(() => handler(ActionType.Action, actionId), "UseAction");
        // The hook sees the press either way; the game refuses a second cast while one is up.
        if (Cast != null)
        {
            Note("client: press refused, already casting");
            return;
        }
        Cast = new CastState { ActionId = actionId, Total = TeleportCastSeconds };
        SetCondition(ConditionFlag.Casting, true);
        ClientSends(ActionRequestOpcode, actionId == TeleportActionId ? "UseAction Teleport" : "UseAction Return");
    }

    // A cast the server began before the firewall went up, whose bar the client only shows now
    // (a late ActorCast): the guard sees a bar older than the stay.
    public void ShowCastBegunEarlier(uint actionId, float elapsed, float total = TeleportCastSeconds)
    {
        Cast = new CastState { ActionId = actionId, Total = total, Elapsed = elapsed };
        SetCondition(ConditionFlag.Casting, true);
        serverZoneChange = (Now + (total - elapsed), TeleportDestination, CityAetherytePosition, "UseAction Teleport (begun before the stay)");
    }

    // A cast bar the client shows without its Casting condition (a server-started cast whose
    // condition hasn't arrived yet).
    public void ShowCastBarOnly(uint actionId, float elapsed, float total)
    {
        Cast = new CastState { ActionId = actionId, Total = total, Elapsed = elapsed };
    }

    // Moving interrupts a cast: the client drops its bar and the move tells the server.
    public void InterruptCast(Vector3 step)
    {
        if (Cast == null) return;
        CancelCast("the player moved");
        ClientPosition += step;
        ClientSends(MovementOpcode, "movement (cast interrupted)");
        lastSentPosition = ClientPosition;
    }

    private void CancelCast(string why)
    {
        Cast = null;
        SetCondition(ConditionFlag.Casting, false);
        Note($"client: cast cancelled ({why})");
    }

    public void Walk(Vector3 step)
    {
        if (LocalPlayerVisible == null || ClientZoning) return;
        ClientPosition += step;
    }

    public void LogOut(int type = 1, int code = 0)
    {
        if (!LoggedIn) return;
        LoggedIn = false;
        serverZoneChange = null;
        Note("client: logged out");
        if (LogoutHandler is { } handler) RunPluginCode(() => handler(type, code), "Logout");
    }

    // Until the plugin's next framework update, nothing in the plugin can know the hook went down:
    // what the game sends in that window is the documented one-frame exposure.
    private bool guardHasNotRunSinceExternalDisable;

    public void DisableHookExternally(HookRole role, string by = "something outside the plugin")
    {
        var hook = role == HookRole.Send ? sendHook : receiveHook;
        if (hook is { IsEnabled: true }) guardHasNotRunSinceExternalDisable = true;
        hook?.ForceDisable(by);
    }

    // Dalamud's own unload: the plugin's hooks are disposed after its Dispose, whether or not that
    // Dispose completed.
    public void DalamudDisposesPluginHooks()
    {
        if (Dead) return;
        sendHook?.ForceDispose("Dalamud's plugin unload");
        receiveHook?.ForceDispose("Dalamud's plugin unload");
    }

    // ---- the frame -------------------------------------------------------------------------

    internal Action? PluginUpdate { get; set; }
    public bool FrameworkTasksBeforeUpdate { get; set; } = true;

    public double LastZoneInAt { get; private set; } = double.NegativeInfinity;
    public double LastServerActingAt { get; private set; } = double.NegativeInfinity;

    public void Frame(float dt = 1f / 60f)
    {
        if (Dead) return;
        if (StartSpecification.ServerActingSoonFlags.Any(HasCondition)) LastServerActingAt = Now;
        Ticks += TimeSpan.FromSeconds(dt).Ticks;
        FireDueTimers();
        ServerTick();
        ClientTick(dt);
        FinishPendingLoad();
        if (FrameworkTasksBeforeUpdate)
        {
            DrainFrameworkQueue();
            RunPluginUpdate();
        }
        else
        {
            RunPluginUpdate();
            DrainFrameworkQueue();
        }
        ClientNetworkTick();
        CheckFrameInvariants();
    }

    private void RunPluginUpdate()
    {
        if (PluginUpdate is { } update) RunPluginCode(update, "framework update");
        guardHasNotRunSinceExternalDisable = false;
    }

    public void RunFor(double seconds, float dt = 1f / 60f)
    {
        var end = Now + seconds;
        while (Now < end - 1e-9 && !Dead) Frame(dt);
    }

    private void ClientNetworkTick()
    {
        if (Dead || !LoggedIn) return;
        if (Now - lastHeartbeatAt >= HeartbeatIntervalSeconds)
        {
            lastHeartbeatAt = Now;
            ClientSends(HeartbeatOpcode, "heartbeat");
        }
        if (LocalPlayerVisible != null && Vector3.Distance(ClientPosition, lastSentPosition) > 0.001f)
        {
            ClientSends(MovementOpcode, "movement");
            lastSentPosition = ClientPosition;
        }
    }

    // ---- invariants ------------------------------------------------------------------------

    // Everything the client holds that the server does not.
    public List<string> Divergence()
    {
        var d = new List<string>();
        // Logged out, or behind or part-way through a zone change the server ordered: nothing the
        // server lacks.
        if (!LoggedIn || clientZoneLoad != null || initZoneInFlight != null) return d;
        if (LoadedTerritory != ServerTerritory) d.Add($"the client has territory {LoadedTerritory} loaded and the server has the character in {ServerTerritory}");
        if (LocalPlayerVisible != null)
        {
            var apart = Vector3.Distance(ClientPosition, ServerPosition);
            if (!(apart <= MaxServerStep)) d.Add($"the character stands at {Fmt(ClientPosition)}, {apart:F1}y from where the server has it ({Fmt(ServerPosition)})");
        }
        if (DirectorContent != null) d.Add($"a sim duty director (content {DirectorContent}) is active");
        if (SyncedLevel != 0) d.Add($"a sim level sync ({SyncedLevel}) is applied");
        return d;
    }

    private void CheckFrameInvariants()
    {
        if (Dead) return;
        var diverged = Divergence();
        if (diverged.Count > 0 && !SendFilterUp) Violation($"the send filter is down while {string.Join("; ", diverged)}");
        // Server traffic can only corrupt a sim the client has loaded; a held move (the debug hold)
        // needs only the send side.
        var simDiverged = diverged.Where(d => !d.StartsWith("the character stands at")).ToList();
        if (simDiverged.Count > 0 && !ReceiveFilterUp) Violation($"the receive filter is down while {string.Join("; ", simDiverged)}");
        if (StuckOracle?.Invoke() is { } stuck) Violation($"STUCK: {stuck}");
        if (ExtraInvariant?.Invoke() is { } broken) Violation(broken);
    }

    internal StartSpec StartSpecNow() => new(
        LoggedIn,
        LocalPlayerVisible != null,
        IsInn(DalamudTerritory),
        LoadedTerritory == ServerTerritory && DalamudTerritory == ServerTerritory,
        serverZoneChange != null || clientZoneLoad != null || initZoneInFlight != null,
        Cast != null,
        Enum.GetValues<ConditionFlag>().Where(HasCondition).ToHashSet(),
        Now - LastZoneInAt,
        StartSpecification.ServerActingSoonFlags.Any(HasCondition) ? 0 : Now - LastServerActingAt);

    private readonly HashSet<string> violationKinds = new();

    public void Violation(string what)
    {
        if (!violationKinds.Add(what) || Violations.Count >= 50) return;
        var line = $"{Now:F3}s {what}";
        Violations.Add(line);
        Timeline.Add($"!! {line}");
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    internal void FailFast(string? message)
    {
        if (!Dead)
        {
            Dead = true;
            DeathReason = message ?? "";
            DiedAt = Now;
            Timeline.Add($"{Now:F3}s GAME STOPPED: {message}");
        }
        throw new GameTerminated(message ?? "");
    }

    internal void Diagnostic(string level, string line)
    {
        DiagnosticLines.Add($"{Now:F3} {level} {line}");
        Timeline.Add($"{Now:F3}s [{level}] {line}");
    }

    public void Note(string line) => Timeline.Add($"{Now:F3}s {line}");

    public static string Fmt(Vector3 p) => $"({p.X:F2},{p.Y:F2},{p.Z:F2})";

    public string Report(int lastLines = 120)
    {
        var tail = Timeline.Skip(Math.Max(0, Timeline.Count - lastLines));
        return $"Violations:\n  {string.Join("\n  ", Violations)}\nDead: {Dead} {DeathReason}\nHooks: {string.Join(" | ", HookHistory)}\nTimeline (last {lastLines}):\n  {string.Join("\n  ", tail)}";
    }
}

internal sealed record StartSpec(
    bool LoggedIn,
    bool PlayerPresent,
    bool DalamudSaysInn,
    bool ClientMatchesServer,
    bool ZoneChangePending,
    bool Casting,
    HashSet<ConditionFlag> Flags,
    double SecondsSinceZoneIn,
    double SecondsSinceServerActing);
