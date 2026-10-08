using System.Collections.Generic;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.UserActions;

// What one status contributes to surviving a hit. Everything is a fraction (0.20f = 20%)
// except ShieldPotency; a status sets only what it grants. ShieldHp and MaxHp are fractions
// of the target's max HP.
public readonly record struct Mitigation(
    float Damage = 0f, float Magic = 0f, float Physical = 0f,
    float ShieldHp = 0f, float ShieldPotency = 0f, float MaxHp = 0f)
{
    // Rough: fitted to a tank's own shields. A healer's potency is worth more HP per point.
    public const float ShieldHpPerPotency = 0.00015f;

    private const float Invulnerable = 1f;

    // Off, no hit checks mitigation, so nothing a bot could press would change the outcome.
    public static bool Required => Plugin.Config.EnableTankMitigation && Natives.UserActions.Enabled;

    public bool IsShield => ShieldHp > 0f || ShieldPotency > 0f;

    private float ShieldFractionOfMaxHp => ShieldHp + ShieldPotency * ShieldHpPerPotency;

    public static readonly IReadOnlyDictionary<ushort, Mitigation> ByStatusId = new Dictionary<ushort, Mitigation>
    {
        // Tank role
        [1191] = new(Damage: 0.20f),   // Rampart
        [1193] = new(Damage: 0.10f),   // Reprisal (on the enemy)

        // PLD
        [82]   = new(Damage: Invulnerable),                   // Hallowed Ground
        [3829] = new(Damage: 0.40f),   // Guardian
        [3830] = new(ShieldPotency: 1000f),   // Guardian's Will
        [77]   = new(Damage: 0.20f),   // Bulwark (guaranteed block; the real reduction scales with the shield)
        [2674] = new(Damage: 0.15f),   // Holy Sheltron
        [2675] = new(Damage: 0.15f),   // Knight's Resolve
        [1174] = new(Damage: 0.10f),   // Intervention
        [1362] = new(ShieldHp: 0.10f), // Divine Veil
        [196]  = new(Damage: 0.80f),   // Last Bastion

        // WAR
        [409]  = new(Damage: Invulnerable),   // Holmgang
        [3832] = new(Damage: 0.40f),   // Damnation
        [87]   = new(MaxHp: 0.20f),    // Thrill of Battle
        [2678] = new(Damage: 0.10f),   // Bloodwhetting
        [2679] = new(Damage: 0.10f),   // Stem the Flow
        [2680] = new(ShieldPotency: 400f),    // Stem the Tide
        [1858] = new(Damage: 0.10f),   // Nascent Glint
        [1457] = new(ShieldHp: 0.15f), // Shake It Off
        [863]  = new(Damage: 0.80f),   // Land Waker

        // DRK
        [810]  = new(Damage: Invulnerable),   // Living Dead
        [3835] = new(Damage: 0.40f),   // Shadowed Vigil
        [746]  = new(Magic: 0.20f, Physical: 0.10f),   // Dark Mind
        [2682] = new(Damage: 0.10f),   // Oblation
        [1178] = new(ShieldHp: 0.25f), // The Blackest Night
        [1894] = new(Magic: 0.10f, Physical: 0.05f),   // Dark Missionary
        [864]  = new(Damage: 0.80f),   // Dark Force

        // GNB
        [1836] = new(Damage: Invulnerable),   // Superbolide
        [3838] = new(Damage: 0.40f, MaxHp: 0.20f),     // Great Nebula
        [1832] = new(Damage: 0.10f),   // Camouflage
        [2683] = new(Damage: 0.15f),   // Heart of Corundum
        [2684] = new(Damage: 0.15f),   // Clarity of Corundum
        [1839] = new(Magic: 0.10f, Physical: 0.05f),   // Heart of Light
        [1931] = new(Damage: 0.80f),   // Gunmetal Soul
    };

    // The single damage reduction that would let a full-HP target survive the same hit, 0..1:
    // 0.5 means a hit of twice its max HP is survivable. Percentage reductions stack
    // multiplicatively and shrink the hit; shields and bonus max HP add to the pool it lands on.
    public static float Effective(IEnumerable<ushort> statusIds, DamageKind kind)
    {
        var taken = 1f;
        var pool = 1f;
        foreach (var id in statusIds)
        {
            if (!ByStatusId.TryGetValue(id, out var m)) continue;
            taken *= (1f - m.Damage) * (1f - kind switch { DamageKind.Magic => m.Magic, DamageKind.Physical => m.Physical, _ => 0f });
            pool += m.ShieldFractionOfMaxHp + m.MaxHp;
        }
        return 1f - taken / pool;
    }

    // Banked shield as a fraction of max HP: what the gold overlay on the HP bar shows.
    public static float ShieldFraction(IEnumerable<ushort> statusIds)
    {
        var shield = 0f;
        foreach (var id in statusIds)
            if (ByStatusId.TryGetValue(id, out var m))
                shield += m.ShieldFractionOfMaxHp;
        return shield;
    }

    // A shield is used up by the hit it was counted against. A status carries either a
    // shield or a reduction, never both, so dropping the whole status loses nothing else.
    public static void SpendShields(SimCharacter target)
    {
        foreach (var status in target.ActiveStatusSnapshot)
            if (ByStatusId.TryGetValue(status.StatusId, out var m) && m.IsShield)
                target.RemoveStatus(status.StatusId);
    }
}
