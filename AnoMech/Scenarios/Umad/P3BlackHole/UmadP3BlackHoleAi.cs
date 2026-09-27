using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

public sealed class UmadP3BlackHoleAi(UmadP3BlackHoleAi.TetherOrder tetherOrder) : IScenarioAi<UmadP3BlackHoleState>
{
    public enum TetherOrder { DpsSupportAccretion, SupportDpsAccretion }

    public string Name => tetherOrder switch
    {
        TetherOrder.SupportDpsAccretion => "Black Hole: S>D>A",
        _ => "Black Hole: D>S>A",
    };

    // The timeline below is written in D>S>A seats. S>D>A is the same choreography with the
    // three support seats and the three DPS seats trading places, line position kept; the two
    // Accretion seats (3 and 7) take the third tether either way.
    private static readonly int[] SupportsBeforeDps = [4, 5, 6, 3, 0, 1, 2, 7];

    private int TetherSeat(int seat) =>
        tetherOrder == TetherOrder.SupportDpsAccretion ? SupportsBeforeDps[seat] : seat;

    private UmadP3BlackHoleState state = null!;
    private SimWorld world = null!;

    public void Run(UmadP3BlackHoleState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        assignedHole.Clear();
        var ai = new AiManager(world);

        world.Events.Add(25.17f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[0]);
        world.Events.Add(55.70f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[1]);
        world.Events.Add(89.95f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[2]);
        world.Events.Add(123.34f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[3]);

        ai.Move(7f, StackCentreTanksHoldBossesCentred);
        ai.Move(11f, StackCentre);
        // No arrivalTime: this wave has four staggered resolves behind one Move, and deferring
        // to the last one wiped the party on the first rows.
        ai.Move(18f, () => DodgeSlap(slapIndex: 0, kefkaIndex: 0));
        // A full second before GrabTether, or StackCentre's deferred MoveTo cancels the Intercept.
        ai.Move(25f, StackCentre);
        world.Events.Add(26.2f, () => GrabTether(tetherIndex: 0, playerIndex: 4));
        world.Events.Add(28.2f, () => PullTether(playerIndex: 4));
        world.Events.Add(32.4f, () => GrabTether(tetherIndex: 0, playerIndex: 4));
        world.Events.Add(32.4f, () => GrabTether(tetherIndex: 1, playerIndex: 0));
        world.Events.Add(34.4f, () => PullTether(playerIndex: 4));
        world.Events.Add(34.4f, () => PullTether(playerIndex: 0));
        // Null (a Share plan) means no invuln -- mitigation handles it instead.
        if (ThunderIIIPlanning.InvulnRole(state.ThunderSet1) is { } set1InvulnRole)
            ai.GiveInvuln(38f, set1InvulnRole);
        // Follow self-sustains, so no AiMove.
        world.Events.Add(40.5f, ResolveFirstThunder);
        ai.Move(43.19f, SwapFirstThunderTanks);
        // Standing invariant for the Set 1 danger window; the swap lands at 43.5f.
        {
            var (set1First, set1Second) = ThunderIIIPlanning.Roles(state.ThunderSet1);
            ScheduleThunderClearance(39.5f, 43.5f, 46.2f, set1First, set1Second);
        }
        ai.Move(46.5f, DodgeEdict);
        // Sprint: DodgeEdict's spot can be ~20y from the slap target, more than RunSpeed covers
        // in the ~1.7s available.
        ai.Move(50.6f, () => DodgeSlap(slapIndex: 1, kefkaIndex: 1), sprint: true);
        ai.Move(56f, StackCentre);
        world.Events.Add(58.1f, () => GrabTether(tetherIndex: 0, playerIndex: 4));
        world.Events.Add(58.1f, () => GrabTether(tetherIndex: 1, playerIndex: 0));
        world.Events.Add(58.1f, () => GrabTether(tetherIndex: 2, playerIndex: 3));
        world.Events.Add(60.1f, () => PullTether(playerIndex: 4));
        world.Events.Add(60.1f, () => PullTether(playerIndex: 0));
        world.Events.Add(60.1f, () => PullTether(playerIndex: 3));
        world.Events.Add(64f, () => GrabTether(tetherIndex: 0, playerIndex: 5, intercept: 1f));
        world.Events.Add(66f, () => ReturnToMiddle(playerIndex: 4));
        world.Events.Add(69f, () => GrabTether(tetherIndex: 1, playerIndex: 1, intercept: 1f));
        world.Events.Add(71f, () => ReturnToMiddle(playerIndex: 0));
        ai.Move(73.1f, DodgeEdictAndLookUpon);
        ai.Move(80f, StackCentre);
        // Only an InvulnsBoth plan grants a scripted invuln; a Share relies on mitigation.
        if (ThunderIIIPlanning.InvulnRole(state.ThunderSet2) is { } set2InvulnRole)
            ai.GiveInvuln(79f, set2InvulnRole);
        world.Events.Add(82f, ResolveSecondThunder);
        ai.Move(84.5f, SwapSecondThunderTanks);
        // Same as Set 1; the swap lands at 84.9f.
        {
            var (set2First, set2Second) = ThunderIIIPlanning.Roles(state.ThunderSet2);
            ScheduleThunderClearance(80.5f, 84.9f, 87.5f, set2First, set2Second);
        }
        ai.Move(88f, StackCentre);
        world.Events.Add(92.3f, () => GrabTether(tetherIndex: 0, playerIndex: 5));
        world.Events.Add(92.3f, () => GrabTether(tetherIndex: 1, playerIndex: 1));
        world.Events.Add(92.3f, () => GrabTether(tetherIndex: 2, playerIndex: 7));
        world.Events.Add(94.3f, () => PullTether(playerIndex: 5));
        world.Events.Add(94.3f, () => PullTether(playerIndex: 1));
        world.Events.Add(94.3f, () => PullTether(playerIndex: 7));
        world.Events.Add(98f, () => GrabTether(tetherIndex: 0, playerIndex: 6, intercept: 1f));
        world.Events.Add(100f, () => ReturnToMiddle(playerIndex: 5));
        world.Events.Add(103f, () => GrabTether(tetherIndex: 1, playerIndex: 2, intercept: 1f));
        world.Events.Add(105f, () => ReturnToMiddle(playerIndex: 1));
        // 7, 6 and 2 hold their tether spots until the wave's holes despawn (~109.3): earlier,
        // a still-tethered hole's next Nothingness would follow them into the stack. Recalled
        // with lead time before Implosion.
        world.Events.Add(109.5f, () => ReturnToMiddle(playerIndex: 7));
        world.Events.Add(110.5f, () => ReturnToMiddle(playerIndex: 6));
        world.Events.Add(111.5f, () => ReturnToMiddle(playerIndex: 2));
        world.Events.Add(110f, () => AnchorMtForImplosion(kefkaIndex: 3));
        // arrivalTime = each hit's actual resolve time, so a role that needs more than RunSpeed
        // sprints instead of arriving late.
        ai.Move(117f, () => DodgeImplosion(shockwaveIndex: 0, slapIndex: 2, slapKefkaIndex: 3), jitter: 0f, arrivalTime: 119.09f);
        ai.Move(119.2f, () => DodgeImplosion(shockwaveIndex: 1, slapIndex: 2, slapKefkaIndex: 3), jitter: 0f, arrivalTime: 121.11f);
        ai.Move(121.3f, () => DodgeSlap(slapIndex: 2, kefkaIndex: 3), arrivalTime: 123.25f);
        ai.Move(124f, StackCentre);
        world.Events.Add(125.9f, () => GrabTether(tetherIndex: 0, playerIndex: 6));
        world.Events.Add(125.9f, () => GrabTether(tetherIndex: 1, playerIndex: 2));
        world.Events.Add(127.9f, () => PullTether(playerIndex: 6));
        world.Events.Add(127.9f, () => PullTether(playerIndex: 2));
        world.Events.Add(132f, () => GrabTether(tetherIndex: 0, playerIndex: 2));
        ai.Move(134f, () => DodgeLookUponSplitHolder(tetherPlayerIndex: 2, lookKefkaIndex: 4), sprint: true);
        ai.Move(134f, () => DodgeLookUponSplitOthers(tetherPlayerIndex: 2, lookKefkaIndex: 4), sprint: true);
        ai.Move(139f, PrepositionForStomp);
        ai.Move(147f, StompBlizzardCorners);
        ai.Move(149.8f, StompStackAndTowers);
        ai.Move(152.4f, StompSwapWest);
        ai.Move(153.7f, StompSwapEast);
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

    // Right's slots are grouped by role: each cone targets one tank, healer and dps, and the
    // co-members share the target's spot as an intentional shared soak.
    private IAiMove DodgeSlap(int slapIndex, int kefkaIndex)
    {
        var direction = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Right
                            ? state.KefkaPosition[kefkaIndex].Flip()
                            : state.KefkaPosition[kefkaIndex];

        IAiMove move = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left
                   ? AiMove.All(new(9f, 0f)).ApplyPositions(direction.Apply).ApplyPositions(p => p.Multiply(1, 9f/7f))
                   : (IAiMove)AiMove.Create(
                                        new(7f, 7f), new(9f, 9f),
                                        new(9f, 0f), new(9f, 0f),
                                        new(7f, -7f), new(7f, -7f), new(7f, -7f), new(7f, -7f))
                                    .NaturalOrder()
                                    .ApplyPositions(direction.Apply);
        for (int i = 0; i < 8; i++)
        {
            var member = world.Party.Get(i);
            if (member is null || !member.IsAlive()) continue;
            AnoMech.Core.DiagnosticLog.Info(
                $"[UmadP3BlackHoleAi] DodgeSlap({slapIndex},{kefkaIndex}): role{i} from ({member.Position.X:F1},{member.Position.Z:F1}) -> target {move[i]}.");
        }
        return move;
    }

    // First Lightning III from Exdeath: a stack-radius tank buster snapshotted twice,
    // 3s apart, onto whoever is closest to Exdeath. The OT eats both by standing in the
    // middle of Exdeath's hitbox (so it stays the closest); everyone else clears the
    // blast by sliding straight out from Exdeath's centre along their own bearing.
    private const float ThunderBusterRadius = 8f;     // blast radius non-OTs must clear
    private const float ThunderClearDistance = 10f;   // where they park, just past it

    // Follow, not a coordinate snapshot: Exdeath keeps chasing OffTank until RunThunder's freeze.
    private void ResolveFirstThunder()
    {
        if (state.ScenarioObjects.Exdeath is not { } exdeath) return;
        var (first, _) = ThunderIIIPlanning.Roles(state.ThunderSet1);
        if (world.Party.Get((int)first) is { } firstMember && firstMember.IsAlive())
            firstMember.Follow(exdeath);
    }

    private void ResolveSecondThunder()
    {
        if (state.ScenarioObjects.Exdeath is not { } exdeath) return;
        var (first, _) = ThunderIIIPlanning.Roles(state.ThunderSet2);
        if (world.Party.Get((int)first) is { } firstMember && firstMember.IsAlive())
            firstMember.Follow(exdeath);
    }

    // Shared by both sets' swap: after the first hit the two tanks trade spots, so "first"
    // slides into the middle and "second" takes over the seat. Reads live positions (Exdeath
    // is frozen across both hits). No-op if this set's plan doesn't call for a swap.
    private IAiMove SwapThunderTanks(ThunderIIIAssignment plan)
    {
        var (first, second) = ThunderIIIPlanning.Roles(plan);
        if (second is not { } secondRole) return AiMove.Create().NaturalOrder();
        var a = world.Party.Get(first);
        var b = world.Party.Get(secondRole);
        if (a is null || b is null) return AiMove.Create().NaturalOrder();

        var coords = new Vector2?[8];
        coords[(int)secondRole] = new Vector2(a.Position.X, a.Position.Z);
        coords[(int)first]      = new Vector2(b.Position.X, b.Position.Z);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove SwapFirstThunderTanks() => SwapThunderTanks(state.ThunderSet1);
    private IAiMove SwapSecondThunderTanks() => SwapThunderTanks(state.ThunderSet2);

    // Keeps every other role at least ThunderClearDistance from Exdeath, reasserted repeatedly
    // rather than predicted once, since Exdeath keeps chasing OffTank. Exdeath only: MainTank
    // permanently follows Chaos, and only Exdeath casts Thunder III.
    private void EnforceThunderClearanceOnce(PartyRole first, PartyRole? second)
    {
        // Re-seated against first's live position each check; first is still converging on Exdeath.
        if (second is { } secondRole
            && world.Party.Get((int)first) is { } firstMember && firstMember.IsAlive()
            && world.Party.Get((int)secondRole) is { } secondMember && secondMember.IsAlive())
        {
            var firstPos = new Vector2(firstMember.Position.X, firstMember.Position.Z);
            var secondPos = new Vector2(secondMember.Position.X, secondMember.Position.Z);
            var offset = secondPos - firstPos;
            if (offset.LengthSquared() < ThunderClearDistance * ThunderClearDistance)
            {
                var away = firstPos.LengthSquared() > 1e-4f ? Vector2.Normalize(-firstPos) : new Vector2(0f, -1f);
                var seat = firstPos + RotateVec(away, MathF.PI / 2f) * ThunderClearDistance;
                // The seat can land past the wall when first is near the edge.
                var seatClearRadius = ArenaRadius - 1f;
                if (seat.LengthSquared() > seatClearRadius * seatClearRadius)
                    seat = seat.LengthSquared() > 1e-4f ? Vector2.Normalize(seat) * seatClearRadius : seat;
                secondMember.MoveTo(new Vector3(seat.X, 0f, seat.Y));
            }
        }

        if (state.ScenarioObjects.Exdeath is { } exdeath && exdeath.IsAlive())
        {
            var bossPos = new Vector2(exdeath.Position.X, exdeath.Position.Z);
            for (int i = 0; i < 8; i++)
            {
                var role = (PartyRole)i;
                if (role == first || (second is { } sec && role == sec)) continue;
                if (world.Party.Get(i) is not { } member || !member.IsAlive()) continue;
                var pos = new Vector2(member.Position.X, member.Position.Z);
                var offset = pos - bossPos;
                if (offset.LengthSquared() >= ThunderClearDistance * ThunderClearDistance) continue;
                var dir = offset.LengthSquared() > 1e-4f ? Vector2.Normalize(offset) : new Vector2(1f, 0f);
                var target = bossPos + dir * ThunderClearDistance;
                // "Away from Exdeath" can point through the wall when Exdeath is near the edge.
                var clearRadius = ArenaRadius - 1f;
                if (target.LengthSquared() > clearRadius * clearRadius)
                    target = target.LengthSquared() > 1e-4f ? Vector2.Normalize(target) * clearRadius : target;
                member.MoveTo(new Vector3(target.X, 0f, target.Y));
            }
        }
    }

    // Split at swapTime: seat-reassertion would fight SwapThunderTanks mid-crossing. After the
    // swap, second is settled at the boss and first is a normal bystander.
    private const float ThunderClearanceInterval = 0.4f;

    private void ScheduleThunderClearance(float fromTime, float swapTime, float toTime, PartyRole first, PartyRole? second)
    {
        for (var t = fromTime; t < swapTime; t += ThunderClearanceInterval)
            world.Events.Add(t, () => EnforceThunderClearanceOnce(first, second));
        var postSwapExcluded = second ?? first;
        for (var t = swapTime; t <= toTime; t += ThunderClearanceInterval)
            world.Events.Add(t, () => EnforceThunderClearanceOnce(postSwapExcluded, null));
    }

    // Dodge the edict by tucking just behind the casting boss, but as close to arena
    // center as the rect allows so the follow-up slap dodge is a short hop. The edict
    // is a forward rect, so the whole half-plane behind the boss is safe; take the
    // point in it nearest center.
    private IAiMove DodgeEdict()
    {
        var boss = state.ScenarioObjects.Chaos;
        if (boss is null) return StackCentre();

        var bossPos = new Vector2(boss.Position.X, boss.Position.Z);
        var fwd = new Vector2(MathF.Sin(boss.Rotation), MathF.Cos(boss.Rotation));
        const float margin = 3f;   // clearance behind the boss (the rect's back edge)

        // Nearest point to center in the safe half-plane (P - bossPos)·fwd <= -margin.
        var s = margin - Vector2.Dot(bossPos, fwd);
        var target = s > 0f ? -fwd * s : Vector2.Zero;
        AnoMech.Core.DiagnosticLog.Info(
            $"[UmadP3BlackHoleAi] DodgeEdict: boss at ({bossPos.X:F1},{bossPos.Y:F1}) rot={boss.Rotation:F3} -> target ({target.X:F1},{target.Y:F1}).");
        return AiMove.All(target);
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
        const float behind = 3f;      // distance to stand behind the boss

        var boss = state.ScenarioObjects.Chaos;
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

    // Implosion fires two +-45deg Shockwave cones from Chaos, the axis rotating 90deg between
    // the two shockwaves, so the only bearings safe from both are the four diagonals at 45deg
    // to the cone axis. Of those, take the diagonal whose arena-border end is deepest into the
    // slap-safe half (farthest from the slap-dangerous edge). Resolve where that diagonal crosses
    // the line parallel to the slap divide that sits 10y into the safe half; if the diagonal
    // never reaches that line inside the arena, hug the border (1y in) instead. Then nudge each
    // shockwave a few degrees off the diagonal toward the perpendicular of its own cone so neither
    // dodge stands on the (hit-counting) cone edge. Read Chaos live, like the edict.
    private const float ArenaRadius = 20f;             // ~outer Black Hole ring (z=-17), tune in-game
    private const float ImplosionSlapSafeMargin = 10f; // park this far past the slap divide, into the safe half
    private const float ImplosionBorderInset = 1f;     // fallback: this far inside the arena border
    private const float ImplosionConeLean = 0.18f;     // ~10deg off the cone edge

    private IAiMove DodgeImplosion(int shockwaveIndex, int slapIndex, int slapKefkaIndex)
    {
        var boss = state.ScenarioObjects.Chaos;
        if (boss is null) return StackCentre();

        var c = new Vector2(boss.Position.X, boss.Position.Z);
        var offset = state.ImplosionAttack == ActionId.LongitudinalImplosion ? 0f : MathF.PI / 2f;
        var baseAxis = boss.Rotation + offset;

        // Slap divide runs through arena center perpendicular to safeDir; dot(P, safeDir) is the
        // signed distance into the slap-safe half. Danger edge is the arena rim on the unsafe side.
        var safeDir = SlapSafeDir(slapIndex, slapKefkaIndex);
        var dangerEdge = -safeDir * ArenaRadius;

        // Of the four 45deg diagonals, keep the one whose outward arena-border point is farthest
        // from that danger edge - deepest into the slap-safe half, robust to Chaos off-center.
        var bestDir = Vector2.Zero;
        var bestBorderDist = 0f;
        var bestDist = float.MinValue;
        for (var k = 0; k < 4; k++)
        {
            var theta = baseAxis + MathF.PI / 4f + k * (MathF.PI / 2f);
            var dir = new Vector2(MathF.Sin(theta), MathF.Cos(theta));
            var borderDist = RayToArenaBorder(c, dir);
            var dist = Vector2.DistanceSquared(c + dir * borderDist, dangerEdge);
            if (dist > bestDist) { bestDist = dist; bestDir = dir; bestBorderDist = borderDist; }
        }

        // Where the diagonal crosses the safe-side parallel line (dot(P, safeDir) == margin):
        // c + t*bestDir lies on it at t = (margin - dot(c, safeDir)) / dot(bestDir, safeDir).
        // If that crossing is ahead of Chaos and inside the arena, dodge around it; otherwise
        // the diagonal leaves the arena before reaching the line, so hug the border instead.
        var denom = Vector2.Dot(bestDir, safeDir);
        var length = bestBorderDist - ImplosionBorderInset;
        if (denom > 1e-4f)
        {
            var t = (ImplosionSlapSafeMargin - Vector2.Dot(c, safeDir)) / denom;
            if (t > 0f && t <= bestBorderDist) length = t;
        }

        // Lean off the diagonal toward the perpendicular of THIS shockwave's cone axis, so we
        // sit just inside the 90deg safe band instead of on its edge.
        var coneAxisAngle = baseAxis + shockwaveIndex * (MathF.PI / 2f);
        var coneAxis = new Vector2(MathF.Sin(coneAxisAngle), MathF.Cos(coneAxisAngle));
        var perp = RotateVec(coneAxis, MathF.PI / 2f);
        if (Vector2.Dot(perp, bestDir) < 0f) perp = -perp;
        var lean = MathF.Sign(bestDir.X * perp.Y - bestDir.Y * perp.X) * ImplosionConeLean;

        return AiMove.All(c + RotateVec(bestDir, lean) * length);
    }

    // Distance from `from` (inside the arena) along unit `dir` to the arena-circle border.
    private static float RayToArenaBorder(Vector2 from, Vector2 dir)
    {
        var proj = Vector2.Dot(from, dir);
        return -proj + MathF.Sqrt(proj * proj + ArenaRadius * ArenaRadius - from.LengthSquared());
    }

    // Rotate an XZ vector by `radians` (CCW in the X->Z plane).
    private static Vector2 RotateVec(Vector2 v, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
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
        state.Roles.Get(TetherSeat(playerIndex))?.MoveTo(new Vector3(0f, 0f, 0f));

    // Look Upon (rect along the KefkaPosition[lookKefkaIndex] axis through centre, 16y
    // wide) split. The tether holder rides the last black hole's tether out to the arena
    // edge, but the hole's bearing may sit in the Look-Upon corridor; nudge it ±45° to the
    // side that clears the line and send the holder there. The rest take the opposite edge
    // (180°), which the centre-symmetric corridor leaves equally clear.
    private Vector3 LookUponHolderSpot(int holderSeat, int lookKefkaIndex)
    {
        var theta = state.KefkaPosition[lookKefkaIndex].RadiansFromNorth;   // Look-Upon line bearing

        // Bearing centre→black hole the holder is tethered to. Falls back to the line's
        // perpendicular (always clear) if no active tether is readable.
        var holder = state.Roles.Get(holderSeat);
        var blackHole = (TetherHeldBy(holder) ?? state.ScenarioObjects.Tethers.FirstOrDefault())?.A;
        var alpha = blackHole is { } bh
                        ? MathF.Atan2(bh.Position.X, -bh.Position.Z)
                        : theta + MathF.PI / 2f;

        // Of alpha±45°, at least one clears the 8y half-width corridor (edge perpendicular
        // = edge·|sin(β-θ)|, ≥ 12.7y even with the hole on the line). Take the side farther
        // from the line — the larger |sin(β-θ)| — which is always the safe one.
        var plus = alpha + MathF.PI / 4f;
        var minus = alpha - MathF.PI / 4f;
        var beta = MathF.Abs(MathF.Sin(plus - theta)) >= MathF.Abs(MathF.Sin(minus - theta)) ? plus : minus;

        const float edge = 18f;   // ride out to the arena edge, past the hole's r=17 ring
        return new Vector3(edge * MathF.Sin(beta), 0f, -edge * MathF.Cos(beta));
    }

    // Both trips sprint against a 3.67s deadline. tetherPlayerIndex is a slot into state.Roles,
    // not a PartyRole ordinal.
    private IAiMove DodgeLookUponSplitHolder(int tetherPlayerIndex, int lookKefkaIndex)
    {
        var holderSeat = TetherSeat(tetherPlayerIndex);
        var holderSpot = LookUponHolderSpot(holderSeat, lookKefkaIndex);
        var holderRole = state.Roles[holderSeat];
        var coords = new Vector2?[8];
        coords[(int)holderRole] = new Vector2(holderSpot.X, holderSpot.Z);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove DodgeLookUponSplitOthers(int tetherPlayerIndex, int lookKefkaIndex)
    {
        var holderSeat = TetherSeat(tetherPlayerIndex);
        var holderSpot = LookUponHolderSpot(holderSeat, lookKefkaIndex);
        var holderRole = state.Roles[holderSeat];
        var coords = new Vector2?[8];
        for (int i = 0; i < 8; i++)
            if ((PartyRole)i != holderRole)
                coords[i] = new Vector2(-holderSpot.X, -holderSpot.Z);
        return AiMove.Create(coords).NaturalOrder();
    }

    // The hole (tether.A) each player's last GrabTether sent them to. PassableEnd seeds an
    // unrelated hole's tether with a random member, so PullTether must pull on the assigned
    // hole, not whatever tether the player happens to hold. The hole, not the SimTether: a
    // peer recreates its local SimTether on every endpoint change.
    private readonly Dictionary<int, SimCharacter?> assignedHole = new();

    private void GrabTether(int tetherIndex, int playerIndex, float intercept = 3f)
    {
        var seat = TetherSeat(playerIndex);
        var player = state.Roles.Get(seat);
        var tether = state.ScenarioObjects.Tethers.ElementAtOrDefault(tetherIndex);
        assignedHole[seat] = tether?.A;
        var role = (player as ISimPartyMember)?.Role.ToString() ?? $"player#{seat}";
        if (tether is null)
        {
            AnoMech.Core.DiagnosticLog.Warn($"[UmadP3BlackHoleAi] GrabTether: tetherIndex {tetherIndex} not found in ScenarioObjects.Tethers ({state.ScenarioObjects.Tethers.Count} known) -- {role} sent nowhere.");
            return;
        }
        var bh = tether.A is { } a ? new Vector2(a.Position.X, a.Position.Z) : Vector2.Zero;
        var from = player is null ? "(no player)" : $"({player.Position.X:F1},{player.Position.Z:F1})";
        AnoMech.Core.DiagnosticLog.Info($"[UmadP3BlackHoleAi] GrabTether: {role} intercepting tetherIndex {tetherIndex} (black hole at ({bh.X:F1},{bh.Y:F1})) from {from}, margin={intercept}.");
        player?.Intercept(tether, intercept);
    }

    // Grab only starts the walk, and a black hole can be ~17y out; a Pull that lands before
    // arrival would otherwise no-op and strand the player.
    private const int PullTetherMaxRetries = 4;

    private void PullTether(int playerIndex, int retriesLeft = PullTetherMaxRetries)
    {
        var seat = TetherSeat(playerIndex);
        var player = state.Roles.Get(seat);
        var role = (player as ISimPartyMember)?.Role.ToString() ?? $"player#{seat}";
        // Only the assigned hole's tether counts (see assignedHole); re-resolved each call.
        var hole = assignedHole.GetValueOrDefault(seat);
        var assigned = hole is null ? null : state.ScenarioObjects.Tethers.FirstOrDefault(t => ReferenceEquals(t.A, hole));
        if (assigned is not { A: { } blackHole, B: { } held } || !ReferenceEquals(held, player))
        {
            AnoMech.Core.DiagnosticLog.Warn($"[UmadP3BlackHoleAi] PullTether: {role} does not hold their assigned tether yet -- pull skipped (still mid-Intercept, grab never happened, or a different hole's random seed briefly gave them someone else's). Current pos ({player?.Position.X:F1},{player?.Position.Z:F1}). Retries left: {retriesLeft}.");
            if (retriesLeft > 0)
                world.Events.Add(1f, () => PullTether(playerIndex, retriesLeft - 1));
            return;
        }
        var bhPos = new Vector2(blackHole.Position.X, blackHole.Position.Z);
        var heldPos = new Vector2(held.Position.X, held.Position.Z);
        // Pull spot, then nudged 1.5y farther from the black hole along the bh→player axis.
        var rawSpot = CardinalClockwise(bhPos) + Vector2.Normalize(heldPos - bhPos) * 1.5f;
        // The raw spot can coincide with a passive hole's avoid radius (both ~14y out), where
        // ClampOutside and Steer fight and the bot stalls; push it clear up front.
        var spot = world.Obstacles.ClampOutside(rawSpot, margin: 2f);
        AnoMech.Core.DiagnosticLog.Info($"[UmadP3BlackHoleAi] PullTether: {role} held at ({heldPos.X:F1},{heldPos.Y:F1}), black hole at ({bhPos.X:F1},{bhPos.Y:F1}) -- moving to ({spot.X:F1},{spot.Y:F1}) (dist {Vector2.Distance(heldPos, spot):F1}y){(spot != rawSpot ? $" [nudged from ({rawSpot.X:F1},{rawSpot.Y:F1}) to clear an obstacle]" : "")}.");
        player?.MoveTo(new Vector3(spot.X, 0f, spot.Y));
    }

    private SimTether? TetherHeldBy(SimCharacter? player) =>
        player is null
            ? null
            : state.ScenarioObjects.Tethers.FirstOrDefault(t => ReferenceEquals(t.B, player));

    // Non-tether players hold centre for the whole wave, so a pulled hole must clear
    // Nothingness's radius from there; 8y still tagged the stack.
    private const float TetherPullRadius = 14f;

    private static Vector2 CardinalClockwise(Vector2 cardinal)
    {
        var dir = Vector2.Normalize(cardinal);
        var c = MathF.Cos(MathF.PI / 3f);
        var s = MathF.Sin(MathF.PI / 3f);
        return new Vector2(dir.X * c - dir.Y * s, dir.X * s + dir.Y * c) * TetherPullRadius;
    }

    // Stomp a Mole resolves in Kefka's spawn frame (KefkaPosition[4]): the two towers
    // sit ±10 along Kefka's east-west, the spreads/stacks along its north-south. All the
    // formations below are authored in that frame and rotated onto it. Roles split into a
    // support group (0-3) and a DPS group (4-7); within each, even indices are the "west"
    // pair and odd indices the "east" pair, so a role's side is consistent from the
    // preposition through the corner spread to the towers.
    private static readonly int[] SupportGroup = [0, 1, 2, 3];
    private static readonly int[] DpsGroup = [4, 5, 6, 7];
    private const float StompTowerX = 10f;        // tower offset along Kefka's east-west axis
    private const float StompTowerPairGap = 1.5f; // the two soakers straddle the tower centre

    // Whichever group owns the first stack marker (StackTargets[0], always one support +
    // one DPS) stacks first; the other group takes the towers first, then they trade.
    private bool SupportsStackFirst() => !state.StackTargets[0].IsDps();

    // Supports gather north of centre, DPS south, ready for the first spread.
    private IAiMove PrepositionForStomp() =>
        AiMove.Create(new(0, -5), new (0, -5), new (0, -5), new (0, -5),
                    new (0, 5), new(0, 5), new (0, 5), new (0, 5))
              .NaturalOrder()
              .ApplyPositions(state.KefkaPosition[4].Apply);

    // First BlizzardIII spreads drop on the prepositioned spots; step out to the four
    // intercardinal corners two-per-corner (supports north, DPS south, west pair vs east).
    private IAiMove StompBlizzardCorners() =>
        AiMove.Create(
                  new(-8f, -10f), new(8f, -10f), new(-10f, -8f), new(10f, -8f),
                  new(-8f, 10f), new(8f, 10f), new(-10f, 8f), new(10f, 8f))
              .NaturalOrder()
              .ApplyPositions(state.KefkaPosition[4].Apply);

    // Second spread has dropped on the corners: the stacking group collapses to centre
    // for the first knockback stack, the other group pairs onto the two towers.
    private IAiMove StompStackAndTowers()
    {
        var coords = new Vector2?[8];
        foreach (var r in SupportsStackFirst() ? SupportGroup : DpsGroup) coords[r] = Vector2.Zero;
        PlaceOnTowers(coords, SupportsStackFirst() ? DpsGroup : SupportGroup);
        return AiMove.Create(coords).NaturalOrder().ApplyPositions(state.KefkaPosition[4].Apply);
    }

    // The swap is staggered by side because the west tower resolves ~1.3s before the east:
    // the west pairs trade the moment the first stack and the west tower are done, the east
    // pairs only once the east tower has resolved (StompSwapEast). Each half sends the old
    // tower pair back to centre for the second stack and the old stack pair out to its tower.
    private IAiMove StompSwapWest() => StompSwapHalf(0, 2, -StompTowerX);
    private IAiMove StompSwapEast() => StompSwapHalf(1, 3, StompTowerX);

    // pairA/pairB are the two group-array indices making up this side's pair; towerX is
    // the tower they trade onto/off of.
    private IAiMove StompSwapHalf(int pairA, int pairB, float towerX)
    {
        var newTower = SupportsStackFirst() ? SupportGroup : DpsGroup; // old stackers now soak
        var newStack = SupportsStackFirst() ? DpsGroup : SupportGroup; // old soakers now stack
        var coords = new Vector2?[8];
        coords[newTower[pairA]] = new Vector2(towerX, -StompTowerPairGap);
        coords[newTower[pairB]] = new Vector2(towerX, StompTowerPairGap);
        coords[newStack[pairA]] = Vector2.Zero;
        coords[newStack[pairB]] = Vector2.Zero;
        return AiMove.Create(coords).NaturalOrder().ApplyPositions(state.KefkaPosition[4].Apply);
    }

    private static void PlaceOnTowers(Vector2?[] coords, int[] group)
    {
        coords[group[0]] = new Vector2(-StompTowerX, -StompTowerPairGap);
        coords[group[2]] = new Vector2(-StompTowerX, StompTowerPairGap);
        coords[group[1]] = new Vector2(StompTowerX, -StompTowerPairGap);
        coords[group[3]] = new Vector2(StompTowerX, StompTowerPairGap);
    }
}
