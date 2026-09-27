using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P1TeleTrouncing;

// Each role to its 7s Tele-portent spot, then its 10s one (from the real resolve at 7.32s), the
// Confetti-3 NW/SE stack/spread spot, then its fixed ring/outside spot (28.44s). During the
// chase window Confused players chase their nearest ally through the scenario's own tick and
// Sleep players don't move. After the chase releases, MysteryFormation puts every role on its
// fixed strat spot unless a real Thunder line covers it, then FaceGaze turns each bot in place.
// Reads state directly (the truth), not Kefka's orbs.
public sealed class UmadP1TeleTrouncingAi : IScenarioAi<UmadP1TeleTrouncingState>
{
    public string Name => "Tele-trouncing";

    private static readonly Vector2 ConfettiStackSupportSpot = new(-6f, -6f);
    private static readonly Vector2 ConfettiSpreadSupportSpot = new(-4f, -4f);
    private static readonly Vector2 ConfettiStackDpsSpot = new(6f, 6f);
    private static readonly Vector2 ConfettiSpreadDpsSpot = new(4f, 4f);

    // Approximations: the guide says "on the boss ring" and "just outside the teleporter" with
    // no distances; the teleporter square's radius is 12.
    private const float RingRadius = 7f;
    private const float OutsideRadius = 15f;

    public void Run(UmadP1TeleTrouncingState state, SimWorld world)
    {
        var ai = new AiManager(world);

        ai.Move(7.5f, () => Spots(state, first: true), jitter: 0.5f, arrivalTime: 14.3f);
        ai.Move(14.4f, () => Spots(state, first: false), jitter: 0.5f, arrivalTime: 17.3f);
        // No arrivalTime: this move starts on the arrow the role just placed, and deferring the
        // walk kept it parked there past the arrow's 3.0s grace.
        ai.Move(17.5f, () => ConfettiSpots(state), jitter: 0.5f);
        // After the 22.78s Confetti knockback finishes sliding (0.7s); an earlier MoveTo cancels it.
        ai.Move(23.6f, () => TetherSpots(state), jitter: 0.5f, arrivalTime: 27.5f);

        // Mystery Magic: lines 41.26s, gaze 41.36s, Flagrant Fire 42.06s. One move, then an
        // in-place turn.
        ai.Move(34.6f, () => MysteryFormation(state), jitter: 0.4f, arrivalTime: 39.5f);
        world.Events.Add(40.9f, () => FaceGaze(state, world));
    }

    // Must match UmadP1TeleTrouncingScenario.ThunderAnchors: half-width 5, length 40 forward
    // from the anchor, mirrored by state.ThunderOrientation; (i + ThunderRealOffset) even = real.
    private const float LineHalfWidth = 5f;
    private const float LineLength = 40f;
    private static readonly float LineBaseRotation = UmadP1TeleTrouncingScenario.ThunderAnchors[0].Rotation;
    private static readonly Vector2[] LineAnchors = DeriveLineAnchors();

    private static Vector2[] DeriveLineAnchors()
    {
        var anchors = UmadP1TeleTrouncingScenario.ThunderAnchors;
        var result = new Vector2[anchors.Length];
        for (var i = 0; i < anchors.Length; i++)
            result[i] = new Vector2(anchors[i].Position.X, anchors[i].Position.Z);
        return result;
    }

    // Signed distance to the nearest real line edge (< 0 = inside); fakes are ignored.
    private static float LineClearance(Vector2 q, int realOffset, float orientation)
    {
        var min = float.PositiveInfinity;
        for (var i = 0; i < 4; i++)
        {
            if ((i + realOffset) % 2 != 0) continue; // fake line -- harmless
            var anchor = new Vector2(LineAnchors[i].X * orientation, LineAnchors[i].Y);
            var rot = LineBaseRotation * orientation;
            var fwd = new Vector2(MathF.Sin(rot), MathF.Cos(rot));
            var right = new Vector2(MathF.Cos(rot), -MathF.Sin(rot));
            var rel = q - anchor;
            var along = Vector2.Dot(rel, fwd);
            if (along < 0f || along > LineLength) continue;
            min = MathF.Min(min, MathF.Abs(Vector2.Dot(rel, right)) - LineHalfWidth);
        }
        return min;
    }

    // Static strat: every role owns one spot and holds it unless a real line covers it. Spread ->
    // the 8 Diamond Waymark spots; Stack -> DPS and supports on opposite corners.

    private static readonly Vector2 Marker1 = new(-6f, -6f);  // MeleeDpsA
    private static readonly Vector2 Marker2 = new(6f, -6f);   // MainTank
    private static readonly Vector2 Marker3 = new(6f, 6f);    // OffTank
    private static readonly Vector2 Marker4 = new(-6f, 6f);   // MeleeDpsB
    private const float SpreadWallRadius = 18f;               // just inside the r=20 arena wall
    private static readonly Vector2 NorthSpot = new(0f, -SpreadWallRadius);  // PhysRangedDps
    private static readonly Vector2 WestSpot = new(-SpreadWallRadius, 0f);   // CasterDps
    private static readonly Vector2 SouthSpot = new(0f, SpreadWallRadius);   // RegenHealer
    private static readonly Vector2 EastSpot = new(SpreadWallRadius, 0f);    // ShieldHealer

    private static readonly (PartyRole Role, Vector2 Spot)[] RangedSpread =
    [
        (PartyRole.PhysRangedDps, NorthSpot), (PartyRole.CasterDps, WestSpot),
        (PartyRole.RegenHealer, SouthSpot), (PartyRole.ShieldHealer, EastSpot),
    ];
    private static readonly (PartyRole Role, Vector2 Marker)[] MeleeSpread =
    [
        (PartyRole.MeleeDpsA, Marker1), (PartyRole.MainTank, Marker2),
        (PartyRole.OffTank, Marker3), (PartyRole.MeleeDpsB, Marker4),
    ];
    private static readonly float[] BothWays = [1f, -1f];

    private const float SpreadLineMargin = 1.5f;
    private const float SpreadSeparation = 6f;      // > the 5y Flagrant Fire III spread radius
    private const float MarkerNudgeMax = 2f;        // clipped marker: nudge this far, else relocate

    private static IAiMove MysteryFormation(UmadP1TeleTrouncingState state)
        => state.FireIsStack ? StackFormation(state) : SpreadFormation(state);

    // DPS stack NW (Diamond WM1), supports SE (WM3); a covered corner slides further out along
    // the same diagonal.
    private static readonly Vector2 NwCorner = Vector2.Normalize(new Vector2(-1f, -1f));
    private static readonly Vector2 SeCorner = Vector2.Normalize(new Vector2(1f, 1f));
    private const float StackCornerRadius = 8.485f; // Diamond WM1/WM3 distance from centre

    private static IAiMove StackFormation(UmadP1TeleTrouncingState state)
    {
        var off = state.ThunderRealOffset;
        var o = state.ThunderOrientation;
        var dpsSpot = StackCorner(NwCorner, off, o);
        var supportsSpot = StackCorner(SeCorner, off, o);

        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
            coords[(int)role] = UmadP1TeleTrouncingState.IsDps(role) ? dpsSpot : supportsSpot;
        return AiMove.Create(coords).NaturalOrder();
    }

    // The corner if clear; else a small perpendicular nudge (a line can run right along the
    // diagonal), then further out along the diagonal.
    private static Vector2 StackCorner(Vector2 dir, int off, float o)
    {
        var spot = dir * StackCornerRadius;
        if (LineClearance(spot, off, o) >= 2f) return spot;
        var perp = new Vector2(-dir.Y, dir.X);
        foreach (var sign in BothWays)
            for (var n = 1f; n <= 4f; n += 1f)
            {
                var q = spot + perp * (n * sign);
                if (LineClearance(q, off, o) >= 2f) return q;
            }
        for (var r = StackCornerRadius + 1f; r <= 17f; r += 0.5f)
            if (LineClearance(dir * r, off, o) >= 2f) return dir * r;
        return spot;
    }

    private static IAiMove SpreadFormation(UmadP1TeleTrouncingState state)
    {
        var off = state.ThunderRealOffset;
        var o = state.ThunderOrientation;
        var crossNorth = CrossAxisNorth(o);
        var coords = new Vector2?[8];
        var taken = new List<Vector2>();

        foreach (var (role, cardinal) in RangedSpread)
        {
            var spot = SlideAlongWall(cardinal, off, o);
            coords[(int)role] = spot;
            taken.Add(spot);
        }

        // Melee/tanks hold their marker or step just off it; a properly covered marker relocates
        // toward centre once every other spot is fixed.
        var displaced = new List<PartyRole>();
        foreach (var (role, marker) in MeleeSpread)
        {
            if (ClearNearMarker(marker, off, o, crossNorth, taken) is { } spot)
            {
                coords[(int)role] = spot;
                taken.Add(spot);
            }
            else
            {
                displaced.Add(role);
            }
        }
        foreach (var role in displaced)
        {
            var spot = NearestSafeSpread(off, o, taken);
            coords[(int)role] = spot;
            taken.Add(spot);
        }

        return AiMove.Create(coords).NaturalOrder();
    }

    // The marker, or a step across the lines (up to MarkerNudgeMax) clear of every taken spot;
    // null when properly covered.
    private static Vector2? ClearNearMarker(Vector2 marker, int off, float o, Vector2 crossNorth, List<Vector2> taken)
    {
        for (var step = 0f; step <= MarkerNudgeMax; step += 1f)
            foreach (var sign in BothWays)
            {
                var q = marker + crossNorth * (step * sign);
                if (LineClearance(q, off, o) < SpreadLineMargin) continue;
                if (taken.Exists(t => Vector2.Distance(t, q) < SpreadSeparation)) continue;
                return q;
            }
        return null;
    }

    // Toward the source on an inverted gaze, away on a normal one.
    private static void FaceGaze(UmadP1TeleTrouncingState state, SimWorld world)
    {
        var source3 = state.GazeInverted
            ? UmadP1TeleTrouncingScenario.GazeSourceInverted
            : UmadP1TeleTrouncingScenario.GazeSourceNormal;
        var source = new Vector2(source3.X, source3.Z);

        for (var i = 0; i < 8; i++)
        {
            if (world.Party.Get(i) is not { } member || !member.IsAlive()) continue;
            var p = new Vector2(member.Position.X, member.Position.Z);
            var faceToward = state.GazeInverted ? source : 2f * p - source;
            member.Face(new Vector3(faceToward.X, 0f, faceToward.Y));
        }
    }

    // Unit vector along the Thunder-line normal, pointing to the "north" side (negative Z).
    private static Vector2 CrossAxisNorth(float orientation)
    {
        var rot = LineBaseRotation * orientation;
        var right = new Vector2(MathF.Cos(rot), -MathF.Sin(rot));
        return right.Y < 0f ? right : -right;
    }

    // The closest point to arena centre clearing every real line and every taken spot.
    private static Vector2 NearestSafeSpread(int realOffset, float orientation, List<Vector2> taken)
    {
        for (var r = 0f; r <= 16f; r += 1f)
            for (var deg = 0; deg < 360; deg += 15)
            {
                var th = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(th) * r, MathF.Cos(th) * r);
                if (LineClearance(q, realOffset, orientation) < SpreadLineMargin) continue;
                if (taken.Exists(t => Vector2.Distance(t, q) < SpreadSeparation)) continue;
                return q;
            }
        return Vector2.Zero;
    }

    // Slid along its wall, then pulled inward if that isn't enough.
    private static Vector2 SlideAlongWall(Vector2 cardinal, int realOffset, float orientation)
    {
        if (LineClearance(cardinal, realOffset, orientation) >= SpreadLineMargin) return cardinal;
        var outward = Vector2.Normalize(cardinal);
        var tangent = new Vector2(-outward.Y, outward.X);
        for (var t = 1.5f; t <= 16f; t += 1.5f)
            foreach (var sign in BothWays)
            {
                var q = cardinal + tangent * (t * sign);
                if (q.Length() <= 19.5f && LineClearance(q, realOffset, orientation) >= SpreadLineMargin)
                    return q;
            }
        for (var pull = 2f; pull <= 12f; pull += 2f)
        {
            var q = cardinal - outward * pull;
            if (LineClearance(q, realOffset, orientation) >= SpreadLineMargin) return q;
        }
        return cardinal;
    }

    // SpotsInExpiryOrder, not Spots: which direction expires at 7s is a coin flip.
    private static IAiMove Spots(UmadP1TeleTrouncingState state, bool first)
    {
        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
        {
            var (at7, at10) = state.SpotsInExpiryOrder(role);
            coords[(int)role] = first ? at7 : at10;
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove ConfettiSpots(UmadP1TeleTrouncingState state)
    {
        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
        {
            var isDps = UmadP1TeleTrouncingState.IsDps(role);
            var isStack = role == (isDps ? state.ConfettiStackDps : state.ConfettiStackSupport);
            coords[(int)role] = (isDps, isStack) switch
            {
                (false, true) => ConfettiStackSupportSpot,
                (false, false) => ConfettiSpreadSupportSpot,
                (true, true) => ConfettiStackDpsSpot,
                (true, false) => ConfettiSpreadDpsSpot,
            };
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    // Fixed by role identity per the strategy guide, independent of the run's arrow scramble
    // and Confused/Sleep roll. Up=North, Right=East, Down=South, Left=West.
    private static readonly Vector2 PhysRangedDpsSpot = CardinalVec(TelePortentDirection.Up) * OutsideRadius;
    private static readonly Vector2 MainTankSpot = CardinalVec(TelePortentDirection.Up) * RingRadius;
    private static readonly Vector2 ShieldHealerSpot = CardinalVec(TelePortentDirection.Right) * OutsideRadius;
    private static readonly Vector2 MeleeDpsBSpot = CardinalVec(TelePortentDirection.Right) * RingRadius;
    private static readonly Vector2 RegenHealerSpot = CardinalVec(TelePortentDirection.Down) * OutsideRadius;
    private static readonly Vector2 MeleeDpsASpot = CardinalVec(TelePortentDirection.Down) * RingRadius;
    private static readonly Vector2 CasterDpsSpot = CardinalVec(TelePortentDirection.Left) * OutsideRadius;
    private static readonly Vector2 OffTankSpot = CardinalVec(TelePortentDirection.Left) * RingRadius;

    private static IAiMove TetherSpots(UmadP1TeleTrouncingState state)
    {
        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
        {
            coords[(int)role] = role switch
            {
                PartyRole.PhysRangedDps => PhysRangedDpsSpot,
                PartyRole.MainTank => MainTankSpot,
                PartyRole.ShieldHealer => ShieldHealerSpot,
                PartyRole.MeleeDpsB => MeleeDpsBSpot,
                PartyRole.RegenHealer => RegenHealerSpot,
                PartyRole.MeleeDpsA => MeleeDpsASpot,
                PartyRole.CasterDps => CasterDpsSpot,
                PartyRole.OffTank => OffTankSpot,
                _ => Vector2.Zero,
            };
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 CardinalVec(TelePortentDirection cardinal) => cardinal switch
    {
        TelePortentDirection.Up => new Vector2(0f, -1f),
        TelePortentDirection.Down => new Vector2(0f, 1f),
        TelePortentDirection.Right => new Vector2(1f, 0f),
        TelePortentDirection.Left => new Vector2(-1f, 0f),
        _ => Vector2.Zero,
    };
}
