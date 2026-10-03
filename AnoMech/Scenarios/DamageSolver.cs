using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;

namespace AnoMech.Scenarios;

public class DamageSolver
{
    private Dictionary<DamageType, List<ushort>> vulnUpStatuses = [];
    private Dictionary<ushort, float> vulnUpRequiredMitigation = [];
    private Dictionary<ushort, int> statusStacksOverwrites = [];

    SimParty party;

    public DamageSolver(SimParty party)
    {
        this.party = party;
    }
    
    
    public IReadOnlyList<SimCharacter> Resolve(
        IPositioned? source, uint actionId, DamageType[] damageType,
        (ushort statusId, float duration)[] statusesToApply,
        ushort[]? removeStatus = null,
        int stackMinTargets = 0, int wildChargeTargets = 0, DamageType[]? wildChargeDamageType = null,
        float? size = null, float? coneRotationDelta = null, SimCharacter[]? excludeTargets = null,
        bool killTargets = true, float requiredMitigation = 0f, float lethalWithin = 0f,
        float? understackedTankMitigation = null, float extraRange = 0f)
    {
        if (source == null) return [];
        var placement = source.Placement();
        if (coneRotationDelta is { } delta)
            placement = placement with { Rotation = placement.Rotation + delta };
        var query = new AoeQuery(actionId, placement, size: size, extraRange: extraRange);
#if DEBUG
        AnoMech.Windows.DamageDebugWindow.Instance?.Record(query);
#endif
        var targets = query.Run(party.Find);
        DiagnosticLog.Info(
            $"[DamageSolver] Resolve: {ActionLookup.Name(actionId)} from ({placement.Position.X:F1},{placement.Position.Z:F1}) rot={placement.Rotation:F3} -- {targets.Count} target(s): "
            + string.Join(", ", targets.Select(t => (t as ISimPartyMember)?.Role.ToString() ?? "?")));
        // Cone-shaped resolves (e.g. AllThingsEndHalfCone) only tell you who got hit --
        // for a mostly-stacked group where a few survive and a few don't, that's not
        // enough to tell whether a near-miss was a positioning bug or just an unlucky
        // cone edge. Mirrors InsideCone's own cos-vs-cosHalf check so this is directly
        // comparable to what actually decided each member's fate, not a separate guess.
        if (size is { } halfAngle)
        {
            var forwardX = MathF.Sin(placement.Rotation);
            var forwardZ = MathF.Cos(placement.Rotation);
            var cosHalf = MathF.Cos(halfAngle);
            var detail = string.Join(", ", party.ActiveMembers().Select(m =>
            {
                var dx = m.Position.X - placement.Position.X;
                var dz = m.Position.Z - placement.Position.Z;
                var dist = MathF.Sqrt(dx * dx + dz * dz);
                var cos = dist < 0.01f ? 1f : (dx * forwardX + dz * forwardZ) / dist;
                var role = (m as ISimPartyMember)?.Role.ToString() ?? "?";
                return $"{role}@({m.Position.X:F1},{m.Position.Z:F1}) dist={dist:F1} cos={cos:F2}{(cos >= cosHalf ? " IN" : "")}";
            }));
            DiagnosticLog.Info($"[DamageSolver] Cone geometry (halfAngle={halfAngle:F2} cosHalf={cosHalf:F2}): {detail}");
        }
        List<SimCharacter> deadTargets = [];
        if (excludeTargets is { Length: > 0 })
            targets = targets.Where(t => !excludeTargets.Contains(t)).ToList();
        HashSet<DamageType> damageTypeBase = [DamageType.Any];
        Array.ForEach(damageType, d => damageTypeBase.Add(d));
        HashSet<DamageType> damageTypeWildCharge = new(damageTypeBase);
        if (wildChargeDamageType != null) Array.ForEach(wildChargeDamageType, d => damageTypeWildCharge.Add(d));
        
        var i = 0;
        foreach (var target in targets)
        {
            bool wildCharge = i++ < wildChargeTargets;
            var understacked = targets.Count < stackMinTargets;
            // A stack taken short kills everyone in it, unless a tank is allowed to eat it on cooldowns.
            var tankSoaks = understacked && understackedTankMitigation != null && IsTank(target);
            var needed = tankSoaks ? MathF.Max(requiredMitigation, understackedTankMitigation!.Value) : requiredMitigation;
            if (understacked && !tankSoaks)
            {
                deadTargets.Add(target);
                if (killTargets)
                    target.Die($"Died to {ActionLookup.Name(actionId)} ({targets.Count}/{stackMinTargets} players in stack)");
            }
            else if (DistanceXZ(target.Position, placement.Position) is var distance && distance < lethalWithin)
            {
                deadTargets.Add(target);
                if (killTargets)
                    target.Die($"Died to {ActionLookup.Name(actionId)} ({distance:F0}y from it, lethal inside {lethalWithin:F0}y)");
            }
            else if (CheckLethal(actionId, target, wildCharge ? damageTypeWildCharge : damageTypeBase, killTargets, needed))
            {
                deadTargets.Add(target);
            }
            
            if (target.IsAlive())
            {
                if (removeStatus is {} r)
                    foreach (var s in r)
                        target.RemoveStatus(s);
                foreach (var status in statusesToApply)
                    target.AddStatus(status.statusId, status.duration);       
            }
        }
        return killTargets ? targets : deadTargets;
    }
    
    private static float DistanceXZ(System.Numerics.Vector3 a, System.Numerics.Vector3 b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    // Gaze resolver. Each member lives or dies by which way it faces the `target`.
    // lookAway == true: safe play is to face away, so anyone "looking" (target inside
    // the front 90° arc) dies. lookAway == false: safe play is to face the target, so
    // anyone "not looking" (target inside the back 90° arc) dies. The target itself is
    // always skipped. Facing uses each member's own rotation (forward = (sin, cos)),
    // matching CharacterFind.InsideCone. Returns the members that were killed.
    public IReadOnlyList<SimCharacter> ResolveGaze(IPositioned? target, bool lookAway)
    {
        if (target == null) return [];
        const float cosHalf = 0.70710677f; // cos(45°) — front/back arcs are 90° wide
        var killed = new List<SimCharacter>();
        foreach (var member in party.ActiveMembers().ToList())
        {
            if (ReferenceEquals(member, target)) continue;
            var dx = target.Position.X - member.Position.X;
            var dz = target.Position.Z - member.Position.Z;
            var distSq = dx * dx + dz * dz;
            if (distSq < 0.0001f) continue; // on top of the target — facing undefined
            var dist = MathF.Sqrt(distSq);
            var cos = (dx * MathF.Sin(member.Rotation) + dz * MathF.Cos(member.Rotation)) / dist;
            var looking = cos >= cosHalf;        // target within front 90° arc
            var notLooking = cos <= -cosHalf;    // target within back 90° arc
            if (lookAway ? looking : notLooking)
            {
                member.Die(lookAway
                    ? "Died to gaze (looked at the target)"
                    : "Died to gaze (faced away from the target)");
                killed.Add(member);
            }
        }
        return killed;
    }

    private bool CheckLethal(uint actionId, SimCharacter target, HashSet<DamageType> damageTypes, bool killTarget, float requiredMitigation)
    {
        var vulnMitigation = VulnUpRequiredMitigation(target, damageTypes);
        var kind = damageTypes.Contains(DamageType.Magic) ? DamageKind.Magic : DamageKind.Physical;
        string? cause;
        if (damageTypes.Contains(DamageType.Lethal)) cause = "";
        else if (vulnMitigation >= 1f) cause = " (had vuln up debuff)";
        else if (damageTypes.Contains(DamageType.TankBuster) && !IsTank(target)) cause = " (tank buster)";
        else if (Survives(target, MathF.Max(requiredMitigation, vulnMitigation ?? 0f), kind)) cause = null;
        else cause = vulnMitigation != null ? " (not enough mitigation for a hit with vuln up)" : " (not enough mitigation)";

        if (cause == null) return false;
        if (killTarget) target.Die($"Died to {ActionLookup.Name(actionId)}{cause}");
        return true;
    }

    private static bool IsTank(SimCharacter target) => target is ISimPartyMember { Role: PartyRole.OffTank or PartyRole.MainTank };

    // The mitigation needed to live through a hit of this many times the target's max HP.
    public static float RequiredMitigation(float hitAsShareOfMaxHp)
        => hitAsShareOfMaxHp <= 1f ? 0f : 1f - 1f / hitAsShareOfMaxHp;

    // Only a human's own statuses are ever checked: a bot always passes. Spends the target's shields.
    public bool Survives(SimCharacter target, float requiredMitigation, DamageKind kind = DamageKind.Physical)
    {
        if (requiredMitigation <= 0f || !ChecksMitigation(target)) return true;
        var effective = EffectiveMitigation(target, kind);
        Mitigation.SpendShields(target);
        var survives = effective >= requiredMitigation - 0.0005f;
        DiagnosticLog.Info($"[DamageSolver] Mitigation check: {(target as ISimPartyMember)?.Role} has {effective:P1} {kind}, needs {requiredMitigation:P1} -- {(survives ? "survives" : "dies")}.");
        return survives;
    }

    // Read-only, for anyone: what a hit's shown number should be scaled by.
    public float EffectiveMitigation(SimCharacter target, DamageKind kind = DamageKind.Physical)
        => Mitigation.Effective(target.ActiveStatusSnapshot.Select(s => s.StatusId), kind);

    private bool ChecksMitigation(SimCharacter target)
        => Plugin.Config.EnableTankMitigation && Natives.UserActions.Enabled
           && target is ISimPartyMember && !party.IsBotDriven(target);

    private List<ushort> VulnUps(DamageType damageType)
    {
        if (!vulnUpStatuses.ContainsKey(damageType))
            vulnUpStatuses[damageType] = [];
        return vulnUpStatuses[damageType];
    }
    
    private IEnumerable<ushort> VulnUps(DamageType[] damageType)
    {
        return damageType
               .SelectMany(VulnUps);
    }
    
    // Null without a vuln; 1 = lethal short of an invuln, which is what a vuln is unless
    // SetMitigableStatuses said otherwise.
    private float? VulnUpRequiredMitigation(SimCharacter target, HashSet<DamageType> damageType)
    {
        var statusId = VulnUps(damageType.ToArray()).FirstOrDefault(target.HasStatus);
        if (statusId == 0) return null;
        var required = vulnUpRequiredMitigation.GetValueOrDefault(statusId, 1f);
        DiagnosticLog.Info($"[DamageSolver] {(target as ISimPartyMember)?.Role} is hit carrying vuln up {statusId}: needs {required:P0} mitigation.");
        return required;
    }

    // Damage feedback: a flytext number sized off the target's own max HP, plus — when the hit
    // is lethal — the KO via Die() (the same sink as every death). `lethal` is the caller's call
    // (exaflare = always; spread = only on overlap); `context` is the death-message parenthetical.
    // Non-party targets ignored. The KO and all HP-bar handling — real-death drop in SimPlayer.OnKilled,
    // and the godmode drop/heal preview in Game.Kill — live downstream of Die(), so this only shows the
    // number and forwards the kill.
    public void ApplyDamage(SimCharacter target, float fractionOfMaxHp, uint actionId, string context, bool lethal)
    {
        if (target is not ISimPartyMember) return;
        var name = ActionLookup.Name(actionId);
        if (target.Proxy is { Exists: true } chara)
            chara.ShowFlyText((uint)MathF.Round(fractionOfMaxHp * chara.MaxHealth), name);
        if (!lethal) return;
        target.Die($"Died to {name} ({context})");
    }

    public void SetStatuses(DamageType type, params ushort[] statuses)
    {
        var list = VulnUps(type);
        Array.ForEach(statuses, list.Add);
    }

    // A vuln a carrier can live through on enough mitigation, instead of only on an invuln.
    public void SetMitigableStatuses(DamageType type, float requiredMitigation, params ushort[] statuses)
    {
        SetStatuses(type, statuses);
        foreach (var status in statuses) vulnUpRequiredMitigation[status] = requiredMitigation;
    }
}
