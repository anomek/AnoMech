using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace AnoMech.Core.Native;

// Hooks the native action and movement input paths so the simulator can stun
// the local player when a mechanic kills them. Status-row writes don't enforce
// anything (the server overwrites StatusManager._status[] on every packet); the
// real lockout is two booleans this class exposes — the detours read them every
// frame and short-circuit the original calls. Owned by Plugin (session-lifetime);
// SimPlayer is the sole writer of the two flags, reconciling them each tick from
// its own Dead / Movement.IsMoving state.
//
// Signatures and detour shapes lifted from FFXIV-RaidsRewritten's
// PlayerMovementOverride.cs / ActionManagerEx.cs (which themselves credit
// awgil's vnavmesh + bossmod). If a future patch breaks a sig, both projects
// will need to rev them together.
public sealed unsafe class LocalPlayerInputHooks : IDisposable
{
    internal const uint SprintActionId = 3;
    private const ushort SprintStatusId = 50;
    private const float SprintDuration = 10f; 
    internal const ushort SprintStatusParam = 30;

    public bool DisableAllActions { get; set; }
    public bool ZeroMovement { get; set; }

    // --- Player activity signals (read by SimPlayer to drive Party.Player.IsMoving/IsActing) ---
    // Movement is the engine's own per-frame movement sample taken in RMIWalkDetour — the same
    // signal bossmod's MovementOverride.IsMoving() reads — captured as the player's true input
    // intent *before* the stun-zeroing. This is intent/input based (matches cast-cancel semantics),
    // not a position delta. Holds its last value on frames where RMIWalk doesn't fire.
    public bool MovementInputActive { get; private set; }

    // True while the player's weapon auto-attack is swinging.
    public bool IsAutoAttacking => UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;

    // True while the player is in a jump arc (CONDITION_JUMP). State poll like
    // IsAutoAttacking — a jump is always self-initiated, so the state flag is
    // equivalent to input intent here (nothing can force the player airborne).
    public bool IsJumping => Plugin.Condition[ConditionFlag.Jumping];

    // Latched whenever the player actually fires a real action; drained once per frame by SimPlayer
    // (PollActionUsed) so a same-frame action press is still observable to a snapshot mechanic.
    private bool actionUsedSincePoll;
    public bool PollActionUsed()
    {
        var used = actionUsedSincePoll;
        actionUsedSincePoll = false;
        return used;
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

        rmiWalkHook.Enable();
        checkStrafeKeybindHook.Enable();
        isInputIdPressedHook.Enable();
        updateHook.Enable();
        useActionHook.Enable();
        useActionLocationHook.Enable();
    }

    public void Dispose()
    {
        rmiWalkHook?.Dispose();
        checkStrafeKeybindHook?.Dispose();
        isInputIdPressedHook?.Dispose();
        updateHook?.Dispose();
        useActionHook?.Dispose();
        useActionLocationHook?.Dispose();
    }

    private void RMIWalkDetour(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk)
    {
        rmiWalkHook.Original(self, sumLeft, sumForward, sumTurnLeft, haveBackwardOrStrafe, a6, bAdditiveUnk);
        // Capture the engine's movement sample as the player's true movement intent, before any
        // stun-zeroing below. (self is a MoveControllerSubMemberForMine*; the sums are its move vector.)
        MovementInputActive = *sumLeft != 0 || *sumForward != 0;
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
        if (!DisableAllActions) return;
        var autosOn = UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;
        if (autosOn) self->UseAction(ActionType.GeneralAction, 1);
    }

    private bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        if (DisableAllActions && !IsStopAutosAction(actionType, actionId)) return false;
        var result = useActionHook.Original(self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        // Record a real action use for Party.Player.IsActing — but ignore the auto-attack-cancel
        // general action that UpdateDetour issues while stunned.
        if (result && !IsStopAutosAction(actionType, actionId))
            actionUsedSincePoll = true;
        if (result && actionType == ActionType.Action && actionId == SprintActionId)
            Plugin.GameInstance?.Player?.AddStatus(SprintStatusId, SprintDuration, SprintStatusParam);
        return result;
    }

    private bool UseActionLocationDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, Vector3* location, uint extraParam, byte a7)
    {
        if (DisableAllActions && !IsStopAutosAction(actionType, actionId)) return false;
        var result = useActionLocationHook.Original(self, actionType, actionId, targetId, location, extraParam, a7);
        if (result) actionUsedSincePoll = true;
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
