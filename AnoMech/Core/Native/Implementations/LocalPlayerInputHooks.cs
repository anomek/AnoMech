using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using AnoMech.Core.Native.Interfaces;
using NativeLimitBreakController = FFXIVClientStructs.FFXIV.Client.Game.UI.LimitBreakController;

namespace AnoMech.Core.Native.Implementations;

// Hooks the native action and movement input paths so a mechanic can stun the local player.
// Status-row writes don't enforce anything (the server overwrites them every packet); the real
// lockout is the flags below, which SimPlayer reconciles each tick.
//
// Signatures and detour shapes lifted from FFXIV-RaidsRewritten's PlayerMovementOverride.cs /
// ActionManagerEx.cs (which credit awgil's vnavmesh + bossmod).
public sealed unsafe class LocalPlayerInputHooks : ILocalPlayerInput, IDisposable
{
    public bool DisableAllActions { get; set; }
    public bool ZeroMovement { get; set; }
    // A knockback slide freezes translation but not turning; Sleep, Confuse and KO freeze
    // rotation too. LockedRotation is re-stamped every frame from the ActionManager::Update
    // hook, after camera-follow rotation.
    public bool ZeroRotation { get; set; }
    public float? LockedRotation { get; set; }

    // Raised after the local player successfully executes a real action (the
    // auto-attack-cancel general action is filtered out). The UserActions module
    // subscribes to resolve effects the sim firewall blocks; nothing here depends
    // on a subscriber.
    public event Action<ActionType, uint, ulong>? ActionExecuted;

    // --- Player activity signals (read by SimPlayer to drive Party.Player.IsMoving/IsActing) ---
    // The engine's own per-frame movement sample (the signal bossmod reads), captured before
    // stun-zeroing: input intent, not a position delta.
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

    public void SetStatusAffliction(bool afflicted)
    {
        var condition = FFXIVClientStructs.FFXIV.Client.Game.Conditions.Instance();
        condition->SufferingStatusAffliction = afflicted;
        condition->SufferingStatusAffliction2 = afflicted;
    }

    // Debug aid for pinning down a real action id; DebugMenu shows these.
    private const int RecentActionsCapacity = 50;
    private readonly Queue<(uint ActionId, ActionType Type)> recentActions = new();
    public IReadOnlyCollection<(uint ActionId, ActionType Type)> RecentActions => recentActions;

    private void RecordRecentAction(uint actionId, ActionType type)
    {
        // GeneralAction 1 is the auto-attack engage/re-engage action -- fires constantly, pure noise here.
        if (type == ActionType.GeneralAction && actionId == 1) return;
        ZoneSession.NoteActionPressed(type, actionId);
        recentActions.Enqueue((actionId, type));
        while (recentActions.Count > RecentActionsCapacity) recentActions.Dequeue();
        var jobId = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId;
        Core.DiagnosticLog.Info($"[LocalPlayerInputHooks] Action pressed: {actionId} ({type}) -- {Core.ActionLookup.Name(actionId)} (job={jobId}).");
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
                Core.DiagnosticLog.Info($"[LocalPlayerInputHooks] Status gained: {gained} -- {Core.StatusLookup.Name(gained)} (job={jobId}, duration={remaining:F1}, x{count}).");
        foreach (var lost in lastLoggedStatusCounts.Keys.Except(current.Keys))
            Core.DiagnosticLog.Info($"[LocalPlayerInputHooks] Status lost: {lost} -- {Core.StatusLookup.Name(lost)} (job={jobId}).");
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

    private enum KeybindType
    {
        StrafeLeft = 325,
        StrafeRight = 326,
    }

    [return: MarshalAs(UnmanagedType.U1)]
    private delegate bool CheckStrafeKeybindDelegate(IntPtr ptr, KeybindType keybind);
    [Signature("E8 ?? ?? ?? ?? 84 C0 74 04 41 C6 06 01 BA 44 01 00 00")]
    private Hook<CheckStrafeKeybindDelegate> checkStrafeKeybindHook = null!;

    private readonly Hook<InputData.Delegates.IsInputIdPressed> isInputIdPressedHook;
    private readonly Hook<ActionManager.Delegates.Update> updateHook;
    private readonly Hook<ActionManager.Delegates.UseAction> useActionHook;
    private readonly Hook<ActionManager.Delegates.UseActionLocation> useActionLocationHook;
    private readonly Hook<Hotbar.Delegates.CancelCast> cancelCastHook;

    public LocalPlayerInputHooks(IGameInteropProvider hook)
    {
        hook.InitializeFromAttributes(this);

        isInputIdPressedHook = hook.HookFromAddress<InputData.Delegates.IsInputIdPressed>(
            InputData.Addresses.IsInputIdPressed.Value, IsInputIdPressedDetour);
        updateHook = hook.HookFromAddress<ActionManager.Delegates.Update>(
            ActionManager.Addresses.Update.Value, UpdateDetour);
        useActionHook = hook.HookFromAddress<ActionManager.Delegates.UseAction>(
            ActionManager.Addresses.UseAction.Value, UseActionDetour);
        useActionLocationHook = hook.HookFromAddress<ActionManager.Delegates.UseActionLocation>(
            ActionManager.Addresses.UseActionLocation.Value, UseActionLocationDetour);
        cancelCastHook = hook.HookFromAddress<Hotbar.Delegates.CancelCast>(
            Hotbar.Addresses.CancelCast.Value, CancelCastDetour);

        rmiWalkHook.Enable();
        checkStrafeKeybindHook.Enable();
        isInputIdPressedHook.Enable();
        updateHook.Enable();
        useActionHook.Enable();
        useActionLocationHook.Enable();
        cancelCastHook.Enable();
    }

    public void Dispose()
    {
        rmiWalkHook?.Dispose();
        checkStrafeKeybindHook?.Dispose();
        isInputIdPressedHook?.Dispose();
        updateHook?.Dispose();
        useActionHook?.Dispose();
        useActionLocationHook?.Dispose();
        cancelCastHook?.Dispose();
    }

    // The player asked to cancel their cast; latch it and let the original run (its
    // outgoing packet is firewalled in the sim, so it has no visible effect here).
    private void CancelCastDetour(Hotbar* thisPtr)
    {
        cancelCastRequested = true;
        cancelCastHook.Original(thisPtr);
    }

    private void RMIWalkDetour(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk)
    {
        rmiWalkHook.Original(self, sumLeft, sumForward, sumTurnLeft, haveBackwardOrStrafe, a6, bAdditiveUnk);
        // self is a MoveControllerSubMemberForMine*; the sums are its move vector.
        MovementInputActive = *sumLeft != 0 || *sumForward != 0;
        if (ZeroRotation) *sumTurnLeft = 0;
        if (!ZeroMovement) return;
        *sumLeft = 0;
        *sumForward = 0;
        *haveBackwardOrStrafe = 0;
    }

    private bool CheckStrafeKeybindDetour(IntPtr ptr, KeybindType keybind)
    {
        if (ZeroMovement && (keybind == KeybindType.StrafeLeft || keybind == KeybindType.StrafeRight))
            return false;
        return checkStrafeKeybindHook.Original(ptr, keybind);
    }

    private bool IsInputIdPressedDetour(InputData* inputData, InputId inputId)
    {
        if (ZeroMovement && (inputId == InputId.JUMP || inputId == InputId.PAD_JUMPANDCANCELCAST))
            return false;
        return isInputIdPressedHook.Original(inputData, inputId);
    }

    // Drains queued auto-attacks while DisableAllActions is set so the player
    // doesn't keep swinging mid-stun; mirrors raid-rewritten's UpdateDetour.
    private void UpdateDetour(ActionManager* self)
    {
        updateHook.Original(self);
        ScanAndLogActiveStatuses();
        // A missed clear must not pin rotation outside an instance.
        if (LockedRotation is { } lockedRot)
        {
            if (Plugin.GameInstance is { } g && g.World.Map.IsInInstance && Plugin.ObjectTable.LocalPlayer is { } lp)
                ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)lp.Address)->SetRotation(lockedRot);
            else
                LockedRotation = null;
        }
        if (!DisableAllActions) return;
        var autosOn = UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;
        if (autosOn) self->UseAction(ActionType.GeneralAction, 1);
    }

    // Null outside a scenario too: there the gauge is the player's own and none of this applies.
    private int? LimitBreakLevel(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action) return null;
        if (Plugin.GameInstance is not { } game || !game.World.Map.IsInInstance) return null;
        if (Plugin.ObjectTable.LocalPlayer is not { } local) return null;
        var lb = NativeLimitBreakController.Instance();
        if (lb == null) return null;
        var character = (Character*)local.Address;
        for (byte i = 0; i < 3; i++)
            if (lb->GetActionId(character, i) == actionId) return i;
        return null;
    }

    // The client's own refusal reason is the only way to see why a press did nothing.
    private void NoteLimitBreakPress(uint actionId, ulong targetId, int level, bool accepted, bool fired)
    {
        var name = Core.ActionLookup.Name(actionId);
        var job = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId;
        if (!accepted)
        {
            var am = ActionManager.Instance();
            var status = am == null ? 0u : am->GetActionStatus(ActionType.Action, actionId, targetId);
            var reason = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.LogMessage>()
                .GetRowOrDefault(status)?.Text.ExtractText() ?? "";
            Core.DiagnosticLog.Info($"[LimitBreak] {name} ({actionId}, job={job}) refused by the client -- status {status} \"{reason}\".");
            return;
        }
        // A queued press comes back through UseAction (mode Queue) when the lock ends, and an
        // area-targeted one through UseActionLocation once it is placed; only that call is the LB
        // going off.
        if (!fired)
        {
            Core.DiagnosticLog.Info($"[LimitBreak] {name} ({actionId}, LB{level + 1}, job={job}) queued or awaiting its ground target -- nothing happens until it fires.");
            return;
        }
        Core.DiagnosticLog.Info($"[LimitBreak] {name} ({actionId}, LB{level + 1}, job={job}) fired by the client, target 0x{targetId:X}"
            + $"{(limitBreakLocation is { } at ? $", ground target ({at.X:F2},{at.Z:F2})" : "")}.");
    }

    private ulong limitBreakTargetId;
    private Vector3? limitBreakLocation;

    private void NoteLimitBreakAim(uint actionId, ulong targetId, Vector3* location)
    {
        limitBreakTargetId = targetId;
        var areaTargeted = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId, out var row) && row.TargetArea;
        limitBreakLocation = areaTargeted && location != null ? *location : null;
    }

    // Where the local player's limit break was aimed as it resolves, scenario-local: toward the enemy
    // it was used on, else the way the player faced, and an area-targeted one's ground location.
    public LimitBreakAim LimitBreakAimNow(SimWorld world)
    {
        if (world.Party.Player is not { } player) return default;
        var aim = new Placement(player.Position, player.Rotation);
        if (world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.IsActive && (ulong)e.GameObjectId == limitBreakTargetId) is { } target)
            aim = aim.Face(target.Position);
        var location = limitBreakLocation is { } global ? world.Coordinates.ToLocal(global) : (Vector3?)null;
        return new LimitBreakAim(aim.Position, aim.Rotation, location);
    }

    public bool PressLimitBreakThree(ulong targetId, Vector3? worldLocation)
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } local) return false;
        var lb = NativeLimitBreakController.Instance();
        var actions = ActionManager.Instance();
        if (lb == null || actions == null) return false;
        var actionId = lb->GetActionId((Character*)local.Address, 2);
        if (actionId == 0) return false;
        if (worldLocation is not { } at) return actions->UseAction(ActionType.Action, actionId, targetId);
        return actions->UseActionLocation(ActionType.Action, actionId, 0xE0000000, &at);
    }

    // UseAction runs UseActionLocation inside itself for everything it executes; an area-targeted
    // action is the exception, executed later from the client's targeting mode.
    private int useActionDepth;

    private bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        RecordRecentAction(actionId, actionType);
        if (DisableAllActions && !IsStopAutosAction(actionType, actionId)) return false;
        var limitBreakLevel = LimitBreakLevel(actionType, actionId);
        // The UseActionLocation it runs, if it executes, names the resolved target instead.
        if (limitBreakLevel != null) NoteLimitBreakAim(actionId, targetId, null);
        // Advances only when the client actually sends the action, not when it queues it.
        var sequence = self->LastUsedActionSequence;
        bool result;
        useActionDepth++;
        try
        {
            result = useActionHook.Original(self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        }
        finally
        {
            useActionDepth--;
        }
        if (limitBreakLevel is { } pressedLevel)
            NoteLimitBreakPress(actionId, targetId, pressedLevel, result, fired: self->LastUsedActionSequence != sequence);
        // Ignore the auto-attack-cancel general action that UpdateDetour issues while stunned.
        if (result && !IsStopAutosAction(actionType, actionId))
        {
            actionUsedSincePoll = true;
            ActionExecuted?.Invoke(actionType, actionId, targetId);
        }
        return result;
    }

    // An area-targeted limit break (a caster's) fires here once its ground target is placed, with
    // no UseAction around it, so the press bookkeeping runs here for it instead.
    private bool UseActionLocationDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, Vector3* location, uint extraParam, byte a7)
    {
        if (DisableAllActions && !IsStopAutosAction(actionType, actionId)) return false;
        var limitBreakLevel = LimitBreakLevel(actionType, actionId);
        var fromTargetingMode = useActionDepth == 0;
        var sequence = self->LastUsedActionSequence;
        var result = useActionLocationHook.Original(self, actionType, actionId, targetId, location, extraParam, a7);
        if (limitBreakLevel is { } firedLevel)
        {
            // Nothing here queues: a true result is the action going off.
            var fired = result || self->LastUsedActionSequence != sequence;
            if (fired) NoteLimitBreakAim(actionId, targetId, location);
            if (fromTargetingMode) NoteLimitBreakPress(actionId, targetId, firedLevel, result, fired);
        }
        if (result)
        {
            actionUsedSincePoll = true;
            ActionExecuted?.Invoke(actionType, actionId, targetId);
        }
        return result;
    }

    // Lets the auto-cancel UseAction from UpdateDetour through; everything else
    // bounces while autos are still firing.
    private static bool IsStopAutosAction(ActionType actionType, uint actionId)
    {
        if (!UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking) return false;
        return actionType == ActionType.GeneralAction && actionId == 1;
    }
}
