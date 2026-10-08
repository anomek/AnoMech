using AnoMech.Core.Game;
using AnoMech.Core.Native.Implementations;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.UserActions;

// The server interrupts a player's cast on move/jump/cancel by echoing an
// ActorControl(15) back, which the client handles to clear the bar. The sim firewall
// eats that packet, so we synthesize it. No interrupt inside the last
// CastInterruptThreshold seconds (~0.5s = when the server resolves the action): that's
// the slidecast window, where the cast is committed.
internal sealed unsafe class CastInterruptHandler : IUserActionHandler
{
    private readonly ActorControl localPlayer = new(() => BattleCharaProxy.LocalPlayer);

    public void OnTick(float deltaSeconds)
    {
        var hooks = Plugin.PlayerInputHooks;
        var cancelRequested = hooks.PollCancelCast(); // drain every frame so a stale latch can't carry over

        var player = (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0);
        if (player == null || !player->CastInfo.IsCasting) return;

        var remaining = player->CastInfo.TotalCastTime - player->CastInfo.CurrentCastTime;
        if (remaining <= Plugin.Config.CastInterruptThreshold) return;

        if (hooks.MovementInputActive || hooks.IsJumping || cancelRequested)
            localPlayer.CancelCast(player->CastInfo.ActionId, CastCancelReason.SelfCancelled);
    }
}
