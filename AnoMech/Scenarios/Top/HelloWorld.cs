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

    // A holder dying with the puddle still on them fails it.
    public static void CheckHolderDeaths(SimWorld world)
    {
        foreach (var member in world.Party.AllMembers())
        {
            if (member.IsAlive()) continue;
            var status = member.HasStatus(StatusId.HelloNearWorld) ? StatusId.HelloNearWorld
                : member.HasStatus(StatusId.HelloDistantWorld) ? StatusId.HelloDistantWorld
                : (ushort)0;
            if (status == 0) continue;
            member.RemoveStatus(status);
            var helper = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.OmegaHelper, Visibility: SpawnVisibility.InvisibleHelper,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Placement: new Placement(member.Position, 0f)));
            if (helper == null) continue;
            world.Events.Add(Duration.MonitorHelperLifetime, helper.Despawn);
            helper.Cast(TopActions.HelloWorldFail);
        }
    }
}
