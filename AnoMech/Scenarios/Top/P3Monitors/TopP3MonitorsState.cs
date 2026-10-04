using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P3Monitors;

// Which side of its own facing a monitor fires at. Mul follows CharacterFind.OnSideN.
public sealed record CleaveSide(int Mul, uint BossActionId, ushort LoadingStatusId)
{
    public static readonly CleaveSide Left = new(1, ActionId.OversampledWaveCannonLeft, StatusId.PlayerMonitorLeft);
    public static readonly CleaveSide Right = new(-1, ActionId.OversampledWaveCannonRight, StatusId.PlayerMonitorRight);
}

public sealed class TopP3MonitorsState
{
    private readonly Rng rng = Rng.Detached;

    public Rng Rng => rng;

    public CleaveSide BossSide { get; }
    public IReadOnlyList<PartyRole> Monitors { get; }
    public IReadOnlyList<CleaveSide> MonitorSides { get; }

    public TopP3MonitorsState(Rng rng, SimParty party, TopP3MonitorsStateOverrides overrides)
    {
        this.rng = rng;
        BossSide = overrides.BossSide switch { MonitorCleave.Left => CleaveSide.Left, MonitorCleave.Right => CleaveSide.Right, _ => RandomSide() };
        var wanted = overrides.Monitor.Resolve(party.PlayerRole).ToDictionary(r => r.Role, r => r.Value);
        Monitors = new RoleListBuilder { Size = 3, Membership = wanted }.Build(rng, party).List;
        MonitorSides = RollMonitorSides();
    }

    // Never all three the same side (UNVERIFIED: inferred, not a known rule).
    private List<CleaveSide> RollMonitorSides()
    {
        while (true)
        {
            var sides = Monitors.Select(_ => RandomSide()).ToList();
            if (sides.Distinct().Count() > 1) return sides;
        }
    }

    private TopP3MonitorsState(CleaveSide bossSide, PartyRole[] monitors, CleaveSide[] monitorSides)
    {
        BossSide = bossSide;
        Monitors = monitors;
        MonitorSides = monitorSides;
    }

    public static TopP3MonitorsState? FromNetworkReplay(bool bossIsLeft, PartyRole[]? monitors, bool[]? monitorsAreLeft)
    {
        if (monitors is not { Length: 3 } || monitors.Distinct().Count() != 3 || !monitors.All(Enum.IsDefined)
            || monitorsAreLeft is not { Length: 3 })
            return null;
        return new(Side(bossIsLeft), monitors, monitorsAreLeft.Select(Side).ToArray());
    }

    private static CleaveSide Side(bool left) => left ? CleaveSide.Left : CleaveSide.Right;

    private CleaveSide RandomSide() => rng.NextBool() ? CleaveSide.Left : CleaveSide.Right;
}
