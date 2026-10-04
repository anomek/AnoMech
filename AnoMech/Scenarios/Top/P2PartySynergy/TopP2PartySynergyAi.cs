using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P2PartySynergy;

public class TopP2PartySynergyAi : IScenarioAi<TopP2PartySynergyState>
{
    public string Name => "Standard";

    private TopP2PartySynergyState state = null!;

    public void Run(TopP2PartySynergyState s, SimWorld world)
    {
        state = s;
        var ai = new AiManager(world);
        ai.Move(1f, CongaLine, jitter: 0.7f);
        ai.Move(11f, AttackDodge, arrivalTime: 14.8f);
        ai.Move(15.4f, SpreadPositions);
        ai.Move(22.3f, KnockbackPositions, arrivalTime: 26.5f);
        ai.Move(30.1f, StackPositions);
    }

    private static readonly Vector2[] CongaSpots =
    [
        new(-2.3f, 6.6f),
        new(0.9f, 6.1f),
        new(-12f, 5.2f),
        new(7.7f, 5.9f),
        new(-5.7f, 6.1f),
        new(3.2f, 5.8f),
        new(5.3f, 5.7f),
        new(-9.1f, 5.5f),
    ];

    private static readonly Vector2 LegsSwordSafeSpot = new(0, -8f);
    private static readonly Vector2 LegsShieldSafeSpot = new(0, 8.4f);
    private static readonly Vector2 StaffShieldSafeSpot = new(-7.07f, 7.07f);
    private static readonly Vector2 StaffSwordWestSafeSpot = new(-13.5f, 2f);
    private static readonly Vector2 StaffSwordEastSafeSpot = new(13.5f, 4f);

    private IAiMove CongaLine()
    {
        return AiMove.Create(CongaSpots.Select(spot => (Vector2?)spot).ToArray()).NaturalOrder();
    }

    private IAiMove AttackDodge()
    {
        return AiMove.Create(Enum.GetValues<PartyRole>().Select(role => (Vector2?)AttackSafeSpot(role)).ToArray())
                     .NaturalOrder()
                     .ApplyPositions(state.AttackDir.Apply);
    }

    private Vector2 AttackSafeSpot(PartyRole role)
    {
        if (state.AttackF == OmegaAttack.Legs)
            return state.AttackM == OmegaAttack.Sword ? LegsSwordSafeSpot : LegsShieldSafeSpot;
        if (state.AttackM == OmegaAttack.Shield)
            return StaffShieldSafeSpot;
        return NearerToCongaSpot(role, StaffSwordWestSafeSpot, StaffSwordEastSafeSpot);
    }

    private Vector2 NearerToCongaSpot(PartyRole role, Vector2 first, Vector2 second)
    {
        var conga = CongaSpots[(int)role];
        return Vector2.Distance(OnArena(first), conga) <= Vector2.Distance(OnArena(second), conga) ? first : second;
    }

    private Vector2 OnArena(Vector2 attackFrameSpot)
    {
        var spot = state.AttackDir.Apply(new Vector3(attackFrameSpot.X, 0f, attackFrameSpot.Y));
        return new Vector2(spot.X, spot.Z);
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
                         new(-3.2f, 0),
                         new(3.2f, 0),
                         new(-3.2f, 0),
                         new(3.2f, 0),
                         new(-3.2f, 0),
                         new(3.2f, 0),
                         new(-3.2f, 0),
                         new(3.2f, 0)
                     )
                     .Assignments(state.Order.List)
                     .ApplySwaps(SwapForCongaOrder, GlitchSwap, AdjustForStacks)
                     .ApplyPositions(TurnSecondGroupSouthForMidGlitch, state.NewNorthB.Apply);
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
                     .ApplyPositions(TurnSecondGroupSouthForMidGlitch, WidenStacksForFarGlitch, state.NewNorthB.Apply);
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

    private void TurnSecondGroupSouthForMidGlitch(IAiPositions move)
    {
        if (state.Glitch != GlitchType.Mid) return;
        for (var i = 0; i < 4; i++)
            move.Rotate(i * 2 + 1, MathF.PI / 2);
    }

    private void WidenStacksForFarGlitch(IAiPositions move)
    {
        if (state.Glitch == GlitchType.Far)
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
