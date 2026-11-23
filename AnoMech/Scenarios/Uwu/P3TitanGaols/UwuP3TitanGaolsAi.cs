using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Uwu.P3TitanGaols;

public sealed class UwuP3TitanGaolsAi : IScenarioAi<UwuP3TitanGaolsState>
{
    public string Name => "Zeal / The Balance";

    private const float GroupSpread = 0.5f;
    private const float UpheavalStandOff = 3.8f;
    private const float TightSpread = 0.3f;

    private static readonly Vector2 TitansLeftSide = new(12f, -5.8f);

    private UwuP3TitanGaolsState state = null!;

    public void Run(UwuP3TitanGaolsState stateParam, SimWorld world)
    {
        state = stateParam;
        var ai = new AiManager(world);

        ai.Move(0f, () => TankAndParty(new Vector2(0f, 5.5f), new Vector2(0f, -5f)));
        ai.Move(3.8f, () => Group(OppositeFirstJump(13.5f)), arrivalTime: 8.67f);
        ai.Move(9.0f, () => Group(state.FromJumpFrame(new Vector2(14f - UpheavalStandOff, 0f)), 0.1f), jitter: 0f, arrivalTime: 13.1f);
        ai.Move(13.1f, () => Group(UpheavalStandingSpot(), 0.1f), jitter: 0f, arrivalTime: 16.85f);
        ai.Automarker(19.31f, MarkTheGaolsInOrder);
        ai.Automarker(31.0f, () => []);
        ai.Move(19.8f, JailedBesideTheirGaolSpotsOutOfTheLandslide, jitter: 0f);
        ai.Move(19.8f, StackOnTheAxisToBaitTheLandslide, jitter: 0f, arrivalTime: 21.49f);
        ai.Move(21.6f, () => PartyTo(new Vector2(-0.8f, -6.1f), withGaolTargets: false), jitter: 0f, arrivalTime: 23.69f);
        ai.Move(21.6f, () => MainTankTo(new Vector2(-11f, -5f * state.SafeSide)), arrivalTime: 23.69f);
        ai.Move(23.70f, JailedIntoTheChain, jitter: 0.5f, arrivalTime: 24.26f);
        ai.Move(23.75f, StackOnTheAxisToBaitTheLandslide, jitter: 0f, arrivalTime: 25.95f);
        ai.Move(26.05f, () => BaitersTo(new Vector2(-8f, -6f * state.SafeSide)), jitter: 0f, arrivalTime: 27.76f);
        ai.Move(29.3f, () => FreedOutOfItsSludge(2), jitter: 0f);
        ai.Move(30.0f, () => FreedOutOfItsSludge(1), jitter: 0f);
        ai.Move(30.0f, () => MainTankTo(new Vector2(13f, -6f * state.SafeSide)), jitter: 0f, arrivalTime: 39.09f);
        ai.Move(30.65f, () => FreedOutOfItsSludge(0), jitter: 0f);
        ai.Move(30.8f, () => PartyTo(TitansLeftSide, withGaolTargets: false), jitter: 0f);
        ai.Move(32.0f, () => PartyTo(TitansLeftSide, withGaolTargets: true), jitter: 0f);
        ai.Move(39.6f, () => MainTankTo(new Vector2(8f, -6f * state.SafeSide)), jitter: 0f);
    }

    private IAiMove Group(Vector2 anchor, float spread = GroupSpread)
    {
        var spots = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++) spots[slot] = anchor + SpreadOffset(slot, spread);
        return AiMove.Create(spots).NaturalOrder();
    }

    private static Vector2 SpreadOffset(int slot, float spread)
    {
        var angle = slot * MathF.PI / 4f;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spread;
    }

    private static IAiMove Only(PartyRole role, Vector2 spot)
    {
        var spots = new Vector2?[8];
        spots[(int)role] = spot;
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove TankAndParty(Vector2 tank, Vector2 party)
    {
        var spots = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++) spots[slot] = party + SpreadOffset(slot, GroupSpread);
        spots[(int)PartyRole.MainTank] = tank;
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 OppositeFirstJump(float radius) => Flat(UwuP3TitanGaolsState.AtBearing(state.FirstJumpBearing + 180f, radius));

    private Vector2 UpheavalStandingSpot()
    {
        var titan = new Vector2(14f, 0f);
        var landing = new Vector2(-13.5f, 4f * state.SafeSide);
        return state.FromJumpFrame(titan + Vector2.Normalize(landing - titan) * UpheavalStandOff);
    }

    private IAiMove PartyTo(Vector2 spot, bool withGaolTargets)
    {
        var anchor = state.FromJumpFrame(spot);
        var spots = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++)
        {
            var role = (PartyRole)slot;
            if (role == PartyRole.MainTank) continue;
            if (!withGaolTargets && state.GaolTargets.Contains(role)) continue;
            spots[slot] = anchor + SpreadOffset(slot, TightSpread);
        }
        return AiMove.Create(spots).NaturalOrder();
    }

    private Dictionary<PartyRole, Sign> MarkTheGaolsInOrder() =>
        state.GaolTargets.Select((role, order) => (role, order)).ToDictionary(x => x.role, x => Sign.Attack1 + x.order);

    private IAiMove MainTankTo(Vector2 jumpFrameSpot) => Only(PartyRole.MainTank, state.FromJumpFrame(jumpFrameSpot));

    private IAiMove StackOnTheAxisToBaitTheLandslide() => BaitersTo(new Vector2(-11f, 0f));

    private IAiMove BaitersTo(Vector2 jumpFrameSpot)
    {
        var spots = new Vector2?[8];
        var anchor = state.FromJumpFrame(jumpFrameSpot);
        for (var slot = 0; slot < 8; slot++)
            if (!state.GaolTargets.Contains((PartyRole)slot)) spots[slot] = anchor + SpreadOffset(slot, TightSpread);
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 BesideGaolSpot(int order) => state.FromJumpFrame(order switch
    {
        0 => new Vector2(5.5f, 3.5f * state.SafeSide),
        1 => new Vector2(0f, 3.8f * state.SafeSide),
        _ => new Vector2(-6.7f, 3.8f * state.SafeSide),
    });

    private IAiMove JailedBesideTheirGaolSpotsOutOfTheLandslide()
    {
        var spots = new Vector2?[8];
        for (var i = 0; i < state.GaolTargets.Count; i++) spots[(int)state.GaolTargets[i]] = BesideGaolSpot(i);
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove JailedIntoTheChain()
    {
        var spots = new Vector2?[8];
        for (var i = 0; i < state.GaolTargets.Count; i++) spots[(int)state.GaolTargets[i]] = state.GaolSpot(i);
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove FreedOutOfItsSludge(int order)
    {
        var gaol = state.GaolSpot(order);
        var outOfTheLine = gaol + state.FromJumpFrame(new Vector2(0f, -7f));
        return Only(state.GaolTargets[order], outOfTheLine);
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
}
