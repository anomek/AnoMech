using System;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Game;

// The server's ActorControl packets for one character, by meaning. The client's own handler
// does what the real game does with them (UI refresh, chat lines, sounds), so a named method
// here is preferred over the proxy's direct engine calls wherever a category exists.
public sealed class ActorControl
{
    private const uint WeaponCategory = 0x00;
    private const uint SetModeCategory = 0x02;
    private const uint CombatCategory = 0x04;
    private const uint DeathAnimationCategory = 0x0E;
    private const uint CancelCastCategory = 0x0F;
    private const uint HeadMarkerCategory = 0x22;
    private const uint SetTetherCategory = 0x23;
    private const uint CorpseFadeCategory = 0x27;
    private const uint PopInCategory = 0x24;
    private const uint ClearTetherCategory = 0x2F;
    private const uint ModeAttributeFlagsCategory = 0x31;
    private const uint TargetableCategory = 0x36;
    private const uint ModelStateCategory = 0x3F;
    private const uint WarpCategory = 0xF1;
    private const uint ActionTimelineCategory = 0x197;
    private const uint FadeOutCategory = 0x25F;

    // 1 = ActionType Action.
    private const uint ActionTypeAction = 1;

    // ActionTimeline specialpop/specialpop.
    private const ushort SpecialPopTimeline = 142;

    // ActionTimeline pc_contentsaction/force_warp.
    private const ushort ForceWarpTimeline = 6192;

    // The server numbers every warp in the instance, counting up by one.
    private static uint warpSequence;

    private readonly Func<IBattleCharaProxy?> proxy;

    internal ActorControl(Func<IBattleCharaProxy?> proxy) => this.proxy = proxy;

    public void Send(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
        => proxy()?.ActorControl(category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);

    public void SetMode(CharacterModes mode, byte param = 0) => Send(SetModeCategory, (uint)mode, param);

    // p2 is 1 on nearly every server draw and sheathe; its meaning is UNVERIFIED.
    public void SetWeaponDrawn(bool drawn) => Send(WeaponCategory, drawn ? 1u : 0u, 1);

    public void SetInCombat(bool inCombat) => Send(CombatCategory, inCombat ? 1u : 0u);

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

    // Reveals an actor spawned SpawnVisibility.HiddenUntilPopIn; the server sends it 0.6–0.9s
    // after the spawn. Timeline 0 = no animation, when an action fired in the same frame plays its
    // own (UWU bomb boulder). Effect on our actors UNVERIFIED.
    public void PopIn(ushort timelineId = SpecialPopTimeline) => Send(PopInCategory, timelineId != 0 ? 1u : 0u, timelineId);

    // The corpse fades out; the server despawns the actor about 1.7s later.
    public void FadeCorpse() => Send(CorpseFadeCategory);

    // The server despawns the actor 1.0–1.5s later; SimEnemy.FadeOut schedules that.
    public void FadeOut()
    {
        if (proxy() is { } chara) chara.ActorControl(FadeOutCategory, chara.EntityId, 1, 0, 100);
    }

    // Drawn from this character to target, in tether slot 0. Every server tether carries
    // p1 = 0 and p4 = 15; their meaning is UNVERIFIED.
    public void SetTether(ushort tetherId, GameObjectId target) => Send(SetTetherCategory, 0, tetherId, target.ObjectId, 15);

    // Clears slot 0. The server sends it before every new tether on the same actor.
    public void ClearTether() => Send(ClearTetherCategory);

    // A Lockon row; the marker's VFX plays out and removes itself.
    public void HeadMarker(uint lockonId)
    {
        if (proxy() is { } chara) chara.ActorControl(HeadMarkerCategory, lockonId, chara.EntityId);
    }

    // The pose; the visible sub-meshes are ModeAttributeFlags. A model swap sends both.
    public void SetModelState(byte value) => Send(ModelStateCategory, value);

    public void SetModeAttributeFlags(byte value) => Send(ModeAttributeFlagsCategory, value);

    public void SetTargetable(bool targetable) => Send(TargetableCategory, targetable ? 1u : 0u);

    // A one-shot; the packet has no loop id.
    public void PlayActionTimeline(ushort timelineId) => Send(ActionTimelineCategory, timelineId);

    // The client plays the timeline and lands the character on the destination ~1.0s later,
    // however far it is. The high half of p3 is always 1 in retail; its meaning is UNVERIFIED.
    public void CarryTo(Vector3 worldDestination, float rotation)
    {
        var position = ((uint)MathUtil.QuantizePosition(worldDestination.X) << 16) | MathUtil.QuantizePosition(worldDestination.Y);
        var facing = ((uint)MathUtil.QuantizePosition(worldDestination.Z) << 16) | MathUtil.QuantizeRotation(rotation);
        Send(WarpCategory, position, facing, 0x10000u | ForceWarpTimeline, ++warpSequence);
    }
}
