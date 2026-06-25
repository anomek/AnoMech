namespace AnoMech.Scenarios.Umad.P3BlackHole;

// User-controlled overrides for UmadP3BlackHoleState's randomized fields. Bound by
// the scenario's settings UI; null/default values leave the field randomized at
// scenario start. The state ctor consumes this directly.
// See UmadP4KefkaSaysStateOverrides for the canonical shape.
public sealed class UmadP3BlackHoleStateOverrides
{
    public int? LineNumber { get; set; }            // null = random; 1/2/3 = First/Second/Third in line (forces the player into that slot)
    public bool? Accretion { get; set; }            // null = random; true = give the player Accretion, false = keep it off them.
                                                    //   Yes is ignored for tanks and third-in-line (they never get Accretion in the fight).
    public uint? FirstSlap { get; set; }            // null = random; else ActionId.SlapHappy_Left / .SlapHappy_Right (debug-only UI)
    public bool? FirstSlapAllOnPlayer { get; set; } // null = random targets; true = aim every first-slap cone at the player (debug-only UI)
}
