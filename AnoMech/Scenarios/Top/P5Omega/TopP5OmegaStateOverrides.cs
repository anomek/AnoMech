using AnoMech.Scenarios.Top;

namespace AnoMech.Scenarios;

// User-controlled overrides for TopP5OmegaState's randomized fields. Bound by
// the scenario's settings UI; null values leave the field randomized at
// scenario start. The state ctor consumes this directly.
public sealed class TopP5OmegaStateOverrides
{
    public OmegaAttack? FirstFAttack { get; set; }
    public OmegaAttack? FirstMAttack { get; set; }
    public OmegaAttack? SecondFAttack { get; set; }
    public OmegaAttack? SecondMAttack { get; set; }
    public bool? FirstWaveCannonFront { get; set; }
    public MonitorSide? MonitorSide { get; set; }
    public EightWayDirection? BettleSpawnDirection { get; set; }
}
