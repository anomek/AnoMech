namespace AnoMech.Scenarios.Dsr.P3Nidhogg;

// User-forced rolls for DsrP3NidhoggState; null leaves a field randomized.
public sealed class DsrP3NidhoggStateOverrides
{
    // Index into DsrP3NidhoggState.ArrowPatterns.
    public int? ArrowPattern { get; set; }
    public bool? FirstOutFirst { get; set; }
    public bool? SecondOutFirst { get; set; }
    // Index into DsrP3NidhoggState.CounterTowerShapes.
    public int? CounterTowerShape { get; set; }
}
