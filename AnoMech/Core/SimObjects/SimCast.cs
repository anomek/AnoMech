using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Numerics;

namespace AnoMech.Core.SimObjects;

// A caster's server-shaped cast packets: the ActorCast that raises the bar and omen, the
// ActionEffect that plays the release, and the interrupt. Holds the animation lock that roots
// the caster after a release. Each packet is recorded with a seq, which is what a peer replays
// field for field. Positions in and out are scenario-local; they go world-space only at the proxy.
public sealed class SimCast : ISimObject
{
    private readonly SimCharacter parent;
    private readonly Coordinates coordinates;

    private float remainingAnimationLock;

    public bool IsCasting => parent.Proxy?.IsCasting ?? false;

    // Read live from CastInfo against the bar's own length.
    public float Progress
    {
        get
        {
            if (parent.Proxy is not { Exists: true } chara || Total <= 0f) return 0f;
            return Math.Clamp(chara.CurrentCastTime / Total, 0f, 1f);
        }
    }

    // Bumped per NativeCast. A peer dedupes on this changing rather than on IsCasting's rising
    // edge, which compared two independent clocks and replayed a cast twice.
    public int CastSeq { get; private set; }
    public uint ActionId { get; private set; }
    public float Total { get; private set; }
    public float OmenDelay { get; private set; }
    // Absolute, with any omen offset already folded in.
    public float Rotation { get; private set; }
    public Vector3? TargetLocation { get; private set; }
    // A raw GameObjectId isn't portable across the network (each client spawns its own doppels),
    // so MultiplayerManager resolves it to a role/NetId.
    public GameObjectId? TargetId { get; private set; }

    // Bumped per NativeActionEffect, release or standalone: an effect never sets IsCasting, so a
    // level-sampled snapshot can't see it.
    public int EffectSeq { get; private set; }
    public uint EffectActionId { get; private set; }
    public float EffectAnimationLock { get; private set; } = 0.6f;
    public byte EffectAnimationVariation { get; private set; }
    public float EffectRotation { get; private set; }
    public Vector3? EffectPosition { get; private set; }
    public GameObjectId? EffectAnimationTargetId { get; private set; }
    public GameObjectId? EffectActionTargetId { get; private set; }

    public int CancelSeq { get; private set; }

    // True while the cast bar is up or the release animation is still playing. A following boss
    // roots itself while busy so the action animation finishes in place instead of sliding.
    public bool IsBusy => IsCasting || remainingAnimationLock > 0f;

    // Owned and ticked by its SimCharacter, never reaped by liveness.
    public bool IsActive => true;

    internal SimCast(SimCharacter parent, Coordinates coordinates)
    {
        this.parent = parent;
        this.coordinates = coordinates;
    }

    public void NativeCast(uint actionId, ActionType actionType, float omenDelay, float castTime, bool interruptible, float? rotation = null, Vector3? position = null, GameObjectId? targetId = null, GameObjectId? ballistaId = null)
    {
        var castRotation = rotation ?? parent.Rotation;
        parent.Proxy?.ReceiveActorCast(new ActorCastData(
            actionId, actionType, castTime, omenDelay, interruptible,
            castRotation, coordinates.ToGlobal(position ?? parent.Position), targetId, ballistaId));

        ActionId = actionId;
        Total = castTime;
        OmenDelay = omenDelay;
        Rotation = castRotation;
        TargetLocation = position;
        TargetId = targetId;
        CastSeq++;
    }

    public void NativeActionEffect(uint actionId, float animationLock, ushort spellId, byte animationVariation, ActionType actionType, byte flags, float? rotation = null, Vector3? position = null, GameObjectId? animationTargetId = null, GameObjectId? actionTargetId = null, GameObjectId? ballistaId = null)
    {
        if (parent.Proxy is not { Exists: true } chara) return;

        var effectRotation = rotation ?? parent.Rotation;
        chara.ReceiveActionEffect(new ActionEffectData(
            actionId, actionType, animationLock, spellId, animationVariation, flags,
            effectRotation, coordinates.ToGlobal(position ?? Vector3.Zero),
            animationTargetId, actionTargetId, ballistaId));

        remainingAnimationLock = animationLock;
        RecordEffect(actionId, animationLock, animationVariation, effectRotation, position, animationTargetId, actionTargetId);
    }

    public void Cancel(CastCancelReason reason)
    {
        parent.ActorControl.CancelCast(ActionId, reason);
        CancelSeq++;
    }

    public void Tick(float deltaSeconds)
    {
        if (remainingAnimationLock > 0f)
            remainingAnimationLock = MathF.Max(0f, remainingAnimationLock - deltaSeconds);
    }

    // Clearing CastInfo matters because despawn deletes the BattleChara via DeleteObjectByIndex ->
    // Character::Terminate, whose scheduler teardown reads a still-live cast/action timeline and
    // crashes on freed state (C0000005 at TimelineGroup.PlayAction).
    public void Despawn() => parent.Proxy?.ClearCast();

    private void RecordEffect(uint actionId, float animationLock, byte animationVariation, float rotation, Vector3? position,
        GameObjectId? animationTargetId, GameObjectId? actionTargetId)
    {
        EffectActionId = actionId;
        EffectAnimationLock = animationLock;
        EffectAnimationVariation = animationVariation;
        EffectRotation = rotation;
        EffectPosition = position;
        EffectAnimationTargetId = animationTargetId;
        EffectActionTargetId = actionTargetId;
        EffectSeq++;
    }
}
