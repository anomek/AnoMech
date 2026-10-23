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
    private const uint GainEffectCategory = 0x14;
    private const uint LoseEffectCategory = 0x15;
    private const uint HeadMarkerCategory = 0x22;
    private const uint SetTetherCategory = 0x23;
    private const uint CorpseFadeCategory = 0x27;
    private const uint PopInCategory = 0x24;
    private const uint ClearTetherCategory = 0x2F;
    private const uint ActivateCategory = 0x30;
    private const uint ModeAttributeFlagsCategory = 0x31;
    private const uint WipeResetCategory = 0x3C;
    private const uint TargetableCategory = 0x36;
    private const uint AnimationStateCategory = 0x3E;
    private const uint VoiceLineCategory = 0x46;
    private const uint LimitBreakCastCategory = 0x47;
    private const uint LimitBreakResolveCategory = 0x48;
    private const uint WallDeathCategory = 0x50;
    private const uint ConfusionCategory = 0x52;
    private const uint ModelStateCategory = 0x3F;
    private const uint GimmickJumpCategory = 0xDC;
    private const uint ZoneInCategory = 0xC8;
    private const uint FellOutCategory = 0xED;
    private const uint FallStateCategory = 0x107;
    private const uint SlideCategory = 0xDF;
    private const uint WarpCategory = 0xF1;
    private const uint ActionTimelineCategory = 0x197;
    private const uint FadeOutCategory = 0x25F;
    private const uint ReviveCategory = 0x5F9;
    private const uint TransformCategory = 0x5FB;

    // ActorControlTarget.
    private const uint SetTargetCategory = 0x32;
    private const uint MarkingCategory = 0x1F6;

    // ActorControlSelf: only the local player's client acts on it.
    private const uint AutoAttackCategory = 0x01;
    private const uint GroundVfxCategory = 0xB7;
    private const uint PushCategory = 0xE1;
    private const uint LimitBreakGaugeCategory = 0x1F9;
    private const uint ForcedMarchStepCategory = 0x602;
    private const uint ForcedMarchEndCategory = 0x603;

    private const uint NoTarget = 0xE0000000;

    // 1 = ActionType Action.
    private const uint ActionTypeAction = 1;

    // ActionTimeline specialpop/specialpop.
    private const ushort SpecialPopTimeline = 142;

    // ActionTimeline pc_contentsaction/force_warp.
    private const ushort ForceWarpTimeline = 6192;

    // ActionTimeline pc_contentsaction/icefloor_short.
    private const ushort IceFloorShortTimeline = 3788;

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

    // ActorControlSelf, retail's answer to the client's own report of a fall out of the arena
    // (outbound 0x01EF): the client reports once, and until answered won't fall again and gets
    // "too far" from every server interaction. Which of the two is the answer is UNVERIFIED.
    public void FellOutOfArena()
    {
        Send(FellOutCategory, 1);
        Send(FallStateCategory, 1);
    }

    // ActorControlSelf, retail's close of a fall once the player is back on the arena.
    public void FallRecovered()
    {
        ZoneIn();
        Send(FallStateCategory, 0);
    }

    // Announces a status the status list already carries; the slot itself comes from the list.
    public void GainEffect(ushort statusId, ushort param) => Send(GainEffectCategory, statusId, param);

    // p4 is 0 or 1 in retail, independent of whether the status expired or was removed early;
    // what it selects is UNVERIFIED, 0 is the more common value.
    public void LoseEffect(ushort statusId, ushort param, GameObjectId source)
        => Send(LoseEffectCategory, statusId, param, source == default ? NoTarget : source.ObjectId);

    // Retail's revive on a wipe reset, all on one tick. 0x3C is seen only there; what it does
    // on its own is UNVERIFIED.
    public void Revive()
    {
        SetMode(CharacterModes.Normal);
        Send(WipeResetCategory);
        Send(ReviveCategory);
    }

    // Retail sends it only for deaths no action dealt (death wall, falling off), never for
    // a mechanic's hit. What the client does with it is UNVERIFIED.
    public void WallDeath(uint health) => Send(WallDeathCategory, health);

    // Retail sends it only with Confused: p1 5284 (UMAD P1) or 1051 (Iron Kiss) as the status lands,
    // 0 as it runs out; what the id selects is UNVERIFIED. Confused is what was last sent: while it
    // holds, the party chases (SimParty), as the server drives a confused player.
    public bool Confused { get; private set; }

    // Retail opens it with auto-attack (ActorControlSelf 0x01: 0, 1) and a weapon draw whose p3 is
    // 1, a draw seen nowhere else, all in one frame. UNVERIFIED that the client's forced follow
    // needs either.
    public void SetConfused(bool confused)
    {
        Confused = confused;
        if (confused)
        {
            Send(AutoAttackCategory, 0, 1);
            Send(WeaponCategory, 1, 1, 1);
        }
        Send(ConfusionCategory, confused ? 5284u : 0u);
    }

    // Reveals an actor spawned SpawnVisibility.HiddenUntilPopIn; 
    // Timeline 0 = no animation, in cases when an action fired in the same frame plays its own animation
    public void PopIn(ushort timelineId = SpecialPopTimeline) => Send(PopInCategory, timelineId != 0 ? 1u : 0u, timelineId);

    public void FadeCorpse() => Send(CorpseFadeCategory);

    // Retail follows every other player's spawn with it, ~0.6-1.2s later.
    public void ZoneIn() => Send(ZoneInCategory);

    public void FadeOut()
    {
        if (proxy() is { } chara) chara.ActorControl(FadeOutCategory, chara.EntityId, 1, 0, 100);
    }

    public void SetTether(ushort tetherId, GameObjectId target) => Send(SetTetherCategory, 0, tetherId, target.ObjectId, 15);

    public void ClearTether() => Send(ClearTetherCategory);

    public void HeadMarker(uint lockonId)
    {
        if (proxy() is { } chara) chara.ActorControl(HeadMarkerCategory, lockonId, chara.EntityId);
    }

    public void SetModelState(byte value) => Send(ModelStateCategory, value);

    public void SetModeAttributeFlags(byte value) => Send(ModeAttributeFlagsCategory, value);

    // Plays the NPC's transformation into a BNpcState row. Retail sends the end state's 0x31 / 0x3F
    // separately on the same tick.
    public void Transform(uint bnpcStateId) => Send(TransformCategory, bnpcStateId);

    public void SetAnimationState(byte slot, byte value) => Send(AnimationStateCategory, slot, value);

    public void Activate(uint state) => Send(ActivateCategory, state);

    public void PlayVoiceLine(uint voiceLineId) => Send(VoiceLineCategory, voiceLineId);

    public void LimitBreakCast(uint actionId, bool isUser) => Send(LimitBreakCastCategory, actionId, isUser ? 0u : 1u);

    public void LimitBreakResolve(uint actionId, bool isUser, uint group)
        => Send(LimitBreakResolveCategory, actionId, isUser ? 0u : 1u, group);

    // ActorControlSelf. p4 is the gain's reason; 0 = passive tick or resync.
    public void SetLimitBreakGauge(uint bars, uint units, uint unitsPerBar) => Send(LimitBreakGaugeCategory, bars, units, unitsPerBar);

    public void SetTargetable(bool targetable) => Send(TargetableCategory, targetable ? 1u : 0u);

    public void SetTarget(GameObjectId target) => proxy()?.ActorControlTarget(SetTargetCategory, target);

    public void ClearTarget() => SetTarget(NoTarget);

    // ActorControlTarget sent to the local player; p2 is whoever placed the marker.
    public void Mark(Sign sign, GameObjectId target)
    {
        if (proxy() is { } chara) chara.ActorControlTarget(MarkingCategory, target, (uint)sign, chara.EntityId);
    }

    public void ClearMark(Sign sign) => Mark(sign, NoTarget);

    public void ClearMarks()
    {
        foreach (var sign in Enum.GetValues<Sign>()) ClearMark(sign);
    }

    public void PlaceGroundVfx(uint vfxId, Vector3 worldPosition)
        => Send(GroundVfxCategory, vfxId, MathUtil.QuantizePosition(worldPosition.X), MathUtil.QuantizePosition(worldPosition.Y),
            MathUtil.QuantizePosition(worldPosition.Z), 0, NoTarget);

    // Knockback on self
    public void Push(float heading, float distance, float durationSeconds)
        => Send(PushCategory, MathUtil.QuantizeRotation(heading), (uint)MathF.Round(distance * 10000f),
            (uint)MathF.Round(durationSeconds * 10000f));

    public void PlayActionTimeline(ushort timelineId) => Send(ActionTimelineCategory, timelineId);

    // ActorControlSelf: walk on toward this point at `speed`, facing `heading`; floats as their bits.
    public void ForcedMarchStep(Vector3 worldPoint, float heading, float speed)
        => Send(ForcedMarchStepCategory, BitConverter.SingleToUInt32Bits(worldPoint.X), BitConverter.SingleToUInt32Bits(worldPoint.Y),
            BitConverter.SingleToUInt32Bits(worldPoint.Z), BitConverter.SingleToUInt32Bits(heading), BitConverter.SingleToUInt32Bits(speed));

    // ActorControlSelf: the march stops here.
    public void ForcedMarchEnd(Vector3 worldPoint, float heading)
        => Send(ForcedMarchEndCategory, BitConverter.SingleToUInt32Bits(worldPoint.X), BitConverter.SingleToUInt32Bits(worldPoint.Y),
            BitConverter.SingleToUInt32Bits(worldPoint.Z), BitConverter.SingleToUInt32Bits(heading));

    // `timeline` is the ActionTimeline the carried character plays; null = force_warp.
    public void CarryTo(Vector3 worldDestination, float rotation, ushort? timeline = null)
    {
        var position = ((uint)MathUtil.QuantizePosition(worldDestination.X) << 16) | MathUtil.QuantizePosition(worldDestination.Y);
        var facing = ((uint)MathUtil.QuantizePosition(worldDestination.Z) << 16) | MathUtil.QuantizeRotation(rotation);
        Send(WarpCategory, position, facing, 0x10000u | (timeline ?? ForceWarpTimeline), ++warpSequence);
    }

    // Launches the character through the air to the destination. The GimmickJump row picks height and
    // loop/landing motions; 5 is UMAD's Death Bomb launch. Unk has no visible effect.
    public void GimmickJump(Vector3 worldDestination, uint gimmickJumpId = 5, uint unk = 151)
    {
        var position = ((uint)MathUtil.QuantizePosition(worldDestination.X) << 16) | MathUtil.QuantizePosition(worldDestination.Y);
        Send(GimmickJumpCategory, position, MathUtil.QuantizePosition(worldDestination.Z), gimmickJumpId, unk);
    }

    // Slides the character in a straight line to the destination, as on FRU's Frost Armor ice.
    // Rotation is the facing during the slide, which retail sets to the slide's direction.
    public void Slide(Vector3 worldDestination, float rotation, ushort timelineId = IceFloorShortTimeline, uint unk = 1)
    {
        var position = ((uint)MathUtil.QuantizePosition(worldDestination.X) << 16) | MathUtil.QuantizePosition(worldDestination.Y);
        var facing = ((uint)MathUtil.QuantizePosition(worldDestination.Z) << 16) | MathUtil.QuantizeRotation(rotation);
        Send(SlideCategory, position, facing, unk, timelineId);
    }
}
