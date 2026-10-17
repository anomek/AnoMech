using System;
using System.Numerics;

namespace AnoMech.Core.SimObjects;

// What UWU P1-P3 and Ultimate Annihilation still call by hand.
public abstract partial class SimCharacter
{
    private const float DefaultKnockbackSpeed = 25f;

    // Straight away from `source`, `distance` yalms at `speed` y/s, as a Knockback row describes.
    public void Knockback(Vector3 source, float distance, float speed = DefaultKnockbackSpeed)
        => Push(source, distance, distance / MathF.Max(speed, 0.01f));

    internal void SendHealth(uint health) => SendStatusList(health, Proxy?.Mana ?? 0);
}
