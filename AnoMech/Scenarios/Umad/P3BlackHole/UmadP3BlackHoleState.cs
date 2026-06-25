using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

// Per-run randomized assignments the scenario and AI consume. Filled in the ctor
// (apply override if set, otherwise pick at random) so Run stays deterministic for
// the duration of one play. See UmadP4KefkaSaysState for the canonical shape.
public sealed class UmadP3BlackHoleState
{
    private readonly Rng rng = new();

    public RoleList Roles { get; }
    public RoleList StackTargets { get; }
    public RoleList EdictTargets { get; }
    
    public uint ImplosionAttack { get; }
    public IReadOnlyList<uint> SlapAttacks { get; }
    public IReadOnlyList<RoleList> ConeTargets { get; }
    
    public IReadOnlyList<Direction> KefkaPosition { get; }
    
    public IReadOnlyList<Direction> BlackHoleDirections { get; }
    
    public int MiniBlackHoleInitialAngle { get; }
    public int MiniBlackHoleChirality { get; }

    public UmadP3BlackHoleState(SimParty party, UmadP3BlackHoleStateOverrides overrides)
    {
        Roles = BuildRoles(party, overrides);

        StackTargets = new RoleList(party, rng.Shuffle(rng.NextSupportRole(), rng.NextDpsRole()));
        EdictTargets = new RoleList(party, [rng.NextRole(), rng.NextRole()]);
        ImplosionAttack = rng.NextObj(ActionId.LatitudinalImplosion, ActionId.LongitudinalImplosion);
        SlapAttacks = [overrides.FirstSlap ?? NextSlap(), NextSlap(), NextSlap()];
        KefkaPosition = Enumerable.Range(0, 5).Select(_ => rng.NextDirection()).ToList();
        ConeTargets = Enumerable.Range(0, 3)
                                .Select(i => NextConeTargets(party, SlapAttacks[i],
                                                             i == 0 && (overrides.FirstSlapAllOnPlayer ?? false)))
                                .ToList();
        BlackHoleDirections = Enumerable.Range(0, 4).Select(_ => rng.NextCardinal()).ToList();
        MiniBlackHoleInitialAngle = rng.NextInt(2);
        MiniBlackHoleChirality = rng.NextSign();
    }
    
    // Final-slot (post-swap) line number and Accretion, mirroring the per-index status
    // assignment in UmadP3BlackHoleScenario.Run_OtherDebuffs. Slots 0-3 hold the supports
    // (slot 3 never a tank), slots 4-7 the DPS; Swap(3,7) trades the two Accretion holders.
    private static readonly int[] SlotLine = [1, 2, 3, 1, 1, 2, 3, 2];          // 1/2/3 = First/Second/Third in line
    private static readonly bool[] SlotAccretion = [false, false, false, true, false, false, false, true];

    // Random arrangement (supports 0-3 with slot 3 non-tank, DPS 4-7, coin-flip Accretion swap),
    // then — if the player asked for a specific line/Accretion — drop the player's role into the
    // matching slot, keeping the support/DPS split and the "slot 3 is never a tank" invariant.
    private RoleList BuildRoles(SimParty party, UmadP3BlackHoleStateOverrides overrides)
    {
        RoleList roles;
        do
        {
            roles = RoleList.RandomRoleStable(party);
        }
        while (roles[3].IsTank());

        var player = party.PlayerRole;
        if (ResolvePlayerSlot(player, overrides) is not { } finalIndex)
        {
            if (rng.NextBool()) roles.Swap(3, 7);
            return roles;
        }

        // Translate the desired final slot into a pre-swap slot + whether Swap(3,7) runs.
        // The two Accretion slots (3, 7) live in different blocks, so they're reached through
        // the swap; the other six slots sit in the player's own block and need no swap.
        var (pre, swap) = finalIndex switch
        {
            3 => player.IsDps() ? (7, true) : (3, false),   // First + Accretion: DPS via swap, healer direct
            7 => player.IsDps() ? (7, false) : (3, true),   // Second + Accretion: DPS direct, healer via swap
            _ => (finalIndex, rng.NextBool()),
        };

        MovePlayerToSlot(roles, player, pre);
        if (swap) roles.Swap(3, 7);
        return roles;
    }

    // Slots the player's role can legitimately occupy given the fight's structure.
    private static int[] ReachableSlots(PartyRole player) =>
        player.IsTank() ? [0, 1, 2]                 // tanks: First/Second/Third, never Accretion
        : player.IsDps() ? [3, 4, 5, 6, 7]          // dps: their block plus the swapped-in Accretion slot 3
        : [0, 1, 2, 3, 7];                          // healers: their block plus the swapped-out Accretion slot 7

    // Pick the final slot matching the requested line/Accretion, or null to leave it random.
    // An impossible Accretion request (tanks, or third-in-line) is silently dropped.
    private int? ResolvePlayerSlot(PartyRole player, UmadP3BlackHoleStateOverrides overrides)
    {
        if (overrides.LineNumber is null && overrides.Accretion is null)
            return null;

        var reachable = ReachableSlots(player);
        bool LineOk(int s) => overrides.LineNumber is not { } ln || SlotLine[s] == ln;
        bool AccrOk(int s) => overrides.Accretion is not { } a || SlotAccretion[s] == a;

        var candidates = reachable.Where(s => LineOk(s) && AccrOk(s)).ToList();
        if (candidates.Count == 0)
            candidates = reachable.Where(LineOk).ToList();   // Accretion impossible here -> ignore it
        if (candidates.Count == 0)
            return null;

        return candidates[rng.NextInt(candidates.Count)];
    }

    // Swap the player's role to pre-swap slot `pre`. Both blocks stay intact (the player only
    // ever targets a slot in its own block); the one invariant this can break is "slot 3 is
    // never a tank" — when a healer vacates slot 3 and pulls a tank in — so restore it.
    private static void MovePlayerToSlot(RoleList roles, PartyRole player, int pre)
    {
        var cur = Array.IndexOf(roles.List, player);
        if (cur != pre) roles.Swap(cur, pre);

        if (roles[3].IsTank())
            for (int i = 0; i < 3; i++)
                if (!roles[i].IsTank() && roles[i] != player)
                {
                    roles.Swap(3, i);
                    break;
                }
    }

    private uint NextSlap()
    {
        return rng.NextObj(ActionId.SlapHappy_Left, ActionId.SlapHappy_Right);
    }

    // Left = one shocking-impact stack target; Right = three shockwave cones. When
    // allOnPlayer is set, every target slot collapses onto the player so the cones
    // all land on the practising player.
    private RoleList NextConeTargets(SimParty party, uint slap, bool allOnPlayer)
    {
        if (slap == ActionId.SlapHappy_Left)
            return new RoleList(party, [allOnPlayer ? party.PlayerRole : rng.NextRole()]);

        return allOnPlayer
                   ? new RoleList(party, [party.PlayerRole, party.PlayerRole, party.PlayerRole])
                   : new RoleList(party, [rng.NextDpsRole(), rng.NextTankRole(), rng.NextHealerRole()]);
    }
}
