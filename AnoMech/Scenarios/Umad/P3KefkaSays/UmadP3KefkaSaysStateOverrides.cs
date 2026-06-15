namespace AnoMech.Scenarios.Umad.P3KefkaSays;

// User-controlled overrides for UmadP3KefkaSaysState's randomized fields. Bound by
// the scenario's settings UI; null/default values leave the field randomized at
// scenario start. The state ctor consumes this directly.
// See UmadP2ForsakenStateOverrides for the canonical shape.
public sealed class UmadP3KefkaSaysStateOverrides
{
    public bool? FirstBlizzardReal { get; set; }    // null = random; true = Real, false = Fake (debug-only UI)
    public bool? FirstLightningReal { get; set; }   // null = random; true = Real, false = Fake (debug-only UI)
    public int?  FirstBlizzardOffset { get; set; }  // null = random; else 0 or 1 (debug-only UI)
    public bool? InfernoReal { get; set; }          // chaos fire: null = random; true = Real, false = Fake (debug-only UI)
    public bool? TsunamiReal { get; set; }          // null = random; true = Real, false = Fake (debug-only UI)
}
