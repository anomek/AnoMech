using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Implementations.Pointers;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions.Jobs;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.UserActions;

// Optional, self-contained module: resolves the local player's own actions
// client-side, filling in the server responses the sim firewall blocks. Handlers run only
// while Enabled, unless they declare RunsWhenDisabled. Nothing in the engine depends on it.
public sealed unsafe class UserActions : IUserActions
{
    private readonly ILocalPlayerInput hooks;

    // OnAction is the action's RESOLUTION — immediately for an instant cast, on completion for a hard
    // cast, and not at all if the cast is interrupted (a spell resolves when its cast bar finishes, not
    // when it starts). In order: ResetHandler first so every other handler's OnScenarioStart lands on
    // the reset; CastTimeHandler before JobActionHandler, which clears the statuses it checks; and
    // JobActionHandler and the state handlers before ActionEffectHandler so their combo-gated logic reads the
    // pre-advance combo state.
    private readonly List<IUserActionHandler> handlers =
    [
        new ResetHandler(),
        new SprintHandler(),
        new CastTimeHandler(),
        new JobActionHandler(),
        new LimitBreakHandler(),
        new SamuraiStateHandler(),
        new BlackMageStateHandler(),
        new SummonerStateHandler(),
        new AstrologianStateHandler(),
        new MonkStateHandler(),
        new PictomancerStateHandler(),
        new DancerStateHandler(),
        new ViperStateHandler(),
        new NinjaStateHandler(),
        new BardStateHandler(),
        new ActionEffectHandler(),
        new TimedGaugeHandler(),
        new CastInterruptHandler(),
        new AutoAttackHandler(),
    ];

    // A hard cast whose effects are deferred until it completes.
    private bool pendingResolve;
    private ActionType pendingType;
    private uint pendingAction;
    private ulong pendingTarget;
    private float pendingTotal, pendingMax;

    // Dedup gate: LastUsedActionSequence advances only at an action's actual execution,
    // so this skips both a queued (spammed) press carrying the PRIOR action's sequence
    // and a handler's own re-entrant fire (ActionEffect Receive re-enters the hook).
    private ushort processedSeq;

    // Unlike statuses (cleared on the SimPlayer's Despawn), the job gauge survives a scenario
    // teardown and would leave the player's real gauge in the simulated state after they leave,
    // so the one the sim zone loaded with is sent back when the session ends.
    private ActorGaugePacket? gaugeSnapshot;

    public bool Enabled { get; private set; }

    public event Action<PlayerAction>? Acted;

    // The module only acts while the sim zone is loaded — never in a real duty / open
    // world, where the client is server-connected and our synthetic packets/writes
    // would be harmful.
    private static bool SimActive => Plugin.Game?.World.Map.IsInInstance ?? false;

    public UserActions(ILocalPlayerInput hooks)
    {
        this.hooks = hooks;
        hooks.ActionExecuted += OnActionExecuted;
        hooks.AdjustActionId = AdjustActionId;
        hooks.AdjustCastTime = AdjustCastTime;
        hooks.AdjustActionStatus = AdjustActionStatus;
        // The server shows a swing to the party as it would any action.
        foreach (var autoAttacks in handlers.OfType<AutoAttackHandler>())
            autoAttacks.Swung += (actionId, targetId) => Acted?.Invoke(new PlayerAction(PlayerActionStep.Resolved, ActionType.Action, actionId, targetId));
    }

    private bool Runs(IUserActionHandler handler) => Enabled || handler.RunsWhenDisabled;

    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;

    // Called by Game at scenario start.
    public void OnScenarioStart()
    {
        pendingResolve = false;
        if (!SimActive) return;
        foreach (var handler in handlers) if (Runs(handler)) handler.OnScenarioStart();
    }

    // Called by Game when the sim zone first loads (a true session start, not a restart within
    // an already-loaded zone). Snapshots unconditionally, so the restore stays correct even if the
    // module is enabled/disabled part-way through the session.
    public void OnSessionStart()
    {
        // The lift rewinds the sequence to here, so a number the last session processed comes again.
        var am = ActionManager.Instance();
        if (am != null) processedSeq = am->LastUsedActionSequence;
        gaugeSnapshot = JobGaugeEdit.Capture();
    }

    // Called by Game when the player leaves the sim zone.
    public void OnSessionEnd()
    {
        if (gaugeSnapshot is { } snapshot) JobGaugeEdit.Restore(snapshot);
        gaugeSnapshot = null;
    }

    // Called each frame by Plugin.OnFrameworkUpdate.
    public void Tick(float deltaSeconds)
    {
        if (!SimActive) return;
        foreach (var handler in handlers) if (Runs(handler)) handler.OnTick(deltaSeconds);
        ResolvePendingCast();
    }

    private void OnActionExecuted(ActionType actionType, uint actionId, ulong targetId)
    {
        if (!SimActive) return;
        var am = ActionManager.Instance();
        if (am == null) return;
        var seq = am->LastUsedActionSequence;
        if (seq == processedSeq) return; // queued press / re-entrant fire — already handled
        processedSeq = seq;

        // Effects resolve when the cast finishes. Instant (or made instant) → now; a real cast bar
        // this action started → deferred to completion (see ResolvePendingCast). The ActionId match
        // keeps oGCDs woven during another cast from being mistaken for a hard cast.
        var bc = (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0);
        bool hardCast = bc != null && bc->CastInfo.IsCasting && bc->CastInfo.TotalCastTime > 0.1f
                        && bc->CastInfo.ActionId == actionId;
        if (hardCast)
        {
            pendingResolve = true;
            pendingType = actionType;
            pendingAction = actionId;
            pendingTarget = targetId;
            pendingTotal = bc->CastInfo.TotalCastTime;
            pendingMax = bc->CastInfo.CurrentCastTime;
            Acted?.Invoke(new PlayerAction(PlayerActionStep.CastStarted, actionType, actionId, targetId, pendingTotal));
            foreach (var handler in handlers) if (Runs(handler)) handler.OnCastStart(actionType, actionId);
        }
        else
        {
            Acted?.Invoke(new PlayerAction(PlayerActionStep.Resolved, actionType, actionId, targetId));
            foreach (var handler in handlers) if (Runs(handler)) handler.OnAction(actionType, actionId, targetId);
        }
    }

    // Fires the deferred hard-cast effects once the cast completes; drops them if it was interrupted.
    private void ResolvePendingCast()
    {
        if (!pendingResolve) return;
        var bc = (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0);
        if (bc == null) { pendingResolve = false; return; }

        if (bc->CastInfo.IsCasting && bc->CastInfo.ActionId == pendingAction)
        {
            if (bc->CastInfo.CurrentCastTime > pendingMax) pendingMax = bc->CastInfo.CurrentCastTime;
            return;
        }

        // Cast ended: apply only if it reached the slidecast window, which an interrupt never does.
        if (pendingMax >= pendingTotal - Plugin.Config.CastInterruptThreshold)
        {
            var am = ActionManager.Instance();
            if (am != null) processedSeq = am->LastUsedActionSequence; // dedup any re-entrant combo fire during dispatch
            Acted?.Invoke(new PlayerAction(PlayerActionStep.Resolved, pendingType, pendingAction, pendingTarget));
            foreach (var handler in handlers) if (Runs(handler)) handler.OnAction(pendingType, pendingAction, pendingTarget);
        }
        else
        {
            Acted?.Invoke(new PlayerAction(PlayerActionStep.Interrupted, pendingType, pendingAction, pendingTarget));
            foreach (var handler in handlers) if (Runs(handler)) handler.OnCastInterrupted(pendingType, pendingAction);
        }
        pendingResolve = false;
        pendingMax = 0;
    }

    public void Dispose()
    {
        hooks.ActionExecuted -= OnActionExecuted;
        hooks.AdjustActionId = null;
        hooks.AdjustCastTime = null;
        hooks.AdjustActionStatus = null;
    }

    // Each takes the first handler's answer, and none outside the sim.
    private uint? AdjustActionId(uint actionId)
    {
        if (!SimActive) return null;
        foreach (var handler in handlers)
            if (Runs(handler) && handler.AdjustActionId(actionId) is { } adjusted) return adjusted;
        return null;
    }

    private int? AdjustCastTime(ActionType actionType, uint actionId)
    {
        if (!SimActive) return null;
        foreach (var handler in handlers)
            if (Runs(handler) && handler.AdjustCastTime(actionType, actionId) is { } adjusted) return adjusted;
        return null;
    }

    private uint? AdjustActionStatus(ActionType actionType, uint actionId, bool checkRecast)
    {
        if (!SimActive) return null;
        foreach (var handler in handlers)
            if (Runs(handler) && handler.AdjustActionStatus(actionType, actionId, checkRecast) is { } adjusted) return adjusted;
        return null;
    }
}
