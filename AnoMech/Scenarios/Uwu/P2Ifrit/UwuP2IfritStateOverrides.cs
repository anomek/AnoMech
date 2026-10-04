namespace AnoMech.Scenarios.Uwu.P2Ifrit;

public enum SearingWindChoice
{
    Random,
    FirstAndThirdHowl,
    SecondHowl,
}

public class UwuP2IfritStateOverrides
{
    public SearingWindChoice SearingWindOnPlayer { get; set; }
}
