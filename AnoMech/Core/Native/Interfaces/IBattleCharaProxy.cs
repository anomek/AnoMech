using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Native.Interfaces;

// One BattleChara: a CharacterManager slot or the local player. The native object is looked up
// again on every call, so while the slot is empty (not created yet, despawned) reads return
// defaults and writes do nothing. Positions and rotations are world space.
public interface IBattleCharaProxy
{
    bool Exists { get; }

    // CharacterManager index; -1 for the local player.
    int Slot { get; }

    uint EntityId { get; }
    GameObjectId GameObjectId { get; }
    string Name { get; }
    byte ClassJob { get; }

    // ── Transform ────────────────────────────────────────────────────────────

    // Where the model is, which trails SetPos/Move by however long the client takes to apply them.
    Vector3 Position { get; }
    float Rotation { get; }

    // The server's ActorSetPos: an instant teleport. Drops the actor's queued Moves.
    void SetPos(Vector3 position, float rotation);

    // The server's ActorMove: a step the client interpolates to. `speed` (y/s) is what the actor
    // moves at, as other players report it. The client ignores it for the local player.
    void Move(Vector3 position, float rotation, ActorMoveFlags flags, float speed);

    // The server's move of an NPC a knockback pushed: the client slides it there over
    // `durationSeconds`. Players and bots never get one.
    void NpcKnockback(Vector3 position, float rotation, float durationSeconds);

    float HitboxRadius { get; }

    // ── Model ────────────────────────────────────────────────────────────────

    // DrawObject.Visibility: SpawnVisibility.HiddenUntilShown while there is no DrawObject.
    bool HasDrawObject { get; }
    bool IsDrawObjectVisible { get; }

    byte ModelState { get; }

    // ── Casting ──────────────────────────────────────────────────────────────

    bool IsCasting { get; }
    uint CastActionId { get; }

    // The engine advances it once ReceiveActorCast started the bar.
    float CurrentCastTime { get; }
    float TotalCastTime { get; }

    void ReceiveActorCast(ActorCastData cast);

    // Returns the packet's global sequence, which the targets' EffectResults name.
    uint ReceiveActionEffect(ActionEffectData effect);

    // The server's EffectResultBasic on this character: confirms its entry in that ActionEffect,
    // which the client then applies (damage flytext), and sets its HP.
    void ReceiveEffectResult(uint globalSequence, byte targetIndex, uint health);

    // The server's ActorControl packet for this actor, through the client's own dispatcher.
    void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0);

    // ActorControlTarget: an ActorControl that also names a target actor.
    void ActorControlTarget(uint category, GameObjectId target, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0);

    // The server's forced move (Confused): the client walks its own player to the target.
    // Self-only; null ends it.
    void ForcedFollow(uint? targetEntityId);

    // Ends the forced move and halts the player where they stand. Self-only.
    void StopForcedMove();

    // ── Combat state ─────────────────────────────────────────────────────────

    uint Health { get; }
    uint MaxHealth { get; }
    uint Mana { get; }
    uint MaxMana { get; }

    byte TargetableStatus { get; }

    // ── Statuses ─────────────────────────────────────────────────────────────

    // The server's status-list packet: statuses[i] lands in StatusManager slot i, slots past the
    // list are cleared. The client plays the lose/gain effects (loop VFX, param-driven looks) on
    // every slot whose id or source changes, and counts the remaining times down itself. Job,
    // level and maxima go out as the character has them. shield (0-100, % of max HP) is the gold
    // overlay on the HP bar, target bar and nameplate.
    void ReceiveStatusList(IReadOnlyList<StatusSlot> statuses, uint health, uint mana, byte shield);

    // ── Effects ──────────────────────────────────────────────────────────────

    ushort GetTetherId(byte slot);

    // Frees the slot; the local player's proxy ignores it.
    void Despawn();

    // ── Diagnostics ──────────────────────────────────────────────────────────

    string DescribeDrawState();
    string DescribeActionTimeline();
    string DescribeTimelineSlotIds();
    string DescribeModelSlots();
}
