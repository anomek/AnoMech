using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Core.SimObjects;

// A character occupying one of the 8 party slots: the local player (SimPlayer)
// or a non-player doppel (SimPartyNpc). Carries the PartyRole, the knockback API,
// and the death model (only party members are killed/KO'd — bosses despawn).
// Deliberately NOT implemented by SimEnemy/SimNpc — enemies are not party slots
// (the reason this is an interface and not a base class). Otherwise thin: the rest
// of a slot's surface is plain SimCharacter, reached by holding the slot as
// SimCharacter (SimParty.Get/Find/RoleList all return SimCharacter); see the
// SimCharacter.IsAlive()/Die() bridge extensions below.
public interface ISimPartyMember : ISimObject, IPositioned
{
    PartyRole Role { get; set;}

    // KO state. Set by the implementer's OnKilled; the member stays present
    // (IsActive) but lies on the floor. Implementers reset it on revive.
    bool Dead { get; }

    // Per-subclass effect of dying — flips Dead, then applies the visual/gameplay
    // KO. Game.Kill calls this (outside godmode). SimPartyNpc sets HP=0 + plays the
    // KO timeline; SimPlayer kicks the input-lockout hooks + KO pose.
    void OnKilled();

    // Default knockback speed (yalms/sec) when the caller doesn't know the
    // specific action — matches the FFXIV Knockback sheet's most common Speed
    // for short slides (~25).
    const float KnockbackSpeed = 25f;

    // No-op when the slot is co-located with `source` (direction undefined). The
    // 3-arg form is the real one — SimPlayer implements a tick-based slide on the
    // real player, SimPartyNpc the MoveTo-based default. The 2-arg and actionId
    // overloads thread through it.
    void Knockback(Vector3 source, float distance) => Knockback(source, distance, KnockbackSpeed);

    void Knockback(Vector3 source, float distance, float speed);

    // Forced movement in a fixed direction (world heading, same convention as Placement.Rotation)
    // rather than away from a point -- see Movement.PushInDirection's own doc comment.
    void PushInDirection(float heading, float distance, float speed);

    // Same as PushInDirection, but eased (ramp up, hold, ramp down) instead of constant-speed
    // -- see Movement.PushInDirectionEased's own doc comment for why this exists separately.
    void PushInDirectionEased(float heading, float distance, float durationSeconds);

    // A scripted snap, as opposed to SetPosition's engine-side use; SimNetworkPuppet hands it to
    // the owning peer.
    void TeleportTo(Placement placement) => ((SimCharacter)this).SetPosition(placement);

    // SimNetworkPuppet hands it to the owning peer.
    void CarryTo(Vector3 destination, CarryMode mode = CarryMode.Native);

    // A no-op for humans, who press their own Sprint; only bots cast, including a debug bot in the
    // local player's seat.
    void UseSprint(float duration) { }
}

// Bridges the party-member death model onto SimCharacter-typed call sites. Party
// slots are handed around as SimCharacter (SimParty.Get/Find return SimCharacter),
// so scenarios call .IsAlive()/.Die(...) on a SimCharacter; these resolve to the
// ISimPartyMember concept for party members and to plain presence otherwise.
public static class SimCharacterDeathExtensions
{
    // Public so SimAssets harvests them: the KO pose rides on RoleState to peers.
    public const ushort KoTimelineId = 72;
    public const ushort KoLoopTimelineId = 73;

    // A party member is alive while not KO'd, an enemy until defeated; any other character
    // is alive while present. Null is not alive.
    public static bool IsAlive(this SimCharacter? c)
        => c is { IsActive: true } and not ISimPartyMember { Dead: true } and not SimEnemy { IsDefeated: true };

    // A death no action deals (the arena wall, an uncleansed debuff): Die's explanation is then the whole message.
    public const uint Environment = 0;

    // Death is party-member-only. Calling Die on a non-party character is a no-op
    // (logged) — bosses are removed via Despawn, not killed.
    extension(SimCharacter c)
    {
        // Returns true only when the member actually went down (see Game.Kill):
        // false on a non-party character, an already-dead member, or one that
        // survived via godmode. Gate extra on-death logic on this.
        // The message is the action's name, with `explanation` after it in parentheses.
        public bool Die(uint actionId, string? explanation = null)
        {
            var name = actionId == Environment ? null : ActionLookup.Name(actionId);
            var message = name is null ? explanation ?? "" : explanation is null ? name : $"{name} ({explanation})";
            return Kill(c, message, actionId);
        }

        // Names no action, so nothing downstream can tell which mechanic it was.
        [Obsolete("Name the action: Die(actionId, explanation).")]
        public bool Die(string cause) => Kill(c, cause, null);

        public void PlayKoActionTimeline()
        {
            c.PlayActionTimeline(KoTimelineId, KoLoopTimelineId);
        }
    }

    private static bool Kill(SimCharacter c, string cause, uint? actionId)
    {
        if (c is ISimPartyMember pm) return Plugin.GameInstance.Kill(pm, cause, actionId);
        Plugin.Log.Warning($"Die() on non-party {c.GetType().Name} ignored: {cause}");
        return false;
    }
}
