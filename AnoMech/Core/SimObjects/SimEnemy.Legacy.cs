using System;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.SimObjects;

// UWU P1-P3 and Ultimate Annihilation still script casts and effects by hand;
// upstream scenarios describe theirs as EnemyActions instead.
public sealed partial class SimEnemy
{
    public void NativeCast(uint actionId, ActionType actionType, float omenDelay, float castTime, bool interruptible, float? rotation = null, Vector3? position = null, GameObjectId? targetId = null, GameObjectId? ballistaId = null)
        => Casting.NativeCast(actionId, actionType, omenDelay, castTime, interruptible, rotation, position, targetId, ballistaId);

    public void NativeActionEffect(uint actionId, float animationLock, ushort spellId, byte animationVariaton, ActionType actionType, byte flags, float? rotation = null, Vector3? position = null, GameObjectId? animationTargetId = null, GameObjectId? actionTargetId = null, GameObjectId? ballistaId = null)
        => Casting.NativeActionEffect(actionId, animationLock, spellId, animationVariaton, actionType, flags, rotation, position, animationTargetId,
            actionTargetId is { } target ? [new ActionEffectTarget(target)] : null, ballistaId);

    // A bar of `castSeconds` (the sheet's when null) whose effect lands as it ends, with no mechanics.
    public EnemyActionCast LegacyCast(uint actionId, Vector3? targetLocation = null, float? castSeconds = null, SimCharacter? target = null,
        float omenDelay = 0f, float omenRotate = 0f, byte animationVariation = 0, float animationLock = 0.6f)
    {
        var action = new EnemyAction(actionId)
        {
            Cast = new() { AnimationLock = animationLock, OmenDelay = omenDelay, CastSecondsOverride = castSeconds, Rotation = omenRotate },
        };
        return actions.Start(action, targetLocation is { } at ? (CastTarget)at : target, animationVariation);
    }

    public static SpawnVisibility LegacyVisibility(bool helper, bool visible) =>
        helper ? SpawnVisibility.InvisibleHelper : visible ? SpawnVisibility.Visible : SpawnVisibility.HiddenUntilShown;

    public bool IsMoving => Motion.Destination != null;

    private bool? desiredVisible;

    // Writes the DrawObject flag directly; a model still streaming in shows itself once loaded,
    // so a hidden enemy is re-checked every tick.
    public void SetVisible(bool visible) => desiredVisible = visible;

    public bool Visible => desiredVisible ?? SpawnConfig.Visibility == SpawnVisibility.Visible;

    private void TickLegacyVisibility()
    {
        if (desiredVisible is not { } visible || Proxy is not { HasDrawObject: true } drawn) return;
        if (drawn.IsDrawObjectVisible != visible) drawn.IsDrawObjectVisible = visible;
    }

    // For adds whose HP bar the sim drains on a timer; the bar shows a fraction of the spawn's max HP.
    public void SetHealth(uint maxHealth, float fraction)
    {
        if (Proxy is not { Exists: true } chara) return;
        SendHealth((uint)MathF.Ceiling(chara.MaxHealth * Math.Clamp(fraction, 0f, 1f)));
    }
}
