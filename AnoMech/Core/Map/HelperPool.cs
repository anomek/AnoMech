namespace AnoMech.Core.Map;

// The invisible helpers a duty spawns at every pull's start and casts its AoEs from, always the same
// count of one generic BNpcBase. Count 0 = the zone has no pool.
public readonly record struct HelperPool(uint BNpcBaseId, int Count);
