namespace AnoMech.Scenarios.Uwu.UltimateAnnihilation;

public enum IfritCorner { Random, SouthEast, SouthWest }

public class UltimateAnnihilationStateOverrides
{
    public IfritCorner IfritCorner { get; set; }
    public bool SearingWindOnPlayer { get; set; }
    public bool FlamingCrushOnPlayer { get; set; }
    public bool UltimaPushedBelowHalf { get; set; }
}
