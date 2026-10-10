using System;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public class TopP2PartySynergyLpduAi : IScenarioAi<TopP2PartySynergyState>
{
    public string Name => "LPDU";

    private TopP2PartySynergyState state = null!;

    public void Run(TopP2PartySynergyState s, SimWorld world)
    {
        state = s;
        var ai = new AiManager(world);
        ai.Move(1f, LightPartyColumns, jitter: 0.7f);
        ai.Move(11f, AttackDodge, arrivalTime: 14.8f);
        ai.Move(16f, SpreadPositions, arrivalTime: 21.5f);
        ai.Move(23f, KnockbackPositions, arrivalTime: 28.4f);
        ai.Move(30f, StackPositions, arrivalTime: 33f);
    }

    private IAiMove LightPartyColumns()
    {
        return AiMove.Create(
            new(-4, -3.6f),
            new(4, -3.6f),
            new(-4, -1.2f),
            new(4, -1.2f),
            new(-4, 1.2f),
            new(4, 1.2f),
            new(-4, 3.6f),
            new(4, 3.6f)
        ).NaturalOrder();
    }

    private IAiMove AttackDodge()
    {
        return AiMove.All(new(0, -1f))
                     .ApplyPositions(
                         AttackSafeCardinal,
                         AttackSafeSpot
                     );
    }

    private IAiMove SpreadPositions()
    {
        return AiMove.Create(
                         new(-11, -16),
                         new(11, -16),
                         new(-11, -5.5f),
                         new(11, -5.5f),
                         new(-11, 5.5f),
                         new(11, 5.5f),
                         new(-11, 16),
                         new(11, 16)
                     )
                     .Assignments(state.Order.List)
                     .ApplySwaps(SwapForWestPriority, FullCrossOnFarGlitch)
                     .ApplyPositions(GlitchGeometry, state.NewNorthA.Apply);
    }

    private IAiMove KnockbackPositions()
    {
        return AiMove.Create(
                         new(-2, 0),
                         new(2, 0),
                         new(-2, 0),
                         new(2, 0),
                         new(-2, 0),
                         new(2, 0),
                         new(-2, 0),
                         new(2, 0)
                     )
                     .Assignments(state.Order.List)
                     .ApplySwaps(SwapForWestPriority, FullCrossOnFarGlitch, SplitSameSideStacks)
                     .ApplyPositions(AdjustKbForFarGlitch, state.NewNorthB.Apply);
    }

    private IAiMove StackPositions()
    {
        return AiMove.Create(
                         new(-15, 0),
                         new(15, 0),
                         new(-15, 0),
                         new(15, 0),
                         new(-15, 0),
                         new(15, 0),
                         new(-15, 0),
                         new(15, 0)
                     )
                     .Assignments(state.Order.List)
                     .ApplySwaps(SwapForWestPriority, FullCrossOnFarGlitch, SplitSameSideStacks)
                     .ApplyPositions(AdjustKbForFarGlitch, state.NewNorthB.Apply);
    }

    private void AttackSafeCardinal(IAiPositions move)
    {
        state.AttackDir.Rotate(1).Apply(move);
    }

    private void AttackSafeSpot(IAiPositions move)
    {
        float mul;
        if (state.AttackF == OmegaAttack.Legs)
            mul = state.AttackM == OmegaAttack.Sword ? 2.5f : -2.5f;
        else
            mul = state.AttackM == OmegaAttack.Sword ? -17f : -10f;
        move.Multiply(mul);
    }

    private void SwapForWestPriority(IAiRoles s)
    {
        for (int i = 0; i < 4; i++)
        {
            if (WestPriority(s.RoleAt(2 * i)) > WestPriority(s.RoleAt(2 * i + 1)))
            {
                s.ByPosition(2 * i, 2 * i + 1);
            }
        }
    }

    private void FullCrossOnFarGlitch(IAiRoles s)
    {
        if (state.Glitch == GlitchType.Far)
        {
            s.ByPosition(1, 7);
            s.ByPosition(3, 5);
        }
    }

    private void GlitchGeometry(IAiPositions move)
    {
        if (state.Glitch == GlitchType.Far)
        {
            var mul = 18f / 11;
            move.MultiplyX(2, mul);
            move.MultiplyX(3, mul);
            move.MultiplyX(4, mul);
            move.MultiplyX(5, mul);
        }
    }

    private void SplitSameSideStacks(IAiRoles s)
    {
        var pos0 = s.PositionOf(state.Stacks[0]);
        var pos1 = s.PositionOf(state.Stacks[1]);
        if ((pos0 + pos1) % 2 != 0) return;
        var southmost = Math.Max(pos0, pos1);
        s.ByPosition(southmost, TetherPartnerAfterCross(southmost));
    }

    private int TetherPartnerAfterCross(int position)
    {
        return state.Glitch == GlitchType.Far ? 7 - position : position ^ 1;
    }

    private void AdjustKbForFarGlitch(IAiPositions move)
    {
        if (state.Glitch == GlitchType.Mid)
            for (var i = 0; i < 4; i++)
                move.Rotate(i * 2 + 1, MathF.PI / 2);
        else
            move.Multiply(19f / 15);
    }

    private static readonly PartyRole[] WestPriorityOrder =
    [
        PartyRole.MainTank,
        PartyRole.RegenHealer,
        PartyRole.MeleeDpsA,
        PartyRole.PhysRangedDps,
        PartyRole.CasterDps,
        PartyRole.MeleeDpsB,
        PartyRole.ShieldHealer,
        PartyRole.OffTank,
    ];

    private static int WestPriority(PartyRole role) => Array.IndexOf(WestPriorityOrder, role);
}
