namespace AnoMech.Scenarios.Top.P2PartySynergy;

// User-controlled overrides for TopP2PartySynergyState's randomized fields.
// Bound by the scenario's settings UI; null values leave the field randomized
// at scenario start. The state ctor consumes this directly.
public sealed class TopP2PartySynergyStateOverrides
{
    public Direction? NewNorthA { get; set; }
    public Direction? NewNorthB { get; set; }
    public GlitchType? Glitch { get; set; }
    public OmegaAttack? AttackM { get; set; }
    public OmegaAttack? AttackF { get; set; }
}
