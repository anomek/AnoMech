using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

// The chain breaks, and Hello World Fail wipes the party, when its holder dies with the puddle still
// on them or a hit kills its target before the last jump. A hit on more than one player kills all of
// them. Every cast comes from a helper acting for `caster` (Omega-M). Forwarded from each scenario's
// hooks.
public sealed class HelloWorldFailures(SimWorld world, Func<SimEnemy?> caster) : IWorldEventListener
{
    private readonly List<HelloWorld> chains = [];

    public HelloWorld Chain(PartyRole holder, bool near)
    {
        var chain = new HelloWorld(world.Party, holder, near);
        chains.Add(chain);
        return chain;
    }

    public void OnStatusLost(StatusLost lost)
    {
        if (lost is not { StatusId: StatusId.HelloNearWorld or StatusId.HelloDistantWorld, Reason: StatusLossReason.Died }) return;
        caster()?.CastFromHelper(TopActions.HelloWorldFail, at: lost.Holder.Placement());
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        if (!IsChainHit(resolved.Action.ActionId) || resolved.Killed.Count == 0) return;
        if (!chains.Any(c => c.JumpsAfter(resolved))) return;
        caster()?.CastFromHelper(TopActions.HelloWorldFail, at: resolved.Origin);
    }

    // UNVERIFIED: no pull overloads Dynamis, so what retail does then (Doom, as for Come Ruin?) is unknown.
    public void OnStatusAdded(StatusAdded added)
    {
        if (added is not { StatusId: StatusId.QuickeningDynamis, PreviousStacks: >= MaxDynamisStacks, Action: { } action }
            || !IsChainHit(action.ActionId)) return;
        caster()?.CastFromHelper(TopActions.HelloWorldFail, at: added.Holder.Placement());
    }

    private const int MaxDynamisStacks = 3;

    private static bool IsChainHit(uint actionId)
        => actionId is ActionId.HelloNearWorld or ActionId.HelloNearWorldJump or ActionId.HelloDistantWorld or ActionId.HelloDistantWorldJump;
}
