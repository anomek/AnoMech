using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;

namespace AnoMech.Scenarios.Tea.I2TemporalStasis;

public sealed class TeaI2TemporalStasisAi : IScenarioAi<TeaI2TemporalStasisState>
{
    public string Name => "NAUR";

    private const float RobotX = 6f;
    private const float NorthOfRobot = -1.4f;
    private const float SouthOfRobot = 1.8f;
    private const float WallX = 18.6f;
    private const float RestrainingWallX = 18.8f;
    private const float WaymarkX = 13.75f;

    private TeaI2TemporalStasisState state = null!;
    private SimWorld world = null!;

    public void Run(TeaI2TemporalStasisState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);
        ai.Move(0.3f, GatherSouthOfCentre);
        ai.Move(8.4f, TakeStasisSpots);
        ai.Move(10.5f, TakeStasisSpots);
    }

    private static IAiMove GatherSouthOfCentre() => ByRole(
        (MainTank, new Vector2(-1.5f, 1.5f)), (OffTank, new Vector2(1.5f, 1.5f)),
        (RegenHealer, new Vector2(-3f, 3.5f)), (ShieldHealer, new Vector2(3f, 3.5f)),
        (MeleeDpsA, new Vector2(-1f, 3f)), (MeleeDpsB, new Vector2(1f, 3f)),
        (PhysRangedDps, new Vector2(-1.5f, 5f)), (CasterDps, new Vector2(1.5f, 5f)));

    private IAiMove TakeStasisSpots()
    {
        var bruteJusticeSide = state.Objects.BruteJustice is { IsActive: true } bruteJustice
            ? MathF.Sign(bruteJustice.Position.X)
            : state.BruteJusticeEast ? 1f : -1f;
        return ByRole(PerRole.All.Select(role => (role, SpotFor(role, bruteJusticeSide == 0f ? 1f : bruteJusticeSide))).ToArray());
    }

    private Vector2 SpotFor(PartyRole role, float bruteJusticeSide)
    {
        var support = !role.IsDps();
        return DebuffSeenOn(role) switch
        {
            StasisDebuff.HouseArrest => new(RobotX, support ? NorthOfRobot : SouthOfRobot),
            StasisDebuff.None => new(-RobotX, support ? NorthOfRobot : SouthOfRobot),
            StasisDebuff.AggravatedAssault => new(bruteJusticeSide * WallX, support ? -0.5f : 0.5f),
            _ => RestrainingOrderSpot(TakesWestRestrainingOrder(role) ? -1f : 1f, bruteJusticeSide),
        };
    }

    private static Vector2 RestrainingOrderSpot(float side, float bruteJusticeSide)
        => side == bruteJusticeSide ? new(side * RestrainingWallX, 0f) : new(side * WaymarkX, 0f);

    private bool TakesWestRestrainingOrder(PartyRole role)
    {
        var partner = PerRole.All.Where(r => r != role && DebuffSeenOn(r) == StasisDebuff.RestrainingOrder).Cast<PartyRole?>().FirstOrDefault();
        if (partner is not { } other || world.Party.Get(role) is not { } me || world.Party.Get(other) is not { } them) return !role.IsDps();
        var westward = them.Position.X - me.Position.X;
        return MathF.Abs(westward) > 0.1f ? westward > 0f : !role.IsDps();
    }

    private StasisDebuff DebuffSeenOn(PartyRole role)
    {
        if (world.Party.Get(role) is not { } member) return StasisDebuff.None;
        if (member.ShowsStatus(StatusId.AggravatedAssault)) return StasisDebuff.AggravatedAssault;
        if (member.ShowsStatus(StatusId.HouseArrest)) return StasisDebuff.HouseArrest;
        return member.ShowsStatus(StatusId.RestrainingOrder) ? StasisDebuff.RestrainingOrder : StasisDebuff.None;
    }

    private static IAiMove ByRole(params (PartyRole Role, Vector2 Spot)[] spots)
    {
        var coords = new Vector2?[8];
        foreach (var (role, spot) in spots) coords[(int)role] = spot;
        return AiMove.Create(coords).NaturalOrder();
    }
}
