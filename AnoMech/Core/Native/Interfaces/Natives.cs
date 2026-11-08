using System;

namespace AnoMech.Core.Native.Interfaces;

// The game boundary's service locator, mirroring Plugin's Dalamud services: Plugin installs the
// native implementations at load, tests install fakes.
public static class Natives
{
    public static IGameData Data { get; internal set; } = null!;

    public static IBattleCharas BattleCharas { get; internal set; } = null!;
    public static IEventObjects EventObjects { get; internal set; } = null!;
    public static ILocalPlayerInput PlayerInput { get; internal set; } = null!;
    public static IUserActions UserActions { get; internal set; } = null!;

    public static IActionTimelinePreload TimelinePreload { get; internal set; } = null!;

    public static IZoneControl Zone { get; internal set; } = null!;
    public static IMapEffects MapEffects { get; internal set; } = null!;
    public static ILayoutFunctions Layout { get; internal set; } = null!;
    public static IInstanceContentDirector Director { get; internal set; } = null!;
    public static IRsvFunctions Rsv { get; internal set; } = null!;
    public static IRsfFunctions Rsf { get; internal set; } = null!;

    public static IPartyHud PartyHud { get; internal set; } = null!;
    public static IHaterList HaterList { get; internal set; } = null!;
    public static IPlayerConditions PlayerConditions { get; internal set; } = null!;
    public static ILimitBreakController LimitBreak { get; internal set; } = null!;
    public static IPlayerActionState PlayerActionState { get; internal set; } = null!;
    public static IWaymarks Waymarks { get; internal set; } = null!;
    public static IBgm Bgm { get; internal set; } = null!;

    // Only the services that must run every frame whether or not a sim is; the sim ticks the
    // ones it drives (Bgm, weather) itself, so they stop with it. Each tick is isolated: the
    // guard must run even when something else throws.
    public static void Tick(float deltaSeconds)
    {
        try { Zone.TickGuard(); }
        catch (Exception e) { DiagnosticLog.Warn($"[Natives] Zone.TickGuard threw: {e}"); }
        try { UserActions.Tick(deltaSeconds); }
        catch (Exception e) { DiagnosticLog.Warn($"[Natives] UserActions.Tick threw: {e}"); }
    }

    // After the sim's own teardown, which still writes through these. Null-tolerant for a load
    // that failed part-way through installing.
    public static void Dispose()
    {
        Bgm?.Dispose();
        MapEffects?.Dispose();
        Zone?.Dispose();
        // Before PlayerInput: it unsubscribes from the hooks' event.
        UserActions?.Dispose();
        PlayerInput?.Dispose();
    }
}
