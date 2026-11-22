using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;

namespace AnoMech.Core.SimObjects;

// The zone's pooled invisible helpers (SimWorld.SpawnHelperPool), each cast by one taken for it. On
// the helper, in the cast's frame, retail sends: an unlink if it is still linked, the name the action
// is shown under, the link to the actor it acts for, the teleport, the cast. Ending a cast sends
// nothing; a linked helper is unlinked on its own once that actor dies or leaves, or its cast is
// cancelled. The helpers themselves are ordinary world children, so the world's teardown covers them.
internal sealed class SimHelperPool : ISimObject
{
    // Retail reuses a helper this long before its animation lock ends: one helper repeats Akh Rhai
    // (0.6s lock) every ~0.55s and the exalines (2.1s lock) every 2.05s.
    private const float EarlyRelease = 0.1f;

    // In spawn order: retail takes the highest-index free one.
    private readonly List<SimEnemy> helpers;
    private readonly Dictionary<SimEnemy, SimEnemy> linkedTo = new();

    internal SimHelperPool(List<SimEnemy> helpers) => this.helpers = helpers;

    public bool IsActive => true;

    // False when every helper is still busy with an earlier cast; nothing is sent then.
    public bool Cast(SimEnemy parent, EnemyAction action, CastTarget target, Placement at, byte animationVariation)
    {
        if (Free() is not { } helper) return false;
        Unlink(helper);
        helper.ActorControl.SetName(action.ShownAs ?? parent.HelperName ?? parent.SpawnConfig.NameId);
        if (parent.IsAlive() && parent.IsActive && parent.EntityId != 0)
        {
            helper.ActorControl.SetOwner(parent.EntityId);
            linkedTo[helper] = parent;
        }
        helper.SetPosition(at);
        helper.Cast(action, target, animationVariation);
        return true;
    }

    // Free once its last cast's bar and ActionEffect are done and its animation lock nearly is.
    private SimEnemy? Free()
    {
        for (var i = helpers.Count - 1; i >= 0; i--)
            if (helpers[i] is { IsActive: true } helper && helper.BusyFor <= EarlyRelease)
                return helper;
        return null;
    }

    public void Unlink(SimEnemy helper)
    {
        if (linkedTo.Remove(helper)) helper.ActorControl.ClearOwner();
    }

    public void Tick(float deltaSeconds)
    {
        foreach (var (helper, parent) in linkedTo.ToList())
            if (parent.Dead || !parent.IsActive)
                Unlink(helper);
    }

    public void Despawn()
    {
        linkedTo.Clear();
        helpers.Clear();
    }
}
