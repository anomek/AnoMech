namespace AnoMech.Scenarios.Umad.P4KefkaSays;

// Exdeath cast 2's Real/Fake override. Unlike the other casts it can also mirror
// cast 1's resolution (OppositeTo1 => Wave2True = !Wave1True).
public enum ExdeathCast2Mode { Auto, Real, Fake, OppositeTo1 }

// User-controlled overrides for UmadP4KefkaSaysState's randomized fields. Bound by
// the scenario's settings UI; null/default values leave the field randomized at
// scenario start. The state ctor consumes this directly.
// See UmadP2ForsakenStateOverrides for the canonical shape.
public sealed class UmadP4KefkaSaysStateOverrides
{
    public bool? FirstBlizzardReal { get; set; }    // null = random; true = Real, false = Fake (debug-only UI)
    public bool? FirstLightningReal { get; set; }   // null = random; true = Real, false = Fake (debug-only UI)
    public int?  FirstBlizzardOffset { get; set; }  // null = random; else 0 or 1 (debug-only UI)

    // Neo Exdeath's four Mystery casts (3x Grand Cross + Flood of Naught), by cast order.
    // null = random; true = Real (boss tells the truth), false = Fake (boss lies).
    public bool? ExdeathCast1Real { get; set; }
    public ExdeathCast2Mode ExdeathCast2 { get; set; }   // Auto/Real/Fake, or Opposite-to-1
    public bool? ExdeathCast3Real { get; set; }
    public bool? ExdeathCast4Real { get; set; }

    // Chaos's two casts, by cast order (the element type Inferno/Tsunami stays randomized).
    // null = random; true = Real, false = Fake.
    public bool? ChaosCast1Real { get; set; }
    public bool? ChaosCast2Real { get; set; }
}
