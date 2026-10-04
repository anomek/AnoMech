using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

// Cosmo Dive 2's two melee LB3s go back to back; the enrage LB3 is one melee's second.
public enum MeleeLimitBreaks { First, FirstAndEnrage, Second, SecondAndEnrage }

public enum DiveSide { Left, Right }

public sealed class TopP6AlphaOmegaStateOverrides
{
    // --- Fight-wide: one roll the whole sim shares -------------------------------------
    public P6Practice Practice { get; set; }
    // null = random.
    public bool? FirstArrowInFirst { get; set; }
    public bool? SecondArrowInFirst { get; set; }
    // The written guide has the off tank invuln the first Wave Cannon's line and the main tank
    // the second.
    public bool MainTankInvulnsFirstWaveCannon { get; set; }
    // The tanks take turns on the tank LB3s: the first takes Cosmo Memory and the second Magic
    // Number, the other the first Magic Number.
    public bool OffTankLimitBreaksFirst { get; set; }
    // The guide's example call has M1 lead Cosmo Dive 2's melee LB3s and send the enrage one.
    public bool MeleeDpsBLimitBreaksFirst { get; set; }
    // The first healer takes the first Magic Number's healer LB3, the other the second's.
    public bool ShieldHealerLimitBreaksFirst { get; set; }
    public bool ShieldHealerGoesMiddle { get; set; }

    // --- Per player: everyone has their own ---------------------------------------------
    public PerRoleSetting<DiveSide> CosmoDiveSide { get; set; } = new();
    public PerRoleSetting<MeleeLimitBreaks> MeleeDuty { get; set; } = new();

    public static readonly PartyRole[] Tanks = [PartyRole.MainTank, PartyRole.OffTank];
    public static readonly PartyRole[] Healers = [PartyRole.RegenHealer, PartyRole.ShieldHealer];
    public static readonly PartyRole[] Melees = [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];

    public static bool IsFirst(MeleeLimitBreaks duty) => duty is MeleeLimitBreaks.First or MeleeLimitBreaks.FirstAndEnrage;

    public static bool HasEnrage(MeleeLimitBreaks duty) => duty is MeleeLimitBreaks.FirstAndEnrage or MeleeLimitBreaks.SecondAndEnrage;

    // Each melee LB3 has one owner, and both tanks can't dive on one side.
    public SettingsConflicts Validate()
    {
        var conflicts = new SettingsConflicts();
        if (!PerRole.SeatsActive) return conflicts;

        conflicts.Forbidden(Others(Tanks, CosmoDiveSide), "take a Cosmo Dive side");
        conflicts.Forbidden(Others(Melees, MeleeDuty), "take a melee LB3");

        conflicts.AtMost(1, Claiming(Tanks, CosmoDiveSide, s => s == DiveSide.Left), "the left side of Cosmo Dive");
        conflicts.AtMost(1, Claiming(Tanks, CosmoDiveSide, s => s == DiveSide.Right), "the right side of Cosmo Dive");

        conflicts.AtMost(1, Claiming(Melees, MeleeDuty, IsFirst), "the first melee LB3 at Cosmo Dive");
        conflicts.AtMost(1, Claiming(Melees, MeleeDuty, d => !IsFirst(d)), "the second melee LB3 at Cosmo Dive");
        conflicts.AtMost(1, Claiming(Melees, MeleeDuty, HasEnrage), "the enrage's melee LB3");
        return conflicts;
    }

    private static List<PartyRole> Others<T>(PartyRole[] allowed, PerRoleSetting<T> setting) where T : struct
        => PerRole.All.Where(r => !allowed.Contains(r) && setting[r].HasValue).ToList();

    private static List<PartyRole> Claiming<T>(PartyRole[] seats, PerRoleSetting<T> setting, Func<T, bool> claims) where T : struct
        => seats.Where(r => setting[r] is { } v && claims(v)).ToList();
}
