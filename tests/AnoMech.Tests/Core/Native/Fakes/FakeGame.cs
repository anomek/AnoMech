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
    public FakeLocalPlayerInput PlayerInput { get; }
    public FakePlayerConditions PlayerConditions { get; }

    private readonly FakeRsvFunctions rsv;
    private readonly DataminingGameData data;
    private readonly FakeActionTimelinePreload timelinePreload;
    private readonly FakeLayoutFunctions layout = new();
    private readonly FakeInstanceContentDirector director;
    private readonly FakeRsfFunctions rsf;
    private readonly FakePartyHud partyHud = new();
    private readonly FakeHaterList haterList;
    private readonly FakeLimitBreakController limitBreak;
    private readonly FakePlayerActionState playerActionState = new();
    private readonly FakeWaymarks waymarks;
    private readonly FakeBgm bgm;

    private FakeGame()
    {
        rsv = new FakeRsvFunctions(Timeline);
        rsf = new FakeRsfFunctions(Timeline);
        timelinePreload = new FakeActionTimelinePreload(Timeline);
        waymarks = new FakeWaymarks(Timeline);
        data = new DataminingGameData(rsv);
        BattleCharas = new FakeBattleCharas(Timeline);
        EventObjects = new FakeEventObjects(Timeline);
        MapEffects = new FakeMapEffects(Timeline);
        Zone = new FakeZoneControl(BattleCharas.Player, Timeline);
        PlayerInput = new FakeLocalPlayerInput(BattleCharas.Player, UserActions);
        limitBreak = new FakeLimitBreakController(BattleCharas.Player);
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
        Natives.TimelinePreload = timelinePreload;
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
        Natives.PlayerActionState = playerActionState;
        Natives.Waymarks = waymarks;
        Natives.Bgm = bgm;
        DalamudServices.Install(nameof(Plugin.Framework), Framework);
    }

    // One client frame in the real order: Dalamud drains queued Framework.Run work, then raises
    // Update (the plugin, UserActions first), then the game runs its own update (cast bars,
    // carries, spawns).
    public void Frame(float deltaSeconds, Action<float> update)
    {
        Framework.RunPending();
        PlayerInput.Tick();
        update(deltaSeconds);
        WalkSteeredPlayer(deltaSeconds);
        PlayerInput.Fly(deltaSeconds);
        BattleCharas.Tick(deltaSeconds);
    }

    private void WalkSteeredPlayer(float deltaSeconds)
    {
        if (PlayerInput.SteerDirection is not { } dir) return;
        var player = BattleCharas.Player;
        if (player.Statuses.Values.Any(s => s.StatusId == ThinIce))
        {
            SlideOnThinIce(player, dir);
            return;
        }
        var step = dir * PlayerInput.SteerSpeed * deltaSeconds;
        player.Position += new System.Numerics.Vector3(step.X, 0f, step.Y);
        player.Rotation = MathF.Atan2(dir.X, dir.Y);
    }

    private const ushort ThinIce = 911;
    private const float ThinIceSlideSpeed = 20f;

    // The client's own slide: along the movement input, the status's param in tenths of a yalm, deaf
    // to input until it stops. A key still held then starts the next one.
    private static void SlideOnThinIce(FakeCharacter player, System.Numerics.Vector2 dir)
    {
        if (player.Sliding) return;
        var distance = player.Statuses.Values.First(s => s.StatusId == ThinIce).Param / 10f;
        player.StartSlide(player.Position + new System.Numerics.Vector3(dir.X, 0f, dir.Y) * distance, MathF.Atan2(dir.X, dir.Y), distance / ThinIceSlideSpeed);
    }
}
