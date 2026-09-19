using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.CrystallizeTime;

internal enum CrystallizeTimeAssignment { AeroWest, IceWest, Eruption, Ice, Darkness, Water, AeroEast, IceEast }
internal enum CrystallizeTimeCorner { NW, NE, SE, SW }
internal enum CrystallizeTimePlayerPattern { Random, RedIce, RedAero, BlueDark, BlueStack }

internal sealed class CrystallizeTimePattern
{
    public const float ReferenceScale = 10f / 23.74f;
    public const float RunSpeed = 6f;
    public bool SlowNorthwest { get; }
    public CrystallizeTimeCorner Corner { get; }
    public bool East => Corner is CrystallizeTimeCorner.NE or CrystallizeTimeCorner.SE;
    public bool North => Corner is CrystallizeTimeCorner.NW or CrystallizeTimeCorner.NE;
    public PartyRole[] Roles { get; }
    public PartyRole[] Quietus { get; }
    public PartyRole JumpTarget { get; }

    public CrystallizeTimePattern(bool slowNorthwest, CrystallizeTimeCorner corner, PartyRole[] roles, PartyRole[] quietus, PartyRole jumpTarget)
    {
        if (roles.Length != 8 || roles.Distinct().Count() != 8) throw new ArgumentException("CT needs all eight distinct roles.");
        SlowNorthwest = slowNorthwest;
        Corner = corner;
        Roles = (PartyRole[])roles.Clone();
        Quietus = (PartyRole[])quietus.Clone();
        JumpTarget = jumpTarget;
    }

    public static CrystallizeTimePattern Randomize(Random random, bool mur, PartyRole? playerRole = null,
        CrystallizeTimePlayerPattern playerPattern = CrystallizeTimePlayerPattern.Random)
    {
        var roles = Enum.GetValues<PartyRole>();
        random.Shuffle(roles);
        if (playerRole is { } player && playerPattern != CrystallizeTimePlayerPattern.Random)
        {
            CrystallizeTimeAssignment[] choices = playerPattern switch {
                CrystallizeTimePlayerPattern.RedIce => [CrystallizeTimeAssignment.IceWest, CrystallizeTimeAssignment.IceEast],
                CrystallizeTimePlayerPattern.RedAero => [CrystallizeTimeAssignment.AeroWest, CrystallizeTimeAssignment.AeroEast],
                CrystallizeTimePlayerPattern.BlueDark => [CrystallizeTimeAssignment.Darkness],
                CrystallizeTimePlayerPattern.BlueStack => [CrystallizeTimeAssignment.Ice, CrystallizeTimeAssignment.Water, CrystallizeTimeAssignment.Eruption],
                _ => throw new ArgumentOutOfRangeException(nameof(playerPattern)),
            };
            var current = Array.IndexOf(roles, player);
            if (current < 0) throw new ArgumentOutOfRangeException(nameof(playerRole));
            var chosen = (int)choices[random.Next(choices.Length)];
            (roles[current], roles[chosen]) = (roles[chosen], roles[current]);
        }
        // Apply side priority after fixing the player's debuff category. A red
        // selection chooses Ice/Aero, not a side that could contradict NA priority.
        PartyRole[] priority = mur
            ? [PartyRole.RegenHealer, PartyRole.PhysRangedDps, PartyRole.MeleeDpsA, PartyRole.MainTank,
                PartyRole.OffTank, PartyRole.MeleeDpsB, PartyRole.CasterDps, PartyRole.ShieldHealer]
            : [PartyRole.ShieldHealer, PartyRole.RegenHealer, PartyRole.OffTank, PartyRole.MainTank,
                PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];
        foreach (var (west, east) in new[] { (0, 6), (1, 7) })
            if (Array.IndexOf(priority, roles[west]) > Array.IndexOf(priority, roles[east]))
                (roles[west], roles[east]) = (roles[east], roles[west]);
        var quietus = Enum.GetValues<PartyRole>();
        random.Shuffle(quietus);
        return new(random.Next(2) == 0, (CrystallizeTimeCorner)random.Next(4), roles, quietus[..3], (PartyRole)random.Next(8));
    }

    public PartyRole Role(CrystallizeTimeAssignment assignment) => Roles[(int)assignment];
    public CrystallizeTimeAssignment Assignment(PartyRole role) => (CrystallizeTimeAssignment)Array.IndexOf(Roles, role);
    public static Vector3 Ref(float north, float east) => new(east * ReferenceScale, 0, -north * ReferenceScale);
    // Native CT layout slots 34..39: N, NE, SE, S, SW, NW at radius eleven.
    public static Vector3 Hourglass(int index)
    {
        var angle = index * MathF.PI / 3f;
        return new(11f * MathF.Sin(angle), 0, -11f * MathF.Cos(angle));
    }
    public int[] HourglassPair(int wave) => wave == 0 ? [0, 3]
        : (wave == 2) == SlowNorthwest ? [5, 2] : [1, 4];

    public Vector3 WaveOrigin(bool second, int step)
        => second ? new(0, 0, (North ? -1 : 1) * (20 - 10 * step))
            : new((East ? 1 : -1) * (20 - 10 * step), 0, 0);
    public Vector3 WaveDirection(bool second)
        => second ? new(0, 0, North ? 1 : -1) : new(East ? -1 : 1, 0, 0);
    public static bool InWave(Vector3 point, Vector3 origin, Vector3 direction)
    {
        var offset = point - origin;
        var forward = Vector3.Dot(offset, direction);
        var side = offset.X * direction.Z - offset.Z * direction.X;
        return forward >= 0 && forward <= 10 && MathF.Abs(side) <= 20;
    }

    public Vector3 Rewind(PartyRole role)
    {
        var group1 = role is PartyRole.MainTank or PartyRole.RegenHealer or PartyRole.MeleeDpsA or PartyRole.PhysRangedDps;
        var tank = role is PartyRole.MainTank or PartyRole.OffTank;
        // The leading axis alternates around the corners. Exactly one tank and
        // its light party are ahead on each axis, so the two first-four sets differ.
        var leadEast = Corner is CrystallizeTimeCorner.NW or CrystallizeTimeCorner.SE ? group1 : !group1;
        var front = tank ? 22.7f : 20.5f;
        var back = tank ? 18.2f : 15.4f;
        return Ref((North ? 1 : -1) * (leadEast ? back : front), (East ? 1 : -1) * (leadEast ? front : back));
    }

    public static Vector3 AkhMorn(PartyRole role)
        => role == PartyRole.MainTank ? new(-9.3f, 0, 0) : Vector3.Zero;

    public IEnumerable<(float Time, IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> Route)> Routes()
    {
        yield return (14, SlowNorthwest ? CrystallizeTimeRoutes.PRE_HG_1_NW : CrystallizeTimeRoutes.PRE_HG_1_NE);
        yield return (18, SlowNorthwest ? CrystallizeTimeRoutes.POST_HG_1_NW : CrystallizeTimeRoutes.POST_HG_1_NE);
        yield return (20.7f, CrystallizeTimeRoutes.PUDDLE_DODGE);
        yield return (21.4f, SlowNorthwest ? CrystallizeTimeRoutes.POST_KB_NW : CrystallizeTimeRoutes.POST_KB_NE);
        yield return (23.2f, SlowNorthwest ? CrystallizeTimeRoutes.POST_HG_2_NW : CrystallizeTimeRoutes.POST_HG_2_NE);
        yield return (24.5f, East ? CrystallizeTimeRoutes.POST_UD_E : CrystallizeTimeRoutes.POST_UD_W);
        yield return (26.2f, East ? CrystallizeTimeRoutes.POST_EARLY_SOAK_E : CrystallizeTimeRoutes.POST_EARLY_SOAK_W);
        yield return (28.3f, East ? CrystallizeTimeRoutes.POST_HG_3_E : CrystallizeTimeRoutes.POST_HG_3_W);
        yield return (29.3f, East ? CrystallizeTimeRoutes.POST_EXA_2_E : CrystallizeTimeRoutes.POST_EXA_2_W);
        yield return (31.2f, Corner switch {
            CrystallizeTimeCorner.NW => CrystallizeTimeRoutes.POST_EXA_3_NW, CrystallizeTimeCorner.NE => CrystallizeTimeRoutes.POST_EXA_3_NE,
            CrystallizeTimeCorner.SE => CrystallizeTimeRoutes.POST_EXA_3_SE, _ => CrystallizeTimeRoutes.POST_EXA_3_SW });
        yield return (33.6f, Corner switch {
            CrystallizeTimeCorner.NW => CrystallizeTimeRoutes.POST_EXA_4_NW, CrystallizeTimeCorner.NE => CrystallizeTimeRoutes.POST_EXA_4_NE,
            CrystallizeTimeCorner.SE => CrystallizeTimeRoutes.POST_EXA_4_SE, _ => CrystallizeTimeRoutes.POST_EXA_4_SW });
    }
    public IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> AfterCleanse => Corner switch {
        CrystallizeTimeCorner.NW => CrystallizeTimeRoutes.POST_SOAK_TARGET_NW, CrystallizeTimeCorner.NE => CrystallizeTimeRoutes.POST_SOAK_TARGET_NE,
        CrystallizeTimeCorner.SE => CrystallizeTimeRoutes.POST_SOAK_TARGET_SE, _ => CrystallizeTimeRoutes.POST_SOAK_TARGET_SW };
}
