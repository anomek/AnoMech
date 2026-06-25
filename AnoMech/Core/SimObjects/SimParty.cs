using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Core.SimObjects;

// Fixed-size 8-slot party indexed by PartyRole. Each slot holds either a
// SimPartyNpc (spawned doppel) or the SimPlayer (the player's own role) — both
// ISimPartyMember. Slots are stored/returned as SimCharacter so they thread
// straight into the character-typed engine APIs (world.Tether, AddStatus, ...);
// the party-slot facets (Role, Knockback) are reached by casting to
// ISimPartyMember.
//
// HUD mirroring is owned by SimWorld (not SimParty) — see SimWorld.partyHud —
// so the addon-lifecycle listener has the same lifespan as the plugin and the
// SimParty.Empty sentinel doesn't accidentally register one at static init.
public sealed class SimParty : ISimObject
{
    private static Random rnd = new();
    
    public static readonly SimParty Empty = new();

    private readonly SimCharacter?[] slots = new SimCharacter?[8];

    public SimParty() { Find = new CharacterFind<SimCharacter>(ActiveMembers); }

    public CharacterFind<SimCharacter> Find { get; }

    public SimCharacter? Get(int roleId)
        => roleId >= 0 && roleId < slots.Length ? slots[roleId] : null;

    public SimCharacter? Get(PartyRole role) => Get((int)role);

    // The role of the slot currently holding the SimPlayer — set by SetSlot
    // when it receives one. Falls back to MainTank if no SimPlayer has been
    // placed (no scenario hits this today).
    public PartyRole PlayerRole { get; private set; } = PartyRole.MainTank;

    public SimPlayer? Player => Get(PlayerRole) as SimPlayer;

    internal void SetSlot(PartyRole role, ISimPartyMember slot)
    {
        slot.Role = role;
        slots[(int)role] = (SimCharacter)slot;
        if (slot is SimPlayer) PlayerRole = role;
    }

    internal void ForEachActive(Action<SimCharacter> action)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m && m.IsAlive()) action(m);
    }

    // Kills every alive member with the given cause. Raidwide wipe primitive
    // used by mechanics whose failure is unsurvivable.
    public void WipeAllPlayers(string cause)
        => ForEachActive(m => { if (m.IsAlive()) m.Die(cause); });

    // Raidwide knockback: pushes every active slot `distance` units away from
    // `source`. Each slot resolves its own direction from its current position.
    public void Knockback(Vector3 source, float distance)
        => ForEachActive(m => (m as ISimPartyMember)?.Knockback(source, distance));

    // Raidwide knockback resolved from KnockbackTable + Lumina Knockback sheet.
    // Resolves once at the party level so an unmapped action logs a single
    // warning rather than one per slot.
    public void Knockback(Vector3 source, uint knockbackId)
    {
        if (!KnockbackLookup.TryGet(knockbackId, out var distance, out var speed))
            return;
        ForEachActive(m => (m as ISimPartyMember)?.Knockback(source, distance, speed));
    }

    internal IEnumerable<SimCharacter> ActiveMembers()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m && m.IsAlive()) yield return m;
    }

    // All filled slots with their role index, alive or dead. Used by PartyFinder
    // proximity queries (which follow the same no-alive-filter contract the old
    // FindClosest* methods had).
    internal IEnumerable<(PartyRole role, SimCharacter member)> FilledSlots()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m) yield return ((PartyRole)i, m);
    }

    // Every filled slot, alive or dead. Used by the party HUD so dead members
    // keep their slot (HP=0 is the visible "dead" state) instead of being
    // silently dropped — and so other members don't shift up the list when
    // someone dies. Mechanic resolvers should keep using ActiveMembers.
    internal IEnumerable<SimCharacter> AllMembers()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m) yield return m;
    }

    public bool IsAlive
    {
        get
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] is { } m && m.IsAlive()) return true;
            return false;
        }
    }

    // The party persists for the whole scenario and is torn down only by Reset /
    // Despawn — a wipe (every member dead) must NOT reap it, so this is not wired
    // to IsAlive.
    public bool IsActive => true;

    public void Tick(float deltaSeconds)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] is null) continue;
            slots[i]!.Tick(deltaSeconds);
            // Reap a slot only if its member is fully gone (handle released).
            // A KO'd doppel keeps IsActive == true, so this is a safety net,
            // not the normal-path death behaviour.
            if (!slots[i]!.IsActive)
            {
                slots[i]!.Despawn();
                slots[i] = null;
            }
        }
    }

    public void Despawn()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i]?.Despawn();
            slots[i] = null;
        }
    }

    public SimCharacter? GetRandom()
    {
        return Get(rnd.Next(8));
    }
}
