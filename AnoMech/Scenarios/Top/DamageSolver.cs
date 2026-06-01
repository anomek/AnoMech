using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
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
        IPositioned? source, uint actionId, DamageType[] damageType, (ushort statusId, float duration)[] statusesToApply,
        int stackMinTargets = 0, int wildChargeTargets = 0, DamageType[]? wildChargeDamageType = null)
    {
        if (source == null) return;
#if DEBUG
        AnoMech.Windows.DamageDebugWindow.Instance?.Record(actionId, source.Placement());
#endif
        var targets = party.Find.InsideActionAoe(actionId, source.Placement());
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
                target.Die($"Died to {actionId} ({targets.Count}/{stackMinTargets} players in stack)");
            }
            else if (!CheckLethal(actionId, target, wildCharge ? damageTypeWildCharge : damageTypeBase))
            {
                foreach (var status in statusesToApply)
                {
                    target.AddStatus(status.statusId, status.duration);       
                }
            }
        }
    }
    
    private bool CheckLethal(uint actionId, SimCharacter target, HashSet<DamageType> damageTypes)
    {
        if (damageTypes.Contains(DamageType.Lethal))
        {
            target.Die($"Died to {actionId}");
            return true;
        }
        else if (IsLethal(target, damageTypes))
        {
            target.Die($"Died to {actionId} (had vuln up debuff)");
            return true;
        }
        else if (damageTypes.Contains(DamageType.TankBuster) && target is not ISimPartyMember { Role: PartyRole.OffTank or PartyRole.MainTank })
        {
            target.Die($"Died to {actionId} (tank buster)");
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
    TankBuster
}
