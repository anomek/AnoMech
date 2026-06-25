using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios;

public class DamageSolver
{
    private Dictionary<DamageType, List<ushort>> vulnUpStatuses = [];
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
        bool killTargets = true)
    {
        if (source == null) return [];
        var placement = source.Placement();
        if (coneRotationDelta is { } delta)
            placement = placement with { Rotation = placement.Rotation + delta };
        var query = new AoeQuery(actionId, placement, size: size);
#if DEBUG
        AnoMech.Windows.DamageDebugWindow.Instance?.Record(query);
#endif
        var targets = query.Run(party.Find);
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
            if (targets.Count < stackMinTargets)
            {
                deadTargets.Add(target);
                if (killTargets)
                    target.Die($"Died to {ActionLookup.Name(actionId)} ({targets.Count}/{stackMinTargets} players in stack)");
            }
            else if (CheckLethal(actionId, target, wildCharge ? damageTypeWildCharge : damageTypeBase, killTargets))
            {
                deadTargets.Add(target);
            }
            else
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

    private bool CheckLethal(uint actionId, SimCharacter target, HashSet<DamageType> damageTypes, bool killTarget)
    {
        if (damageTypes.Contains(DamageType.Lethal))
        {
            if (killTarget)  target.Die($"Died to {ActionLookup.Name(actionId)}");
            return true;
        }
        else if (IsLethal(target, damageTypes))
        {
            if (killTarget) target.Die($"Died to {ActionLookup.Name(actionId)} (had vuln up debuff)");
            return true;
        }
        else if (damageTypes.Contains(DamageType.TankBuster) && target is not ISimPartyMember { Role: PartyRole.OffTank or PartyRole.MainTank })
        {
            if(killTarget) target.Die($"Died to {ActionLookup.Name(actionId)} (tank buster)");
            return true;
        }
        else
        {
            return false;
        }
    }
    
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
    
    private bool IsLethal(SimCharacter target, HashSet<DamageType> damageType)
    {
        var statusId = VulnUps(damageType.ToArray())
            .FirstOrDefault(target.HasStatus);
        if (statusId != 0)
        {
            Plugin.Log.Info($"{(target as ISimPartyMember)?.Role} got lethal damage due to {statusId}");
        }
        return statusId != 0;
    }

    public void SetStatuses(DamageType type, params ushort[] statuses)
    {
        var list = VulnUps(type);
        Array.ForEach(statuses, list.Add);
    }
}

public enum DamageType
{
    Lethal,
    Any,
    Magic,
    TankBuster,
    Lightning,
    Earth,
    Black,
    White,
}
