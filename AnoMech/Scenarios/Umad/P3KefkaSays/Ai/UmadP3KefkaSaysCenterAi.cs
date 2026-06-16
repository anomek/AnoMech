using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P3KefkaSays.Ai;

// Party-movement AI for UMAD P3 "Kefka Says".
//
// Handled so far:
//  - The opening three Mystery Magic casts (the Blizzard-cone + Thrumming-Thunder-
//    line "Kefka Says" puzzles that resolve at ~16s / ~31s / ~46s): the whole party
//    stacks on one safe spot a couple of yards off arena centre — inside a fake
//    Blizzard cone's gap and on the safe side of the real Thunder lines.
//  - Flood of Naught (~63s): Neo Exdeath cleaves the whole arena with two giant
//    Antilights (left/right of centre) split by a thin Edge of Death line. Every
//    player is hit by an antilight, so each picks the left/right side by colour.
//  - Elemental wave 1 + Death Shriek gaze (~71s/~80s): the first Death Bolt/Wave
//    spread and the Wave1 gaze pair.
//  - Stray Flames (Inferno "Mana Release", ~92s): bait the fire stacked in the
//    middle, then run out for the real Chariot or stay in for the lie's Donut.
//  - Elemental wave 2 under Blizzard (~96s/~97s): the same wave-1 cardinal cross,
//    each spot stepped one notch into Mystery[3]'s Blizzard-safe wedge to clear the cones.
//  - Wave2 Death Shriek (~104s): a centre solve — pair the two gaze sources across
//    the middle and resolve both shrieks with one radial facing, nothing to dodge.
//  - Stray Spray (Tsunami "Mana Release") + last Mystery Magic (~115s): bait the
//    water in the middle, then one final move that clears it (in for the real Donut /
//    out for the fake Chariot) and solves Mystery[4]'s cones+lines at the same time.
//
// All are derived from the per-run randomized state (the ground-truth layout),
// not from the in-game lockon/lie clues, because the AI knows the truth.
//
// That covers every positional mechanic through to the LightOfJudgment enrage
// (~120s). Mana Charge and Ultima Upsurge are raidwides that need no movement. See
// UmadP2ForsakenRinonAiHelper for the fuller pattern.
public sealed class UmadP3KefkaSaysCenterAi : IScenarioAi<UmadP3KefkaSaysState>
{
    public string Name => "Kefka Says (WIP)";

    public void Run(UmadP3KefkaSaysState state, SimWorld world)
    {
        var ai = new AiManager(world);

        // Gather near centre before the first Mystery Magic resolves (~16s).
        ai.Move(2f, () => Stack(new Vector2(0f, 0f)), jitter: 2f, arrivalTime: 9f);
        ai.Move(11.6f, () => Stack(SafeSpot(state.Mystery[0])), jitter: 0.8f, arrivalTime: 15.4f);
        ai.Move(26.4f, () => Stack(SafeSpot(state.Mystery[1])), jitter: 0.8f, arrivalTime: 30.3f);
        ai.Move(41.5f, () => Stack(SafeSpot(state.Mystery[2])), jitter: 0.8f, arrivalTime: 45.4f);

        ai.Move(57.6f, () => FloodOfNaught(state), jitter: 2.5f, arrivalTime: 62.2f);
        ai.Move(63, () => ResolveElements(state.ElemRoles[0], state.ElemTrue[0]), arrivalTime: 70.5f);
        ai.Move(72, () => ResolveGaze(state.Wave1, state.Mystery[3]), jitter: 0, arrivalTime: 76f);
        ai.Move(78.5f, () => ResolveGazeLook(world, state.Mystery[3], state.Wave1True), jitter: 0);

        // Stray Flames: bait the fire stacked in the middle (scenario locks each bait
        // at ~87.3s), then react to the resolved shape, which lands ~92.4s.
        ai.Move(81f, () => Stack(new Vector2(0f, 0f)), jitter: 1f, arrivalTime: 86.5f);
        ai.Move(88f, () => StrayFlames(state.InfernoMystery), jitter: 0.5f, arrivalTime: 92f);

        // Elemental wave 2 (~96.5s) folded into Mystery[3]'s Blizzard-safe wedges (~97.4s).
        ai.Move(92.5f, () => ResolveElementsUnderBlizzard(state.ElemRoles[1], state.ElemTrue[1], state.Mystery[3]), arrivalTime: 96.3f);

        // Wave2 Death Shriek (~104.4s): a pure positioning+facing solve in the middle,
        // nothing else live. Position the pair, then nudge to set the gaze facing.
        ai.Move(98f, () => ResolveGazeCentre(state.Wave2), jitter: 0, arrivalTime: 101.5f);
        ai.Move(102.5f, () => ResolveGazeCentreLook(world, state.Wave2True), jitter: 0);

        // Water bait (~109.98 lock) + last Mystery Magic (Mystery[4], ~115.6s): bait
        // Stray Spray stacked in the middle, then one final move that clears the water
        // (in for the real Donut / out for the fake Chariot) and solves Mystery[4].
        ai.Move(106f, () => Stack(new Vector2(0f, 0f)), jitter: 0.8f, arrivalTime: 109.5f);
        ai.Move(111f, () => StraySprayAndMystery(state.TsunamiMystery, state.Mystery[4]), jitter: 0.3f, arrivalTime: 114.5f);
    }

    // The two gaze sources (Wave1[0]/Wave1[4]) stack 0.5y apart and opposite, just off
    // centre toward Mystery[3]'s Thunder-safe lane (arena centre itself is on a line
    // boundary). Everyone resolves both shrieks by facing toward/away from them; the rest
    // fan ~5y out along the safe lane, since the lines resolve in the same window.
    private IAiMove ResolveGaze(RoleList wave, MysteryCast thunder)
    {
        var rot = LineBaseRotation * thunder.LightningOrientation;
        var lane = new Vector2(MathF.Sin(rot), MathF.Cos(rot));
        var safeSpot = ThunderSafeSpot(thunder);
        var pairCentre = safeSpot * 0.5f;

        var coords = new Vector2?[8];
        coords[(int)wave[0]] = pairCentre + lane * 0.5f;
        coords[(int)wave[4]] = pairCentre - lane * 0.5f;

        int[] others = [1, 2, 3, 5, 6, 7];
        for (var k = 0; k < others.Length; k++)
        {
            var along = (k - (others.Length - 1) / 2f) * 1.5f;
            coords[(int)wave[others[k]]] = safeSpot + lane * along;
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    // MoveTo faces the travel direction, so a small radial step out/in sets the gaze
    // facing the resolver reads: out (look away) when the cast is true, in (look toward)
    // on the lie. The step is backed off if it would land inside a real Thunder line.
    private IAiMove ResolveGazeLook(SimWorld world, MysteryCast thunder, bool lookAway)
    {
        var scale = lookAway ? 1.2f : 0.8f;
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var member = world.Party.Get(i);
            if (member == null || !member.IsAlive()) continue;
            var p = new Vector2(member.Position.X, member.Position.Z);
            coords[i] = ThunderSafeNudge(p, scale, thunder);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    // Scale `p` radially (set facing), easing the scale back toward 1 until the result
    // clears the real Thunder lines.
    private static Vector2 ThunderSafeNudge(Vector2 p, float scale, MysteryCast thunder)
    {
        for (var t = 1f; t > 0f; t -= 0.2f)
        {
            var q = p * (1f + (scale - 1f) * t);
            if (LineClearance(q, thunder) > 1f) return q;
        }
        return p;
    }

    // --- Wave2 Death Shriek (centre solve) ------------------------------------
    //
    // Two gaze sources (wave[0]/wave[4]) and nothing else live, so it's pure
    // positioning + facing. The pair straddles the centre (one either side of the
    // origin along Z), so a radial nudge from centre points each of them away
    // from / toward the other; everyone else rings them at GazeRingRadius and
    // faces radially too, resolving both shrieks at once because the sources sit
    // near the middle. lookAway (Wave2True) -> face out; else face in.
    private const float GazePairOffset = 2f;
    private const float GazeRingRadius = 6f;

    private IAiMove ResolveGazeCentre(RoleList wave)
    {
        var coords = new Vector2?[8];
        coords[(int)wave[0]] = new Vector2(0f, -GazePairOffset);
        coords[(int)wave[4]] = new Vector2(0f, GazePairOffset);

        int[] others = [1, 2, 3, 5, 6, 7];
        for (var k = 0; k < others.Length; k++)
        {
            var th = (k + 0.5f) * (MathF.PI / 3f); // 6 spokes, offset off the N/S pair axis
            coords[(int)wave[others[k]]] = new Vector2(MathF.Sin(th), MathF.Cos(th)) * GazeRingRadius;
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    // Radial nudge from centre to set the gaze facing (cf. ResolveGazeLook, but with
    // nothing to dodge, so no line back-off): scale each position out (look away) or
    // in (look toward) so MoveTo orients everyone relative to the centre pair.
    private static IAiMove ResolveGazeCentreLook(SimWorld world, bool lookAway)
    {
        var scale = lookAway ? 1.2f : 0.8f;
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var member = world.Party.Get(i);
            if (member == null || !member.IsAlive()) continue;
            coords[i] = new Vector2(member.Position.X, member.Position.Z) * scale;
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    // Wave-1 elements: no Blizzard live yet, so the 3+1 / 3+1 stack-and-solo split
    // sits on the cardinals (support stack N + solo W, dps stack S + solo E).
    private IAiMove ResolveElements(RoleList roleList, bool isTrue) =>
        ElementFormation(roleList, isTrue, ElementCardinals);

    // Wave-2 elements (~96.5s) resolve in the same window as Mystery[3]'s Blizzard
    // cones (~97.4s). Keep the exact wave-1 cardinal cross, but step each spot one
    // notch into its Blizzard-safe wedge. Cardinals sit on cone-edge boundaries; the
    // two real cones are an opposite inter-cardinal pair (offset 0 -> SE+NW real, safe
    // NE+SW; offset 1 -> NE+SW real, safe SE+NW), so each spot steps toward its single
    // safe neighbour and the sign flips with the offset. Lethality is offset-only (NOT
    // isTrue — that's the Bolt/Wave lie); only the cones are live here (no Thunder lines).
    private const float BlizzardNudge = 3f;
    private IAiMove ResolveElementsUnderBlizzard(RoleList roleList, bool isTrue, MysteryCast blizzard)
    {
        var s = blizzard.BlizzardOffset == 0 ? BlizzardNudge : -BlizzardNudge;
        return ElementFormation(roleList, isTrue, ElementCardinals, p =>
        {
            p.AddX(0, s); p.AddX(1, s); p.AddX(2, s);    // N stack -> NE (offset 0)
            p.AddY(3, s);                                // W solo  -> SW
            p.AddX(4, -s); p.AddX(5, -s); p.AddX(6, -s); // S stack -> SW
            p.AddY(7, -s);                               // E solo  -> NE
        });
    }

    // Shared element router: positions 0,1 + the stack-marker fill one stack; the
    // solo-marker stands alone. The isTrue swaps send each Death Bolt/Wave marker
    // (roleList 2/3 and 6/7) to the stack-vs-solo spot that matches its required count,
    // mirroring Run_Neo_Exdeath_400040E9_5's minTargets. `adjust` optionally tweaks the
    // coordinates (null = leave on the cardinals).
    private static IAiMove ElementFormation(RoleList roleList, bool isTrue, Vector2?[] coords,
                                            Action<IAiPositions>? adjust = null)
    {
        var move = AiMove.Create(coords)
                         .Assignments([
                             roleList[0],
                             roleList[1],
                             isTrue ? roleList[3] : roleList[2],
                             isTrue ? roleList[2] : roleList[3],
                             roleList[4],
                             roleList[5],
                             isTrue ? roleList[7] : roleList[6],
                             isTrue ? roleList[6] : roleList[7]
                         ]);
        return adjust is null ? move : move.ApplyPositions(adjust);
    }

    // coords[0..2] = stack (3) N, coords[3] = solo W; coords[4..6] = stack (3) S, coords[7] = solo E.
    private static readonly Vector2?[] ElementCardinals =
    {
        new(0f, -10f), new(0f, -10f), new(0f, -10f), new(-10f, 0f),
        new(0f, 10f), new(0f, 10f), new(0f, 10f), new(10f, 0f),
    };

    // Every slot to the same destination.
    private static IAiMove Stack(Vector2 spot) => AiMove.All(spot);

    // --- Stray Flames bait (Inferno "Mana Release") ------------------------------
    //
    // Each player carries the Inferno fire and drops a self-centred AOE where it sits
    // when the debuff expires (scenario Run_Chaos_400040E2_2 locks each bait to the
    // player's position at ~87.3s, then resolves InfernoMystery.Solution ~5s later).
    // The whole party baits stacked in the arena centre so the AOEs land on top of
    // each other, and the resolved shape decides the safe spot:
    //   real -> StrayFlames_Chariot (circle r=6 on the bait) -> everyone runs out
    //   lie  -> StrayFlames_Donut   (safe inside r=6)         -> everyone stays in
    private static IAiMove StrayFlames(ChaosMystery inferno)
    {
        if (!inferno.SolutionIsChariot) // Donut: the middle (inside r=6) is safe — stay stacked.
            return AiMove.All(new Vector2(0f, 0f));

        // Chariot: the circle covers the centre; fan straight out well past r=6.
        const float radius = 11f;
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var th = i * MathF.PI / 4f;
            coords[i] = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    // --- Stray Spray bait (Tsunami "Mana Release") + last Mystery Magic -----------
    //
    // The water bait (~115.1s) resolves a beat before Mystery[4]'s cones+lines
    // (~115.6s — the second Mana Release fires both together), so this single final
    // move has to clear both at once. Everyone baited stacked in the middle, so the
    // resolved water shape just picks which Mystery-safe spot to stack on:
    //   donut (real)   -> safe inside r=6  -> the near Mystery spot (≤6y, also dodges
    //                                          Mystery[4]) keeps us in the donut hole
    //   chariot (fake) -> safe outside r=6 -> a far Mystery spot (8-12y) is both out
    //                                          of the circle and in the safe wedge/lane
    private static IAiMove StraySprayAndMystery(ChaosMystery water, MysteryCast mystery) =>
        AiMove.All(water.SolutionIsChariot ? FarSafeSpot(mystery) : SafeSpot(mystery));

    // --- Mystery Magic safe-spot solver ---------------------------------------
    //
    // Mirrors the hazard geometry the scenario resolves against (see
    // UmadP3KefkaSaysScenario.Run_Kefka_400040E5_1 / Run_Kefka_400040E6_1 and the
    // shapes in CharacterFind), so "safe here" matches "DamageSolver won't hit me".

    // Blizzard: 4 cones, apex at arena centre, facing the 4 inter-cardinals, 45°
    // half-angle. The two with (i + BlizzardOffset) even are real.
    private const float ConeHalfAngle = MathF.PI / 4f;

    // Thrumming Thunder: 4 parallel rect lines (half-width 5, length 40 projected
    // forward from the caster anchor). The two with (i + LightningOffset) even are
    // real. Anchors/rotation match the scenario's `placements`, mirrored on X and
    // rotation-flipped by LightningOrientation (Placement.MulX / MulRot).
    private const float LineHalfWidth = 5f;
    private const float LineLength = 40f;
    private const float LineBaseRotation = -MathF.PI / 4f;
    private static readonly Vector2[] LineAnchors =
    {
        new(24.75f, -3.54f),
        new(17.68f, -10.61f),
        new(10.61f, -17.68f),
        new(3.54f, -24.75f),
    };

    // Scan outward rings for the bearing with the most clearance from every real
    // hazard. Stay ~2y off centre when a layout leaves a roomy spot there, and only
    // step further out for the layouts where the gap and the safe lane don't line up.
    private static Vector2 SafeSpot(MysteryCast mystery)
    {
        const float wantClearance = 1.5f;
        Vector2 best = new(0f, 0f);
        var bestClear = float.NegativeInfinity;
        for (var radius = 2f; radius <= 6f; radius += 0.5f)
        {
            for (var deg = 0; deg < 360; deg += 2)
            {
                var th = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
                var clear = Clearance(q, mystery);
                if (clear > bestClear) { bestClear = clear; best = q; }
            }
            if (bestClear >= wantClearance) break; // a close-in spot is roomy enough
        }
        return best;
    }

    // The best-clearance bearing in the 8-12y ring — used when the water Chariot forces
    // everyone out past r=6 while the last Mystery's cones/lines still have to be dodged.
    private static Vector2 FarSafeSpot(MysteryCast mystery)
    {
        Vector2 best = new(0f, 10f);
        var bestClear = float.NegativeInfinity;
        for (var radius = 8f; radius <= 12f; radius += 0.5f)
        {
            for (var deg = 0; deg < 360; deg += 2)
            {
                var th = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
                var clear = Clearance(q, mystery);
                if (clear > bestClear) { bestClear = clear; best = q; }
            }
        }
        return best;
    }

    // Signed distance to the nearest real-hazard edge at `q` (negative = inside one).
    private static float Clearance(Vector2 q, MysteryCast mystery)
    {
        var min = LineClearance(q, mystery);
        var r = q.Length();
        var qBearing = MathF.Atan2(q.X, q.Y);

        for (var i = 0; i < 4; i++)
        {
            if ((i + mystery.BlizzardOffset) % 2 != 0) continue; // fake cone — harmless
            var coneBearing = MathF.PI / 2f * i + MathF.PI / 4f;
            var d = AngleDiff(qBearing, coneBearing);
            min = MathF.Min(min, r * MathF.Sin(d - ConeHalfAngle)); // < 0 when inside the cone
        }
        return min;
    }

    // Signed distance to the nearest real Thunder-line edge (Blizzard cones ignored —
    // they aren't live during the gaze).
    private static float LineClearance(Vector2 q, MysteryCast mystery)
    {
        var min = float.PositiveInfinity;
        for (var i = 0; i < 4; i++)
        {
            if ((i + mystery.LightningOffset) % 2 != 0) continue; // fake line — harmless
            var anchor = new Vector2(LineAnchors[i].X * mystery.LightningOrientation, LineAnchors[i].Y);
            var rot = LineBaseRotation * mystery.LightningOrientation;
            var fwd = new Vector2(MathF.Sin(rot), MathF.Cos(rot));
            var right = new Vector2(MathF.Cos(rot), -MathF.Sin(rot));
            var rel = q - anchor;
            var along = Vector2.Dot(rel, fwd);
            if (along < 0f || along > LineLength) continue; // beyond the strip's length
            min = MathF.Min(min, MathF.Abs(Vector2.Dot(rel, right)) - LineHalfWidth); // < 0 inside the strip
        }
        return min;
    }

    // The ~5y-off-centre point deepest in a Thunder-safe lane.
    private static Vector2 ThunderSafeSpot(MysteryCast thunder)
    {
        const float radius = 5f;
        Vector2 best = new(0f, radius);
        var bestClear = float.NegativeInfinity;
        for (var deg = 0; deg < 360; deg += 2)
        {
            var th = deg * MathF.PI / 180f;
            var q = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
            var clear = LineClearance(q, thunder);
            if (clear > bestClear) { bestClear = clear; best = q; }
        }
        return best;
    }

    // Smallest angle between two bearings, in [0, PI].
    private static float AngleDiff(float a, float b)
    {
        var d = MathF.Abs(a - b) % (2f * MathF.PI);
        return d > MathF.PI ? 2f * MathF.PI - d : d;
    }

    // --- Flood of Naught antilight split --------------------------------------
    //
    // Neo Exdeath fires three parallel rects across the arena along NeoExdeathDirection
    // (scenario Run_Neo_Exdeath_400040E7_1/E8_2/E9_2): a thin Edge of Death down the
    // centre line (instant death) flanked by two giant Antilights — Antilights[0] on
    // the left (local x -9.5), Antilights[1] on the right (local x +9.5) — which
    // together blanket the whole front. So nobody can dodge: every player picks a
    // side, off the centre line.
    //
    // Each side carries one resolved colour (White/Black, always opposite); a player's
    // wound (White/Black) plus whether the cast is a lie (Wave4True) decides which
    // colour is lethal. The antilight cleanses iff the player holds BeyondDeathState
    // AND takes the *opposite* colour to their wound; a matching colour kills them.
    // So the only players who *can* live in an antilight are the BeyondDeathState
    // holders, and they must mismatch — everyone else takes the matching (lethal) one.
    //
    // Player debuffs come from Wave3 (scenario applies at 51.67s): position parity →
    // BeyondDeath (even) / AllaganField (odd); Wounds[i] → White / Black wound.
    // BeyondDeathState = Wave4True ? BeyondDeath : AllaganField, so the "must-cleanse"
    // parity flips with the lie — that's the "fake cast" dependency.
    private const float AntilightLocalX = 8f; // off the Edge of Death, inside the side rect
    private static IAiMove FloodOfNaught(UmadP3KefkaSaysState state)
    {
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var role = state.Wave3[i];
            var rightSide = (i % 2 == 0) ^ state.Wounds[i] ^ (state.Antilights[0].Antilight == Antilight.White);
            var local = new Vector3(rightSide ? -AntilightLocalX : AntilightLocalX, 0f, 0f);
            var world = state.NeoExdeathDirection.Apply(local);
            coords[(int)role] = new Vector2(world.X, world.Z);
        }
        return AiMove.Create(coords).NaturalOrder();
    }
}
