using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P5Sigma;

public sealed class TopP5SigmaTuuflessAi : TopP5SigmaAi
{
    private static readonly PartyRole[] Priority =
    [
        PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps,
        PartyRole.CasterDps, PartyRole.ShieldHealer,
    ];

    private static readonly int[] LineupPairs = [2, 0, 3, 1];

    public override string Name => "Tuufless (Elemental)";

    private IReadOnlyList<PartyRole> omegaFGroup = [];

    protected override IAiMove Lineup()
    {
        const float armsLineZ = 7.07f;
        return AiMove.Create(
            new(-2, armsLineZ - 2), new(2, armsLineZ - 2),
            new(-2, armsLineZ + 2), new(2, armsLineZ + 2),
            new(-2, armsLineZ - 4), new(2, armsLineZ - 4),
            new(-2, armsLineZ), new(2, armsLineZ)
        )
        .Assignments(state.Order.List)
        .ApplyPositions(state.NewNorthA.Apply);
    }

    protected override IReadOnlyList<PartyRole> WaveCannonAssignments()
    {
        var fullPairs = LineupPairs.Where(p => p != state.FirstMissing / 2 && p != state.SecondMissing / 2).ToList();
        var halfMissing = new[] { state.FirstMissing, state.SecondMissing }
            .OrderBy(m => Array.IndexOf(LineupPairs, m / 2)).ToList();
        PartyRole Partner(int slot) => state.Order[slot % 2 == 0 ? slot + 1 : slot - 1];
        return
        [
            state.Order[2 * fullPairs[0]],
            state.Order[halfMissing[1]],
            state.Order[2 * fullPairs[1] + 1],
            Partner(halfMissing[0]),
            state.Order[2 * fullPairs[0] + 1],
            Partner(halfMissing[1]),
            state.Order[2 * fullPairs[1]],
            state.Order[halfMissing[0]],
        ];
    }

    protected override RoleList BuildMarkingsOrder(SimParty party)
    {
        var hellos = state.HelloWorldTargets.List;
        var candidates = Priority.Where(r => !hellos.Contains(r)).ToList();
        var withDynamis = candidates.Where(r => state.DynamisTargets.Contains(r)).Take(2).ToList();
        omegaFGroup = withDynamis.Concat(candidates.Except(withDynamis)).Take(3).ToList();

        var armBaits = omegaFGroup.Where(r => r.IsTank())
                                  .Concat(omegaFGroup.Where(r => !r.IsTank()))
                                  .Take(2).ToList();
        var groupJump = omegaFGroup.Except(armBaits).First();
        var jumpOrder = candidates.Except(armBaits).Where(r => r != groupJump).Prepend(groupJump).Take(4).ToList();
        return new(party, [armBaits[0], jumpOrder[0], armBaits[1], jumpOrder[1],
                           jumpOrder[2], jumpOrder[3], hellos[0], hellos[1]]);
    }

    protected override IReadOnlyList<PartyRole> OmegaFOrder()
        => omegaFGroup.Concat(Priority.Except(omegaFGroup)).ToArray();

    protected override Dictionary<PartyRole, Sign> MarkerMapping()
        => new()
        {
            [markingsOrder[0]] = Sign.Attack1,
            [markingsOrder[1]] = Sign.Ignore1,
            [markingsOrder[2]] = Sign.Attack2,
            [markingsOrder[3]] = Sign.Bind1,
            [markingsOrder[4]] = Sign.Bind2,
            [markingsOrder[5]] = Sign.Ignore2,
            [markingsOrder[6]] = Sign.Triangle,
            [markingsOrder[7]] = Sign.Cross,
        };
}
