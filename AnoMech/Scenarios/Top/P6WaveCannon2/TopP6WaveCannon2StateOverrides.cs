namespace AnoMech.Scenarios.Top.P6WaveCannon2;

// User-controlled overrides for TopP6WaveCannon2State's randomized fields. Bound
// by the scenario's settings UI; null/default values leave the field randomized
// at scenario start. The state ctor consumes this directly.
// See TopP5DeltaStateOverrides for the canonical shape.
public sealed class TopP6WaveCannon2StateOverrides
{
    // Cosmo Arrow: true = In first, false = Out first, null = randomized.
    public bool? InFirst { get; set; }
}
