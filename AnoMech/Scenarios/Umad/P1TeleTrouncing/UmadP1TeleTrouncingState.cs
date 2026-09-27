using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Umad.P1TeleTrouncing;

public enum TelePortentDirection { Up, Down, Left, Right }

// One role category (supports or DPS, PartyRole's own 0-3/4-7 split) gets two matching
// Tele-portent stacks and the other two different ones, one coin flip.
// The matching category gets a random 1-to-1 assignment of the 4 directions. The different
// category gets the 4 adjacent pairs of a FIXED geometric cycle: a shuffle can put opposite
// directions next to each other, which degenerates IntercardinalSpot to a plain cardinal and
// lands on the matching role that owns it. Which slot of a different pair gets 7s vs 10s is an
// independent coin flip (see DifferentPolarity). A second, independent coin flip decides which
// category gets Confused and which Sleep.
public sealed class UmadP1TeleTrouncingState
{
    private static readonly PartyRole[] SupportRoles =
        [PartyRole.MainTank, PartyRole.OffTank, PartyRole.RegenHealer, PartyRole.ShieldHealer];
    private static readonly PartyRole[] DpsRoles =
        [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];
    private static readonly TelePortentDirection[] AllDirections =
        [TelePortentDirection.Up, TelePortentDirection.Down, TelePortentDirection.Left, TelePortentDirection.Right];
    // Every consecutive pair is adjacent, never opposite (see the class comment).
    private static readonly TelePortentDirection[] AdjacentCycle =
        [TelePortentDirection.Up, TelePortentDirection.Right, TelePortentDirection.Down, TelePortentDirection.Left];

    private readonly Rng rng = new();

    public bool DpsGetsDifferent { get; }
    public bool DpsGetsConfused { get; }

    // Mystery Magic (end of P1), all rolled independently in the ctor.

    // true = AveMaria (inverted, look toward the NW statue); false = IndolentWill (normal, look
    // away from the NE statue). An even coin flip.
    public bool GazeInverted { get; }

    public bool FireIsStack { get; }
    // Kefka's fire orb: true = lie (shown icon is the opposite of FireIsStack), false = truth.
    public bool FireIsLie { get; }
    // What the stack/spread headmarker actually shows.
    public bool FireShowsStackIcon => FireIsStack ^ FireIsLie;
    // The 2 stack targets when FireIsStack (one support, one DPS; no real data on who).
    public PartyRole FireStackSupport { get; }
    public PartyRole FireStackDps { get; }

    // 4 parallel diagonal rect lines (len 40, width 10), 2 real: ThunderRealOffset 0 -> slots
    // {0,2}, 1 -> {1,3}. Lie -> the other 2 also telegraph a harmless fake and the reals use
    // 0xBAA1; truth -> only the reals telegraph, using 0xBA9F.
    public int ThunderRealOffset { get; }
    public bool ThunderOrientationFlipped { get; }
    public bool ThunderIsLie { get; }
    // +1 / -1 multiplier for Placement.MulX/MulRot and the Ai's LineClearance.
    public float ThunderOrientation => ThunderOrientationFlipped ? -1f : 1f;

    // Confetti passes a stack marker across 3 waves starting well before Tele-trouncing; the
    // earlier waves' scenarios don't exist yet, so simplified to one Support and one DPS holder
    // (the far spot), everyone else in that category spreading (the near spot).
    public PartyRole ConfettiStackSupport { get; }
    public PartyRole ConfettiStackDps { get; }

    // Each role's two directions; for a matching role both entries are identical.
    public IReadOnlyDictionary<PartyRole, (TelePortentDirection First, TelePortentDirection Second)> Debuffs { get; }

    // Scenario-local XZ. Every teleporter is grouped purely by its own direction: each direction's
    // 4 sit evenly along one side of a square (Right north, Down east, Left south, Up west), side
    // length spanning the real cardinal radius (12) in 4 steps of 6. A matching role fills 2 of
    // its direction's slots; the two different roles carrying that direction fill the rest.
    // Within-side ordering isn't pinned down by data.
    public IReadOnlyDictionary<PartyRole, (Vector2 First, Vector2 Second)> Spots { get; }

    // Which of a different role's two directions gets the 7s (place-first) debuff: an independent
    // per-instance coin flip in the log (the same pair showed both polarities across pulls).
    // true = First gets the primary id + 10s, Second the secondary + 7s; false = the reverse.
    // Meaningless for a matching role.
    public IReadOnlyDictionary<PartyRole, bool> DifferentPolarity { get; }

    // The direction whose debuff expires at 7s vs 10s, as opposed to Debuffs' purely geometric
    // (First, Second).
    public (TelePortentDirection At7s, TelePortentDirection At10s) ExpiryDirections(PartyRole role)
    {
        var (first, second) = Debuffs[role];
        if (first == second) return (first, second);
        return DifferentPolarity[role] ? (second, first) : (first, second);
    }

    // The same polarity flip applied to Spots, so a bot's wave-1 target is the slot the 7s
    // direction owns.
    public (Vector2 At7s, Vector2 At10s) SpotsInExpiryOrder(PartyRole role)
    {
        var (first, second) = Debuffs[role];
        var (spotFirst, spotSecond) = Spots[role];
        if (first == second) return (spotFirst, spotSecond);
        return DifferentPolarity[role] ? (spotSecond, spotFirst) : (spotFirst, spotSecond);
    }

    public static bool IsDps(PartyRole role) => role is PartyRole.MeleeDpsA or PartyRole.MeleeDpsB
        or PartyRole.PhysRangedDps or PartyRole.CasterDps;

    public UmadP1TeleTrouncingState(UmadP1TeleTrouncingStateOverrides overrides)
    {
        DpsGetsDifferent = overrides.DpsGetsDifferent ?? rng.NextBool();
        DpsGetsConfused = rng.NextBool();
        ConfettiStackSupport = rng.NextSupportRole();
        ConfettiStackDps = rng.NextDpsRole();

        // Independent of DpsGetsConfused: BossMod's P1StatueGaze keys purely off which prop plays
        // the wind-up.
        GazeInverted = overrides.GazeInverted ?? rng.NextBool();
        FireIsStack = overrides.FireIsStack ?? rng.NextBool();
        FireIsLie = overrides.FireIsLie ?? rng.NextBool();
        FireStackSupport = rng.NextSupportRole();
        FireStackDps = rng.NextDpsRole();
        ThunderRealOffset = overrides.ThunderRealOffset ?? rng.NextInt(2);
        ThunderOrientationFlipped = overrides.ThunderOrientationFlipped ?? rng.NextBool();
        ThunderIsLie = overrides.ThunderIsLie ?? rng.NextBool();
        var matchingRoles = DpsGetsDifferent ? SupportRoles : DpsRoles;
        var differentRoles = DpsGetsDifferent ? DpsRoles : SupportRoles;

        var matchingDirections = rng.Shuffle(AllDirections);
        var debuffs = new Dictionary<PartyRole, (TelePortentDirection, TelePortentDirection)>();
        for (var i = 0; i < 4; i++)
            debuffs[matchingRoles[i]] = (matchingDirections[i], matchingDirections[i]);

        var cycleRoles = rng.Shuffle(differentRoles);
        var polarity = new Dictionary<PartyRole, bool>();
        for (var i = 0; i < 4; i++)
        {
            debuffs[cycleRoles[i]] = (AdjacentCycle[i], AdjacentCycle[(i + 1) % 4]);
            polarity[cycleRoles[i]] = rng.NextBool();
        }

        Debuffs = debuffs;
        DifferentPolarity = polarity;
        Spots = BuildSpots(debuffs);
    }

    // Each person's two teleporters land next to each other: a different role's pair are two
    // adjacent sides, so its arrows take the last slot of the first side and the first slot of
    // the second, straddling the corner. That claims slots 0 and 3 on every side, leaving the
    // adjacent 1 and 2 for the matching role that owns the side.
    private static Dictionary<PartyRole, (Vector2 First, Vector2 Second)> BuildSpots(IReadOnlyDictionary<PartyRole, (TelePortentDirection First, TelePortentDirection Second)> debuffs)
    {
        var spots = new Dictionary<PartyRole, (Vector2, Vector2)>();
        foreach (var (role, (first, second)) in debuffs)
        {
            spots[role] = first == second
                ? (SideSpot(first, 1), SideSpot(first, 2))
                : (SideSpot(first, 3), SideSpot(second, 0));
        }
        return spots;
    }

    // Half the square's side: the one confirmed real number (the Diamond Waymark cardinals sit at
    // this radius, see UmadZone.Waymarks).
    private const float Radius = 12f;
    private const float Step = 6f;

    // Slot 0..3 along the side, clockwise from its leading corner; each side's slot 0 is the
    // previous side's trailing corner.
    private static Vector2 SideSpot(TelePortentDirection direction, int slot) => direction switch
    {
        TelePortentDirection.Right => new Vector2(-Radius + slot * Step, -Radius),
        TelePortentDirection.Down => new Vector2(Radius, -Radius + slot * Step),
        TelePortentDirection.Left => new Vector2(Radius - slot * Step, Radius),
        TelePortentDirection.Up => new Vector2(-Radius, Radius - slot * Step),
        _ => Vector2.Zero,
    };
}
