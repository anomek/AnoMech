using System.Collections.Generic;

namespace AnoMech.Core.Map;

// What a duty's server sends the client on entering it. Every client adds Rsv/Rsf before the zone load.
// MapEffectSnapshot is every slot's resting state (the same at zone-in and every wipe reset), sent
// at the start of every run merged with IPhase.ArenaMapEffectOverrides; slots past its end are 0,
// and empty = the zone has no MapEffect slots.
public sealed record DutyInitData(
    EntranceBarrier EntranceBarrier,
    HelperPool HelperPool,
    IReadOnlyList<ushort> MapEffectSnapshot,
    IReadOnlyList<byte[]> Rsv,
    IReadOnlyList<byte[]> Rsf);
