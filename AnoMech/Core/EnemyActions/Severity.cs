namespace AnoMech.Core.EnemyActions;

public enum SeverityKind
{
    Normal,
    TankBuster,
    Lethal,
    Harmless,
}

// How big a hit is, coarsely: who can live through it, and on how much mitigation.
public sealed record Severity(SeverityKind Kind, float MinMitigation = 0f)
{
    public static readonly Severity Normal = new(SeverityKind.Normal);
    public static readonly Severity TankBuster = new(SeverityKind.TankBuster);
    public static readonly Severity Lethal = new(SeverityKind.Lethal);
    // A hit that lands for 0: survivable unless the action's vulns say otherwise.
    public static readonly Severity Harmless = new(SeverityKind.Harmless);

    // Checked on the human player only: a bot always passes.
    public Severity MinMit(float required) => this with { MinMitigation = required };

    // The flytext a survived hit shows in place of its kind's, where the game's own is fixed.
    public uint? ShownAmount { get; init; }
    public Severity Showing(uint amount) => this with { ShownAmount = amount };

    // Flytext only, never compared to HP.
    public uint FlyTextAmount(bool kills)
        => kills ? 120_000u : ShownAmount ?? Kind switch
        {
            SeverityKind.TankBuster => 80_000u,
            SeverityKind.Harmless => 0u,
            _ => 40_000u,
        };
}

// A null Vulns is the cast's own.
public sealed record Hit(VulnSpec? Vulns, Severity Severity)
{
    public static implicit operator Hit(Severity severity) => new(null, severity);
}
