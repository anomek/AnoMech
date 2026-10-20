using System;
using System.Collections.Generic;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Tests;

// Nobody at the keyboard, unless a test holds a movement key.
internal sealed class FakeLocalPlayerInput : ILocalPlayerInput
{
    public event Action<ActionType, uint, ulong>? ActionExecuted { add { } remove { } }

    public bool MovementInputActive { get; set; }
    public bool IsJumping => false;
    public bool IsAutoAttacking => false;
    // A test's press: the player's next frame sees it once.
    private bool pressed;
    public void PressAction() => pressed = true;

    public bool PollActionUsed()
    {
        var used = pressed;
        pressed = false;
        return used;
    }
    public bool PollCancelCast() => false;
    public IReadOnlyCollection<(uint ActionId, ActionType Type)> RecentActions => [];

    public System.Numerics.Vector2? SteerDirection { get; private set; }
    public float SteerSpeed { get; private set; }

    public void Steer(System.Numerics.Vector2? direction, float speed)
    {
        SteerDirection = direction is { } d && d.LengthSquared() > 0f ? System.Numerics.Vector2.Normalize(d) : null;
        SteerSpeed = speed;
    }

    public Func<uint, uint?>? AdjustActionId { get; set; }
    public Func<ActionType, uint, int?>? AdjustCastTime { get; set; }
    public Func<ActionType, uint, bool, uint?>? AdjustActionStatus { get; set; }

    public void Dispose() { }
}
