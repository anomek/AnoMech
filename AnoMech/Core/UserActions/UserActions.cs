using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions.Jobs;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.UserActions;

// Optional, self-contained module: resolves the local player's own actions
// client-side, filling in the server responses the sim firewall blocks. Sprint and limit
// breaks are resolved unconditionally, since mechanics are built around the limit breaks;
// everything else only while Enabled.
public sealed unsafe class UserActions : IUserActions, IDisposable
{
    private readonly LocalPlayerInputHooks hooks;
    private readonly IUserActionHandler sprint = new SprintHandler();

    // Cast-time enablers. Runs when the cast BEGINS: enabler bookkeeping (Swiftcast/Dualcast/…) is
    // tied to starting the cast, and it does its own cast-completion tracking for the Dualcast grant.
    private readonly IUserActionHandler castTime = new CastTimeHandler();

    // Runs after every other handler's OnScenarioStart so a handler's own reset can't undo the seed.
    private readonly IUserActionHandler startingResources = new StartingResourcesHandler();

    // The spell's own effects (gauge writes, status grants/clears, combo advancement). Applied at cast
    // RESOLUTION — immediately for an instant cast, on completion for a hard cast, and not at all if the
    // cast is interrupted (a spell resolves when its cast bar finishes, not when it starts). In order:
    // JobActionHandler and the state handlers precede ComboHandler so their combo-gated logic reads the
    // pre-advance combo state.
    private readonly List<IUserActionHandler> effectHandlers =
    [
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
        new ComboHandler(),
    ];

    // Per-frame-only handlers (no cast-sensitive OnAction effect).
    private readonly List<IUserActionHandler> tickHandlers =
    [
        new TimedGaugeHandler(),
        new CastInterruptHandler(),
        new AutoAttackHandler(),
    ];

    private readonly List<IUserActionHandler> alwaysHandlers = [new JobActionHandler(), new LimitBreakHandler()];
    private readonly IUserActionHandler castInterrupt;

    // A hard cast whose effects are deferred until it completes.
    private bool pendingResolve;
    private ActionType pendingType;
    private uint pendingAction;
    private ulong pendingTarget;
    private float pendingTotal, pendingMax, pendingWatched;
    private List<IUserActionHandler> pendingHandlers = [];

    // Dedup gate: LastUsedActionSequence advances only at an action's actual execution,
    // so this skips both a queued (spammed) press carrying the PRIOR action's sequence
    // and a handler's own re-entrant fire (ActionEffect Receive re-enters the hook).
    private ushort processedSeq;

    // The job gauge is the one player resource the handlers write straight to native memory
    // instead of through the tick-managed status list — so, unlike statuses (cleared on the
    // SimPlayer's Despawn), it survives a scenario teardown and would leave the player's real
    // gauge in the simulated state after they leave. We snapshot it once when the sim zone
    // first loads and write it back when the session ends. Layout — FFXIVClientStructs
    // JobGaugeManager (size 0x60): gauge union at 0x08, ClassJobId at 0x58 — so the gauge data
    // is the 0x50-byte region [0x08, 0x58); CurrentGauge (0x00) is a pointer we must not touch.
    private const int GaugeUnionOffset = 0x08;
    private const int GaugeUnionSize = 0x50;
    private readonly byte[] gaugeSnapshot = new byte[GaugeUnionSize];
    private bool hasGaugeSnapshot;
    private byte snapshotJob;

    public bool Enabled { get; private set; }

    // The module only acts while the sim zone is loaded — never in a real duty / open
    // world, where the client is server-connected and our synthetic packets/writes
    // would be harmful.
    private static bool SimActive => Plugin.GameInstance?.World.Map.IsInInstance ?? false;

    public UserActions(LocalPlayerInputHooks hooks)
    {
        this.hooks = hooks;
        castInterrupt = tickHandlers.OfType<CastInterruptHandler>().First();
        hooks.ActionExecuted += OnActionExecuted;
    }

    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;

    // Called by Game at scenario start.
    public void OnScenarioStart()
    {
        sprint.OnScenarioStart();
        pendingResolve = false;
        if (!Enabled) return;
        castTime.OnScenarioStart();
        foreach (var handler in effectHandlers) handler.OnScenarioStart();
        foreach (var handler in tickHandlers) handler.OnScenarioStart();
        startingResources.OnScenarioStart();
    }

    // Called by Game when the sim zone first loads (a true session start, not a restart within
    // an already-loaded zone). Captures the player's pristine job gauge for OnSessionEnd to
    // restore. Snapshots unconditionally — cheap, and it keeps the restore correct even if the
    // module is enabled/disabled part-way through the session.
    public void OnSessionStart()
    {
        var jgm = JobGaugeManager.Instance();
        if (jgm == null) return;
        snapshotJob = jgm->ClassJobId;
        fixed (byte* dst = gaugeSnapshot)
            Buffer.MemoryCopy((byte*)jgm + GaugeUnionOffset, dst, GaugeUnionSize, GaugeUnionSize);
        hasGaugeSnapshot = true;
    }

    // Called by Game when the player leaves the sim zone. Writes back the gauge captured at
    // session start, undoing every client-side gauge write the run made. Skips the write if the
    // player changed jobs mid-session — the snapshot bytes would decode as a different gauge.
    public void OnSessionEnd()
    {
        if (!hasGaugeSnapshot) return;
        hasGaugeSnapshot = false;
        var jgm = JobGaugeManager.Instance();
        if (jgm == null || jgm->ClassJobId != snapshotJob) return;
        fixed (byte* src = gaugeSnapshot)
            Buffer.MemoryCopy(src, (byte*)jgm + GaugeUnionOffset, GaugeUnionSize, GaugeUnionSize);
    }

    // Called each frame by Plugin.OnFrameworkUpdate.
    public void Tick(float deltaSeconds)
    {
        if (!SimActive) return;
        sprint.OnTick(deltaSeconds);
        if (!Enabled)
        {
            // A limit break's cast still resolves, and still breaks on movement.
            if (pendingResolve) castInterrupt.OnTick(deltaSeconds);
            else hooks.PollCancelCast();
            ResolvePendingCast(deltaSeconds);
            return;
        }
        castTime.OnTick(deltaSeconds);
        foreach (var handler in effectHandlers) handler.OnTick(deltaSeconds);
        foreach (var handler in tickHandlers) handler.OnTick(deltaSeconds);
        ResolvePendingCast(deltaSeconds);
    }

    private void OnActionExecuted(ActionType actionType, uint actionId, ulong targetId)
    {
        if (!SimActive) return;
        var am = ActionManager.Instance();
        if (am == null) return;
        var seq = am->LastUsedActionSequence;
        if (seq == processedSeq) return; // queued press / re-entrant fire — already handled
        processedSeq = seq;

        sprint.OnAction(actionType, actionId);
        var handlers = Enabled ? effectHandlers : IsAlwaysResolved(actionType, actionId) ? alwaysHandlers : null;
        if (handlers == null) return;

        // Enabler bookkeeping happens as the cast begins.
        if (Enabled) castTime.OnAction(actionType, actionId);

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
            pendingWatched = 0f;
            pendingHandlers = handlers;
        }
        else
        {
            foreach (var handler in handlers) handler.OnAction(actionType, actionId, targetId);
        }
    }

    private static bool IsAlwaysResolved(ActionType actionType, uint actionId)
        => actionType == ActionType.Action && LimitBreakHandler.IsLimitBreak(actionId);

    // Fires the deferred hard-cast effects once the cast completes; drops them if it was interrupted.
    private void ResolvePendingCast(float deltaSeconds)
    {
        if (!pendingResolve) return;
        var bc = (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0);
        if (bc == null) { pendingResolve = false; return; }

        if (bc->CastInfo.IsCasting && bc->CastInfo.ActionId == pendingAction)
        {
            if (bc->CastInfo.CurrentCastTime > pendingMax) pendingMax = bc->CastInfo.CurrentCastTime;
            pendingWatched += deltaSeconds;
            if (pendingWatched <= pendingTotal + CastInterruptHandler.OverstaySeconds) return;
            pendingMax = pendingTotal;
        }

        // Cast ended: apply only if it reached the slidecast window, which an interrupt never does.
        if (pendingMax >= pendingTotal - Plugin.Config.CastInterruptThreshold)
        {
            var am = ActionManager.Instance();
            if (am != null) processedSeq = am->LastUsedActionSequence; // dedup any re-entrant combo fire during dispatch
            foreach (var handler in pendingHandlers) handler.OnAction(pendingType, pendingAction, pendingTarget);
        }
        pendingResolve = false;
        pendingMax = 0;
    }

    public void Dispose() => hooks.ActionExecuted -= OnActionExecuted;
}
