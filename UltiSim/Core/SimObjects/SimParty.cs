using System.Collections.Generic;

namespace UltiSim.Core.SimObjects;

// Fixed-size 8-slot party indexed by PartyRole. Each slot holds either a
// SimPartyMember (spawned NPC) or the SimPlayer (the player's own role).
// Get(role) returns the SimPartySlot at the slot or null when unfilled.
//
// Owns its own PartyHud mirror: any slot mutation (SetSlot) flips a dirty
// flag, and the next Tick refreshes the HUD. Despawn clears the HUD
// directly. Dispose unregisters the AddonLifecycle listener for plugin teardown.
public sealed class SimParty : ISimObject
{
    private readonly SimPartySlot?[] slots = new SimPartySlot?[8];
    private readonly PartyHud partyHud = new();
    private readonly CharacterFind<SimPartySlot> finder;

    public SimParty() { finder = new CharacterFind<SimPartySlot>(ActiveMembers); }

    public CharacterFind<SimPartySlot> Find => finder;

    public SimPartySlot? Get(int roleId)
        => roleId >= 0 && roleId < slots.Length ? slots[roleId] : null;

    public SimPartySlot? Get(PartyRole role) => Get((int)role);

    // The role the local player fills — i.e. the slot SimPartyMember spawning
    // skipped (PartyPresets returns null for the player's own job). After Game
    // wires SimPlayer in, that slot is the SimPlayer reference. Falls back to
    // MainTank if no slot is occupied by a SimPlayer (no scenario hits this today).
    public PartyRole PlayerRole
    {
        get
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] is SimPlayer) return (PartyRole)i;
            return PartyRole.MainTank;
        }
    }

    internal void SetSlot(PartyRole role, SimPartySlot slot)
    {
        slot.Role = role;
        slots[(int)role] = slot;
    }

    internal IEnumerable<SimPartySlot> ActiveMembers()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { IsAlive: true } m) yield return m;
    }

    // All filled slots with their role index, alive or dead. Used by PartyFinder
    // proximity queries (which follow the same no-alive-filter contract the old
    // FindClosest* methods had).
    internal IEnumerable<(PartyRole role, SimPartySlot member)> FilledSlots()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m) yield return ((PartyRole)i, m);
    }

    // Every filled slot, alive or dead. Used by the party HUD so dead members
    // keep their slot (HP=0 is the visible "dead" state) instead of being
    // silently dropped — and so other members don't shift up the list when
    // someone dies. Mechanic resolvers should keep using ActiveMembers.
    internal IEnumerable<SimPartySlot> AllMembers()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m) yield return m;
    }

    public bool IsAlive
    {
        get
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] is { IsAlive: true }) return true;
            return false;
        }
    }

    public void Tick(float deltaSeconds)
    {
        // Skip the SimPlayer slot — SimWorld.Tick ticks the player directly so
        // it stays alive even when no scenario has wired the player into the
        // party. Ticking it here too would double-tick our overlays (statuses,
        // VFX expiry) every frame.
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] is null or SimPlayer) continue;
            slots[i]!.Tick(deltaSeconds);
        }
        // Refresh every tick: per-member state (HP=0 on death, HP changes if we
        // ever simulate damage) needs to propagate without us tracking every
        // mutation. Refresh internally early-outs when there's nothing to write.
        partyHud.Refresh(this);
    }

    public void Despawn()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i]?.Despawn();
            slots[i] = null;
        }
        partyHud.Clear();
    }
}
