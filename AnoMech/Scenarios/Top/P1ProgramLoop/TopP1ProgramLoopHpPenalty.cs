using System;
using System.Collections.Generic;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P1ProgramLoop;

// A status that caps max HP at a share of the full pool while it lasts (TOP's HP Penalty). The
// server enforces the cap in the real fight; here the status's presence drives it. Max HP comes
// back when the status ends, the HP the cap took does not.
public sealed class TopP1ProgramLoopHpPenalty(ushort statusId, float remainingShare)
{
    private readonly Dictionary<SimCharacter, uint> fullMaxHealth = new();

    public void Tick(SimParty party)
    {
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (party.Get(role) is not { Proxy.Exists: true } member) continue;
            var capped = fullMaxHealth.TryGetValue(member, out var full);
            if (member.HasStatus(statusId))
            {
                if (capped) continue;
                full = member.MaxHealth;
                fullMaxHealth[member] = full;
                member.SetMaxHealth(Math.Max(1u, (uint)(full * remainingShare)));
            }
            else if (capped)
            {
                fullMaxHealth.Remove(member);
                member.SetMaxHealth(full);
            }
        }
    }

    // What a hit is sized off: the pool the cap came from, not the cap.
    public uint FullMaxHealth(SimCharacter member)
        => fullMaxHealth.TryGetValue(member, out var full) ? full : member.MaxHealth;
}
