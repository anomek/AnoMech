using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;

namespace AnoMech.Tests;

// Nobody at the keyboard. The debug bot's LB3 press runs as the client runs it: refused unless
// the gauge is full, then the cast, then what UserActions does as it resolves: the action's own
// effects (a tank LB3's mitigation), then LimitBreakHandler's spend.
internal sealed class FakeLocalPlayerInput(FakeCharacter player) : ILocalPlayerInput
{
    public bool MovementInputActive => false;
    public bool IsJumping => false;
    public bool IsAutoAttacking => false;
    public bool PollActionUsed() => false;

    public bool ZeroMovement { get; set; }
    public bool ZeroRotation { get; set; }
    public bool DisableAllActions { get; set; }
    public float? LockedRotation { get; set; }

    public void SetStatusAffliction(bool afflicted) { }

    public bool PressLimitBreakThree(ulong targetId, Vector3? worldLocation)
    {
        if (player.IsCasting || Plugin.GameInstance?.World is not { } world) return false;
        var actionId = LimitBreakHandler.ActionId(player.ClassJob, 3);
        if (actionId == 0 || Natives.LimitBreak.Read() is not { } gauge || gauge.CurrentUnits < gauge.BarUnits * 3) return false;
        var castSeconds = Natives.Data.Action(actionId)?.CastSeconds ?? 0f;
        player.IsCasting = castSeconds > 0f;
        player.CastActionId = actionId;
        player.CurrentCastTime = 0f;
        player.TotalCastTime = castSeconds;
        world.Events.Add(castSeconds, () => Resolve(world, actionId, targetId, worldLocation));
        return true;
    }

    private static void Resolve(SimWorld world, uint actionId, ulong targetId, Vector3? worldLocation)
    {
        if (world.Party.Player is not { } self || !self.IsAlive()) return;
        JobActions.ApplyEffects(self, actionId, targetId);
        world.Party.LimitBreak.Spend();
        var aim = new Placement(self.Position, self.Rotation);
        if (world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.IsActive && (ulong)e.GameObjectId == targetId) is { } target)
            aim = aim.Face(target.Position);
        var location = worldLocation is { } global ? world.Coordinates.ToLocal(global) : (Vector3?)null;
        world.Party.LimitBreak.Land(actionId, new LimitBreakAim(aim.Position, aim.Rotation, location));
    }
}
