using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top;

public class DamageSolver
{
    private Dictionary<DamageType, List<ushort>> vulnUpStatuses = [];
    private Dictionary<ushort, int> statusStacksOverwrites = [];

    SimParty party;

    public DamageSolver(SimParty party)
    {
        this.party = party;
    }
    
    
    public void Resolve(
        IPositioned source, uint actionId, DamageType[] damageType, (ushort statusId, float duration)[] statusesToApply,
        int stackMinTargets = 0)
    {
        var targets = party.Find.InsideActionAoe(actionId, source.Placement());
        HashSet<DamageType> damageTypes = [DamageType.Any];
        Array.ForEach(damageType, d => damageTypes.Add(d));
        foreach (var target in targets)
        {
            if (targets.Count < stackMinTargets)
            {
                target.Die($"Died to {actionId} ({targets.Count}/{stackMinTargets} players in stack)");
            }
            else if (damageTypes.Contains(DamageType.Lethal))
            {
                target.Die($"Died to {actionId}");
            }
            else if (IsLethal(target, damageTypes))
            {
                target.Die($"Died to {actionId} (had vuln up debuff)");
            }
            else
            {
                foreach (var status in statusesToApply)
                {
                    target.AddStatus(status.statusId, status.duration);       
                }
            }
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
    Magic
}
