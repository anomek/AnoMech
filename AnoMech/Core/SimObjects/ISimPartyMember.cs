using System;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.SimObjects;

// A character occupying one of the 8 party slots: the local player (SimPlayer)
// or a non-player doppel (SimPartyNpc). Carries the PartyRole and the death model
// (only party members are killed/KO'd — bosses despawn).
// Deliberately NOT implemented by SimEnemy/SimNpc — enemies are not party slots
// (the reason this is an interface and not a base class). Otherwise thin: the rest
// of a slot's surface is plain SimCharacter, reached by holding the slot as
// SimCharacter (SimParty.Get/Find/RoleList all return SimCharacter); see the
// SimCharacter.IsAlive()/Die() bridge extensions below.
public interface ISimPartyMember : ISimObject, IPositioned
{
    PartyRole Role { get; set;}

    // KO: the member stays present (IsActive) but lies on the floor.
    bool Dead { get; }

    // Game.Kill calls this outside godmode. byEnvironment: no action dealt the death.
    void OnKilled(bool byEnvironment);

    // A no-op for humans, who press their own Sprint; only bots cast, including a debug bot in the
    // local player's seat.
    void UseSprint(float duration) { }

    byte ClassJob { get; }

    // Same split as UseSprint: presses `actionId` at `target` (null: themselves) with the effects
    // JobActions gives a player's press; one aimed at an enemy lands on it (SimEnemy.Attacked).
    // False when KO'd, locked out of actions, or the target is gone.
    bool UseAction(uint actionId, SimCharacter? target = null) => false;
}

// Bridges the party-member death model onto SimCharacter-typed call sites. Party
// slots are handed around as SimCharacter (SimParty.Get/Find return SimCharacter),
// so scenarios call .IsAlive()/.Die(...) on a SimCharacter; these resolve to the
// ISimPartyMember concept for party members and to plain presence otherwise.
public static class SimCharacterDeathExtensions
{
    // A party member is alive while not KO'd, an enemy until defeated; any other character
    // is alive while present. Null is not alive.
    public static bool IsAlive(this SimCharacter? c)
        => c is { IsActive: true, Dead: false };

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
            var name = actionId == Environment ? null : Natives.Data.ActionName(actionId);
            return Kill(c, Message(name, explanation), actionId, statusId: null);
        }

        // Killed by a status itself, with no action and none of the environment's wall-death packet:
        // retail's death when TOP's Doom runs out.
        public bool DieOfStatus(ushort statusId, string? explanation = null)
            => Kill(c, Message(Natives.Data.StatusName(statusId), explanation), actionId: null, statusId);

        // Names no action, so nothing downstream can tell which mechanic it was.
        [Obsolete("Name the action: Die(actionId, explanation).")]
        public bool Die(string cause) => Kill(c, cause, null, null);
    }

    private static string Message(string? name, string? explanation)
        => name is null ? explanation ?? "" : explanation is null ? name : $"{name} ({explanation})";

    private static bool Kill(SimCharacter c, string cause, uint? actionId, ushort? statusId)
    {
        if (c is ISimPartyMember pm) return Plugin.Game.Kill(pm, cause, actionId, statusId);
        Plugin.Log.Warning($"Die() on non-party {c.GetType().Name} ignored: {cause}");
        return false;
    }
}
