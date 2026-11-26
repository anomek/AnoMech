using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// The game client behind Natives for a headless run. A multiplayer test runs one per client and
// Activates whichever it is driving.
internal sealed class FakeGame
{
    public SimTimeline Timeline { get; } = new();
    public FakeBattleCharas BattleCharas { get; }
    public FakeEventObjects EventObjects { get; }
    public FakeFramework Framework { get; } = FakeFramework.Create();
    public FakeZoneControl Zone { get; }
    public FakeMapEffects MapEffects { get; }
    public FakeUserActions UserActions { get; } = new();
    public FakeLocalPlayerInput PlayerInput { get; } = new();
    public FakePlayerConditions PlayerConditions { get; }

    private readonly FakeRsvFunctions rsv = new();
    private readonly DataminingGameData data;
    private readonly FakeLayoutFunctions layout = new();
    private readonly FakeInstanceContentDirector director;
    private readonly FakeRsfFunctions rsf = new();
    private readonly FakePartyHud partyHud = new();
    private readonly FakeHaterList haterList;
    private readonly FakeLimitBreakController limitBreak = new();
    private readonly FakeWaymarks waymarks = new();
    private readonly FakeBgm bgm;

    private FakeGame()
    {
        data = new DataminingGameData(rsv);
        BattleCharas = new FakeBattleCharas(Timeline);
        EventObjects = new FakeEventObjects(Timeline);
        MapEffects = new FakeMapEffects(Timeline);
        Zone = new FakeZoneControl(BattleCharas.Player, Timeline);
        PlayerConditions = new FakePlayerConditions(Timeline);
        director = new FakeInstanceContentDirector(Zone, Timeline);
        haterList = new FakeHaterList(Timeline);
        bgm = new FakeBgm(Timeline);
    }

    public static FakeGame Install()
    {
        var game = Create();
        game.Activate();
        return game;
    }

    public static FakeGame Create() => new();

    // Points Natives and Plugin.Framework at this client's fakes.
    public void Activate()
    {
        Natives.Data = data;
        Natives.BattleCharas = BattleCharas;
        Natives.EventObjects = EventObjects;
        Natives.PlayerInput = PlayerInput;
        Natives.UserActions = UserActions;
        Natives.Zone = Zone;
        Natives.MapEffects = MapEffects;
        Natives.Layout = layout;
        Natives.Director = director;
        Natives.Rsv = rsv;
        Natives.Rsf = rsf;
        Natives.PartyHud = partyHud;
        Natives.HaterList = haterList;
        Natives.PlayerConditions = PlayerConditions;
        Natives.LimitBreak = limitBreak;
        Natives.Waymarks = waymarks;
        Natives.Bgm = bgm;
        DalamudServices.Install(nameof(Plugin.Framework), Framework);
    }

    // One client frame in the real order: Dalamud drains queued Framework.Run work, then raises
    // Update (the plugin), then the game runs its own update (cast bars, carries, spawns).
    public void Frame(float deltaSeconds, Action<float> update)
    {
        Framework.RunPending();
        update(deltaSeconds);
        WalkSteeredPlayer(deltaSeconds);
        BattleCharas.Tick(deltaSeconds);
    }

    private void WalkSteeredPlayer(float deltaSeconds)
    {
        if (PlayerInput.SteerDirection is not { } dir) return;
        var step = dir * PlayerInput.SteerSpeed * deltaSeconds;
        BattleCharas.Player.Position += new System.Numerics.Vector3(step.X, 0f, step.Y);
        BattleCharas.Player.Rotation = MathF.Atan2(dir.X, dir.Y);
    }
}
