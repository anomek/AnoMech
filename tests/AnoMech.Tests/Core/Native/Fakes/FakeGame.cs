using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// The game client behind Natives for a headless run.
internal sealed class FakeGame
{
    public FakeBattleCharas BattleCharas { get; } = new();
    public FakeEventObjects EventObjects { get; } = new();
    public FakeFramework Framework { get; } = FakeFramework.Create();
    public FakeZoneSession Zone { get; }
    public FakeMapEffects MapEffects { get; } = new();
    public FakeUserActions UserActions { get; } = new();

    private FakeGame() => Zone = new FakeZoneSession(BattleCharas.Player);

    public static FakeGame Install()
    {
        var game = new FakeGame();
        var rsv = new FakeRsvFunctions();
        Natives.Data = new DataminingGameData(rsv);
        Natives.BattleCharas = game.BattleCharas;
        Natives.EventObjects = game.EventObjects;
        Natives.HiddenObjects = new FakeHiddenObjects();
        Natives.PlayerInput = new FakeLocalPlayerInput();
        Natives.UserActions = game.UserActions;
        Natives.Vfx = new FakeVfxFunctions();
        Natives.TimelinePreload = new FakeActionTimelinePreload();
        Natives.Zone = game.Zone;
        Natives.MapEffects = game.MapEffects;
        Natives.Layout = new FakeLayoutFunctions();
        Natives.Director = new FakeInstanceContentDirector(game.Zone);
        Natives.Rsv = rsv;
        Natives.Rsf = new FakeRsfFunctions();
        Natives.PartyHud = new FakePartyHud();
        Natives.HaterList = new FakeHaterList();
        Natives.LimitBreak = new FakeLimitBreakController();
        Natives.Markings = new FakeMarkings();
        Natives.Waymarks = new FakeWaymarks();
        Natives.Bgm = new FakeBgm();
        Natives.VfxSpawnLog = new FakeVfxSpawnLog();
        DalamudServices.Install(nameof(Plugin.Framework), game.Framework);
        return game;
    }

    // One client frame in the real order: Dalamud drains queued Framework.Run work, then raises
    // Update (the plugin), then the game runs its own update (cast bars, carries, spawns).
    public void Frame(float deltaSeconds, Action<float> update)
    {
        Framework.RunPending();
        update(deltaSeconds);
        BattleCharas.Tick(deltaSeconds);
        EventObjects.Tick(deltaSeconds);
    }
}
