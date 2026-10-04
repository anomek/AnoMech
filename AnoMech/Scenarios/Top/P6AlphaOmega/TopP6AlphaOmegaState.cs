using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.P6AlphaOmega.TopP6AlphaOmegaStateOverrides;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

// Exasquares: InFirst fires the centre cross first and the outer pinwheel 2s later; otherwise the
// pinwheel leads.
public sealed record ArrowPattern(bool InFirst);

// The four exaflares of an Unlimited Wave Cannon start 1s apart at radius 24, the first at
// StartOctant (0 = N, clockwise), each next one 45 degrees further in Turn's direction.
public sealed record ExaflareSweep(int StartOctant, int Turn)
{
    public int Octant(int index) => ((StartOctant + Turn * index) % 8 + 8) % 8;
}

public sealed class TopP6AlphaOmegaState
{
    private readonly Rng rng = Rng.Detached;

    public Rng Rng => rng;

    public IReadOnlyList<ArrowPattern> Arrows { get; }
    public IReadOnlyList<ExaflareSweep> Exaflares { get; }
    // The first four Wave Cannon proteans target these; the other four go 2s later.
    public IReadOnlyList<IReadOnlyList<PartyRole>> ProteanFirstWave { get; }
    public IReadOnlyList<PartyRole> WildChargeTargets { get; }
    // Cosmo Meteor spreads: the first four of each round go 1s before the rest.
    public IReadOnlyList<IReadOnlyList<PartyRole>> SpreadFirstWave { get; }
    public IReadOnlyList<PartyRole> FlareTargets { get; }
    public PartyRole StackTarget { get; }

    // The cutscene call-outs: a seat's own pick, else the fight-wide one or the NAUR guide's.
    public PartyRole CosmoMemoryTank { get; }
    public PartyRole MagicNumber1Tank { get; }
    public PartyRole MagicNumber2Tank { get; }
    public PartyRole LeftDiveTank { get; }
    public PartyRole MagicNumber1Healer { get; }
    public PartyRole MagicNumber2Healer { get; }
    public PartyRole MeteorMiddleHealer { get; }
    public PartyRole FirstMelee { get; }
    public PartyRole SecondMelee { get; }
    public PartyRole? EnrageMelee { get; }
    // The tank at the head of each Wave Cannon's line, under an invuln.
    public IReadOnlyList<PartyRole> WaveCannonInvulns { get; }
    public float ThirdBarReadyAt { get; }
    public P6Practice Practice { get; }

    public PartyRole RightDiveTank => Other(LeftDiveTank);

    public TopP6AlphaOmegaState(Rng rng, SimParty party, TopP6AlphaOmegaStateOverrides overrides)
    {
        this.rng = rng;
        Arrows = [RollArrow(overrides.FirstArrowInFirst), RollArrow(overrides.SecondArrowInFirst)];
        Exaflares = RollExaflares();
        ProteanFirstWave = [Pick(4), Pick(4)];
        WildChargeTargets = [rng.NextRole(), rng.NextRole()];
        SpreadFirstWave = [Pick(4), Pick(4)];
        FlareTargets = Pick(3);
        StackTarget = rng.NextObj(PerRole.All.Except(FlareTargets).ToArray());

        var me = party.PlayerRole;

        CosmoMemoryTank = overrides.OffTankLimitBreaksFirst ? PartyRole.OffTank : PartyRole.MainTank;
        MagicNumber1Tank = Other(CosmoMemoryTank);
        MagicNumber2Tank = CosmoMemoryTank;

        var sides = Forced(overrides.CosmoDiveSide, me, Tanks);
        LeftDiveTank = Owner(sides, s => s == DiveSide.Left, PartyRole.MainTank);

        MeteorMiddleHealer = overrides.ShieldHealerGoesMiddle ? PartyRole.ShieldHealer : PartyRole.RegenHealer;
        MagicNumber1Healer = overrides.ShieldHealerLimitBreaksFirst ? PartyRole.ShieldHealer : PartyRole.RegenHealer;
        MagicNumber2Healer = OtherHealer(MagicNumber1Healer);

        var meleeDuty = Forced(overrides.MeleeDuty, me, Melees);
        FirstMelee = Owner(meleeDuty, IsFirst, overrides.MeleeDpsBLimitBreaksFirst ? PartyRole.MeleeDpsB : PartyRole.MeleeDpsA);
        SecondMelee = FirstMelee == PartyRole.MeleeDpsA ? PartyRole.MeleeDpsB : PartyRole.MeleeDpsA;
        EnrageMelee = meleeDuty.Count == 0 ? FirstMelee : EnrageOwner(meleeDuty);

        WaveCannonInvulns = overrides.MainTankInvulnsFirstWaveCannon
            ? [PartyRole.MainTank, PartyRole.OffTank]
            : [PartyRole.OffTank, PartyRole.MainTank];
        ThirdBarReadyAt = rng.NextFloat(TopP6AlphaOmegaConstants.ThirdBarEarliest, TopP6AlphaOmegaConstants.ThirdBarLatest);
        Practice = overrides.Practice;
    }

    private TopP6AlphaOmegaState(ArrowPattern[] arrows, ExaflareSweep[] exaflares, PartyRole[] wildChargeTargets, PartyRole[] flareTargets,
        PartyRole stackTarget, PartyRole leftDiveTank, PartyRole meteorMiddleHealer, PartyRole firstWaveCannonInvuln, P6Practice practice)
    {
        Practice = practice;
        Arrows = arrows;
        Exaflares = exaflares;
        ProteanFirstWave = [[], []];
        WildChargeTargets = wildChargeTargets;
        SpreadFirstWave = [[], []];
        FlareTargets = flareTargets;
        StackTarget = stackTarget;
        LeftDiveTank = leftDiveTank;
        MeteorMiddleHealer = meteorMiddleHealer;
        WaveCannonInvulns = [firstWaveCannonInvuln, Other(firstWaveCannonInvuln)];
        MagicNumber1Healer = meteorMiddleHealer;
        MagicNumber2Healer = OtherHealer(meteorMiddleHealer);
    }

    public static TopP6AlphaOmegaState? FromNetworkReplay(bool[]? arrowsInFirst, int[]? exaflareStarts, int[]? exaflareTurns, PartyRole[]? wildChargeTargets,
        PartyRole[]? flareTargets, PartyRole stackTarget, PartyRole leftDiveTank, PartyRole meteorMiddleHealer, PartyRole firstWaveCannonInvuln,
        P6Practice practice)
    {
        if (arrowsInFirst is not { Length: 2 }
            || exaflareStarts is not { Length: 2 } || exaflareStarts.Any(o => o is < 0 or > 7)
            || exaflareTurns is not { Length: 2 } || exaflareTurns.Any(d => d is not (1 or -1))
            || wildChargeTargets is not { Length: 2 } || !wildChargeTargets.All(Enum.IsDefined)
            || flareTargets is not { Length: 3 } || flareTargets.Distinct().Count() != 3 || !flareTargets.All(Enum.IsDefined)
            || !Enum.IsDefined(stackTarget) || flareTargets.Contains(stackTarget)
            || !Tanks.Contains(leftDiveTank) || !Healers.Contains(meteorMiddleHealer) || !Tanks.Contains(firstWaveCannonInvuln)
            || !Enum.IsDefined(practice))
            return null;
        return new(arrowsInFirst.Select(inFirst => new ArrowPattern(inFirst)).ToArray(),
            exaflareStarts.Zip(exaflareTurns, (start, turn) => new ExaflareSweep(start, turn)).ToArray(),
            wildChargeTargets, flareTargets, stackTarget, leftDiveTank, meteorMiddleHealer, firstWaveCannonInvuln, practice);
    }

    private static List<(PartyRole Role, T Value)> Forced<T>(PerRoleSetting<T> setting, PartyRole me, PartyRole[] seats) where T : struct
        => setting.Resolve(me).Where(s => seats.Contains(s.Role)).Select(s => (s.Role, s.Value)).ToList();

    // A duty the seats named goes to its claimant; one a single named seat turned down goes to the
    // other of the pair; otherwise the guide's pick stands.
    private static PartyRole Owner<T>(List<(PartyRole Role, T Value)> forced, Func<T, bool> claims, PartyRole guide)
    {
        foreach (var (role, value) in forced)
            if (claims(value)) return role;
        return forced.Count == 1 ? PairOf(forced[0].Role) : guide;
    }

    private static PartyRole? EnrageOwner(List<(PartyRole Role, MeleeLimitBreaks Value)> forced)
    {
        foreach (var (role, value) in forced)
            if (HasEnrage(value)) return role;
        return forced.Count == 1 ? PairOf(forced[0].Role) : null;
    }

    private static PartyRole PairOf(PartyRole role) => role switch
    {
        PartyRole.MainTank => PartyRole.OffTank,
        PartyRole.OffTank => PartyRole.MainTank,
        PartyRole.RegenHealer => PartyRole.ShieldHealer,
        PartyRole.ShieldHealer => PartyRole.RegenHealer,
        PartyRole.MeleeDpsA => PartyRole.MeleeDpsB,
        _ => PartyRole.MeleeDpsA,
    };

    private static PartyRole Other(PartyRole tank) => tank == PartyRole.MainTank ? PartyRole.OffTank : PartyRole.MainTank;

    private static PartyRole OtherHealer(PartyRole healer) => healer == PartyRole.RegenHealer ? PartyRole.ShieldHealer : PartyRole.RegenHealer;

    private ArrowPattern RollArrow(bool? inFirst) => new(inFirst ?? rng.NextBool());

    // Each Unlimited Wave Cannon starts at a random cardinal or intercardinal and runs either way.
    private IReadOnlyList<ExaflareSweep> RollExaflares()
        => [new ExaflareSweep(rng.NextInt(8), rng.NextSign()), new ExaflareSweep(rng.NextInt(8), rng.NextSign())];

    private IReadOnlyList<PartyRole> Pick(int count) => rng.Shuffle(PerRole.All.ToArray()).Take(count).ToList();
}
