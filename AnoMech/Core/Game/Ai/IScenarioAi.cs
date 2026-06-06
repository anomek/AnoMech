using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game.Ai;

// A scenario AI ("strat") — one selectable party-movement choreography. A scenario
// can expose several; the user picks one in the main window before Start.
//
// The non-generic view carries only what the UI needs (the display Name) so strats
// of different scenarios can be listed uniformly. The generic subtype carries the
// actual entry point: state arrives via Run (not the constructor), so a strat is a
// stateless strategy that is constructed once and fed the per-run state at start.
public interface IScenarioAi
{
    string Name { get; }
}

public interface IScenarioAi<TState> : IScenarioAi
{
    void Run(TState state, SimWorld world);
}
