using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.P3UltimateRelativity.FruP3UltimateRelativityState;

namespace AnoMech.Scenarios.Fru.P3UltimateRelativity;

public sealed class FruP3UltimateRelativityJpAi : IScenarioAi<FruP3UltimateRelativityState>
{
    public string Name => "JP · Mana";
    public string Group => "JP";

    private const float S = 1f / 2.358f;
    private const float FirstPressEarliest = 1f;
    private const float FirstPressLatest = 2f;
    private const float GapBetweenPressesShortest = 0.3f;
    private const float GapBetweenPressesLongest = 1f;

    private FruP3UltimateRelativityState state = null!;
    private readonly Dictionary<PartyRole, Sign> selfMarks = new();

    public void Run(FruP3UltimateRelativityState stateParam, SimWorld world)
    {
        state = stateParam;
        SelfMarkInTheOrderEveryonePressesTheirMacro(world);
        var ai = new AiManager(world);
        ai.Move(1f, () => ByRole(CenterSpot));
        ai.Move(19.2f, () => ByRole(role => FireSpot(role, 0)), arrivalTime: FireAt(0));
        ai.Move(23.9f, () => ByRole(role => BaitSpot(role, 0)), arrivalTime: ReturnAt(earlyReturn: true));
        LeaveLaserNowOthersArriveBy(ai, 28.6f, 0, role => FireSpot(role, 1, intermediate: true), arrivalTime: 30.6f);
        ai.Move(30.6f, () => ByRole(role => FireSpot(role, 1)), jitter: 0f, arrivalTime: FireAt(1));
        ai.Move(34f, () => ByRole(role => BaitSpot(role, 1)), arrivalTime: ReturnAt(earlyReturn: false));
        LeaveLaserNowOthersArriveBy(ai, 38.7f, 1, role => FireSpot(role, 2), arrivalTime: FireAt(2));
        ai.Move(45f, () => ByRole(role => BaitSpot(role, 2)), arrivalTime: LaserHitAt(2, 0));
        LeaveLaserNowOthersArriveBy(ai, 49.1f, 2, CenterSpot, arrivalTime: 51f);
        world.Events.Add(51f, () => FaceOutwardForShadoweye(world));
        ai.Move(StunEndAt + 0.1f, () => ByRole(role => Direction(role) * 0.5f), arrivalTime: 59.2f);
        world.Events.Add(StunEndAt + 1f, world.Party.ClearMarks);
    }

    private void SelfMarkInTheOrderEveryonePressesTheirMacro(SimWorld world)
    {
        selfMarks.Clear();
        var reactions = world.Stream("relativity-self-marks");
        var pressedAfter = Between(reactions, FirstPressEarliest, FirstPressLatest);
        foreach (var role in reactions.Shuffle(Enum.GetValues<PartyRole>()))
        {
            var sign = MacroMark(role);
            selfMarks[role] = sign;
            world.Events.Add(AssignAt + pressedAfter, () =>
            {
                if (world.Party.Get(role) is { } member && member.IsAlive() && world.Party.IsBotDriven(member))
                    world.Party.Mark(sign, member);
            });
            pressedAfter += Between(reactions, GapBetweenPressesShortest, GapBetweenPressesLongest);
        }
    }

    private static float Between(Rng rng, float shortest, float longest) => shortest + rng.NextSingle() * (longest - shortest);

    private Sign MacroMark(PartyRole role) => state.Debuff(role) switch
    {
        RelativityDebuff.ShortDps => NextFree(Sign.Attack1, Sign.Attack2),
        RelativityDebuff.ShortSupport => Sign.Attack3,
        RelativityDebuff.MediumSupport => Sign.Ignore1,
        RelativityDebuff.MediumDps => Sign.Ignore2,
        RelativityDebuff.LongSupport => NextFree(Sign.Bind1, Sign.Bind2),
        _ => Sign.Bind3,
    };

    private Sign NextFree(params Sign[] numbered) => numbered.First(sign => !selfMarks.ContainsValue(sign));

    private void LeaveLaserNowOthersArriveBy(AiManager ai, float time, int laserWave, Func<PartyRole, Vector3?> at, float arrivalTime)
    {
        ai.Move(time, () => ByRole(role => BaitedLaser(role, laserWave) ? at(role) : null));
        ai.Move(time, () => ByRole(role => BaitedLaser(role, laserWave) ? null : at(role)), arrivalTime: arrivalTime);
    }

    private bool BaitedLaser(PartyRole role, int wave) => Wave(HourglassClock(role)) == wave;

    private void FaceOutwardForShadoweye(SimWorld world)
    {
        foreach (var role in Enum.GetValues<PartyRole>())
            if (world.Party.Get(role) is { } member && member.IsAlive() && world.Party.IsBotDriven(member))
            {
                member.StopMoving();
                member.Face(member.Position + Direction(role) * 100f);
            }
    }

    private static int HourglassClockFromSingleYellowTetherNorth(int manaClock) => (manaClock + 4) % 8;

    private int HourglassClock(PartyRole role) => HourglassClockFromSingleYellowTetherNorth(state.Debuff(role) switch
    {
        RelativityDebuff.LongDps => 0,
        RelativityDebuff.ShortDps => selfMarks[role] == Sign.Attack1 ? 7 : 1,
        RelativityDebuff.MediumDps => 2,
        RelativityDebuff.LongSupport => selfMarks[role] == Sign.Bind1 ? 5 : 3,
        RelativityDebuff.ShortSupport => 4,
        _ => 6,
    });

    private Vector3 Direction(PartyRole role) => state.Direction(HourglassClock(role));
    private Vector3 At(PartyRole role, float radius) => Direction(role) * radius;

    private Vector3 Bait(PartyRole role)
    {
        var direction = Direction(role);
        var tangent = new Vector3(-direction.Z, 0f, direction.X);
        return direction * (23f * S) + tangent * (state.Clockwise(HourglassClock(role)) ? -1f : 1f) * (4.1f * S);
    }

    private Vector3? FireSpot(PartyRole role, int wave, bool intermediate = false)
    {
        var fireWave = FruP3UltimateRelativityState.FireWave(state.Debuff(role));
        if (wave == 0 && state.Debuff(role) == RelativityDebuff.ShortSupport && !state.DpsIce) return At(role, 20.5f * S);
        return At(role, fireWave == wave ? (intermediate ? 20.5f : 32.5f) * S : 3f * S);
    }

    private Vector3? BaitSpot(PartyRole role, int wave)
    {
        if (BaitedLaser(role, wave)) return Bait(role);
        if (wave == 0) return At(role, state.DarkEruption(role) ? 20.5f * S : 3f * S);
        if (wave == 1 && state.Debuff(role) is RelativityDebuff.MediumDps or RelativityDebuff.MediumSupport) return At(role, 10.25f * S);
        return At(role, 3f * S);
    }

    private Vector3? CenterSpot(PartyRole role) => At(role, 3f * S);

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
