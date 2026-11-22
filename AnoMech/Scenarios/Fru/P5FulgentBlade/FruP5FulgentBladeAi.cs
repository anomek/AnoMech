using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.P5FulgentBlade;

public sealed class FruP5FulgentBladeAi : IScenarioAi<FruP5FulgentBladeState>
{
    public string Name => "Standard";
    public string Group => "NA";

    private const float FruSimUnitsToYalms = 5f / 11.855f;

    private FruP5FulgentBladeState state = null!;

    public void Run(FruP5FulgentBladeState stateParam, SimWorld world)
    {
        state = stateParam;
        var ai = new AiManager(world);
        ai.Move(0f, () => ByRole(role => role == PartyRole.MainTank ? MainTankNorthOfBoss : null));
        ai.Move(13.2f, () => ByRole(_ => state.Origin), jitter: 0f, arrivalTime: 17.2f);
        ai.Move(17.2f, () => ByRole(_ => Dodge(0)), jitter: 0f, arrivalTime: BeforeStripsRoll(0));
        for (var step = 1; step < 6; step++)
        {
            var dodge = step;
            ai.Move(19.6f + step * 2.05f, () => ByRole(_ => Dodge(dodge)), jitter: 0f, arrivalTime: BeforeStripsRoll(dodge));
        }
        ai.Move(31.9f, () => ByRole(LightPartyStack), arrivalTime: 34f);
    }

    private static float BeforeStripsRoll(int dodge) => FruP5FulgentBladeState.HitAt(0, dodge + 1) - 0.2f;

    private static readonly Vector3 MainTankNorthOfBoss = new(0f, 0f, -6f);

    private Vector3 Dodge(int step)
    {
        var offset = step switch
        {
            0 or 5 => new Vector3(-1.15f, 0f, -2.77f),
            1 or 4 => new Vector3(1.15f, 0f, 2.77f),
            2 => new Vector3(-2.77f, 0f, 1.15f),
            _ => new Vector3(2.77f, 0f, -1.15f),
        };
        return state.Place(state.Pocket + Vector3.Transform(offset * FruSimUnitsToYalms, Quaternion.CreateFromAxisAngle(Vector3.UnitY, state.Rotation)));
    }

    private Vector3? LightPartyStack(PartyRole role)
    {
        var outward = Vector3.Normalize(state.Origin);
        var bossLeft = new Vector3(outward.Z, 0f, -outward.X);
        var side = (int)role % 2 == 0 ? -1f : 1f;
        return outward * (14.6f * FruSimUnitsToYalms) + bossLeft * (side * 9.6f * FruSimUnitsToYalms);
    }

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
