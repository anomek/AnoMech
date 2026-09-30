using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace AnoMech.Core.Native;

// Hooks the native action and movement input paths so a mechanic can stun the local player.
// Status-row writes don't enforce anything (the server overwrites them every packet); the real
// lockout is the flags below, which SimPlayer reconciles each tick.
//
// Signatures and detour shapes lifted from FFXIV-RaidsRewritten's PlayerMovementOverride.cs /
// ActionManagerEx.cs (which credit awgil's vnavmesh + bossmod).
public sealed unsafe class LocalPlayerInputHooks : IDisposable
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

    // Debug aid for pinning down a real action id; DebugMenu shows these.
    private const int RecentActionsCapacity = 50;
    private readonly Queue<(uint ActionId, ActionType Type)> recentActions = new();
    public IReadOnlyCollection<(uint ActionId, ActionType Type)> RecentActions => recentActions;

    private void RecordRecentAction(uint actionId, ActionType type)
    {
        // GeneralAction 1 is the auto-attack engage/re-engage action -- fires constantly, pure noise here.
        if (type == ActionType.GeneralAction && actionId == 1) return;
        Core.Map.ZoneSession.NoteActionPressed(type, actionId);
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
        // Game.Dispose doesn't route through ResetInternal.
        RestoreGaugeIllusion();
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
        UpdateGaugeIllusion();
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

    // ---- Gauge illusion (client-side only) -----------------------------------
    // The hotbar icon reads the real job gauge to render as available, so a gauge-gated
    // mitigation (Holy Sheltron needs 50 Oath) would look disabled. Topped up for display only;
    // the real value is saved once and restored when the sim ends.
    private byte? savedOathGauge;
    private const byte PaladinClassJobId = 19;

    private void UpdateGaugeIllusion()
    {
        if (Plugin.GameInstance is not { } game || !game.World.Map.IsInInstance)
        {
            RestoreGaugeIllusion();
            return;
        }
        UpdateLimitBreakIllusion();
        // Only Holy Sheltron (Paladin/Oath) needs this today.
        if (Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId != PaladinClassJobId) return;
        var gauge = (PaladinGauge*)Plugin.JobGauges.Address;
        if (gauge == null) return;
        savedOathGauge ??= gauge->OathGauge;
        gauge->OathGauge = 100;
    }

    // Same for the limit break gauge, but only once a scenario asks for it (SimWorld.SetLimitBreakGauge):
    // solo in the inn the real gauge is empty. Client-side only; a press is intercepted or
    // swallowed, so no LB packet ever leaves. Spent by UserActions' LimitBreakHandler.
    private (byte BarCount, ushort CurrentUnits, ushort BarUnits)? savedLimitBreak;
    private const ushort LimitBreakUnitsPerBar = 10000;
    private const byte LimitBreakBars = 3;
    // Null until the scenario sets it; the gauge is then left alone and every LB press dropped.
    private ushort? limitBreakUnits;

    public void SetLimitBreakGauge(float bars)
        => limitBreakUnits = (ushort)(Math.Clamp(bars, 0f, LimitBreakBars) * LimitBreakUnitsPerBar);

    private void UpdateLimitBreakIllusion()
    {
        if (limitBreakUnits is not { } units) return;
        var lb = LimitBreakController.Instance();
        if (lb == null) return;
        savedLimitBreak ??= (lb->BarCount, lb->CurrentUnits, lb->BarUnits);
        lb->BarCount = LimitBreakBars;
        lb->BarUnits = LimitBreakUnitsPerBar;
        lb->CurrentUnits = units;
    }

    // Also called from Game.ResetInternal so the restore is immediate on Reset/Leave.
    public void RestoreGaugeIllusion()
    {
        if (savedLimitBreak is { } lbSaved)
        {
            var lb = LimitBreakController.Instance();
            if (lb != null)
            {
                lb->BarCount = lbSaved.BarCount;
                lb->CurrentUnits = lbSaved.CurrentUnits;
                lb->BarUnits = lbSaved.BarUnits;
            }
            savedLimitBreak = null;
        }
        limitBreakUnits = null;
        if (savedOathGauge is not { } saved) return;
        if (Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId == PaladinClassJobId)
        {
            var gauge = (PaladinGauge*)Plugin.JobGauges.Address;
            if (gauge != null) gauge->OathGauge = saved;
        }
        savedOathGauge = null;
    }

    // Null outside a scenario too: there the gauge is the player's own and none of this applies.
    private int? LimitBreakLevel(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action) return null;
        if (Plugin.GameInstance is not { } game || !game.World.Map.IsInInstance) return null;
        if (Plugin.ObjectTable.LocalPlayer is not { } local) return null;
        var lb = LimitBreakController.Instance();
        if (lb == null) return null;
        var character = (Character*)local.Address;
        for (byte i = 0; i < 3; i++)
            if (lb->GetActionId(character, i) == actionId) return i;
        return null;
    }

    // Only LB3, and only from a full gauge. Everything else is the client's own: with the gauge faked
    // it runs the real UseAction, and the request packet that goes with it is eaten by the firewall.
    private bool RefuseLimitBreak(int level, uint actionId)
    {
        var name = Core.ActionLookup.Name(actionId);
        var job = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId;
        if (level < 2)
        {
            Core.DiagnosticLog.Info($"[LimitBreak] {name} ({actionId}, LB{level + 1}, job={job}) pressed -- only LB3 is simulated, press dropped.");
            return true;
        }
        // UserActions is what applies the effect and spends the gauge.
        if (!Plugin.UserActions.Enabled)
        {
            Core.DiagnosticLog.Info($"[LimitBreak] {name} ({actionId}, job={job}) pressed but user actions are disabled -- press dropped.");
            return true;
        }
        if (limitBreakUnits is not { } units)
        {
            Core.DiagnosticLog.Info($"[LimitBreak] {name} ({actionId}, job={job}) pressed but this scenario grants no limit break -- press dropped.");
            return true;
        }
        if (units < LimitBreakUnitsPerBar * LimitBreakBars)
        {
            Core.DiagnosticLog.Info($"[LimitBreak] {name} ({actionId}, job={job}) pressed but the gauge isn't full ({units}/{LimitBreakUnitsPerBar * LimitBreakBars}) -- press dropped.");
            return true;
        }
        // The gauge stays full until a cast lands, so a press queued behind it would fire a second one.
        if (Plugin.GameInstance?.World.Party.Player?.IsLimitBreaking == true)
        {
            Core.DiagnosticLog.Info($"[LimitBreak] {name} ({actionId}, job={job}) pressed while the last one is still casting -- press dropped.");
            return true;
        }
        return false;
    }

    public bool LimitBreakReady => limitBreakUnits >= LimitBreakUnitsPerBar * LimitBreakBars;

    public void SpendLimitBreak()
    {
        if (limitBreakUnits != null) limitBreakUnits = 0;
    }

    private bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        RecordRecentAction(actionId, actionType);
        if (DisableAllActions && !IsStopAutosAction(actionType, actionId)) return false;
        var limitBreakLevel = LimitBreakLevel(actionType, actionId);
        if (limitBreakLevel is { } level && RefuseLimitBreak(level, actionId)) return false;
        var result = useActionHook.Original(self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        // Ignore the auto-attack-cancel general action that UpdateDetour issues while stunned.
        if (result && !IsStopAutosAction(actionType, actionId))
        {
            actionUsedSincePoll = true;
            ActionExecuted?.Invoke(actionType, actionId, targetId);
        }
        return result;
    }

    private bool UseActionLocationDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, Vector3* location, uint extraParam, byte a7)
    {
        if (DisableAllActions && !IsStopAutosAction(actionType, actionId)) return false;
        var result = useActionLocationHook.Original(self, actionType, actionId, targetId, location, extraParam, a7);
        if (result)
        {
            actionUsedSincePoll = true;
            ActionExecuted?.Invoke(actionType, actionId, targetId);
            if (actionType == ActionType.Action) Plugin.GameInstance?.NotifyPlayerAction(actionId, targetId);
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
