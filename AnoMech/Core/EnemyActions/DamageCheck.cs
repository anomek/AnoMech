using System;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;

namespace AnoMech.Core.EnemyActions;

internal static class DamageCheck
{
    // Null when the hit is survivable, otherwise why it kills ("" when it simply does).
    public static string? LethalCause(SimCharacter target, VulnSpec spec, Severity severity, DamageKind kind, SimParty party)
    {
        if (spec.Protections.Any(id => target.FindStatus(id) != null)) return null;
        if (severity.Kind == SeverityKind.Lethal) return MissingProtection(spec) ?? "";
        var vuln = CarriedVulnerability(target, spec);
        if (vuln is { RequiredMitigation: null }) return "had vuln up debuff";
        if (severity.Kind == SeverityKind.TankBuster && !IsTank(target)) return "tank buster";
        if (Survives(target, party, MathF.Max(severity.MinMitigation, vuln?.RequiredMitigation ?? 0f), kind)) return null;
        return vuln != null ? "not enough mitigation for a hit with vuln up" : "not enough mitigation";
    }

    private static string? MissingProtection(VulnSpec spec)
        => spec.Protections.Count > 0 ? $"no {StatusLookup.Name(spec.Protections[0])}" : null;

    public static bool IsTank(SimCharacter target) => target is ISimPartyMember { Role: PartyRole.OffTank or PartyRole.MainTank };

    private static Vulnerability? CarriedVulnerability(SimCharacter target, VulnSpec spec)
    {
        foreach (var vuln in spec.Vulnerabilities)
        {
            if (target.FindStatus(vuln.StatusId) is not { } status || status.Stacks < vuln.MinStacks) continue;
            DiagnosticLog.Info($"[EnemyAction] {(target as ISimPartyMember)?.Role} is hit carrying vuln up {vuln.StatusId}: needs {(vuln.RequiredMitigation is { } required ? $"{required:P0} mitigation" : "nothing, always lethal")}.");
            return vuln;
        }
        return null;
    }

    // Only a human's own statuses are ever checked: a bot always passes. Spends the target's shields.
    private static bool Survives(SimCharacter target, SimParty party, float requiredMitigation, DamageKind kind)
    {
        if (requiredMitigation <= 0f || !ChecksMitigation(target, party)) return true;
        var effective = Mitigation.Effective(target.ActiveStatusSnapshot.Select(s => s.StatusId), kind);
        Mitigation.SpendShields(target);
        var survives = effective >= requiredMitigation - 0.0005f;
        DiagnosticLog.Info($"[EnemyAction] Mitigation check: {(target as ISimPartyMember)?.Role} has {effective:P1} {kind}, needs {requiredMitigation:P1} -- {(survives ? "survives" : "dies")}.");
        return survives;
    }

    private static bool ChecksMitigation(SimCharacter target, SimParty party)
        => Mitigation.Required && target is ISimPartyMember && !party.IsBotDriven(target);
}
