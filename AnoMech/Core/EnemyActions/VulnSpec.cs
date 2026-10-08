using System.Collections.Generic;

namespace AnoMech.Core.EnemyActions;

// RequiredMitigation null = always lethal, bots and mitigation-off included; 1 = lethal short of an invuln.
public readonly record struct Vulnerability(ushort StatusId, float? RequiredMitigation = null, int MinStacks = 1);

// The statuses that change whether a hit kills. How big the hit is lives in its Severity, per cast.
public sealed record VulnSpec
{
    public static readonly VulnSpec None = new();

    public IReadOnlyList<Vulnerability> Vulnerabilities { get; init; } = [];

    // Carrying any of these, the hit can't kill: it outranks Severity and every vulnerability.
    public IReadOnlyList<ushort> Protections { get; init; } = [];

    public VulnSpec VulnerableTo(ushort statusId, float? requiredMitigation = null, int minStacks = 1)
        => this with { Vulnerabilities = [.. Vulnerabilities, new Vulnerability(statusId, requiredMitigation, minStacks)] };

    public VulnSpec ProtectedBy(ushort statusId) => this with { Protections = [.. Protections, statusId] };
}
