using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Native.Implementations;

// The real game client behind Natives; tests install FakeGame instead.
internal static class GameNatives
{
    public static void Install()
    {
        Natives.Data = new GameData();
        Natives.BattleCharas = new BattleCharas();
        Natives.EventObjects = new EventObjects();
        Natives.PlayerInput = new LocalPlayerInputHooks(Plugin.GameInterop);
        Natives.UserActions = new UserActions.UserActions(Natives.PlayerInput);
        Natives.TimelinePreload = new ActionTimelinePreload();
        Natives.MapEffects = new MapEffects();
        Natives.Zone = new ZoneControl();
        Natives.Layout = new LayoutFunctions();
        Natives.Director = new InstanceContentDirector();
        Natives.Rsv = new RsvFunctions();
        Natives.Rsf = new RsfFunctions();
        Natives.PartyHud = new PartyHud();
        Natives.HaterList = new HaterList();
        Natives.PlayerConditions = new PlayerConditions();
        Natives.LimitBreak = new LimitBreakController();
        Natives.PlayerActionState = new PlayerActionState();
        Natives.Waymarks = new Waymarks();
        Natives.Bgm = new Bgm();
    }
}
