using System;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Multiplayer;

public sealed partial class MultiplayerManager
{
    // ---- A player's own actions ---------------------------------------------

    // Retail's lock for most actions. The seat's character moves by its player's poses, which
    // that player's own lock already holds back.
    private const float ActionAnimationLock = 0.6f;
    private const uint NoTargetId = 0xE0000000;

    // Every client's player shows its own actions itself (UserActions); the rest of the party sees
    // them on that player's seat, as the host shows them there.
    private void OnActed(PlayerAction action)
    {
        if (!running || action.ActionType != ActionType.Action || MyClaimedRole is not { } seat) return;
        if (IsHost)
        {
            if (hostScenarioStarted) ShowAction(seat, action.Step, action.ActionId, ObjectIdOf(action.TargetId), action.CastSeconds);
        }
        else if (peerRunStarted && replay is { } r)
            _ = Send(new ActedMessage(MyPeerId, action.Step, action.ActionType, action.ActionId, r.HostActorOfObject(action.TargetId), action.CastSeconds));
    }

    // Its own seat only, a player's action only.
    private void OnPeerActed(ActedMessage acted)
    {
        if (Session.RoleOf(acted.PeerId) is not { } seat || !running || !hostScenarioStarted) return;
        if (!Enum.IsDefined(acted.Step) || acted.ActionType != ActionType.Action || !SimAssets.AllowAction(acted.ActionId, "a player's action")) return;
        ShowAction(seat, acted.Step, acted.ActionId, HostObjectOf(acted.Target), NetGuard.Clamp(acted.CastSeconds, 0f, NetGuard.MaxPlayerCastSeconds));
    }

    // As the server shows a seat's own action to the party: its cast bar, the cast's cancel, the
    // action. The seat's own client has shown it already (PacketRecorder.ShownBy).
    private void ShowAction(PartyRole seat, PlayerActionStep step, uint actionId, GameObjectId? target, float castSeconds)
    {
        if (recorder is not { } r || Plugin.Game.World.Party.Get(seat) is not { } member) return;
        r.ShownBy(seat, () =>
        {
            switch (step)
            {
                case PlayerActionStep.CastStarted:
                    member.Casting.NativeCast(actionId, ActionType.Action, 0f, castSeconds, interruptible: true, targetId: target);
                    break;
                case PlayerActionStep.Resolved:
                    var at = target ?? member.GameObjectId;
                    member.Casting.NativeActionEffect(actionId, ActionAnimationLock, (ushort)actionId, 0, ActionType.Action, 0,
                        animationTargetId: at, targets: [new ActionEffectTarget(at)]);
                    break;
                case PlayerActionStep.Interrupted:
                    member.Casting.Cancel(CastCancelReason.Silent);
                    break;
            }
        });
    }

    // A peer's target, named as the host's packets name actors.
    private static GameObjectId? HostObjectOf(uint actor)
    {
        if (PacketActor.IsSeat(actor, out var role)) return Plugin.Game.World.Party.Get(role)?.GameObjectId;
        if (!PacketActor.IsSpawned(actor)) return null;
        return Plugin.Game.World.Children.OfType<SimEnemy>().FirstOrDefault(e => e.EntityId == actor)?.GameObjectId;
    }

    private static GameObjectId? ObjectIdOf(ulong id)
        => (uint)id is 0 or NoTargetId ? null : new GameObjectId { ObjectId = (uint)id, Type = (byte)(id >> 32) };
}
