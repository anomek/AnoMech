namespace AnoMech.Core.Native.Interfaces;

// The game boundary's service locator, mirroring Plugin's Dalamud services: Plugin installs the
// native implementations at load, tests install fakes.
public static class Natives
{
    public static IGameData Data { get; internal set; } = null!;

    public static IBattleCharas BattleCharas { get; internal set; } = null!;
    public static IEventObjects EventObjects { get; internal set; } = null!;
    public static IHiddenObjects HiddenObjects { get; internal set; } = null!;
    public static ILocalPlayerInput PlayerInput { get; internal set; } = null!;
    public static IUserActions UserActions { get; internal set; } = null!;

    public static IVfxFunctions Vfx { get; internal set; } = null!;
    public static IActionTimelinePreload TimelinePreload { get; internal set; } = null!;

    public static IZoneSession Zone { get; internal set; } = null!;
    public static IMapEffects MapEffects { get; internal set; } = null!;
    public static ILayoutFunctions Layout { get; internal set; } = null!;
    public static IInstanceContentDirector Director { get; internal set; } = null!;
    public static IRsvFunctions Rsv { get; internal set; } = null!;
    public static IRsfFunctions Rsf { get; internal set; } = null!;

    public static IPartyHud PartyHud { get; internal set; } = null!;
    public static IHaterList HaterList { get; internal set; } = null!;
    public static ILimitBreakController LimitBreak { get; internal set; } = null!;
    public static IMarkings Markings { get; internal set; } = null!;
    public static IWaymarks Waymarks { get; internal set; } = null!;
    public static IBgm Bgm { get; internal set; } = null!;

    public static IVfxSpawnLog VfxSpawnLog { get; internal set; } = null!;
}
