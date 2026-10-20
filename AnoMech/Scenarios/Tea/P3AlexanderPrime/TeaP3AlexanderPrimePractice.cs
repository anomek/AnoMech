namespace AnoMech.Scenarios.Tea.P3AlexanderPrime;

public enum P3Practice
{
    WholePhase,
    Inception,
    Wormhole,
}

public sealed record P3PracticeEntry(P3Practice Practice, string Label, float Start, string Covers);

// Each mechanic starts where the party regroups under Alexander Prime before it, with his target
// where the phase would have left it.
public static class TeaP3AlexanderPrimePractice
{
    public static readonly P3PracticeEntry[] All =
    [
        new(P3Practice.WholePhase, "Whole phase", 0f, "Alexander Prime's arrival to Divine Judgment."),
        new(P3Practice.Inception, "Inception", 0f,
            "Chastening Heat and Divine Spear, then the orbs, crystals and True Heart, the debuffs, Sacrament, Super Jump and the swords."),
        new(P3Practice.Wormhole, "Wormhole", 91.6f,
            "Chastening Heat and Divine Spear, then Limit Cut, the chakrams and wormholes, Sacrament, Apocalyptic Ray, Enumeration, "
            + "Mega Holy and Divine Judgment."),
    ];

    public static P3PracticeEntry Of(P3Practice practice) => All[(int)practice];

    // Where a run of `practice` stops: Wormhole's start after Inception, otherwise the phase's end.
    public static float RunEnd(P3Practice practice) => practice == P3Practice.Inception ? Of(P3Practice.Wormhole).Start : float.MaxValue;

    // Whether a timeline moment is part of the run.
    public static bool Covers(P3Practice practice, float time) => time >= Of(practice).Start && time < RunEnd(practice);
}
