using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

public sealed class UmadP3BlackHoleAi : IScenarioAi<UmadP3BlackHoleState>
{
    public string Name => "Black Hole (WIP)";

    private UmadP3BlackHoleState state = null!;
    private SimWorld world = null!;

    public void Run(UmadP3BlackHoleState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);

        ai.Move(7f, StackCentreTanksHoldBossesCentred);
        ai.Move(11f, StackCentre);
        ai.Move(18f, () => DodgeSlap(slapIndex: 0, kefkaIndex: 0));
        ai.Move(26f, StackCentre);
        world.Events.Add(28f, () => GrabTether(kefkaIndex: 0, tetherIndex: 0, playerIndex: 4));
        world.Events.Add(30f, () => PullTether(playerIndex: 4));
        world.Events.Add(34f, () => GrabTether(kefkaIndex: 0, tetherIndex: 0, playerIndex: 4));
        world.Events.Add(34f, () => GrabTether(kefkaIndex: 0, tetherIndex: 1, playerIndex: 0));
        world.Events.Add(36f, () => PullTether(playerIndex: 4));
        world.Events.Add(36f, () => PullTether(playerIndex: 0));
        ai.Move(46.5f, DodgeEdict);
        ai.Move(50.6f, () => DodgeSlap(slapIndex: 1, kefkaIndex: 1));
        ai.Move(56f, StackCentre);
        world.Events.Add(59f, () => GrabTether(kefkaIndex: 1, tetherIndex: 0, playerIndex: 4));
        world.Events.Add(59f, () => GrabTether(kefkaIndex: 1, tetherIndex: 1, playerIndex: 0));
        world.Events.Add(59f, () => GrabTether(kefkaIndex: 1, tetherIndex: 2, playerIndex: 3));
        world.Events.Add(61f, () => PullTether(playerIndex: 4));
        world.Events.Add(61f, () => PullTether(playerIndex: 0));
        world.Events.Add(61f, () => PullTether(playerIndex: 3));
        world.Events.Add(64f, () => GrabTether(kefkaIndex: 1, tetherIndex: 0, playerIndex: 5));
        world.Events.Add(66f, () => ReturnToMiddle(playerIndex: 4));
        world.Events.Add(69f, () => GrabTether(kefkaIndex: 1, tetherIndex: 1, playerIndex: 1));
        world.Events.Add(71f, () => ReturnToMiddle(playerIndex: 0));
        ai.Move(74f, DodgeEdictAndLookUpon);
        ai.Move(80f, StackCentre);
        world.Events.Add(93f, () => GrabTether(kefkaIndex: 2, tetherIndex: 0, playerIndex: 5));
        world.Events.Add(93f, () => GrabTether(kefkaIndex: 2, tetherIndex: 1, playerIndex: 1));
        world.Events.Add(93f, () => GrabTether(kefkaIndex: 2, tetherIndex: 2, playerIndex: 7));
        world.Events.Add(95f, () => PullTether(playerIndex: 5));
        world.Events.Add(95f, () => PullTether(playerIndex: 1));
        world.Events.Add(95f, () => PullTether(playerIndex: 7));
        world.Events.Add(98f, () => GrabTether(kefkaIndex: 2, tetherIndex: 0, playerIndex: 6));
        world.Events.Add(100f, () => ReturnToMiddle(playerIndex: 5));
        world.Events.Add(103f, () => GrabTether(kefkaIndex: 2, tetherIndex: 1, playerIndex: 2));
        world.Events.Add(105f, () => ReturnToMiddle(playerIndex: 1));
        world.Events.Add(110f, () => AnchorMtForImplosion(kefkaIndex: 3));
        ai.Move(117f, () => DodgeImplosion(shockwaveIndex: 0, slapIndex: 2, slapKefkaIndex: 3), jitter: 0f);
        ai.Move(119.2f, () => DodgeImplosion(shockwaveIndex: 1, slapIndex: 2, slapKefkaIndex: 3), jitter: 0f);
        ai.Move(121.3f, () => DodgeSlap(slapIndex: 2, kefkaIndex: 3));
        ai.Move(124f, StackCentre);
        world.Events.Add(127f, () => GrabTether(kefkaIndex: 3, tetherIndex: 0, playerIndex: 6));
        world.Events.Add(127f, () => GrabTether(kefkaIndex: 3, tetherIndex: 1, playerIndex: 2));
        world.Events.Add(129f, () => PullTether(playerIndex: 6));
        world.Events.Add(129f, () => PullTether(playerIndex: 2));
        world.Events.Add(132f, () => GrabTether(kefkaIndex: 3, tetherIndex: 0, playerIndex: 2));
        world.Events.Add(134f, () => DodgeLookUponSplit(tetherPlayerIndex: 2, lookKefkaIndex: 4));
        ai.Move(139f, StackCentre);
    }

    private static IAiMove StackCentreTanksHoldBossesCentred() =>
        AiMove.Create(
            new(6.0f, 0f),
            new(-3.8f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f)).NaturalOrder();

    private static IAiMove StackCentre() => AiMove.All(new(0f, 0f));

    private IAiMove DodgeSlap(int slapIndex, int kefkaIndex)
    {
        var direction = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Right
                            ? state.KefkaPosition[kefkaIndex].Flip()
                            : state.KefkaPosition[kefkaIndex];
        
        return state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left
                   ? AiMove.All(new(9f, 0f)).ApplyPositions(direction.Apply)
                   : (IAiMove)AiMove.Create(
                                        new(7f, 7f), new(7f, 7f),
                                        new(9f, 0f), new(9f, 0f),
                                        new(7f, -7f), new(7f, -7f), new(7f, -7f), new(7f, -7f))
                                    .NaturalOrder()
                                    .ApplyPositions(direction.Apply);
    }

    // Dodge the edict by tucking just behind the casting boss, but as close to arena
    // center as the rect allows so the follow-up slap dodge is a short hop. The edict
    // is a forward rect, so the whole half-plane behind the boss is safe; take the
    // point in it nearest center.
    private IAiMove DodgeEdict()
    {
        var boss = world.Children.OfType<SimEnemy>()
                        .FirstOrDefault(e => e.IsAlive() && e.CastActionId == ActionId.DamningEdict);
        if (boss is null) return StackCentre();

        var bossPos = new Vector2(boss.Position.X, boss.Position.Z);
        var fwd = new Vector2(MathF.Sin(boss.Rotation), MathF.Cos(boss.Rotation));
        const float margin = 3f;   // clearance behind the boss (the rect's back edge)

        // Nearest point to center in the safe half-plane (P - bossPos)·fwd <= -margin.
        var s = margin - Vector2.Dot(bossPos, fwd);
        return AiMove.All(s > 0f ? -fwd * s : Vector2.Zero);
    }

    // Damning Edict (rect 60x80 projected from the boss) and Look Upon Me and Despair
    // (rect 100x16 along the KefkaPosition[2] axis through center) snapshot only ~1.4s
    // apart — too tight to dodge in two hops. Hold one spot safe from both: behind the
    // edict boss and clear of the Look-Upon corridor.
    private IAiMove DodgeEdictAndLookUpon()
    {
        var theta = state.KefkaPosition[2].RadiansFromNorth;
        var axis = new Vector2(-MathF.Sin(theta), MathF.Cos(theta));     // Look-Upon line direction
        var lookRight = new Vector2(MathF.Cos(theta), MathF.Sin(theta)); // perpendicular to it
        const float lookSafe = 11f;   // past the 8y half-width, with margin
        const float behind = 8f;      // distance to stand behind the boss

        var boss = world.Children.OfType<SimEnemy>()
                        .FirstOrDefault(e => e.IsAlive() && e.CastActionId == ActionId.DamningEdict);
        if (boss is null) return AiMove.All(lookRight * lookSafe);

        var bossPos = new Vector2(boss.Position.X, boss.Position.Z);
        var fwd = new Vector2(MathF.Sin(boss.Rotation), MathF.Cos(boss.Rotation));
        var denom = Vector2.Dot(axis, fwd);

        // A corridor edge offset by sign*lookSafe (always clear of Look-Upon), slid
        // along the line to sit `behind` the boss. Falls back to the edge midpoint when
        // the boss faces across the line (sliding can't change how far behind it sits).
        Vector2 Seat(float sign)
        {
            var edge = lookRight * (sign * lookSafe);
            if (MathF.Abs(denom) < 0.1f) return edge;
            var t = (-behind - Vector2.Dot(edge - bossPos, fwd)) / denom;
            return edge + axis * t;
        }

        var a = Seat(1f);
        var b = Seat(-1f);
        float Fwd(Vector2 p) => Vector2.Dot(p - bossPos, fwd);   // < 0 = behind the boss
        bool aOk = Fwd(a) < -1f, bOk = Fwd(b) < -1f;
        if (aOk == bOk) return AiMove.All(a.LengthSquared() <= b.LengthSquared() ? a : b);
        return AiMove.All(aOk ? a : b);
    }

    // Implosion fires two opposite 90-degree cones from Chaos that rotate 90 degrees
    // between the two shockwaves (shockwaveIndex 0 then 1). Read Chaos live (like the
    // edict) and stand in the perpendicular safe wedge relative to it; pick the side
    // toward the upcoming slap so the second shockwave dodge doubles as the slap dodge.
    private IAiMove DodgeImplosion(int shockwaveIndex, int slapIndex, int slapKefkaIndex)
    {
        var boss = world.Children.OfType<SimEnemy>()
                        .FirstOrDefault(e => e.IsAlive() && e.BNpcBaseId == BNpcBaseId.ChaosP3);
        var bossPos = boss is null ? Vector2.Zero : new Vector2(boss.Position.X, boss.Position.Z);
        var bossRot = boss?.Rotation ?? MathF.PI;

        var offset = state.ImplosionAttack == ActionId.LongitudinalImplosion ? 0f : MathF.PI / 2f;
        var coneRot = bossRot + offset + shockwaveIndex * (MathF.PI / 2f);
        var safe = new Vector2(MathF.Sin(coneRot + MathF.PI / 2f), MathF.Cos(coneRot + MathF.PI / 2f));

        var slapSafe = SlapSafeDir(slapIndex, slapKefkaIndex);
        var dir = Vector2.Dot(safe, slapSafe) >= 0f ? safe : -safe;
        float radius = shockwaveIndex == 0 ? 1f : 5f;
        return AiMove.All(bossPos + dir * radius);
    }

    // Unit vector toward the safe side of a Slap Happy (the side away from the r=13
    // circles, which sit at +-10 along the KefkaPosition axis on the `mul` side).
    private Vector2 SlapSafeDir(int slapIndex, int kefkaIndex)
    {
        var phi = state.KefkaPosition[kefkaIndex].RadiansFromNorth;
        var slapX = new Vector2(MathF.Cos(phi), MathF.Sin(phi));
        var mul = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left ? -1f : 1f;
        return slapX * -mul;
    }

    // Park the MT 45 degrees clockwise from Kefka's facing (Kefka sits centre, oriented
    // to KefkaPosition[kefkaIndex]) so Chaos, which the MT holds, is dragged to that known
    // angle before the implosion cast locks in its cone axis. MT looks at Chaos on arrival.
    private void AnchorMtForImplosion(int kefkaIndex)
    {
        var mt = world.Party.Get(PartyRole.MainTank);
        if (mt is null) return;
        var spot = state.KefkaPosition[kefkaIndex].Rotate(1).Apply(new Vector3(0f, 0f, -5f));
        mt.MoveTo(spot);
    }

    private void ReturnToMiddle(int playerIndex) =>
        state.Roles.Get(playerIndex)?.MoveTo(new Vector3(0f, 0f, 0f));

    // Look Upon (rect along the KefkaPosition[lookKefkaIndex] axis through centre) split:
    // the tether holder dodges to one perpendicular safe side, the rest to the opposite
    // side. Both sides clear the 16y corridor.
    private void DodgeLookUponSplit(int tetherPlayerIndex, int lookKefkaIndex)
    {
        var phi = state.KefkaPosition[lookKefkaIndex].RadiansFromNorth;
        var perp = new Vector2(MathF.Cos(phi), MathF.Sin(phi));   // perpendicular to the rect line
        const float dist = 11f;                                   // past the 8y half-width, with margin
        var holderSpot = new Vector3(perp.X * dist, 0f, perp.Y * dist);
        for (int i = 0; i < 8; i++)
            state.Roles.Get(i)?.MoveTo(i == tetherPlayerIndex ? holderSpot : -holderSpot);
    }

    private void GrabTether(int kefkaIndex, int tetherIndex, int playerIndex)
    {
        var tethers = ActiveTethersClockwiseFrom(state.KefkaPosition[kefkaIndex]);
        state.Roles.Get(playerIndex)?.Intercept(tethers.ElementAtOrDefault(tetherIndex));
    }

    private void PullTether(int playerIndex)
    {
        var player = state.Roles.Get(playerIndex);
        if (TetherHeldBy(player)?.B is not { } blackHole) return;
        var spot = CardinalClockwise(new Vector2(blackHole.Position.X, blackHole.Position.Z));
        player?.MoveTo(new Vector3(spot.X, 0f, spot.Y));
    }

    private SimTether? TetherHeldBy(SimCharacter? player) =>
        player is null
            ? null
            : world.Children.OfType<SimTether>()
                   .FirstOrDefault(t => t.IsActive && t.TetherId == TetherId.GrabbyTether && ReferenceEquals(t.A, player));

    private IReadOnlyList<SimTether> ActiveTethersClockwiseFrom(Direction north) =>
        world.Children.OfType<SimTether>()
             .Where(t => t.IsActive && t.TetherId == TetherId.GrabbyTether && t.B is not null)
             .OrderBy(t => ClockwiseFrom(north.RadiansFromNorth, t.B!.Position))
             .ToList();

    private static float ClockwiseFrom(float north, Vector3 p)
    {
        var d = (MathF.Atan2(p.X, -p.Z) - north) % MathF.Tau;
        return d < 0f ? d + MathF.Tau : d;
    }

    private static Vector2 CardinalClockwise(Vector2 cardinal)
    {
        var dir = Vector2.Normalize(cardinal);
        var c = MathF.Cos(MathF.PI / 2f);
        var s = MathF.Sin(MathF.PI / 2f);
        return new Vector2(dir.X * c - dir.Y * s, dir.X * s + dir.Y * c) * 7f;
    }
}
