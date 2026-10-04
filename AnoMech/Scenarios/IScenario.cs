using System.Collections.Generic;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
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

    // Core replication works for any scenario; this also needs the debug-bot AI replay plumbing
    // (IMultiplayerReplayable).
    bool SupportsMultiplayer => false;

    // Selectable strats. Run's selectedAi indexes this (null = solo); region buttons derive
    // from each strat's IScenarioAi.Group.
    IReadOnlyList<IScenarioAi> AiStrats { get; }

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

    // Settings each player keeps for themselves, never sent to the host: drawn for a peer too,
    // above the host's summary. localRole is null while a peer has no seat yet.
    bool HasLocalSettings => false;
    void DrawLocalSettings(PartyRole? localRole) { }

    // The overrides object DrawSettings edits, for the lobby's read-only summary
    // (ScenarioSettingsSummary); null when there is nothing to configure.
    object? SettingsOverrides => null;

    // The lobby's read-only lines for those overrides. A scenario whose settings read badly as
    // property names and values words its own.
    IReadOnlyList<string> SettingsSummary => ScenarioSettingsSummary.Describe(SettingsOverrides);

    // Per-player settings the fight can't produce together (see SettingsConflicts). A start is
    // refused while this is non-empty, rather than running something the host didn't ask for.
    IReadOnlyList<string> SettingsConflicts => [];

    // Deterministic instance-progress replay (native DirectorUpdate/AddEffect calls) with no
    // host-only dependency, so Game.RunScenarioInternal schedules it for a peer too.
    void RunInstanceEvents(SimWorld world) { }
}
