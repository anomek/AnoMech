using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P4DarklitDragonsong.FruP4DarklitDragonsongState;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

public sealed class FruP4DarklitDragonsongJpAi : IScenarioAi<FruP4DarklitDragonsongState>
{
    public string Name => "JP · Mana";
    public string Group => "JP";

    private const float S = 1f / 2.358f;
    private const float IntercardMarkerRadius = 10f;

    private static readonly PartyRole[] DpsWestToEast = [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];

    private FruP4DarklitDragonsongState state = null!;
    private SimWorld world = null!;
    private PartyRole[] seats = [];
    private PartyRole[] afterWaterSwap = [];
    private bool baitEast;

    public void Run(FruP4DarklitDragonsongState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        seats = SeatFreePlayersBetweenTheTowers();
        afterWaterSwap = SwapFreeWaterWithItsPartnerWhenWatersShareASide();
        var ai = new AiManager(world);
        ai.Move(2f, () => ByRole(Middle), arrivalTime: 6.6f);
        ai.Move(6.65f, () => ByRole(OpeningSpread), arrivalTime: 8.8f);
        ai.Move(14.5f, () => ByRole(TankHealerNorthDpsSouthLineup), arrivalTime: 24.2f);
        ai.Move(24.2f, () => ByRole(role => Formation(role, seats)), arrivalTime: 28.2f);
        ai.Move(28.2f, () => ByRole(role => Formation(role, afterWaterSwap)), arrivalTime: TowersAt);
        ai.Move(33.1f, () => ByRole(SpiritTakerSpread), arrivalTime: 35.6f);
        ai.Move(35.65f, () => ByRole(IntercardMarkerOnTheSafeHalf), arrivalTime: 40f);
        world.Events.Add(40.5f, BaitSomberDanceAwayFromOracle);
        ai.Move(40.6f, () => ByRole(role => role == PartyRole.MainTank ? FromSim(0, baitEast ? 43 : -43) : null), arrivalTime: 43.8f);
        ai.Move(42.4f, () => ByRole(role => role == PartyRole.MainTank ? null : AwayFromTheSecondHit(role)), arrivalTime: 43.8f);
        ai.Move(45f, () => ByRole(role => role == PartyRole.MainTank ? JustPastOracleFromCenter() : null), jitter: 0f, arrivalTime: 47f);
        ai.Move(50.15f, () => ByRole(SevenToOneAkhMorn), arrivalTime: 57.7f);
    }

    private PartyRole[] SeatFreePlayersBetweenTheTowers()
    {
        var chained = state.LineupOrder.Take(4).ToArray();
        var free = state.LineupOrder.Skip(4).ToArray();
        var freeDps = DpsWestToEast.Where(free.Contains).ToArray();
        var freeHealer = free.First(r => r is PartyRole.RegenHealer or PartyRole.ShieldHealer);
        var freeTank = free.First(r => r.IsTank());
        return [.. chained, freeHealer, freeTank, freeDps[1], freeDps[0]];
    }

    private PartyRole[] SwapFreeWaterWithItsPartnerWhenWatersShareASide()
    {
        var after = seats.ToArray();
        var free = Array.IndexOf(after, state.FreeWater);
        var tetherNorth = Array.IndexOf(after, state.TetherWater) < 2;
        var freeNorth = free < 6;
        if (tetherNorth == freeNorth)
        {
            var partner = 4 + 3 - (free - 4);
            (after[free], after[partner]) = (after[partner], after[free]);
        }
        return after;
    }

    private Vector3 OraclePosition()
        => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.OracleP4)?.Position ?? Vector3.Zero;

    private Vector3 JustPastOracleFromCenter()
    {
        var oracle = OraclePosition();
        return oracle.LengthSquared() > 0.01f ? oracle + Vector3.Normalize(oracle) * 2f : oracle;
    }

    private void BaitSomberDanceAwayFromOracle()
    {
        var x = OraclePosition().X;
        baitEast = x < -4f * S || MathF.Abs(x) < 4f * S && state.EastWing;
    }

    private static Vector3 FromSim(float north, float east) => new(east * S, 0, -north * S);

    private int Seat(PartyRole role) => Array.IndexOf(afterWaterSwap, role);
    private bool North(PartyRole role) => Seat(role) % 4 < 2;

    private static Vector3? Middle(PartyRole role) => FromSim((int)role % 2 * 0.3f, (int)role * 0.08f);

    private static Vector3? OpeningSpread(PartyRole role) => role switch
    {
        PartyRole.MainTank => FromSim(16.4f, 10.1f),
        PartyRole.OffTank => FromSim(10.1f, 16.4f),
        PartyRole.RegenHealer => FromSim(10.1f, -16.4f),
        PartyRole.ShieldHealer => FromSim(16.4f, -10.1f),
        PartyRole.MeleeDpsA => FromSim(-16.4f, 10.1f),
        PartyRole.MeleeDpsB => FromSim(-10.1f, 16.4f),
        PartyRole.PhysRangedDps => FromSim(-10.1f, -16.4f),
        _ => FromSim(-16.4f, -10.1f),
    };

    private static Vector3? TankHealerNorthDpsSouthLineup(PartyRole role) => role switch
    {
        PartyRole.RegenHealer => new(-7.4f, 0, 0.6f),
        PartyRole.ShieldHealer => new(-2.2f, 0, -1.4f),
        PartyRole.MainTank => new(2.4f, 0, -1.4f),
        PartyRole.OffTank => new(7f, 0, 0.6f),
        PartyRole.MeleeDpsA => new(-7.4f, 0, 8.9f),
        PartyRole.MeleeDpsB => new(-2.2f, 0, 12.2f),
        PartyRole.PhysRangedDps => new(2.4f, 0, 12.2f),
        _ => new(7f, 0, 8.9f),
    };

    private static Vector3 Quadrant(int seat, float north, float east)
        => FromSim(seat % 4 < 2 ? north : -north, seat % 4 is 1 or 2 ? east : -east);

    private static Vector3? Formation(PartyRole role, PartyRole[] order)
    {
        var seat = Array.IndexOf(order, role);
        return seat < 4 ? Quadrant(seat, 19, 5.2f) : Quadrant(seat, 5.9f, 8.2f);
    }

    private Vector3? SpiritTakerSpread(PartyRole role)
    {
        var seat = Seat(role);
        if (seat < 4) return Quadrant(seat, 19, 17.2f);
        var west = seat is 4 or 7;
        return FromSim(0, west ? ((int)role < 4 ? -38 : -19) : ((int)role < 4 ? 19 : 0));
    }

    private Vector3? IntercardMarkerOnTheSafeHalf(PartyRole role)
    {
        var east = !state.EastWing;
        var octant = North(role) ? (east ? 1 : 7) : (east ? 3 : 5);
        var angle = octant * MathF.PI / 4;
        return new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle)) * IntercardMarkerRadius + Middle(role)!.Value * 0.3f;
    }

    private Vector3? AwayFromTheSecondHit(PartyRole role) => FromSim(North(role) ? 15 : -15, 0) + Middle(role)!.Value * 0.3f;

    private static Vector3? SevenToOneAkhMorn(PartyRole role) => role == PartyRole.MainTank ? new Vector3(-9.3f, 0, 0) : Vector3.Zero;

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
