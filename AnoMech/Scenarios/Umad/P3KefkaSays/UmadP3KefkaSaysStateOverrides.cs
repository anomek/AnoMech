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

    // Neo Exdeath's four Mystery casts (3x Grand Cross + Flood of Naught), by cast order.
    // null = random; true = Real (boss tells the truth), false = Fake (boss lies).
    public bool? ExdeathCast1Real { get; set; }
    public bool? ExdeathCast2Real { get; set; }
    public bool? ExdeathCast3Real { get; set; }
    public bool? ExdeathCast4Real { get; set; }

    // Chaos's two casts, by cast order (the element type Inferno/Tsunami stays randomized).
    // null = random; true = Real, false = Fake.
    public bool? ChaosCast1Real { get; set; }
    public bool? ChaosCast2Real { get; set; }
}
