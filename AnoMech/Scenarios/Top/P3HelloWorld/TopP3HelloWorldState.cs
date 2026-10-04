using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

// Each patch every pair steps one role along this cycle, so over the four patches everyone plays
// each role once.
public enum HelloWorldRole { Defamation, Remote, Stack, Local }

public sealed record RotColor(
    ushort RotStatusId, ushort PrepStatusId, ushort DebuggerStatusId, uint ExplosionActionId,
    ushort TowerStatusId, uint TowerActionId, uint TowerUnsoakedActionId, uint TowerExpiredActionId)
{
    public static readonly RotColor Red = new(
        StatusId.HWRedRot, StatusId.HWPrepRedRot, StatusId.HWImmuneRedRot, ActionId.CriticalUnderflowBug,
        StatusId.HWRedTower, ActionId.CascadingLatentDefect, ActionId.CascadingLatentDefectUnsoaked, ActionId.CascadingLatentDefectExpired);

    public static readonly RotColor Blue = new(
        StatusId.HWBlueRot, StatusId.HWPrepBlueRot, StatusId.HWImmuneBlueRot, ActionId.CriticalPerformanceBug,
        StatusId.HWBlueTower, ActionId.LatentPerformanceDefect, ActionId.LatentPerformanceDefectUnsoaked, ActionId.LatentPerformanceDefectExpired);

    public RotColor Other => this == Red ? Blue : Red;

    public override string ToString() => this == Red ? "red" : "blue";
}

// One patch's four towers, 14y out on the cardinals or the intercardinals. The two red towers are
// neighbours (RedStart and the next one clockwise); the blue pair takes the other two.
public sealed record HelloWorldTowers(bool Intercardinal, int RedStart)
{
    public Vector3 Position(int index) => Direction.All[(Intercardinal ? 1 : 0) + 2 * index].Apply(new Vector3(0f, 0f, -14f));

    public RotColor ColorOf(int index) => (index - RedStart + 4) % 4 < 2 ? RotColor.Red : RotColor.Blue;
}

public sealed class TopP3HelloWorldState
{
    private readonly Rng rng = Rng.Detached;

    public Rng Rng => rng;

    // Pairs in cycle order: [0,1] start as defamations, [2,3] remote, [4,5] stacks, [6,7] local.
    public RoleList Pairs { get; }
    public RotColor DefamationColor { get; }
    public IReadOnlyList<HelloWorldTowers> Towers { get; }

    public TopP3HelloWorldState(Rng rng, SimParty party, TopP3HelloWorldStateOverrides overrides)
    {
        this.rng = rng;
        Pairs = new RoleListBuilder
        {
            Slots = overrides.Role.Resolve(party.PlayerRole)
                             .ToDictionary(r => r.Role, r => new[] { 2 * (int)r.Value, 2 * (int)r.Value + 1 }),
        }.Build(rng, party);
        DefamationColor = overrides.DefamationColor switch { DefamationRot.Red => RotColor.Red, DefamationRot.Blue => RotColor.Blue, _ => rng.NextBool() ? RotColor.Red : RotColor.Blue };
        Towers = Enumerable.Range(0, 4).Select(_ => new HelloWorldTowers(rng.NextBool(), rng.NextInt(4))).ToList();
    }

    private TopP3HelloWorldState(SimParty party, PartyRole[] pairs, RotColor defamationColor, HelloWorldTowers[] towers)
    {
        Pairs = new RoleList(party, pairs);
        DefamationColor = defamationColor;
        Towers = towers;
    }

    public static TopP3HelloWorldState? FromNetworkReplay(SimParty party, PartyRole[]? pairs, bool defamationIsRed, bool[]? intercardinal, int[]? redStart)
    {
        if (pairs is not { Length: 8 } || pairs.Distinct().Count() != 8 || !pairs.All(Enum.IsDefined)
            || intercardinal is not { Length: 4 } || redStart is not { Length: 4 } || redStart.Any(i => i is < 0 or > 3))
            return null;
        return new(party, pairs, defamationIsRed ? RotColor.Red : RotColor.Blue,
                   Enumerable.Range(0, 4).Select(i => new HelloWorldTowers(intercardinal[i], redStart[i])).ToArray());
    }

    public RotColor StackColor => DefamationColor.Other;

    public HelloWorldRole InitialRole(PartyRole role) => (HelloWorldRole)(Pairs.List.ToList().IndexOf(role) / 2);

    // Patch is 0..3.
    public HelloWorldRole RoleIn(PartyRole role, int patch) => (HelloWorldRole)(((int)InitialRole(role) + patch) % 4);

    public IEnumerable<PartyRole> WithRole(HelloWorldRole role, int patch)
        => PerRole.All.Where(member => RoleIn(member, patch) == role);
}
