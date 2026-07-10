namespace AnoMech.Scenarios.Umad.P5Exaflares;

// Column order for one side's three exaflare waves. Lines are numbered 1-6 facing the
// fire source; each wave fires a pair (1/4, 2/5, or 3/6). Random resolves to one of the
// six fixed orders at scenario start.
public enum ExaFlareOrder
{
    Random,
    Line14_25_36,
    Line14_36_25,
    Line25_14_36,
    Line25_36_14,
    Line36_14_25,
    Line36_25_14,
}

// User-controlled overrides for UmadP5ExaflaresState's randomized fields. Bound by the
// scenario's settings UI; Random leaves the field randomized at scenario start. The state
// ctor consumes this directly. Same shape as TopP5DeltaStateOverrides.
public sealed class UmadP5ExaflaresStateOverrides
{
    public ExaFlareOrder LeftOrder { get; set; } = ExaFlareOrder.Random;
    public ExaFlareOrder RightOrder { get; set; } = ExaFlareOrder.Random;
}
