namespace AnoMech.Core.EnemyActions;

// Unique also covers darkness and typeless hits: the game cuts them only by mitigation that reduces
// all damage, and shows them with the same flytext icon.
public enum DamageKind { Physical, Magic, Unique }

public static class DamageKinds
{
    // From the Action sheet's AttackType: 1-4 slashing/piercing/blunt/shot, 5 magic, 6 darkness.
    public static DamageKind FromAttackType(sbyte attackType) => attackType switch
    {
        >= 1 and <= 4 => DamageKind.Physical,
        5 => DamageKind.Magic,
        _ => DamageKind.Unique,
    };

    public static FlyTextIcon Icon(this DamageKind kind) => kind switch
    {
        DamageKind.Physical => FlyTextIcon.Physical,
        DamageKind.Magic => FlyTextIcon.Magic,
        _ => FlyTextIcon.Unique,
    };
}
