namespace AnoMech.Scenarios.Fru.P5FulgentBlade;

// User-forced rolls for FruP5FulgentBladeState; null leaves a field randomized.
public sealed class FruP5FulgentBladeStateOverrides
{
    // 0 = south, 1 = north, 2 = east, 3 = west pocket, before the mirror.
    public int? PositionIndex { get; set; }
    public int? RotationIndex { get; set; }
    public bool? Mirrored { get; set; }
    // One per group.
    public bool[]? CardinalDarkInward { get; set; }
    // 0-3: which of the four furthest to Pandora's left / right each Akh Morn picks.
    public int? AkhMornLeftPick { get; set; }
    public int? AkhMornRightPick { get; set; }
}
