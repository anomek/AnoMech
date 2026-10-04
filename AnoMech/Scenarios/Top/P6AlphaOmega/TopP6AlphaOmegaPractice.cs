namespace AnoMech.Scenarios.Top.P6AlphaOmega;

public enum P6Practice
{
    WholePhase,
    CosmoMemory,
    CosmoArrowOne,
    UnlimitedWaveCannonOne,
    WaveCannonOne,
    CosmoArrowTwo,
    UnlimitedWaveCannonTwo,
    CosmoDiveTwo,
    CosmoMeteor,
    MagicNumberOne,
    MagicNumberTwo,
    RunDynamis,
}

public sealed record P6PracticeEntry(P6Practice Practice, string Label, float Start, string Covers);

// Each mechanic starts where the party has regrouped before it, with the limit break gauge and
// everyone's Dynamis where the phase would have left them (TopP6AlphaOmegaFastForward).
public static class TopP6AlphaOmegaPractice
{
    public static readonly P6PracticeEntry[] All =
    [
        new(P6Practice.WholePhase, "Whole phase", 0f, "Cosmo Memory to Alpha Omega's death."),
        new(P6Practice.CosmoMemory, "Cosmo Memory", 0f, "The opening raidwide and its tank LB3, until Brilliant Dynamis."),
        new(P6Practice.CosmoArrowOne, "Cosmo Arrow 1 + Cosmo Dive 1", 16f,
            "Flash Gales, the first exasquares, then the dive's two tankbusters and its stack."),
        new(P6Practice.UnlimitedWaveCannonOne, "Unlimited Wave Cannon 1", 47f,
            "Flash Gales, then the exaflares and the six puddles along the wall."),
        new(P6Practice.WaveCannonOne, "Wave Cannon 1", 76.8f, "The proteans and the wild charge line, from the last puddle."),
        new(P6Practice.CosmoArrowTwo, "Cosmo Arrow 2 + Wave Cannon 2", 92f,
            "Flash Gales, the second exasquares, then the second proteans and wild charge."),
        new(P6Practice.UnlimitedWaveCannonTwo, "Unlimited Wave Cannon 2", 126f, "Flash Gales, then the second exaflares and puddles."),
        new(P6Practice.CosmoDiveTwo, "Cosmo Dive 2", 148f,
            "The second dive and its two melee LB3s; the third bar is sometimes not up by the stack."),
        new(P6Practice.CosmoMeteor, "Cosmo Meteor", 170f,
            "Puddles, comets and meteors that need the caster and physical ranged LB3s, spreads, flares and the stack."),
        new(P6Practice.MagicNumberOne, "Magic Number 1", 205f, "A tank LB3 for the raidwide, then a healer LB3 for Magic Number."),
        new(P6Practice.MagicNumberTwo, "Magic Number 2", 220f, "The second Magic Number's tank and healer LB3s."),
        new(P6Practice.RunDynamis, "Run: ****mi*", 235f, "The enrage and its melee LB3."),
    ];

    public static P6PracticeEntry Of(P6Practice practice) => All[(int)practice];

    public static P6Practice MechanicAt(float time)
    {
        var mechanic = P6Practice.CosmoMemory;
        foreach (var entry in All)
            if (entry.Practice != P6Practice.WholePhase && entry.Start <= time) mechanic = entry.Practice;
        return mechanic;
    }
}
