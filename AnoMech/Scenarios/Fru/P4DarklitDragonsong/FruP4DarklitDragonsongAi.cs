using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P4DarklitDragonsong.FruP4DarklitDragonsongState;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

public sealed class FruP4DarklitDragonsongAi(bool playerBaitsSomberDance) : IScenarioAi<FruP4DarklitDragonsongState>
{
    public string Name => playerBaitsSomberDance ? "NA · you take Somber Dance" : "NA";
    public string Group => "NA";

    private const float S = 1f / 2.358f;

    private FruP4DarklitDragonsongState state = null!;
    private SimWorld world = null!;
    private PartyRole[] swapped = [];
    private PartyRole somberTank;
    private bool baitEast;
    private Vector3 farDanceLanding;

    public void Run(FruP4DarklitDragonsongState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        swapped = SwapFreeWaterToItsGroup();
        somberTank = state.SomberTank ??= ChooseSomberTank();
        var ai = new AiManager(world);
        ai.Move(2f, () => ByRole(Middle), arrivalTime: 6.6f);
        ai.Move(6.65f, () => ByRole(OpeningSpread), arrivalTime: 8.8f);
        ai.Move(14.5f, () => ByRole(Lineup), arrivalTime: 24.2f);
        ai.Move(24.2f, () => ByRole(role => Bowtie(role, state.LineupOrder.ToArray())), arrivalTime: 28.2f);
        ai.Move(28.2f, () => ByRole(role => Bowtie(role, swapped)), arrivalTime: TowersAt);
        ai.Move(33.1f, () => ByRole(SpiritTakerSpread), arrivalTime: 35.6f);
        ai.Move(35.65f, () => ByRole(WaterStack), arrivalTime: 40f);
        world.Events.Add(40.5f, BaitSomberDanceAwayFromOracle);
        ai.Move(40.6f, () => ByRole(role => role == somberTank ? FromSim(0, baitEast ? 43 : -43) : null), arrivalTime: 43.8f);
        ai.Move(42.4f, () => ByRole(role => role == somberTank ? null : DanceParty(role)), arrivalTime: 43.8f);
        world.Events.Add(43.8f, ExpectTheOracleToJumpShortOfTheFarthest);
        ai.Move(45f, () => ByRole(role => role == somberTank ? JustPastOracleFromCenter() : null), jitter: 0f, arrivalTime: 47f);
        ai.Move(50.15f, () => ByRole(AkhMorn), arrivalTime: 57.7f);
    }

    private PartyRole[] SwapFreeWaterToItsGroup()
    {
        var after = state.LineupOrder.ToArray();
        if ((state.TetherWaterIndex < 2) == (state.FreeWaterIndex < 2))
            (after[4 + state.FreeWaterIndex], after[4 + 3 - state.FreeWaterIndex]) = (after[4 + 3 - state.FreeWaterIndex], after[4 + state.FreeWaterIndex]);
        return after;
    }

    private PartyRole ChooseSomberTank()
    {
        var player = world.Party.PlayerRole;
        if (playerBaitsSomberDance && player.IsTank()) return player;
        return world.Party.Get(PartyRole.MainTank) is { } mainTank && world.Party.IsBotDriven(mainTank) ? PartyRole.MainTank : PartyRole.OffTank;
    }

    private Vector3 OraclePosition()
        => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.OracleP4)?.Position ?? Vector3.Zero;

    private void ExpectTheOracleToJumpShortOfTheFarthest()
    {
        var oracle = OraclePosition();
        farDanceLanding = world.Party.ActiveMembers().MaxBy(m => Vector3.DistanceSquared(m.Position, oracle)) is { } farthest
            ? JumpLanding(oracle, farthest.Position)
            : oracle;
    }

    private Vector3 JustPastOracleFromCenter()
    {
        var oracle = farDanceLanding;
        return oracle.LengthSquared() > 0.01f ? oracle + Vector3.Normalize(oracle) * 2f : oracle;
    }

    private void BaitSomberDanceAwayFromOracle()
    {
        var x = OraclePosition().X;
        baitEast = x < -4f * S || MathF.Abs(x) < 4f * S && state.EastWing;
    }

    private static Vector3 FromSim(float north, float east) => new(east * S, 0, -north * S);

    private static Vector3 Quadrant(int index, float north, float east)
        => FromSim(index % 4 < 2 ? north : -north, index % 4 is 1 or 2 ? east : -east);

    private int Index(PartyRole role) => Array.IndexOf(swapped, role);
    private bool North(PartyRole role) => Index(role) % 4 < 2;

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

    private static Vector3? Lineup(PartyRole role) => role switch
    {
        PartyRole.MainTank => FromSim(4.5f, 5),
        PartyRole.OffTank => FromSim(0.2f, 11.3f),
        PartyRole.RegenHealer => FromSim(0.2f, -11.3f),
        PartyRole.ShieldHealer => FromSim(4.5f, -5),
        PartyRole.MeleeDpsA => FromSim(-25.5f, 5),
        PartyRole.MeleeDpsB => FromSim(-21.2f, 11.3f),
        PartyRole.PhysRangedDps => FromSim(-21.2f, -11.3f),
        _ => FromSim(-25.5f, -5),
    };

    private static Vector3? Bowtie(PartyRole role, PartyRole[] order)
    {
        var index = Array.IndexOf(order, role);
        return index < 4 ? Quadrant(index, 19, 5.2f) : Quadrant(index, 6.7f, 14.6f);
    }

    private Vector3? SpiritTakerSpread(PartyRole role)
    {
        var index = Index(role);
        if (index < 4) return Quadrant(index, 19, 17.2f);
        var west = index is 4 or 7;
        return FromSim(0, west ? ((int)role < 4 ? -38 : -19) : ((int)role < 4 ? 19 : 0));
    }

    private Vector3? WaterStack(PartyRole role)
        => FromSim(North(role) ? 17.3f : -17.3f, state.EastWing ? -15.5f : 15.5f) + Middle(role)!.Value * 0.3f;

    private Vector3? DanceParty(PartyRole role) => FromSim(North(role) ? 15 : -15, 0) + Middle(role)!.Value * 0.3f;

    private static Vector3? AkhMorn(PartyRole role) => role == PartyRole.MainTank ? new Vector3(-9.3f, 0, 0) : Vector3.Zero;

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
