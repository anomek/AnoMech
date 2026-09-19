using System;
using System.Numerics;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru.FulgentBlade;

// FRU-Sim's exawave_controller.tscn and exawave.tscn, converted to game units.
internal sealed class FulgentBladePattern(int positionIndex, int rotationIndex, bool eastFirst)
{
    public const int GroupCount = 3;
    public const int WavesPerGroup = 4;
    public const int HitCount = 7;

    private readonly Vector3 root = (positionIndex switch
    {
        0 => new Vector3(0f, 0f, 17f),
        1 => new Vector3(0f, 0f, -17f),
        2 => new Vector3(17f, 0f, 0f),
        _ => new Vector3(-17f, 0f, 0f),
    }) * Geometry.ReferenceScale;
    private readonly float rotation = rotationIndex * MathF.PI / 2f;

    public Vector3 Origin => root;

    public Vector3 DodgePosition(int step)
    {
        var offset = step switch
        {
            0 => new Vector3(1.15f, 0f, 2.77f) * (eastFirst ? -1f : 1f),
            1 or 4 => new Vector3(1.15f, 0f, 2.77f) * (eastFirst ? 1f : -1f),
            2 => new Vector3(-2.77f, 0f, 1.15f),
            3 => new Vector3(2.77f, 0f, -1.15f),
            5 => new Vector3(1.15f, 0f, 2.77f) * (eastFirst ? -1f : 1f),
            _ => throw new ArgumentOutOfRangeException(nameof(step)),
        };
        return root + Vector3.Transform(offset * Geometry.ReferenceScale,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation));
    }

    public (Vector3 Position, float Rotation) ArrowWarning(int group, int line)
    {
        var dark = Wave(group, line * 2);
        // b3560omn02 is authored along Z, with purple arrows traveling +X
        // and gold arrows -X. Our strips extend along local X and travel +Z.
        return (dark.Position, dark.Rotation - MathF.PI / 2f);
    }

    public (Vector3 Position, float Rotation, bool IsLight) Wave(int group, int wave, int hit = 0)
    {
        var emitter = group == 1 ? 2 : (group == 0) == eastFirst ? 0 : 1;
        var (position, yaw) = emitter switch
        {
            0 => (new Vector3(9.821f, 0f, 23.710f), 0f),
            1 => (new Vector3(-9.821f, 0f, -23.710f), MathF.PI),
            _ => (new Vector3(23.710f, 0f, -9.821f), MathF.PI / 2f),
        };
        var localYaw = wave switch
        {
            0 => 0f,
            1 => MathF.PI,
            2 => 5f * MathF.PI / 4f,
            _ => MathF.PI / 4f,
        };
        // TSCN serializes basis rows (not the columns exposed by Godot's API).
        // Its +45-degree CD basis and root yaw both send +Z toward +X.
        var origin = root + Vector3.Transform(position * Geometry.ReferenceScale,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation));
        var facing = rotation + yaw + localYaw + MathF.PI;
        var forward = new Vector3(MathF.Sin(facing), 0f, MathF.Cos(facing));
        return (origin + forward * (hit * Geometry.ExalineStep), facing, wave % 2 == 1);
    }
}
