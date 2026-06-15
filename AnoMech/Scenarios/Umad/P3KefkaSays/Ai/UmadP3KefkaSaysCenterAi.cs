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
//
// Both are derived from the per-run randomized state (the ground-truth layout),
// not from the in-game lockon/lie clues, because the AI knows the truth.
//
// Everything else (Mana Charge, Ultima Upsurge, the elemental waves, …) is not
// handled yet. See UmadP2ForsakenRinonAiHelper for the fuller pattern to grow into.
public sealed class UmadP3KefkaSaysCenterAi : IScenarioAi<UmadP3KefkaSaysState>
{
    public string Name => "Kefka Says (WIP)";

    public void Run(UmadP3KefkaSaysState state, SimWorld world)
    {
        var ai = new AiManager(world);

        // Gather near centre before the first Mystery Magic resolves (~16s).
        ai.Move(2f, () => Stack(new Vector2(0f, 0f)), jitter: 0f, arrivalTime: 9f);

        // Mystery Magic k resolves at cast+5 (~16.0 / 30.9 / 46.1). Lockons/cast land
        // at ~11 / 26 / 41, so move in once the layout is shown and arrive just before.
        ai.Move(11.6f, () => Stack(SafeSpot(state.Mystery[0])), jitter: 0f, arrivalTime: 15.4f);
        ai.Move(26.4f, () => Stack(SafeSpot(state.Mystery[1])), jitter: 0f, arrivalTime: 30.3f);
        ai.Move(41.5f, () => Stack(SafeSpot(state.Mystery[2])), jitter: 0f, arrivalTime: 45.4f);

        // Flood of Naught: antilights/edge cast at 57.39, resolve at 62.89. Lie state
        // shows at 57.30, so split to colours once it's up and arrive just before.
        ai.Move(57.6f, () => FloodOfNaught(state), jitter: 2.5f, arrivalTime: 62.2f);
    }

    // Every slot to the same destination.
    private static IAiMove Stack(Vector2 spot) => AiMove.All(spot);

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

    // Signed distance to the nearest real-hazard edge at `q` (negative = inside one).
    private static float Clearance(Vector2 q, MysteryCast mystery)
    {
        var min = float.PositiveInfinity;
        var r = q.Length();
        var qBearing = MathF.Atan2(q.X, q.Y);

        for (var i = 0; i < 4; i++)
        {
            if ((i + mystery.BlizzardOffset) % 2 != 0) continue; // fake cone — harmless
            var coneBearing = MathF.PI / 2f * i + MathF.PI / 4f;
            var d = AngleDiff(qBearing, coneBearing);
            min = MathF.Min(min, r * MathF.Sin(d - ConeHalfAngle)); // < 0 when inside the cone
        }

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
            var hasBeyondDeathState = (i % 2 == 0) == state.Wave4True;
            var wound = state.Wounds[i] ? DamageType.White : DamageType.Black;

            // Cleanse group survives by mismatching its wound; everyone else matches (dies).
            var needed = hasBeyondDeathState ? Opposite(wound) : wound;

            // Exactly one side resolves to `needed` (the two are always opposite colours).
            var rightSide = state.Antilights[1].ResolvedDamageType == needed;
            var local = new Vector3(rightSide ? AntilightLocalX : -AntilightLocalX, 0f, 0f);
            var world = state.NeoExdeathDirection.Apply(local);
            coords[(int)role] = new Vector2(world.X, world.Z);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static DamageType Opposite(DamageType t) => t == DamageType.White ? DamageType.Black : DamageType.White;
}
