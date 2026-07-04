namespace AnoMech.Scenarios.Uwu.UltimatePredation;

public class UltimatePredationStateOverrides
{
    /// <value>
    /// If <see langword="null"/>, the bosses will be positioned randomly.
    /// If not <see langword="null"/> and <see langword="true"/>, the bosses will be positioned semi-randomly, allowing to execute the center dodge.
    /// </value>
    public bool? CenterDodge { get; set; }
}
