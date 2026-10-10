using System.Collections.Generic;

namespace AnoMech.Core.Native.Interfaces;

// The local player's enmity share on one enemy, 0-100.
public readonly record struct Hater(uint EntityId, byte Enmity);

// The server's Hater packet: the enemies that have the local player on their enmity list.
// The game fills UIState.Hater from it, and the _EnemyList addon (names, HP, cast bars) from that.
public interface IHaterList
{
    // Holds 32 entries; the rest are dropped.
    void Receive(IReadOnlyList<Hater> haters);
}
