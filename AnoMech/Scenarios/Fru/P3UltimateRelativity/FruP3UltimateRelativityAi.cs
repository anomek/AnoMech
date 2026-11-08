using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.P3UltimateRelativity.FruP3UltimateRelativityState;

namespace AnoMech.Scenarios.Fru.P3UltimateRelativity;

public sealed class FruP3UltimateRelativityAi : IScenarioAi<FruP3UltimateRelativityState>
{
    public string Name => "NA";
    public string Group => "NA";

    private const float S = 1f / 2.358f;

    private static readonly PartyRole[] DpsPriority = [PartyRole.CasterDps, PartyRole.PhysRangedDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];
    private static readonly PartyRole[] SupportPriority = [PartyRole.ShieldHealer, PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank];

    private FruP3UltimateRelativityState state = null!;

    public void Run(FruP3UltimateRelativityState stateParam, SimWorld world)
    {
        state = stateParam;
        var ai = new AiManager(world);
        ai.Move(1f, () => ByRole(CenterSpot));
        ai.Move(19.2f, () => ByRole(role => FireSpot(role, 0)), arrivalTime: FireAt(0));
        ai.Move(23.9f, () => ByRole(role => BaitSpot(role, 0)), arrivalTime: ReturnAt(earlyReturn: true));
        LeaveNowOthersArriveBy(ai, 28.6f, role => BaitedLaser(role, 0), role => FireSpot(role, 1, intermediate: true), arrivalTime: 30.6f);
        ai.Move(30.6f, () => ByRole(role => FireSpot(role, 1)), jitter: 0f, arrivalTime: FireAt(1));
        LeaveNowOthersArriveBy(ai, 34f, role => FireWave(state.Debuff(role)) == 1, role => BaitSpot(role, 1), arrivalTime: ReturnAt(earlyReturn: false));
        LeaveNowOthersArriveBy(ai, 38.7f, role => BaitedLaser(role, 1), role => FireSpot(role, 2), arrivalTime: FireAt(2));
        ai.Move(45f, () => ByRole(role => BaitSpot(role, 2)), arrivalTime: LaserHitAt(2, 0));
        LeaveNowOthersArriveBy(ai, 49.1f, role => BaitedLaser(role, 2), CenterSpot, arrivalTime: 51f);
        world.Events.Add(51f, () => FaceOutwardForShadoweye(world));
        ai.Move(StunEndAt + 0.1f, () => ByRole(role => Direction(role) * 0.5f), arrivalTime: 59.2f);
    }

    private static void LeaveNowOthersArriveBy(AiManager ai, float time, Func<PartyRole, bool> leaveNow, Func<PartyRole, Vector3?> at, float arrivalTime)
    {
        ai.Move(time, () => ByRole(role => leaveNow(role) ? at(role) : null));
        ai.Move(time, () => ByRole(role => leaveNow(role) ? null : at(role)), arrivalTime: arrivalTime);
    }

    private bool BaitedLaser(PartyRole role, int wave) => Wave(Clock(role)) == wave;

    private void FaceOutwardForShadoweye(SimWorld world)
    {
        foreach (var role in Enum.GetValues<PartyRole>())
            if (world.Party.Get(role) is { } member && member.IsAlive() && world.Party.IsBotDriven(member))
            {
                member.StopMoving();
                member.Face(member.Position + Direction(role) * 100f);
            }
    }

    private int Clock(PartyRole role)
    {
        var debuff = state.Debuff(role);
        return debuff switch
        {
            RelativityDebuff.ShortDps => IsWestOfPair(role, DpsPriority) ? 5 : 3,
            RelativityDebuff.ShortSupport => 0,
            RelativityDebuff.MediumDps => 2,
            RelativityDebuff.MediumSupport => 6,
            RelativityDebuff.LongSupport => IsWestOfPair(role, SupportPriority) ? 7 : 1,
            _ => 4,
        };
    }

    private bool IsWestOfPair(PartyRole role, PartyRole[] priority)
        => priority.First(r => state.Debuff(r) == state.Debuff(role)) == role;

    private Vector3 Direction(PartyRole role) => state.Direction(Clock(role));
    private Vector3 At(PartyRole role, float radius) => Direction(role) * radius;

    private Vector3 Bait(PartyRole role)
    {
        var direction = Direction(role);
        var tangent = new Vector3(-direction.Z, 0f, direction.X);
        return direction * (23f * S) + tangent * (state.Clockwise(Clock(role)) ? -1f : 1f) * (4.1f * S);
    }

    private Vector3? FireSpot(PartyRole role, int wave, bool intermediate = false)
        => At(role, state.HasFire(role, wave) ? (intermediate ? 20.5f : 32.5f) * S : 3f * S);

    private Vector3? BaitSpot(PartyRole role, int wave)
    {
        if (BaitedLaser(role, wave)) return Bait(role);
        if (wave == 0) return At(role, state.DarkEruption(role) ? EruptionRewind(role) : 3f * S);
        return At(role, 3f * S);
    }

    private float EruptionRewind(PartyRole role) => (state.Debuff(role) == RelativityDebuff.ShortDps ? 22f : 20.5f) * S;

    private Vector3? CenterSpot(PartyRole role) => At(role, 3f * S);

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
