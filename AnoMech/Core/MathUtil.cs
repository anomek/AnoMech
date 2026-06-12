using System;

namespace AnoMech.Core;

internal static class MathUtil
{
    // Wraps any rotation into the half-open range (-π, +π]. The game stores
    // facings unbounded; some math (atan2, accumulated rotates from MoveTo)
    // can drift outside that band and trip animation/state code that assumes
    // it. Normalize on every write rather than relying on every caller.
    public static float NormalizeRotation(float r)
    {
        r = MathF.IEEERemainder(r, 2f * MathF.PI);
        if (r <= -MathF.PI) r = MathF.PI;
        return r;
    }

    public static ushort QuantizeRotation(float degrees)
    {
        return (ushort)((degrees + MathF.PI) / (2 * MathF.PI) * ushort.MaxValue);
    }

    public static ushort QuantizePosition(float value)
    {
        return (ushort)((value + 1000) * 100 * 0.32767f);
    }
}
