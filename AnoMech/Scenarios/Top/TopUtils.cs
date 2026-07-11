using System.Collections.Generic;
using System.Numerics;
using AnoMech;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Top.P5Delta;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Top.TopConstants;

public record TopUtils(SimWorld World)
{
    
    public void CheckHelloWorldDeath()
    {
        foreach (var member in World.Party.AllMembers())
        {
            if (member.IsAlive()) continue;
            if (member.HasStatus(StatusId.HelloNearWorld))
            {
                member.RemoveStatus(StatusId.HelloNearWorld);
                HelloWorldFail(member.Position);
            }
            else if (member.HasStatus(StatusId.HelloDistantWorld))
            {
                member.RemoveStatus(StatusId.HelloDistantWorld);
                HelloWorldFail(member.Position);
            }
        }
    }
    
    public void HelloWorldFail(Vector3 pos)
    {
        var helper = World.SpawnEnemy(new EnemySpawnConfig(
                                          BNpcBaseId: BNpcBaseId.OmegaHelper,
                                          Targetable: false,
                                          EnemyList: EnemyListMode.Never,
                                          Placement: new Placement(pos, 0f)));
        if (helper != null) World.Events.Add(Duration.MonitorHelperLifetime, helper.Despawn);
        helper?.Cast(ActionId.HelloWorldFail);
        World.Party.WipeAllPlayers("Hello World Fail");
    }

    public static IReadOnlyList<Waymark> TopWaymarks => WaymarkPresets.Ring(13.63f);

    public bool IsDamageLethal(SimCharacter character, bool ruin)
    {
        var who = (character as ISimPartyMember)?.Role.ToString() ?? character.GetType().Name;
        var vuln = character.HasStatus(StatusId.VulnerabilityUp);
        var magicVuln = character.HasStatus(StatusId.MagicVulnerabilityUp);
        var magicVuln2 = character.FindStatus(StatusId.MagicVulnerabilityUpMini) is { Stacks: >= 2 };
        var twiceRuin = character.HasStatus(StatusId.TwiceComeRuin);
        var hpPenalty = character.HasStatus(StatusId.HPPenalty);
        var lethal = vuln || magicVuln || (ruin && twiceRuin) || magicVuln2 || hpPenalty;
        Plugin.Log.Info($"IsDamageLethal: {who} ruin={ruin} → {lethal} [VulnUp={vuln} MagicVulnUp={magicVuln} TwiceComeRuin={twiceRuin} MagicVuln2={magicVuln2} HPPenalty={hpPenalty}]");
        return lethal;
    }

    public void ResolveOmegaAttack(SimEnemy? omega, uint actionId)
    {
        if (omega == null) return;
        if (actionId == ActionId.SuperliminalSteel)
        {
            foreach (var hit in World.Party.Find.OutsideRect(omega.Placement().MoveForward(-10f), Geometry.SuperliminalSteelSafeHalfWidth, Geometry.OmegaFAttackHalfLength))
            {
                Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Superliminal Steel (lethal)");
                hit.Die("Superliminal Steel");
            }
        }
        else if (actionId == ActionId.OptimizedBlizzardIII)
        {
            foreach (var hit in World.Party.Find.InsideCross(omega.Placement(), Geometry.OptimizedBlizzardArmHalfWidth, Geometry.OmegaFAttackHalfLength))
            {
                Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Optimized Blizzard III (lethal)");
                hit.Die("Optimized Blizzard III");
            }
        } 
        else if (actionId == ActionId.BeyondStrength)
        {
            foreach (var hit in World.Party.Find.OutsideCircle(omega.Position, Geometry.BeyondStrengthSafeRadius))
            {
                Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Beyond Strength (lethal)");
                hit.Die("Beyond Strength");
            }
        }
        else if (actionId == ActionId.EfficientBladework)
        {
            foreach (var hit in World.Party.Find.InsideActionAoe(actionId, omega.Placement()))
            {
                Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Efficient Bladework (lethal)");
                hit.Die("Efficient Bladework");
            }
        }
        else
        {
            Plugin.Log.Warning($"Unknown omega: {actionId}");
        }
    }
    
    public HelloWorldSolver HelloWorld(PartyRole partyRole, bool near)
    {
        return new HelloWorldSolver(this, near, World.Party.Get(partyRole));
    }
    

    public class HelloWorldSolver
    {
        private readonly SimParty party;
        private readonly TopUtils utils;
        private readonly bool near;
        private SimCharacter? currentTarget;
        private bool first;

        public HelloWorldSolver(TopUtils utils, bool near, SimCharacter? currentTarget)
        {
            this.utils = utils;
            this.near = near;
            this.currentTarget = currentTarget;
            party = utils.World.Party;
            first = true;
        }

        public Vector3? Position => currentTarget?.Position;

        public void SetPosition(SimEnemy? helper)
        {
            if (currentTarget == null || helper == null) return; 
            helper.SetPosition(currentTarget.Position);
        }
        
        public void CastSpell(SimEnemy? helper)
        {
            if (helper == null || currentTarget == null) return;
            if (first)
            {
                var actionId = near ? ActionId.HelloNearWorld : ActionId.HelloDistantWorld;
                helper.Cast(actionId, targetId: currentTarget.GameObjectId);
                ResolveDamage(currentTarget.Position, actionId);
                first = false;
            }
            else
            {
               var nextTarget =  near 
                         ? party.Find.Closest(currentTarget.Position, currentTarget)
                         : party.Find.Farest(currentTarget.Position, currentTarget);
               if (nextTarget == null)
               {
                   utils.HelloWorldFail(currentTarget.Position);
                   return;
               }
               var actionId = near ? ActionId.HelloNearWorldJump : ActionId.HelloDistantWorldJump;
               helper.Cast(actionId, targetId: nextTarget.GameObjectId);
               ResolveDamage(nextTarget.Position, actionId);
               currentTarget = nextTarget;
            }
        }
        
        private void ResolveDamage(Vector3 pos, uint actionId)
        {
            var inAoe = party.Find.InsideActionAoe(actionId, new Placement(pos, 0f));
            if (inAoe.Count != 1)
            {
                Plugin.Log.Info($"Hit: ALL PARTY by Hello World fail (lethal raidwide {actionId} {pos}, soakers={inAoe.Count})");
                utils.HelloWorldFail(pos);
                return;
            }
            var soaker = inAoe[0];
            if (utils.IsDamageLethal(soaker, ruin: false))
            {
                Plugin.Log.Info($"Hit: {(soaker as ISimPartyMember)?.Role} by Hello World (lethal) → raidwide fail");
                utils.HelloWorldFail(pos);
                return;
            }
            if (soaker.FindStatus(StatusId.QuickeningDynamis) is { Stacks: >= 3})
            {
                utils.HelloWorldFail(pos);
                return;
            }
            Plugin.Log.Info($"Hit: {(soaker as ISimPartyMember)?.Role} by Hello World (non-lethal soak)");
            soaker.AddStatus(StatusId.QuickeningDynamis, stacks: 1);
            soaker.AddStatus(StatusId.MagicVulnerabilityUp, 4.96f);
        }
    }

    public void ResolveOpticalLaser(SimEnemy? opticalUnit)
    {
        if (opticalUnit is not { IsActive: true }) return;
        // The canonical eye-laser beam is duty-scripted scenery we can't reproduce;
        // flash a synthetic rectangle omen over the lethal zone so the AOE is visible.
        // Same placement + (halfWidth, 1, length) scale InsideRect uses, so it overlaps 1:1.
        World.SpawnOmen(
            "vfx/omen/eff/general02f.avfx",
            new Placement(new (0, 0, 0), opticalUnit.Rotation)
                .MoveForward(-20),
            new Vector3(Geometry.OpticalLaserHalfWidth, 1f, 40),
            durationSeconds: 1f);
        
        // World.SpawnOmen(
        //     "vfx/omen/eff/general02f.avfx",
        //     opticalUnit.Placement(),
        //     new Vector3(Geometry.OpticalLaserHalfWidth, 1f, Geometry.OpticalLaserLength),
        //     durationSeconds: 1.0f);
        foreach (var hit in World.Party.Find.InsideRect(opticalUnit.Placement(), Geometry.OpticalLaserHalfWidth, Geometry.OpticalLaserLength))
        {
            Plugin.Log.Info($"Hit: {(hit as ISimPartyMember)?.Role} by Optical Laser (lethal)");
            hit.Die("Optical Laser");
        }
    }
}
