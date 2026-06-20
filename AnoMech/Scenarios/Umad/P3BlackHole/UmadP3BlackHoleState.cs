using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// Per-run randomized assignments the scenario and AI consume. Filled in the ctor
// (apply override if set, otherwise pick at random) so Run stays deterministic for
// the duration of one play. See UmadP4KefkaSaysState for the canonical shape.
public sealed class UmadP3BlackHoleState
{
    private readonly Rng rng = new();

    public UmadP3BlackHoleState(SimParty party, UmadP3BlackHoleStateOverrides overrides)
    {
        // TODO: for each randomized field — use overrides.<X> if set, else pick at random.
        // `party` exposes PlayerRole and the 8 slots (e.g. RoleList.RandomRoleStable(party)).
    }
}
