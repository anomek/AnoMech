using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P3UltimateRelativity;

// Short fire: two DPS and a support; medium: a DPS and a support; long: two supports and a DPS.
// Either the long DPS or the short support holds Dark Blizzard instead of fire.
public enum RelativityDebuff { ShortDps, ShortSupport, MediumDps, MediumSupport, LongSupport, LongDps }

// Three waves of fire and Unholy Darkness, each followed by the hourglasses of that wave
// lasering their nearest player and sweeping. Short and medium players save their Return after
// the first sweep, long players after the second; everyone is then stunned, rewound, and resolves
// Shadoweye (long), Dark Eruption (short, medium support) or Dark Water (medium DPS).
public sealed class FruP3UltimateRelativityState
{
    public static float FireAt(int wave) => wave switch { 0 => 22.8f, 1 => 32.8f, _ => 42.8f };
    public static float LaserStartAt(int wave) => wave switch { 0 => 23.9f, 1 => 34f, _ => 44.95f };
    public static float HourglassFlowAt(int wave) => wave switch { 0 => 21.8f, 1 => 26.9f, _ => 31.8f };
    public static float LaserHitAt(int wave, int shot) => (wave switch { 0 => 27.95f, 1 => 38f, _ => 49f }) + (shot == 0 ? 0f : 2.04f + (shot - 1) * 1.075f);
    public const int LaserShots = 10;
    public static float ReturnAt(bool earlyReturn) => earlyReturn ? 27.8f : 37.8f;
    public const float AssignAt = 11.8f;
    public const float StunAt = 51.75f;
    public const float RewindAt = 52.83f;
    public const float FinalAt = 54.8f;
    public const float StunEndAt = 55.8f;

    private readonly Dictionary<PartyRole, RelativityDebuff> debuffs = new();
    private readonly PartyRole[] darkness = new PartyRole[3];

    public bool DpsIce { get; }
    // Eighths of a turn the hourglass layout is turned from its base.
    public int North { get; }
    public int ClockwiseMask { get; }
    public PartyRole ShellCrusherTarget { get; }

    public FruP3UltimateRelativityState(Rng rng, FruP3UltimateRelativityStateOverrides overrides)
    {
        var dps = rng.Shuffle(PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps);
        var supports = rng.Shuffle(PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer);
        debuffs[dps[0]] = RelativityDebuff.LongDps;
        debuffs[dps[1]] = RelativityDebuff.MediumDps;
        debuffs[dps[2]] = RelativityDebuff.ShortDps;
        debuffs[dps[3]] = RelativityDebuff.ShortDps;
        debuffs[supports[0]] = RelativityDebuff.ShortSupport;
        debuffs[supports[1]] = RelativityDebuff.MediumSupport;
        debuffs[supports[2]] = RelativityDebuff.LongSupport;
        debuffs[supports[3]] = RelativityDebuff.LongSupport;
        if (overrides.Debuffs is { } forced)
            foreach (var (role, debuff) in forced) debuffs[role] = debuff;
        DpsIce = overrides.DpsIce ?? rng.NextBool();
        North = overrides.North ?? rng.Next(8);
        ClockwiseMask = overrides.ClockwiseMask ?? rng.Next(256);
        for (var wave = 0; wave < 3; wave++)
        {
            var w = wave;
            var valid = Enum.GetValues<PartyRole>().Where(role => CanTakeDarkness(role, w) && !darkness.Take(w).Contains(role)).ToArray();
            darkness[wave] = overrides.Darkness?[wave] ?? valid[rng.Next(valid.Length)];
        }
        ShellCrusherTarget = overrides.ShellCrusherTarget ?? rng.NextRole();
    }

    public RelativityDebuff Debuff(PartyRole role) => debuffs[role];

    public PartyRole IceHolder => debuffs.First(d => d.Value == (DpsIce ? RelativityDebuff.LongDps : RelativityDebuff.ShortSupport)).Key;

    public PartyRole Darkness(int wave) => darkness[wave];

    public static int FireWave(RelativityDebuff debuff) => debuff switch
    {
        RelativityDebuff.ShortDps or RelativityDebuff.ShortSupport => 0,
        RelativityDebuff.MediumDps or RelativityDebuff.MediumSupport => 1,
        _ => 2,
    };

    public bool HasFire(PartyRole role, int wave) => role != IceHolder && FireWave(Debuff(role)) == wave;

    public bool EarlyReturn(PartyRole role) => FireWave(Debuff(role)) < 2;
    public bool Shadoweye(PartyRole role) => !EarlyReturn(role);
    public bool DarkWater(PartyRole role) => Debuff(role) == RelativityDebuff.MediumDps;
    public bool DarkEruption(PartyRole role) => EarlyReturn(role) && !DarkWater(role);

    private bool CanTakeDarkness(PartyRole role, int wave) => FireWave(Debuff(role)) != wave;

    // Clock 0 = north before the layout's turn; the hourglasses of each wave sit at fixed clocks.
    public static int Wave(int clock) => clock switch { 7 or 1 or 4 => 0, 5 or 3 or 0 => 1, _ => 2 };
    public bool Clockwise(int clock) => (ClockwiseMask & (1 << clock)) != 0;

    public Vector3 Direction(int clock)
        => new(MathF.Sin((clock + North) * MathF.PI / 4), 0, -MathF.Cos((clock + North) * MathF.PI / 4));

    public Vector3 Hourglass(int clock) => Direction(clock) * 9.5f;
    public byte HourglassSlot(int clock) => (byte)(FruConstants.MapEffect.RelativityFirstHourglass + (clock + North) % 8);
}
