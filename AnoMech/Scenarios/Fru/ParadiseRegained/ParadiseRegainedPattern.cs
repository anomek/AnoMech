using System;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.ParadiseRegained;

// FRU-Sim's three rotations, either tower order, and either wing order.
// Coordinates and hit shapes use game yalms, not the reference's visual scale.
internal sealed class ParadiseRegainedPattern
{
    public const float RunSpeed = 6f;
    public const float TowerRadius = 3f;
    public const float BusterRadius = 4f;
    public const float CleaveRange = 100f;
    public const float CleaveHalfAngle = 2f * MathF.PI / 3f;
    public const float FirstHit = 15.7f;
    public const float SecondHit = 19.2f;
    public const float ThirdHit = 22.7f;

    public int Rotation { get; }
    public bool EastSecond { get; }
    public bool DarkFirst { get; }

    public ParadiseRegainedPattern(int rotation, bool eastSecond, bool darkFirst)
    {
        if (rotation is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(rotation));
        Rotation = rotation;
        EastSecond = eastSecond;
        DarkFirst = darkFirst;
    }

    public bool IsDark(int hit) => hit == 0 ? DarkFirst : !DarkFirst;
    public static PartyRole CleaveTank(int hit) => hit == 0 ? PartyRole.MainTank : PartyRole.OffTank;
    public static PartyRole BusterTank(int hit) => hit == 0 ? PartyRole.OffTank : PartyRole.MainTank;
    public static float CleaveOffset(bool dark) => (dark ? -1f : 1f) * MathF.PI / 3f;
    public static float TowerSpawnTime(int tower) => 6f + 3.5f * tower;
    public static float TowerHitTime(int tower) => FirstHit + 3.5f * tower;

    private int TowerIndex(int tower)
    {
        if (tower is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(tower));
        var index = tower == 0 ? 0 : ((tower == 1) == EastSecond ? 2 : 1);
        return (index + Rotation) % 3;
    }

    // ContentDirectorManagedSG 181: 51=NW, 52=NE, 53=S.
    public byte TowerSlot(int tower) => TowerIndex(tower) switch { 1 => 51, 2 => 52, _ => 53 };
    public Vector3 TowerPosition(int tower) => TowerIndex(tower) switch
    {
        1 => new(-6.0622f, 0f, -3.5f),
        2 => new(6.0622f, 0f, -3.5f),
        _ => new(0f, 0f, 7f),
    };

    public Vector3 Rotate(Vector3 position)
    {
        var angle = -Rotation * 2f * MathF.PI / 3f;
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        return new(position.X * cos + position.Z * sin, position.Y, position.Z * cos - position.X * sin);
    }

    // Healers take the first tower. M1/R1 take relative NW, M2/R2 NE.
    // The native tower's smaller radius requires positions inside its actual
    // ring; the Godot scene's larger placeholder rings are not used here.
    public Vector3 Position(PartyRole role, int stage)
    {
        Vector3 position;
        if (stage == 0)
            return new(((int)role - 3.5f) * 0.35f, 0f, 7f);
        if (stage == 1)
        {
            position = role switch
            {
                PartyRole.MainTank => new(DarkFirst ? -6.0622f : 6.0622f, 0f, -3.5f),
                PartyRole.OffTank => DarkFirst ? new(0f, 0f, 1f) : new(10f, 0f, 10f),
                PartyRole.RegenHealer => new(-0.65f, 0f, 7f),
                PartyRole.ShieldHealer => new(0.65f, 0f, 7f),
                PartyRole.MeleeDpsA => new(-3.5f, 0f, 9.5f),
                PartyRole.PhysRangedDps => new(-3.8f, 0f, 9.7f),
                PartyRole.MeleeDpsB => new(3.5f, 0f, 9.5f),
                _ => new(3.8f, 0f, 9.7f),
            };
        }
        else
        {
            position = role switch
            {
                PartyRole.MainTank => new(0f, 0f, DarkFirst ? -14f : -1f),
                PartyRole.OffTank => new(DarkFirst ? -6.0622f : 6.0622f, 0f, 3.5f),
                PartyRole.RegenHealer => new(-0.65f, 0f, -7.5f),
                PartyRole.ShieldHealer => new(0.65f, 0f, -7.5f),
                PartyRole.MeleeDpsA => new(-5f, 0f, -4.5f),
                PartyRole.PhysRangedDps => new(-5.2f, 0f, -4.7f),
                PartyRole.MeleeDpsB => new(5f, 0f, -4.5f),
                _ => new(5.2f, 0f, -4.7f),
            };
        }
        return Rotate(position);
    }
}
