namespace AnoMech.Scenarios.Tea.P4PerfectAlexander;

public enum P4Practice
{
    WholePhase,
    TheFinalWord,
    OpticalSight,
    FateCalibrationAlpha,
    CapitalPunishmentOne,
    FateCalibrationBeta,
    CapitalPunishmentTwo,
    AlmightyJudgmentOne,
    CapitalPunishmentThree,
    AlmightyJudgmentTwo,
    TemporalPrison,
}

public sealed record P4PracticeEntry(P4Practice Practice, string Label, float Start, string Covers);

// Each mechanic starts where the party regroups under Perfect Alexander before it, with its target
// and the last Ordained Punishment's vulnerability where the phase would have left them.
public static class TeaP4PerfectAlexanderPractice
{
    public static readonly P4PracticeEntry[] All =
    [
        new(P4Practice.WholePhase, "Whole phase", 0f, "The Final Word to Perfect Alexander's death."),
        new(P4Practice.TheFinalWord, "The Final Word", 0f, "The beacons, the two Ordained casts and the forced march."),
        new(P4Practice.OpticalSight, "Optical Sight", 36f, "The two Optical Sights, spread and stack in either order."),
        new(P4Practice.FateCalibrationAlpha, "Fate Calibration α", 55f,
            "The projections live out the future; then the defamation, shared stack, aggravated, Ordained ×2 and Sacraments."),
        new(P4Practice.CapitalPunishmentOne, "Ordained Capital Punishment 1", 108.5f, "The shared tankbuster, Ordained Punishment and the swap."),
        new(P4Practice.FateCalibrationBeta, "Fate Calibration β", 128f,
            "The projections' future, then the march, tethers, shared stack, J Jumps, Optical Sight and Radiant Sacrament."),
        new(P4Practice.CapitalPunishmentTwo, "Ordained Capital Punishment 2", 208.5f, "The off tank's solo tankbusters and the swap."),
        new(P4Practice.AlmightyJudgmentOne, "Almighty Judgment 1", 227f, "The exatrines in the box and Irresistible Grace."),
        new(P4Practice.CapitalPunishmentThree, "Ordained Capital Punishment 3", 254f, "The main tank's solo tankbusters and the swap."),
        new(P4Practice.AlmightyJudgmentTwo, "Almighty Judgment 2", 272f, "The second exatrines and Irresistible Grace."),
        new(P4Practice.TemporalPrison, "Temporal Prison", 299f, "Temporal Interference and the jails."),
    ];

    public static P4PracticeEntry Of(P4Practice practice) => All[(int)practice];

    // The mechanic a timeline moment belongs to.
    public static P4Practice MechanicAt(float time)
    {
        var mechanic = P4Practice.TheFinalWord;
        foreach (var entry in All)
            if (entry.Practice != P4Practice.WholePhase && entry.Start <= time) mechanic = entry.Practice;
        return mechanic;
    }

    // Where a run of `practice` stops: the next mechanic's start, or Perfect Alexander's death.
    public static float RunEnd(P4Practice practice)
    {
        if (practice is P4Practice.WholePhase or P4Practice.TemporalPrison) return TeaP4PerfectAlexanderState.KillAt;
        return System.MathF.Min(TeaP4PerfectAlexanderState.KillAt, All[(int)practice + 1].Start);
    }

    // Whether a timeline moment is part of the run.
    public static bool Covers(P4Practice practice, float time)
        => time >= Of(practice).Start && time <= RunEnd(practice)
           && (practice == P4Practice.WholePhase || MechanicAt(time) == practice);
}
