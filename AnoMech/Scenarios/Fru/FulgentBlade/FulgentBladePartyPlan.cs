using System;
using System.Numerics;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru.FulgentBlade;

// Game-independent destinations and timing, shared with the regression checks.
internal sealed class FulgentBladePartyPlan(FulgentBladePattern pattern)
{
    public const float RunSpeed = 6f;
    public const float MainTankSetupTime = 8.5f;
    public const float PrepositionTime = 15.5f;
    public const float AkhMornCastTime = 28.5f;
    public const float StackTime = 33.5f;
    public const int DodgeCount = 6;
    public const int MainTankAimStep = 3;

    private const float TankAdjustmentLimit = 1.5f;
    private const float WaveClearance = 0.25f;

    // Face toward the cardinal dodge pocket, rather than choosing a fixed north.
    public Vector3 TankFacing => Vector3.Normalize(pattern.Origin);
    public Vector3 MainTankSetup => TankFacing * 6f;

    public Vector3 Preposition => pattern.Origin;
    public Vector3 Dodge(int step) => pattern.DodgePosition(step);

    public Vector3 MainTankDodge(int step, Vector3 bossPosition)
    {
        var dodge = Dodge(step);
        if (step != MainTankAimStep) return dodge;

        // Move the tank toward the cardinal ray from Pandora before her cast
        // locks facing. Keep the ordinary route when the boss is beyond the pocket.
        var forwardDistance = Vector3.Dot(dodge - bossPosition, TankFacing);
        if (forwardDistance < 1f) return dodge;
        var aligned = bossPosition + TankFacing * forwardDistance;
        aligned.Y = dodge.Y;
        var adjustment = aligned - dodge;
        if (adjustment.LengthSquared() > TankAdjustmentLimit * TankAdjustmentLimit)
            aligned = dodge + Vector3.Normalize(adjustment) * TankAdjustmentLimit;

        // Exact alignment can cross a live strip. Search back toward the known
        // safe dodge, leaving a quarter-yalm margin at the 28.5s snapshot.
        for (var sample = 32; sample > 0; sample--)
        {
            var candidate = Vector3.Lerp(dodge, aligned, sample / 32f);
            if (SafeAtAkhMornCast(candidate)) return candidate;
        }
        return dodge;
    }

    private bool SafeAtAkhMornCast(Vector3 position)
    {
        for (var group = 0; group < FulgentBladePattern.GroupCount; group++)
        for (var wave = 0; wave < FulgentBladePattern.WavesPerGroup; wave++)
        {
            // At cast start, the three trains snapshot hits 4, 2, and 0.
            var strip = pattern.Wave(group, wave, 4 - group * 2);
            var delta = position - strip.Position;
            var depth = delta.X * MathF.Sin(strip.Rotation) + delta.Z * MathF.Cos(strip.Rotation);
            var width = delta.X * MathF.Cos(strip.Rotation) - delta.Z * MathF.Sin(strip.Rotation);
            if (depth >= -WaveClearance && depth <= Geometry.ExalineStep + WaveClearance
                && MathF.Abs(width) <= Geometry.ExalineHalfWidth + WaveClearance)
                return false;
        }
        return position.LengthSquared() < Geometry.ArenaRadius * Geometry.ArenaRadius;
    }

    public static float DodgeTime(int step) => step switch
    {
        0 => 19.5f,
        >= 1 and < DodgeCount => 21.5f + step * 2f,
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    public (Vector3 Left, Vector3 Right) Stacks(Vector3 bossPosition, float bossRotation)
    {
        // FRU-Sim's two Akh Morn spots: 14.6 units toward the pattern's
        // starting edge and 9.6 units to either side. They lie inside the
        // cleared central space once the six dodges are complete.
        var outward = Vector3.Normalize(pattern.Origin);
        var tangent = new Vector3(outward.Z, 0f, -outward.X);
        var center = outward * (14.6f * Geometry.ReferenceScale);
        var offset = tangent * (9.6f * Geometry.ReferenceScale);
        var a = center - offset;
        var b = center + offset;
        var bossRight = new Vector3(MathF.Cos(bossRotation), 0f, -MathF.Sin(bossRotation));
        // Label by Pandora's actual facing, not by the randomized arena corner.
        return Vector3.Dot(a - bossPosition, bossRight) <= Vector3.Dot(b - bossPosition, bossRight)
            ? (a, b) : (b, a);
    }

    // MT/H1/M1/R1 left; OT/H2/M2/R2 right. PartyRole has this paired ordering.
    public static bool UsesLeftStack(int role) => role % 2 == 0;
}
