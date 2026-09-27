using System.Collections.Generic;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios;

// The mechanic timeline for one encounter fragment. Shared identity lives on the owning
// IZone (via Phase.Zone) and IPhase; a scenario declares only what is its own.
public interface IScenario
{
    string Name { get; }

    // Its phase — usually `=> TopZone.P5`.
    IPhase Phase { get; }

    bool SupportsSolo => false;

    // Whether the scenario has been checked against host replication (see MultiplayerManager).
    bool SupportsMultiplayer => false;

    // Selectable strats. Run's selectedAi indexes this (null = solo); region buttons derive
    // from each strat's IScenarioAi.Group.
    IReadOnlyList<IScenarioAi> AiStrats { get; }

    // Tankbusters the multiplayer host can pre-plan mitigation for (see TankMitigation).
    IReadOnlyList<TankBusterCastInfo> TankBusters => [];

    // Tanks spawn at this HP so TankMitigation's fixed numbers land against a real tank's
    // pool; null = the generic doppel HP.
    uint? TankMaxHealth => null;

    // How far into the phase's track the real fight is when this scenario starts.
    float BgmSecondsAtStart => 0f;

    void Run(SimWorld world, int? selectedAi);
    void Tick(float delta, float elapsed) { }

    // The fight-wide rolls only: one setting the whole sim shares.
    void DrawSettings() { }

    // Mechanic roles a single player carries -- a Limit Cut number, an Accretion, a tether. Not
    // part of DrawSettings: these are per seat, and mixing them into the fight-wide panel is
    // what made them hard to read. Drawn in their own dialog instead.
    bool HasPerPlayerSettings => false;
    void DrawPerPlayerSettings() { }

    // Stays editable while a multiplayer fight is running, unlike DrawSettings (e.g. a bot
    // tank's mitigation plan).
    void DrawMultiplayerSettings() { }

    // The overrides object DrawSettings edits, for the lobby's read-only summary
    // (ScenarioSettingsSummary); null when there is nothing to configure.
    object? SettingsOverrides => null;

    // Per-player settings the fight can't produce together (see SettingsConflicts). A start is
    // refused while this is non-empty, rather than running something the host didn't ask for.
    IReadOnlyList<string> SettingsConflicts => [];

    // Deterministic instance-progress replay (native DirectorUpdate/AddEffect calls) with no
    // host-only dependency, so Game.RunScenarioInternal schedules it for a peer too.
    void RunInstanceEvents(SimWorld world) { }
    // Whether the scenario has reached its own natural end, for Game's mechanic-streak
    // tracking. Default covers scenarios whose whole timeline lives on world.Events (the
    // common case: nothing to override). A scenario that schedules its mechanic on a private
    // EventScheduler instead (e.g. one kept immune to EventTimeScale for real-time precision)
    // must override this to check that queue instead, since world.Events would otherwise
    // look permanently empty from the first tick.
    bool IsFinished(SimWorld world) => world.Events.IsEmpty;
}
