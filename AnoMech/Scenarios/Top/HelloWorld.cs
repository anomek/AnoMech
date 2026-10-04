using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

// One Hello World puddle chain: the first hit lands on the starting holder, each jump on the party
// member closest to (near) or farthest from (distant) the last one hit.
public sealed class HelloWorld(SimParty party, PartyRole holder, bool near)
{
    private SimCharacter? currentTarget = party.Get(holder);
    private bool first = true;

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
            first = false;
            helper.Cast(near ? TopActions.HelloNearWorld : TopActions.HelloDistantWorld, currentTarget);
            return;
        }
        var nextTarget = near
            ? party.Find.Closest(currentTarget.Position, currentTarget)
            : party.Find.Farest(currentTarget.Position, currentTarget);
        if (nextTarget == null)
        {
            helper.Cast(TopActions.HelloWorldFail);
            return;
        }
        helper.Cast(near ? TopActions.HelloNearWorldJump : TopActions.HelloDistantWorldJump, nextTarget);
        currentTarget = nextTarget;
    }

    // A holder dying with the puddle still on them fails it, cast by one of `helpers` when given.
    public static void CheckHolderDeaths(SimWorld world, TopHelpers? helpers = null)
    {
        foreach (var member in world.Party.AllMembers())
        {
            if (member.IsAlive()) continue;
            var status = member.HasStatus(StatusId.HelloNearWorld) ? StatusId.HelloNearWorld
                : member.HasStatus(StatusId.HelloDistantWorld) ? StatusId.HelloDistantWorld
                : (ushort)0;
            if (status == 0) continue;
            member.RemoveStatus(status);
            var placement = new Placement(member.Position, 0f);
            (helpers?.Next(placement) ?? SpawnHelper(world, placement))?.Cast(TopActions.HelloWorldFail);
        }
    }

    // Hello World leaves everyone an Underflow and a Performance Debugger, and four of them a
    // Synchronization Debugger (its second and last stack holders), until Blue Screen.
    public static void ApplyDebuggers(SimParty party, Rng rng)
    {
        var synchronization = rng.Shuffle(PerRole.All.ToArray()).Take(4).ToHashSet();
        foreach (var role in PerRole.All)
        {
            if (party.Get(role) is not { } member) continue;
            member.AddStatus(StatusId.HWImmuneRedRot);
            member.AddStatus(StatusId.HWImmuneBlueRot);
            if (synchronization.Contains(role)) member.AddStatus(StatusId.HWImmuneStack);
        }
    }

    public static void RemoveDebuggers(SimParty party)
    {
        foreach (var member in party.AllMembers())
        {
            member.RemoveStatus(StatusId.HWImmuneRedRot);
            member.RemoveStatus(StatusId.HWImmuneBlueRot);
            member.RemoveStatus(StatusId.HWImmuneStack);
        }
    }

    private static SimEnemy? SpawnHelper(SimWorld world, Placement placement)
    {
        var helper = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaHelper,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Placement: placement));
        if (helper != null) world.Events.Add(Duration.MonitorHelperLifetime, helper.Despawn);
        return helper;
    }
}
