using System;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.Game;

// The server's ActorControl packets for one character, by meaning. The client's own handler
// does what the real game does with them (UI refresh, chat lines, sounds), so a named method
// here is preferred over the proxy's direct engine calls wherever a category exists.
public sealed class ActorControl
{
    private const uint SetModeCategory = 0x02;
    private const uint DeathAnimationCategory = 0x0E;
    private const uint CancelCastCategory = 0x0F;
    private const uint CorpseFadeCategory = 0x27;
    private const uint ClearTetherCategory = 0x2F;
    private const uint ModeAttributeFlagsCategory = 0x31;
    private const uint TargetableCategory = 0x36;
    private const uint ModelStateCategory = 0x3F;
    private const uint ActionTimelineCategory = 0x197;

    // 1 = ActionType Action.
    private const uint ActionTypeAction = 1;

    private readonly Func<IBattleCharaProxy?> proxy;

    internal ActorControl(Func<IBattleCharaProxy?> proxy) => this.proxy = proxy;

    public void Send(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
        => proxy()?.ActorControl(category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);

    public void SetMode(CharacterModes mode, byte param = 0) => Send(SetModeCategory, (uint)mode, param);

    // Params (LogMessage row, ActionType, actionId, flag); LogMessage 0 prints nothing.
    public void CancelCast(uint actionId, CastCancelReason reason)
    {
        var (logMessage, flag) = reason switch
        {
            CastCancelReason.Interrupted => (540u, 1u),
            CastCancelReason.Cancelled => (537u, 0u),
            CastCancelReason.SelfCancelled => (538u, 0u),
            _ => (0u, 0u),
        };
        Send(CancelCastCategory, logMessage, ActionTypeAction, actionId, flag);
    }

    public void PlayDeathAnimation() => Send(DeathAnimationCategory);

    // The corpse fades out; the server despawns the actor about 1.7s later.
    public void FadeCorpse() => Send(CorpseFadeCategory);

    public void ClearTether() => Send(ClearTetherCategory);

    // The pose; the visible sub-meshes are ModeAttributeFlags. A model swap sends both.
    public void SetModelState(byte value) => Send(ModelStateCategory, value);

    public void SetModeAttributeFlags(byte value) => Send(ModeAttributeFlagsCategory, value);

    public void SetTargetable(bool targetable) => Send(TargetableCategory, targetable ? 1u : 0u);

    // A one-shot; the packet has no loop id.
    public void PlayActionTimeline(ushort timelineId) => Send(ActionTimelineCategory, timelineId);
}
