using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Uwu.P1Garuda;

public sealed class UwuP1GarudaAi : IScenarioAi<UwuP1GarudaState>
{
    public string Name => "Zeal / The Balance";

    private const float RingRadius = 9.8f;
    private const float InterceptAheadOfStack = 2.2f;
    private const float MesohighInterceptFromSister = 5.5f;
    private const float FeatherClearance = 3.8f;
    private const float TornadoClearance = 8.8f;
    private const float GigastormClearance = 7.5f;
    private const float ArenaReach = 17.5f;
    private const float SearchStep = 0.5f;

    private static readonly Vector2 SistersStack = new(6.8f, 6.9f);
    private static readonly Vector2 SouthStack = new(0.5f, 4.7f);
    private static readonly Vector2 BubbleCleanseSpot = new(-2.2f, 5.1f);
    private static readonly Vector2 WestTetherHold = new(-8.2f, 1.2f);
    private static readonly Vector2 EastTetherHold = new(7.6f, 0.2f);

    private UwuP1GarudaState state = null!;
    private SimWorld world = null!;

    public void Run(UwuP1GarudaState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);

        ai.Move(0.5f, () => AiMove.Create(OpenerSpots()).NaturalOrder());
        ai.Move(5.4f, MistralSongTargetJoinsHealersEast);
        ai.Move(11.2f, DragGarudaWestPartyBehind);
        ai.Move(34.3f, () => DodgeFeathers(-1f, Keepout.Around(UwuP1GarudaState.GigastormSpot, GigastormClearance)), jitter: 0f);
        ai.Move(38.4f, LeaveGigastormAndTankCentre);
        ai.Move(43.6f, () => AiMove.Create(BubbleGroupSpots()).NaturalOrder());
        ai.Move(46.8f, OffTankAndMeleeStepOutForFirstFriction);
        ai.Move(52.8f, () => StackAt(SouthStack, SouthStack));
        ai.Move(57.3f, () => SingleIntoBubble(PartyRole.MeleeDpsB));
        ai.Move(60.3f, () => SingleIntoBubble(PartyRole.MeleeDpsA));
        ai.Move(63.3f, () => StackAt(SouthStack, new Vector2(0f, -3.2f)));
        ai.Move(69.2f, () => DodgeFeathers(1f), jitter: 0f);
        ai.Move(72.3f, () => StackAt(new Vector2(0f, 2.8f), new Vector2(0f, -3.2f)));
        ai.Move(90.1f, () => DodgeFeathers(1f), jitter: 0f);
        ai.Move(94.3f, TanksInterceptSistersSongsPartyStackSouthEast);
        ai.Move(101.35f, () => DodgeFeathers(-ArenaReach, Keepout.Around(state.SuparnaSongIntercept, TornadoClearance), Keepout.Around(state.ChiradaSongIntercept, TornadoClearance)), jitter: 0f);
        ai.Move(106.0f, () => StackAt(new Vector2(0f, 4.2f), new Vector2(0f, -3.2f)));
        ai.Move(112.4f, () => StackAt(new Vector2(0f, 4.2f), new Vector2(0f, -8.8f)));
        for (var t = 119.7f; t < 123.9f; t += 0.3f)
            ai.Move(t, TakeMesohighTethers, jitter: 0f);
        ai.Move(125.0f, () => StackAt(new Vector2(0f, 4.2f), new Vector2(0f, -8.8f)));
        ai.Move(127.1f, () => DodgeFeathers(-5f), jitter: 0f);
    }

    private static Vector2?[] OpenerSpots() =>
    [
        new(-1f, -3.4f),
        new(3.2f, 0.3f),
        new(6.5f, -0.8f),
        new(6.5f, 0.8f),
        new(-1.5f, 3.5f),
        new(1.5f, 3.5f),
        new(-2.5f, 4.5f),
        new(2.5f, 4.5f),
    ];

    private IAiMove MistralSongTargetJoinsHealersEast()
    {
        var spots = OpenerSpots();
        spots[(int)state.MistralSongTarget] = new Vector2(6.5f, 0f);
        return AiMove.Create(spots).NaturalOrder();
    }

    private static IAiMove DragGarudaWestPartyBehind() => AiMove.Create(
        new(-6.3f, -3.3f),
        new(-8.2f, 1.5f),
        new(-6.6f, 3.8f),
        new(-7.4f, 3.3f),
        new(-8.0f, 2.4f),
        new(-8.0f, 3.2f),
        new(-7.1f, 3.0f),
        new(-6.6f, 3.2f)
    ).NaturalOrder();

    private static IAiMove LeaveGigastormAndTankCentre() => AiMove.Create(
        new(0f, -3.2f),
        new(-1.5f, -1.5f),
        new(1.0f, 4.5f),
        new(1.6f, 4.0f),
        new(0.6f, 3.6f),
        new(1.4f, 5.0f),
        new(2.0f, 4.2f),
        new(0.8f, 5.2f)
    ).NaturalOrder();

    private static Vector2?[] BubbleGroupSpots() =>
    [
        new(-3.8f, 3.2f),
        new(-3.6f, 5.6f),
        new(-2.8f, 5.4f),
        new(-3.0f, 4.6f),
        new(-4.2f, 6.6f),
        new(-4.4f, 5.2f),
        new(-3.3f, 5.8f),
        new(-3.0f, 6.3f),
    ];

    private static IAiMove OffTankAndMeleeStepOutForFirstFriction()
    {
        var spots = BubbleGroupSpots();
        spots[(int)PartyRole.OffTank] = new Vector2(0.2f, 4.4f);
        spots[(int)PartyRole.MeleeDpsA] = new Vector2(0.2f, 3.8f);
        spots[(int)PartyRole.MeleeDpsB] = new Vector2(0.4f, 4.6f);
        return AiMove.Create(spots).NaturalOrder();
    }

    private static IAiMove StackAt(Vector2 party, Vector2 mainTank) => AiMove.Create(StackSpots(party, mainTank)).NaturalOrder();

    private static Vector2?[] StackSpots(Vector2 party, Vector2 mainTank)
    {
        var spots = new Vector2?[8];
        spots[0] = mainTank;
        for (var slot = 1; slot < 8; slot++) spots[slot] = party;
        return spots;
    }

    private static IAiMove SingleIntoBubble(PartyRole cleanser)
    {
        var spots = StackSpots(SouthStack, SouthStack);
        spots[(int)cleanser] = BubbleCleanseSpot;
        return AiMove.Create(spots).NaturalOrder();
    }

    private IAiMove TanksInterceptSistersSongsPartyStackSouthEast()
    {
        var mainTankTakesSuparna = UwuP1GarudaState.BearingOf(state.SuparnaSongSpot) < UwuP1GarudaState.BearingOf(state.ChiradaSongSpot);
        state.SuparnaSongIntercept = ToWorld(InterceptSpot(state.SuparnaSongSpot, SistersStack));
        state.ChiradaSongIntercept = ToWorld(InterceptSpot(state.ChiradaSongSpot, SistersStack));
        var spots = new Vector2?[8];
        spots[0] = Flat(mainTankTakesSuparna ? state.SuparnaSongIntercept : state.ChiradaSongIntercept);
        spots[1] = Flat(mainTankTakesSuparna ? state.ChiradaSongIntercept : state.SuparnaSongIntercept);
        for (var slot = 2; slot < 8; slot++) spots[slot] = SistersStack;
        return AiMove.Create(spots).NaturalOrder();
    }

    private static Vector2 InterceptSpot(Vector3 sister, Vector2 stack)
    {
        var start = Flat(sister);
        var ring = PointOnRingTowards(start, stack);
        if (Vector2.Distance(ring, stack) >= InterceptAheadOfStack && Vector2.Distance(start, ring) < Vector2.Distance(start, stack))
            return ring;
        return stack + Vector2.Normalize(start - stack) * InterceptAheadOfStack;
    }

    private static Vector2 PointOnRingTowards(Vector2 start, Vector2 towards)
    {
        var direction = Vector2.Normalize(towards - start);
        var b = Vector2.Dot(start, direction);
        var c = start.LengthSquared() - RingRadius * RingRadius;
        var along = -b - MathF.Sqrt(MathF.Max(0f, b * b - c));
        return start + direction * along;
    }

    private IAiMove TakeMesohighTethers()
    {
        var spots = StackSpots(new Vector2(0f, 4.2f), new Vector2(0f, -8.8f));
        spots[(int)PartyRole.OffTank] = TetherTakeSpot(state.SuparnaMesohigh, PartyRole.OffTank, WestTetherHold);
        spots[(int)PartyRole.CasterDps] = TetherTakeSpot(state.ChiradaMesohigh, PartyRole.CasterDps, EastTetherHold);
        return AiMove.Create(spots).NaturalOrder();
    }

    private Vector2 TetherTakeSpot(SimTether? tether, PartyRole taker, Vector2 holdSpot)
    {
        if (tether?.A is not { } sister || tether.B is not { } holder || holder == world.Party.Get(taker)) return holdSpot;
        var anchor = Flat(sister.Position);
        var toHolder = Flat(holder.Position) - anchor;
        return toHolder.Length() < 0.1f ? holdSpot : anchor + Vector2.Normalize(toHolder) * MesohighInterceptFromSister;
    }

    private readonly record struct Keepout(Vector2 Centre, float Radius)
    {
        public static Keepout Around(Vector3 centre, float radius) => new(Flat(centre), radius);
    }

    private IAiMove DodgeFeathers(float nonMainTankMinZ, params Keepout[] hazards)
    {
        var puddles = new List<Vector2>();
        for (var slot = 0; slot < 8; slot++)
            if (world.Party.Get(slot) is { } member && member.IsAlive()) puddles.Add(Flat(member.Position));

        var spots = new Vector2?[8];
        for (var slot = 0; slot < 8; slot++)
        {
            if (world.Party.Get(slot) is not { } member || !member.IsAlive()) continue;
            var minZ = slot == (int)PartyRole.MainTank ? -ArenaReach : nonMainTankMinZ;
            spots[slot] = NearestSafeSpot(Flat(member.Position), puddles, hazards, minZ);
        }
        return AiMove.Create(spots).NaturalOrder();
    }

    private static Vector2 NearestSafeSpot(Vector2 from, IReadOnlyList<Vector2> puddles, IReadOnlyList<Keepout> hazards, float minZ)
    {
        var best = from;
        var bestDistance = float.MaxValue;
        for (var x = -ArenaReach; x <= ArenaReach; x += SearchStep)
            for (var z = -ArenaReach; z <= ArenaReach; z += SearchStep)
            {
                var spot = new Vector2(x, z);
                if (spot.Length() > ArenaReach || z < minZ) continue;
                if (puddles.Any(p => Vector2.Distance(p, spot) < FeatherClearance)) continue;
                if (hazards.Any(h => Vector2.Distance(h.Centre, spot) < h.Radius)) continue;
                var distance = Vector2.Distance(from, spot);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = spot;
            }
        return best;
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);

    private static Vector3 ToWorld(Vector2 p) => new(p.X, 0f, p.Y);
}
