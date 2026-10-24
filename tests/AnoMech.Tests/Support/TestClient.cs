using System.Reflection;
using System.Text.Json;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;
using Dalamud.Plugin.Services;

namespace AnoMech.Tests;

// One player's game client: its own fake game, Game and MultiplayerManager. Natives and Plugin's
// services are process-wide, so a client points them at its own for each call into it. Its
// timeline is stamped with the relay's clock, which every client shares: a client's own run clock
// only starts with its run, after it has already shown the host's first packets. What a frame
// does carries the time the frame started, as the game shows a frame's packets together.
internal sealed class TestClient
{
    private static readonly IObjectTable NoObjects = DispatchProxy.Create<IObjectTable, DalamudServices.NullService>();

    private readonly Configuration config = new();
    private readonly NativesState natives = new();
    private readonly Dictionary<uint, List<(double At, string Name)>> names = new();
    private readonly ISessionNetwork relay;
    private bool botControl;
    private double? frameAt;

    public string Name { get; }
    public PartyRole Seat { get; }
    public bool IsHost { get; }
    public Guid PeerId => Multiplayer.MyPeerId;
    // Every numbered message as it first left, as it was then, and every one taken from another
    // client, on the relay's clock.
    public List<(double At, MpMessage Message, string Json)> StreamSent { get; } = [];
    public List<(double At, Guid From, MpMessage Message)> StreamTaken { get; } = [];
    public Configuration Config => config;
    public FakeGame Fake { get; }
    public Game Game { get; }
    public MultiplayerManager Multiplayer { get; }
    public TraceLog Log { get; }

    public TestClient(string name, PartyRole seat, int seed, ISessionNetwork relay, bool isHost, int slotOffset, (int Min, int Max) spawnFrames)
    {
        Name = name;
        Seat = seat;
        IsHost = isHost;
        this.relay = relay;
        Fake = FakeGame.Create();
        Fake.BattleCharas.SlotOffset = slotOffset;
        Fake.EventObjects.SlotOffset = slotOffset;
        Fake.BattleCharas.PacketSpawnFrames = spawnFrames;
        // A real player's job always fits their seat; job-keyed actions read it.
        Fake.BattleCharas.Player.ClassJob = PartyPresets.Standard[(int)seat].ClassJob;
        Game? game = null;
        Log = TraceLog.Create(() => game?.Events.Elapsed ?? 0f);
        Fake.Activate();
        InstallServices();
        Game = game = new Game();
        Multiplayer = new MultiplayerManager { RelayFactory = relay.Connect, DisplayName = name, Time = relay.Time, NextSeed = () => seed };
        Multiplayer.ActorReplayed += (local, host) => Remember(local, $"actor 0x{host:X}", Now);
        Multiplayer.StreamSent += message => StreamSent.Add((Now, message, JsonSerializer.Serialize(message)));
        Multiplayer.StreamTaken += (from, message) => StreamTaken.Add((Now, from, message));
        Fake.Timeline.Clock = () => (float)Now;
        InstallServices();
        Deactivate();
    }

    public void Do(Action action)
    {
        Activate();
        try { action(); }
        finally { Deactivate(); }
    }

    private double Now => frameAt ?? relay.Now;

    // A frame in Plugin.OnFrameworkUpdate's order.
    public void Frame(float deltaSeconds) => Do(() =>
    {
        frameAt = relay.Now;
        try
        {
            relay.RaiseLinkEvents(PeerId);
            Fake.Frame(deltaSeconds, delta =>
            {
                Natives.Tick(delta);
                Game.Tick(delta);
                Multiplayer.Tick(delta);
            });
            RecordNames();
        }
        finally
        {
            frameAt = null;
        }
    });

    // An entity id as both clients name it at `at`: its seat's role, or the host's entity id for an
    // actor the host spawned.
    public string NameAt(uint entityId, double at)
    {
        if (entityId is 0 or 0xE0000000) return "nobody";
        if (names.TryGetValue(entityId, out var history))
        {
            var name = history[0].Name;
            foreach (var (from, later) in history)
                if (from <= at + 1e-4) name = later;
            return name;
        }
        return IsHost ? $"actor 0x{entityId:X}" : $"unknown 0x{entityId:X}";
    }

    private void RecordNames()
    {
        var at = Now;
        foreach (var role in Enum.GetValues<PartyRole>())
            if (Game.World.Party.Get(role) is { EntityId: not 0 } member) Remember(member.EntityId, role.ToString(), at);
    }

    private void Remember(uint entityId, string name, double at)
    {
        if (!names.TryGetValue(entityId, out var history)) names[entityId] = history = [];
        if (history.Count == 0 || history[^1].Name != name) history.Add((history.Count == 0 ? double.NegativeInfinity : at, name));
    }

    private void Activate()
    {
        natives.Restore();
        DalamudServices.Install(nameof(Plugin.Framework), Fake.Framework);
        InstallServices();
        DebugBotControl.Enabled = botControl;
    }

    private void InstallServices()
    {
        DalamudServices.Install(nameof(Plugin.Log), Log);
        DalamudServices.Install(nameof(Plugin.Config), config);
        DalamudServices.Install(nameof(Plugin.ObjectTable), NoObjects);
        if (Game != null) DalamudServices.Install(nameof(Plugin.Game), Game);
        if (Multiplayer != null) DalamudServices.Install(nameof(Plugin.Multiplayer), Multiplayer);
    }

    private void Deactivate()
    {
        natives.Capture();
        botControl = DebugBotControl.Enabled;
    }
}
