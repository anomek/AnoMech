using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AnoMech.Core.SimObjects;

// A caster's server-shaped cast packets: the ActorCast that raises the bar and omen, the
// ActionEffect that plays the release, and the interrupt. Holds the animation lock that roots
// the caster after a release. Positions in and out are scenario-local; they go world-space only
// at the proxy.
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

    public uint ActionId { get; private set; }
    public float Total { get; private set; }
    public float OmenDelay { get; private set; }
    // Absolute, with any omen offset already folded in.
    public float Rotation { get; private set; }
    public Vector3? TargetLocation { get; private set; }
    public GameObjectId? TargetId { get; private set; }

    // True while the cast bar is up or the release animation is still playing. A following boss
    // roots itself while busy so the action animation finishes in place instead of sliding.
    public bool IsBusy => IsCasting || remainingAnimationLock > 0f;

    // How many ActionEffects it has sent: a mechanic watching for an action in a window counts
    // the moment one goes out, not the lock it leaves.
    public int ActionsSent { get; private set; }

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
    }

    // Returns the global sequence the targets' EffectResults name; 0 when nothing was sent.
    public uint NativeActionEffect(uint actionId, float animationLock, ushort spellId, byte animationVariation, ActionType actionType, byte flags, float? rotation = null, Vector3? position = null, GameObjectId? animationTargetId = null, IReadOnlyList<ActionEffectTarget>? targets = null, GameObjectId? ballistaId = null)
    {
        if (parent.Proxy is not { Exists: true } chara) return 0;

        var effectRotation = rotation ?? parent.Rotation;
        var sequence = chara.ReceiveActionEffect(new ActionEffectData(
            actionId, actionType, animationLock, spellId, animationVariation, flags,
            effectRotation, coordinates.ToGlobal(position ?? Vector3.Zero),
            animationTargetId, targets, ballistaId));

        remainingAnimationLock = animationLock;
        ActionsSent++;
        return sequence;
    }

    public void Cancel(CastCancelReason reason)
    {
        parent.ActorControl.CancelCast(ActionId, reason);
    }

    public void Tick(float deltaSeconds)
    {
        if (remainingAnimationLock > 0f)
            remainingAnimationLock = MathF.Max(0f, remainingAnimationLock - deltaSeconds);
    }

    // Character::Terminate's scheduler teardown crashes on a still-live cast timeline
    // (C0000005 at TimelineGroup.PlayAction), so the cast is called off before the despawn.
    public void Despawn()
    {
        if (parent.Proxy is { IsCasting: true } chara)
            parent.ActorControl.CancelCast(chara.CastActionId, CastCancelReason.Silent);
    }
}
