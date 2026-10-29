using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Native.Implementations;

// Hooks the native action and movement input paths to observe the local player's input and
// steer them.
//
// Signatures and detour shapes lifted from FFXIV-RaidsRewritten's PlayerMovementOverride.cs /
// ActionManagerEx.cs (which credit awgil's vnavmesh + bossmod).
public sealed unsafe class LocalPlayerInputHooks : ILocalPlayerInput
{
    public event Action<ActionType, uint, ulong>? ActionExecuted;

    // --- Player activity signals (read by SimPlayer to drive Party.Player.IsMoving/IsActing) ---
    // The engine's own per-frame movement sample (the signal bossmod reads): input intent, not a
    // position delta.
    public bool MovementInputActive { get; private set; }

    public bool IsAutoAttacking => UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;

    // State poll: a jump is always self-initiated.
    public bool IsJumping => Plugin.Condition[ConditionFlag.Jumping];

    // Latched on a real action press; drained once per frame by SimPlayer.
    private bool actionUsedSincePoll;
    public bool PollActionUsed()
    {
        var used = actionUsedSincePoll;
        actionUsedSincePoll = false;
        return used;
    }

    // Debug aid for pinning down a real action id; DebugMenu shows these.
    private const int RecentActionsCapacity = 50;
    private readonly Queue<(uint ActionId, ActionType Type)> recentActions = new();
    public IReadOnlyCollection<(uint ActionId, ActionType Type)> RecentActions => recentActions;

    private void RecordRecentAction(uint actionId, ActionType type)
    {
        // GeneralAction 1 is the auto-attack engage/re-engage action -- fires constantly, pure noise here.
        if (type == ActionType.GeneralAction && actionId == 1) return;
        ZoneControl.NoteActionPressed(type, actionId);
        recentActions.Enqueue((actionId, type));
        while (recentActions.Count > RecentActionsCapacity) recentActions.Dequeue();
        var jobId = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId;
        Core.DiagnosticLog.Info($"[LocalPlayerInputHooks] Action pressed: {actionId} ({type}) -- {Natives.Data.ActionName(actionId)} (job={jobId}).");
    }

    // Edge-triggered gain/loss logging; reads the local player directly so it works with no
    // scenario running.
    // Counted, not just present: the same id can be held twice.
    private readonly Dictionary<ushort, int> lastLoggedStatusCounts = new();

    private void ScanAndLogActiveStatuses()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null) return;
        var bc = (BattleChara*)localPlayer.Address;
        if (bc == null) return;
        var jobId = localPlayer.ClassJob.RowId;
        var current = new Dictionary<ushort, (int Count, float Remaining)>();
        foreach (var status in bc->StatusManager.Status)
        {
            if (status.StatusId == 0) continue;
            var seen = current.GetValueOrDefault(status.StatusId);
            current[status.StatusId] = (seen.Count + 1, status.RemainingTime);
        }
        foreach (var (gained, (count, remaining)) in current)
            if (count > lastLoggedStatusCounts.GetValueOrDefault(gained))
                Core.DiagnosticLog.Info($"[LocalPlayerInputHooks] Status gained: {gained} -- {Natives.Data.StatusName(gained)} (job={jobId}, duration={remaining:F1}, x{count}).");
        foreach (var lost in lastLoggedStatusCounts.Keys.Except(current.Keys))
            Core.DiagnosticLog.Info($"[LocalPlayerInputHooks] Status lost: {lost} -- {Natives.Data.StatusName(lost)} (job={jobId}).");
        lastLoggedStatusCounts.Clear();
        foreach (var (id, (count, _)) in current) lastLoggedStatusCounts[id] = count;
    }

    // Latched whenever the game calls Hotbar.CancelCast — i.e. the player requested a
    // cast cancel (ESC / the cancel-cast keybind, however it's bound; also cancels
    // auto-attack). This is the outgoing intent the server would act on; the sim
    // firewall eats that round-trip, so UserActions drains this each frame and
    // synthesizes the interrupt itself.
    private bool cancelCastRequested;
    public bool PollCancelCast()
    {
        var requested = cancelCastRequested;
        cancelCastRequested = false;
        return requested;
    }

    private delegate void RMIWalkDelegate(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk);
    [Signature("E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D")]
    private Hook<RMIWalkDelegate> rmiWalkHook = null!;

    private readonly Hook<ActionManager.Delegates.Update> updateHook;
    private readonly Hook<ActionManager.Delegates.UseAction> useActionHook;
    private readonly Hook<ActionManager.Delegates.UseActionLocation> useActionLocationHook;
    private readonly Hook<Hotbar.Delegates.CancelCast> cancelCastHook;
    private readonly Hook<ActionManager.Delegates.GetAdjustedActionId> adjustedActionIdHook;
    private readonly Hook<ActionManager.Delegates.GetAdjustedCastTime> adjustedCastTimeHook;
    private readonly Hook<ActionManager.Delegates.GetActionStatus> actionStatusHook;

    public Func<uint, uint?>? AdjustActionId { get; set; }
    public Func<ActionType, uint, int?>? AdjustCastTime { get; set; }
    public Func<ActionType, uint, bool, uint?>? AdjustActionStatus { get; set; }

    public LocalPlayerInputHooks(IGameInteropProvider hook)
    {
        hook.InitializeFromAttributes(this);

        updateHook = hook.HookFromAddress<ActionManager.Delegates.Update>(
            ActionManager.Addresses.Update.Value, UpdateDetour);
        useActionHook = hook.HookFromAddress<ActionManager.Delegates.UseAction>(
            ActionManager.Addresses.UseAction.Value, UseActionDetour);
        useActionLocationHook = hook.HookFromAddress<ActionManager.Delegates.UseActionLocation>(
            ActionManager.Addresses.UseActionLocation.Value, UseActionLocationDetour);
        cancelCastHook = hook.HookFromAddress<Hotbar.Delegates.CancelCast>(
            Hotbar.Addresses.CancelCast.Value, CancelCastDetour);
        adjustedActionIdHook = hook.HookFromAddress<ActionManager.Delegates.GetAdjustedActionId>(
            ActionManager.Addresses.GetAdjustedActionId.Value, AdjustedActionIdDetour);
        adjustedCastTimeHook = hook.HookFromAddress<ActionManager.Delegates.GetAdjustedCastTime>(
            ActionManager.Addresses.GetAdjustedCastTime.Value, AdjustedCastTimeDetour);
        actionStatusHook = hook.HookFromAddress<ActionManager.Delegates.GetActionStatus>(
            ActionManager.Addresses.GetActionStatus.Value, ActionStatusDetour);

        rmiWalkHook.Enable();
        updateHook.Enable();
        useActionHook.Enable();
        useActionLocationHook.Enable();
        cancelCastHook.Enable();
        adjustedActionIdHook.Enable();
        adjustedCastTimeHook.Enable();
        actionStatusHook.Enable();
    }

    public void Dispose()
    {
        rmiWalkHook?.Dispose();
        updateHook?.Dispose();
        useActionHook?.Dispose();
        useActionLocationHook?.Dispose();
        cancelCastHook?.Dispose();
        adjustedActionIdHook?.Dispose();
        adjustedCastTimeHook?.Dispose();
        actionStatusHook?.Dispose();
    }

    // The three below run every frame for every hotbar slot: an override that throws must not take the
    // game down.
    private uint AdjustedActionIdDetour(ActionManager* self, uint actionId)
    {
        var native = adjustedActionIdHook.Original(self, actionId);
        try
        {
            return AdjustActionId?.Invoke(actionId) ?? native;
        }
        catch (Exception e)
        {
            Plugin.Log.Error($"[LocalPlayerInputHooks] AdjustActionId: {e.Message}");
            return native;
        }
    }

    private int AdjustedCastTimeDetour(ActionType actionType, uint actionId, bool applyProcs, ActionManager.CastTimeProc* outOptProc)
    {
        var native = adjustedCastTimeHook.Original(actionType, actionId, applyProcs, outOptProc);
        try
        {
            return AdjustCastTime?.Invoke(actionType, actionId) ?? native;
        }
        catch (Exception e)
        {
            Plugin.Log.Error($"[LocalPlayerInputHooks] AdjustCastTime: {e.Message}");
            return native;
        }
    }

    private uint ActionStatusDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, bool checkRecastActive, bool checkCastingActive, uint* outOptExtraInfo)
    {
        var native = actionStatusHook.Original(self, actionType, actionId, targetId, checkRecastActive, checkCastingActive, outOptExtraInfo);
        if (native == 0) return native;
        try
        {
            return AdjustActionStatus?.Invoke(actionType, actionId, checkRecastActive) ?? native;
        }
        catch (Exception e)
        {
            Plugin.Log.Error($"[LocalPlayerInputHooks] AdjustActionStatus: {e.Message}");
            return native;
        }
    }

    // The player asked to cancel their cast; latch it and let the original run (its
    // outgoing packet is firewalled in the sim, so it has no visible effect here).
    private void CancelCastDetour(Hotbar* thisPtr)
    {
        cancelCastRequested = true;
        cancelCastHook.Original(thisPtr);
    }

    private Vector2? steerDirection;

    public void Steer(Vector2? direction, float speed)
        => steerDirection = direction is { } d && d.LengthSquared() > 0f ? Vector2.Normalize(d) : null;
    private void RMIWalkDetour(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk)
    {
        rmiWalkHook.Original(self, sumLeft, sumForward, sumTurnLeft, haveBackwardOrStrafe, a6, bAdditiveUnk);
        // self is a MoveControllerSubMemberForMine*; the sums are its move vector.
        MovementInputActive = *sumLeft != 0 || *sumForward != 0;
        // bAdditiveUnk != 0 is the game's second, additive sample; bossmod only steers the first.
        if (steerDirection is { } dir && bAdditiveUnk == 0 && SteerReference() is { } reference)
        {
            var relative = MathF.Atan2(dir.X, dir.Y) - reference;
            *sumLeft = MathF.Sin(relative);
            *sumForward = MathF.Cos(relative);
            *haveBackwardOrStrafe = 0;
        }
    }

    // The angle the move vector is relative to (bossmod's MovementOverride): the character's
    // facing in standard mode, the camera's back in legacy mode. Null with no player or camera.
    private static float? SteerReference()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return null;
        var legacy = Plugin.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 1;
        if (!legacy) return player.Rotation;
        var camera = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->GetActiveCamera();
        var render = camera != null ? camera->SceneCamera.RenderCamera : null;
        if (render == null) return null;
        var view = render->ViewMatrix;
        return MathF.Atan2(view.M13, view.M33) + MathF.PI;
    }

    private void UpdateDetour(ActionManager* self)
    {
        updateHook.Original(self);
        ScanAndLogActiveStatuses();
    }

    private bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        RecordRecentAction(actionId, actionType);
        // The hotbar presses the button's own id; what runs is what the button is adjusted to, which
        // the action itself can change (enchanted melee spending the mana its swap depends on).
        var ran = actionType == ActionType.Action ? self->GetAdjustedActionId(actionId) : actionId;
        var sequence = self->LastUsedActionSequence;
        var result = useActionHook.Original(self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        if (result)
        {
            actionUsedSincePoll = true;
            // A press during the GCD succeeds by queueing; it runs later, through here again. Reporting
            // it now would apply its effects before it runs and can leave it unusable when it does.
            if (!Queued(self, actionId, ran, sequence)) ActionExecuted?.Invoke(actionType, ran, targetId);
        }
        return result;
    }

    private static bool Queued(ActionManager* self, uint actionId, uint ran, uint sequence)
        => self->ActionQueued && (self->QueuedActionId == actionId || self->QueuedActionId == ran)
           && self->LastUsedActionSequence == sequence;

    private bool UseActionLocationDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, Vector3* location, uint extraParam, byte a7)
    {
        var ran = actionType == ActionType.Action ? self->GetAdjustedActionId(actionId) : actionId;
        var result = useActionLocationHook.Original(self, actionType, actionId, targetId, location, extraParam, a7);
        if (result)
        {
            actionUsedSincePoll = true;
            ActionExecuted?.Invoke(actionType, ran, targetId);
        }
        return result;
    }
}
