using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Scenarios.Top.P3Monitors;

public sealed class TopP3MonitorsAi(bool automarkers) : IScenarioAi<TopP3MonitorsState>
{
    public string Name => automarkers ? "NA (automarkers)" : "NA";

    private const float MovePrompt = 0.3f;

    private static readonly PartyRole[] Conga =
    [
        PartyRole.RegenHealer, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MainTank,
        PartyRole.OffTank, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.ShieldHealer,
    ];

    private static readonly Vector2[] MonitorSpots = [new(9.5f, -8.4f), new(9.5f, 8.4f), new(-9.9f, 11.6f)];
    private static readonly Vector2[] OtherSpots = [new(-1.4f, -18.3f), new(-8f, -12f), new(-16.5f, -4.2f), new(-16.8f, 5f), new(-1.7f, 18.3f)];
    private static readonly Vector2 MainTankBesideOmega = new(-1.5f, -11f);
    private static readonly Vector2 MainTankNorth = new(0f, -15.5f);

    private const float SettleDistance = 2.5f;
    private const float DepartureSpread = 0.6f;
    private const float SettleSpread = 0.4f;

    private TopP3MonitorsState state = null!;
    private Rng noise = null!;
    private AiManager ai = null!;

    public void Run(TopP3MonitorsState s, SimWorld world)
    {
        state = s;
        noise = world.Stream("top-p3-monitors-ai");
        ai = new AiManager(world);
        ai.Move(1f + MovePrompt, CongaWestOfOmega, jitter: 0.2f);
        foreach (var role in MonitorsNorthToSouth())
            HeadForTheLayout(role, 11.7f, 15.5f);
        foreach (var role in OthersNorthToSouth())
            HeadForTheLayout(role, 12.1f, 16f);
        var monitors = MonitorsNorthToSouth();
        for (var i = 0; i < monitors.Count; i++)
            TurnInPlace(world, 17f, monitors[i], FacingToCleave(i));
        ai.Move(20.8f + MovePrompt, MainTankReturnsToOmega);
        ai.Move(23.9f + MovePrompt, MainTankPullsOmegaNorth);

        if (!automarkers) return;
        ai.Automarker(10.8f, MonitorMarkers);
        ai.Automarker(20.4f, () => new Dictionary<PartyRole, Sign>());
    }

    private static IAiMove CongaWestOfOmega()
        => AiMove.Create(Enumerable.Range(0, 8).Select(i => (Vector2?)new Vector2(-12.8f, -9.8f + 2.8f * i)).ToArray())
                 .Assignments(Conga);

    private void HeadForTheLayout(PartyRole role, float leave, float settle)
    {
        var spot = LayoutSpot(role);
        var shortOfTheSpot = spot + Vector2.Normalize(CongaSpot(role) - spot) * SettleDistance;
        ai.Move(leave + MovePrompt + noise.NextFloat(0f, DepartureSpread), () => AiMove.Single(role, shortOfTheSpot));
        ai.Move(settle + MovePrompt + noise.NextFloat(0f, SettleSpread), () => AiMove.Single(role, spot));
    }

    private static Vector2 CongaSpot(PartyRole role) => new(-12.8f, -9.8f + 2.8f * Array.IndexOf(Conga, role));

    private Vector2 LayoutSpot(PartyRole role)
    {
        var monitors = MonitorsNorthToSouth();
        var spot = monitors.Contains(role) ? MonitorSpots[monitors.IndexOf(role)] : OtherSpots[OthersNorthToSouth().IndexOf(role)];
        return state.BossSide == CleaveSide.Left ? spot with { X = -spot.X } : spot;
    }

    private static IAiMove MainTankReturnsToOmega() => AiMove.Single(PartyRole.MainTank, MainTankBesideOmega);

    private static IAiMove MainTankPullsOmegaNorth() => AiMove.Single(PartyRole.MainTank, MainTankNorth);

    private List<PartyRole> MonitorsNorthToSouth() => Conga.Where(role => state.Monitors.Contains(role)).ToList();

    private List<PartyRole> OthersNorthToSouth() => Conga.Where(role => !state.Monitors.Contains(role)).ToList();

    private static void TurnInPlace(SimWorld world, float time, PartyRole role, float rotation)
        => world.Events.Add(time, () =>
        {
            if (world.Party.Get(role) is { } member && member.IsAlive())
                member.MoveTo(member.Position, AiManager.RunSpeed, rotation);
        });

    private float FacingToCleave(int monitor)
    {
        var cleave = monitor switch
        {
            0 => 0f,
            1 => 180f,
            _ => state.BossSide == CleaveSide.Left ? 90f : 270f,
        };
        var side = state.MonitorSides[state.Monitors.ToList().IndexOf(MonitorsNorthToSouth()[monitor])];
        var facing = side == CleaveSide.Right ? cleave - 90f : cleave + 90f;
        return MathF.PI - facing * MathF.PI / 180f;
    }

    private Dictionary<PartyRole, Sign> MonitorMarkers()
    {
        var marks = new Dictionary<PartyRole, Sign>();
        Sign[] binds = [Sign.Bind1, Sign.Bind2, Sign.Bind3];
        Sign[] attacks = [Sign.Attack1, Sign.Attack2, Sign.Attack3, Sign.Attack4, Sign.Attack5];
        var monitors = MonitorsNorthToSouth();
        var others = OthersNorthToSouth();
        for (var i = 0; i < monitors.Count; i++) marks[monitors[i]] = binds[i];
        for (var i = 0; i < others.Count; i++) marks[others[i]] = attacks[i];
        return marks;
    }
}
