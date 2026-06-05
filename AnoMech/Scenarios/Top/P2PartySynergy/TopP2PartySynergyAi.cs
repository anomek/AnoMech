using System;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public class TopP2PartySynergyAi
{
    private readonly TopP2PartySynergyState state;

    public TopP2PartySynergyAi(TopP2PartySynergyState state)
    {
        this.state = state;
    }

    public void Run(SimWorld world)
    {
        var ai = new AiManager(world);
        ai.Move(1f, CongaLine, jitter: 0.7f);
        ai.Move(11f, AttackDodge, arrivalTime: 14.8f);
        ai.Move(16f, SpreadPositions, arrivalTime: 21.5f);
        ai.Move(23f, KnockbackPositions, arrivalTime: 28.4f);
        ai.Move(30f, StackPositions, arrivalTime: 33f);
    }

    private IAiMove CongaLine()
    {
        return AiMove.Create(
            new(-1.2f, 2),
            new(1.2f, 2),
            new(-8.4f, 2),
            new(8.4f, 2),
            new(-3.6f, 2),
            new(3.6f, 2),
            new(6, 2),
            new(-6, 2)
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
                     .ApplySwaps(SwapForCongaOrder, GlitchSwap)
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
                     .ApplySwaps(SwapForCongaOrder, GlitchSwap, AdjustForStacks)
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
                     .ApplySwaps(SwapForCongaOrder, GlitchSwap, AdjustForStacks)
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

    private void SwapForCongaOrder(IAiRoles s)
    {
        for (int i = 0; i < 4; i++)
        {
            if (Conga(s.RoleAt(2 * i)) > Conga(s.RoleAt(2 * i + 1)))
            {
                s.ByPosition(2 * i, 2 * i + 1);
            }
        }
    }

    private void GlitchSwap(IAiRoles s)
    {
        if (state.Glitch == GlitchType.Far)
            s.ByPosition(1, 7);
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


    private void AdjustForStacks(IAiRoles s)
    {
        var pos0 = s.PositionOf(state.Stacks[0]);
        var pos1 = s.PositionOf(state.Stacks[1]);
        Plugin.Log.Info($"Stack adjustments positions {pos0} {pos1}");
        if ((pos0 + pos1) % 2 == 0)
        {
            if (pos0 > pos1)
                pos0 = pos1;
            var partner = pos0 % 2 == 0 ? pos0 + 1 : pos0 - 1;
            if (state.Glitch == GlitchType.Far && pos0 is < 2 or > 5)
                partner = pos0 < 2 ? partner + 6 : partner - 6;
            Plugin.Log.Info($"Stack adjustments partners {pos0} {partner}");
            s.ByPosition(pos0, partner);
        }
    }

    private void AdjustKbForFarGlitch(IAiPositions move)
    {
        if (state.Glitch == GlitchType.Mid)
            for (var i = 0; i < 4; i++)
                move.Rotate(i * 2 + 1, MathF.PI / 2);
        else
            move.Multiply(19f / 15);
    }

    private static readonly PartyRole[] CongaOrder =
    [
        PartyRole.RegenHealer,
        PartyRole.CasterDps,
        PartyRole.MeleeDpsA,
        PartyRole.MainTank,
        PartyRole.OffTank,
        PartyRole.MeleeDpsB,
        PartyRole.PhysRangedDps,
        PartyRole.ShieldHealer,
    ];

    private static int Conga(PartyRole role) => Array.IndexOf(CongaOrder, role);
}
