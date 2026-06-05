namespace AnoMech.Scenarios.Umad.P2Forsaken;

// User-controlled overrides for UmadP2ForsakenState's randomized fields. Bound
// by the scenario's settings UI; null/default values leave the field randomized
// at scenario start. The state ctor consumes this directly.
// See TopP5DeltaStateOverrides for the canonical shape.
public sealed class UmadP2ForsakenStateOverrides
{
    public EndAttack? FirstEndAttack { get; set; }   // null = Auto/randomize
}
