using System.Linq;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

// One Hello World puddle chain: the first hit lands on the starting holder, each jump on the party
// member closest to (near) or farthest from (distant) the last one hit, cast by a helper standing on
// the last one hit.
public sealed class HelloWorld(SimParty party, PartyRole holder, bool near)
{
    private const int ChainHits = 3;

    private SimCharacter? currentTarget = party.Get(holder);
    private EnemyAction? lastAction;
    private int hits;

    // The resolve is this chain's latest hit and another one follows it.
    public bool JumpsAfter(ActionResolved resolved)
        => resolved.Action == lastAction && currentTarget is not null && resolved.Hits.Contains(currentTarget) && hits < ChainHits;

    public void CastSpell(SimEnemy? caster)
    {
        if (caster == null || currentTarget == null) return;
        var from = currentTarget.Placement();
        if (hits == 0)
        {
            Hit(caster, near ? TopActions.HelloNearWorld : TopActions.HelloDistantWorld, currentTarget, from);
            return;
        }
        var nextTarget = near
            ? party.Find.Closest(currentTarget.Position, currentTarget)
            : party.Find.Farest(currentTarget.Position, currentTarget);
        if (nextTarget == null)
        {
            caster.CastFromHelper(TopActions.HelloWorldFail, at: from);
            return;
        }
        Hit(caster, near ? TopActions.HelloNearWorldJump : TopActions.HelloDistantWorldJump, nextTarget, from);
    }

    private void Hit(SimEnemy caster, EnemyAction action, SimCharacter target, Placement from)
    {
        caster.CastFromHelper(action, target, at: from);
        currentTarget = target;
        lastAction = action;
        hits++;
    }
}
