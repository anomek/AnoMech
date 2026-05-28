namespace AnoMech.Scenarios.Top.P5Sigma;

public enum HelloWorldOption { Auto, Near, Far, No }

// User-controlled overrides for TopP5SigmaState's randomized fields. Bound by
// the scenario's settings UI; null values leave the field randomized at
// scenario start. The state ctor consumes this directly.
public sealed class TopP5SigmaStateOverrides
{
    public Direction? NewNorthA { get; set; }
    public GlitchType? CloseFarTether { get; set; }
    public bool? TowerNorthFlip { get; set; }
    public Direction? NewNorthB { get; set; }
    public Rotation? SpinnerRotation { get; set; }
    public OmegaAttack? OmegaFForm { get; set; }
    public HelloWorldOption HelloWorld { get; set; }
    public bool? Dynamis { get; set; }
}
