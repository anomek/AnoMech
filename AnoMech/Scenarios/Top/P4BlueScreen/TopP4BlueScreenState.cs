using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P4BlueScreen;

public sealed class TopP4BlueScreenState
{
    private readonly Rng rng = Rng.Detached;
    private readonly PartyRole[][] stackTargets;

    public Rng Rng => rng;

    // Each Wave Cannon's two line-stack targets.
    public IReadOnlyList<IReadOnlyList<PartyRole>> StackTargets => stackTargets;
    // The melee LB3's seat, null for the guide's pick, and when it goes.
    public PartyRole? MeleeLimitBreakBy { get; }
    public LimitBreakTiming MeleeLimitBreakTiming { get; }

    public TopP4BlueScreenState(Rng rng, TopP4BlueScreenStateOverrides overrides)
    {
        this.rng = rng;
        stackTargets = [Roll(overrides.FirstStacks), Roll(overrides.SecondStacks), Roll(overrides.ThirdStacks)];
        MeleeLimitBreakBy = overrides.MeleeLimitBreakBy;
        MeleeLimitBreakTiming = overrides.MeleeLimitBreakTiming ?? LimitBreakTiming.PhaseStart;
    }

    private TopP4BlueScreenState(PartyRole[][] stackTargets) => this.stackTargets = stackTargets;

    public static TopP4BlueScreenState? FromNetworkReplay(PartyRole[]? stackTargets)
    {
        if (stackTargets is not { Length: 6 } || !stackTargets.All(Enum.IsDefined)) return null;
        var sets = stackTargets.Chunk(2).ToArray();
        return sets.Any(pair => pair[0] == pair[1]) ? null : new TopP4BlueScreenState(sets);
    }

    public void Retarget(int set, PartyRole[] targets) => stackTargets[set] = targets;

    public static bool IsWest(PartyRole role)
        => role is PartyRole.MainTank or PartyRole.CasterDps or PartyRole.RegenHealer or PartyRole.MeleeDpsA;

    private PartyRole[] Roll(StackSplit? split)
    {
        var west = PerRole.All.Where(IsWest).ToArray();
        var east = PerRole.All.Where(r => !IsWest(r)).ToArray();
        return split switch
        {
            StackSplit.OnePerSide => [rng.NextObj(west), rng.NextObj(east)],
            StackSplit.OneSide => rng.Shuffle(rng.NextBool() ? west : east).Take(2).ToArray(),
            _ => rng.Shuffle(PerRole.All.ToArray()).Take(2).ToArray(),
        };
    }
}
