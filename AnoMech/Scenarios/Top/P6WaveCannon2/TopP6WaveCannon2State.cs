using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P6WaveCannon2;

// Per-run randomized assignments the scenario and AI consume. Filled in the ctor
// (apply override if set, otherwise pick at random) so Run stays deterministic
// for the duration of one play. See TopP5DeltaState for the canonical shape.
public sealed class TopP6WaveCannon2State
{
    private readonly Rng rng = new();

    public RoleList ProteanOrder { get; }
    public PartyRole WildChargeTarget { get; }
    
    public bool InFirst { get; }

    public TopP6WaveCannon2State(SimParty party, TopP6WaveCannon2StateOverrides overrides)
    {
        ProteanOrder = RoleList.Random(party);
        WildChargeTarget = rng.NextRole();
        InFirst = overrides.InFirst ?? rng.NextBool();
    }
}
