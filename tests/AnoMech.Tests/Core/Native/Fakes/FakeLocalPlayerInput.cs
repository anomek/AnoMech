using System;
using System.Collections.Generic;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Tests;

// Nobody at the keyboard, unless a test holds a movement key or presses an action. A press goes as
// the client and UserActions take it: refused while the player is casting, and a limit break
// unless the gauge holds its bars; then the player's own cast bar, which its client shows without
// a packet, and what UserActions does with the action as it starts and as it ends, which needs the
// module on. The job gauge cost (JobActions.ApplyCost) writes the game's own memory and isn't
// taken.
internal sealed class FakeLocalPlayerInput(FakeCharacter player, FakeUserActions userActions) : ILocalPlayerInput
{
    public event Action<ActionType, uint, ulong>? ActionExecuted { add { } remove { } }

    public bool MovementInputActive { get; set; }
    public bool IsJumping { get; set; }
    public bool IsAutoAttacking => false;
    public bool PollActionUsed() => false;
    public bool PollCancelCast() => false;
    public IReadOnlyCollection<(uint ActionId, ActionType Type)> RecentActions => [];

    public System.Numerics.Vector2? SteerDirection { get; private set; }
    public float SteerSpeed { get; private set; }

    public void Steer(System.Numerics.Vector2? direction, float speed)
    {
        SteerDirection = direction is { } d && d.LengthSquared() > 0f ? System.Numerics.Vector2.Normalize(d) : null;
        SteerSpeed = speed;
    }

    // A jump as the client flies one, after retail's: JumpHeight up at its top, JumpSeconds in the
    // air. The client's own update takes the press, so the plugin first sees a jump already under
    // way, and its landing already on the ground.
    public const float JumpSeconds = 0.8f;
    public const float JumpHeight = 1.8f;
    private bool jumpPressed;
    private float? takeOff;
    private float airborne;

    public void Jump() => jumpPressed = !IsJumping;

    // FakeGame's part of the frame, after the plugin's update.
    public void Fly(float deltaSeconds)
    {
        if (jumpPressed)
        {
            jumpPressed = false;
            IsJumping = true;
            takeOff = player.Position.Y;
            airborne = 0f;
        }
        if (takeOff is not { } ground) return;
        airborne = MathF.Min(airborne + deltaSeconds, JumpSeconds);
        var s = airborne / JumpSeconds;
        player.Position = player.Position with { Y = ground + JumpHeight * 4f * s * (1f - s) };
        if (airborne < JumpSeconds) return;
        IsJumping = false;
        takeOff = null;
    }

    private readonly LimitBreakHandler limitBreak = new();
    private readonly Random rng = new(0);
    private PlayerAction? pending;
    private float pendingMax;

    public bool Press(uint actionId, ulong targetId)
    {
        var gauge = player.LimitBreak;
        var bars = LimitBreakBarsFor(actionId);
        if (actionId == 0 || player.IsCasting || gauge.BarCount < bars || gauge.CurrentUnits < bars * gauge.BarUnits) return false;
        var castSeconds = Natives.Data.Action(actionId)?.CastSeconds ?? 0f;
        if (castSeconds <= 0f)
        {
            Resolve(actionId, targetId);
            return true;
        }
        player.IsCasting = true;
        player.CastActionId = actionId;
        player.CurrentCastTime = 0f;
        player.TotalCastTime = castSeconds;
        pending = new PlayerAction(PlayerActionStep.CastStarted, ActionType.Action, actionId, targetId, castSeconds);
        pendingMax = 0f;
        userActions.Act(pending);
        if (userActions.Enabled) limitBreak.OnCastStart(ActionType.Action, actionId);
        return true;
    }

    // A limit break's level, 0 for any other action.
    private int LimitBreakBarsFor(uint actionId)
    {
        if (!LimitBreakHandler.IsLimitBreak(actionId)) return 0;
        for (var level = 1; level < 3; level++)
            if (LimitBreakHandler.ActionId(player.ClassJob, level) == actionId) return level;
        return 3;
    }

    // UserActions' per-frame check: the cast resolves once its bar ended in the slidecast window.
    public void Tick()
    {
        if (pending is not { } cast) return;
        if (player.IsCasting && player.CastActionId == cast.ActionId)
        {
            pendingMax = MathF.Max(pendingMax, player.CurrentCastTime);
            return;
        }
        pending = null;
        if (pendingMax >= cast.CastSeconds - Plugin.Config.CastInterruptThreshold) Resolve(cast.ActionId, cast.TargetId);
        else userActions.Act(new PlayerAction(PlayerActionStep.Interrupted, ActionType.Action, cast.ActionId, cast.TargetId));
    }

    private void Resolve(uint actionId, ulong targetId)
    {
        userActions.Act(new PlayerAction(PlayerActionStep.Resolved, ActionType.Action, actionId, targetId));
        if (!userActions.Enabled) return;
        if (Plugin.Game?.World.Party.Player is { } self)
        {
            JobActions.ClearStatuses(self, actionId);
            JobActions.ApplyEffects(self, actionId, targetId, rng);
        }
        limitBreak.OnAction(ActionType.Action, actionId);
    }

    public void Dispose() { }
}
