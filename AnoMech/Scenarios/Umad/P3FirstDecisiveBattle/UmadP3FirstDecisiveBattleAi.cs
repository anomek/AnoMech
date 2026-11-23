using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P3LimitCut;

namespace AnoMech.Scenarios.Umad.P3FirstDecisiveBattle;

public sealed class UmadP3FirstDecisiveBattleAi : IScenarioAi<UmadP3FirstDecisiveBattleState>
{
    public string Name => "Yoruha";
    public string Group => "JP";

    public void Run(UmadP3FirstDecisiveBattleState state, SimWorld world)
    {
        var ai = new AiManager(world);
        ai.Move(0.1f, () => Positions(r => IsChaosParty(r) ? new Vector3(-10, 0, 0) : new Vector3(10, 0, 0)), jitter: 0f);
        ai.Move(5.4f, () => AiMove.Single(PartyRole.OffTank, CentrePullSpot(state.Chaos!, world.Party.Get(PartyRole.OffTank)!)), jitter: 0f);
        ai.Move(5.4f, () => AiMove.Single(PartyRole.MainTank, CentrePullSpot(state.Exdeath!, world.Party.Get(PartyRole.MainTank)!)), jitter: 0f);
        ai.Move(24.8f, () => Positions(r => state.FixedSpot(r, false)), jitter: 0f);
        ai.Move(25.0f, () => AiMove.Single(PartyRole.OffTank, Flat(state.At(14, -14))), jitter: 0f);
        ai.Move(25.0f, () => AiMove.Single(PartyRole.MainTank, Flat(state.At(-14, 14))), jitter: 0f);
        ai.Move(36.8f, () => AiMove.Single(PartyRole.OffTank, Flat(state.At(5, -5))), jitter: 0f);
        ai.Move(37.0f, () => AiMove.Single(PartyRole.MainTank, Flat(state.At(5, -5))), jitter: 0f);
        ai.Move(45.8f, () => AiMove.Single(PartyRole.MainTank, Flat(state.Exdeath!.Position + Vector3.Normalize(state.Exdeath.Position) * 2f)), jitter: 0f);
        ai.Move(47.0f, () => AiMove.Single(PartyRole.PhysRangedDps, Flat(state.At(5, -5))), jitter: 0f);
        ai.Move(47.0f, () => AiMove.Single(PartyRole.CasterDps, Flat(state.At(5, -5))), jitter: 0f);
        ai.Move(57.0f, () => AiMove.Single(PartyRole.MainTank, Flat(state.At(5, -5))), jitter: 0f);
        ai.Move(61.5f, () => Positions(r => DodgePosition(state, world, r, !state.FrontFirst)), jitter: 0f, sprint: true);
        ai.Move(67.6f, () => Positions(r => DodgePosition(state, world, r, state.FrontFirst)), jitter: 0f, sprint: true);
        ai.Move(69.7f, () => Positions(r => state.FixedSpot(r, true)), jitter: 0f, sprint: true);

    }

    private static Vector2 CentrePullSpot(SimEnemy boss, SimCharacter tank)
    {
        var direction = Vector2.Normalize(new Vector2(-boss.Position.X, -boss.Position.Z));
        return direction * (boss.HitboxRadius + tank.HitboxRadius);
    }

    private static Vector3 DodgePosition(UmadP3FirstDecisiveBattleState state, SimWorld world, PartyRole role, bool frontSafe)
    {
        var boss = state.Chaos!;
        var preferred = state.ImplosionSpot(role, frontSafe);
        var facing = new Vector2(MathF.Sin(boss.Rotation), MathF.Cos(boss.Rotation));
        Vector3 best = default;
        var score = float.MaxValue;
        for (var x = -19; x <= 19; x++)
        for (var z = -19; z <= 19; z++)
        {
            if (x * x + z * z > 19.5f * 19.5f) continue;
            var delta = new Vector2(x - boss.Position.X, z - boss.Position.Z);
            if (delta.LengthSquared() < 4f) continue;
            var dot = MathF.Abs(Vector2.Dot(Vector2.Normalize(delta), facing));
            if (frontSafe ? dot < 0.85f : dot > 0.5f) continue;
            var candidate = new Vector3(x, 0, z);
            var distance = Vector3.DistanceSquared(candidate, world.Party.Get(role)!.Position) + 0.05f * Vector3.DistanceSquared(candidate, preferred);
            if (distance >= score) continue;
            score = distance;
            best = candidate;
        }
        return best;
    }

    internal static bool IsChaosParty(PartyRole role)
        => role is PartyRole.OffTank or PartyRole.ShieldHealer or PartyRole.MeleeDpsA or PartyRole.MeleeDpsB;

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
    private static IAiMove Positions(Func<PartyRole, Vector3> position)
    {
        var coords = new Vector2?[8];
        foreach (var role in Enum.GetValues<PartyRole>()) coords[(int)role] = Flat(position(role));
        return AiMove.Create(coords).NaturalOrder();
    }

    private static void FaceWind(UmadP3FirstDecisiveBattleState state, SimWorld world)
    {
        if (state.Exdeath is not { } boss) return;
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (world.Party.Get(role) is not { } member || !world.Party.IsBotDriven(member)) continue;
            var away = member.Position - boss.Position;
            member.Face(state.Headwinds[role] ? member.Position + away : boss.Position);
        }
    }
}
