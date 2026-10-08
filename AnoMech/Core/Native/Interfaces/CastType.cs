namespace AnoMech.Core.Native.Interfaces;

// The Action sheet's CastType: the shape of an action's area.
// The *2 shapes reach further than the sheet's range by the caster's hitbox radius
public enum CastType : byte
{
    None = 0,
    SingleTarget = 1,
    Circle = 2,
    Cone2 = 3,
    Rectangle2 = 4,
    Circle2 = 5,
    // defaults to Circle, should be overriden
    Custom = 6,
    GroundCircle = 7,
    Charge = 8,
    Donut = 10,
    Cross = 11,
    Rectangle = 12,
    Cone = 13,
}

public static class CastTypeExtensions
{
    public static bool AddsCasterHitbox(this CastType castType)
        => castType is CastType.Circle2 or CastType.Cone2 or CastType.Rectangle2;
}
