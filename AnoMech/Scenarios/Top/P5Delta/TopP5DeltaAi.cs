using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants.Geometry;

namespace AnoMech.Scenarios.Top.P5Delta;

// First AI strategy for TOP P5 Delta. Reads the shared TopP5DeltaState so its
// movement decisions stay in sync with the scenario's randomized layout, and
// schedules movement through World.Events so it can react to fight timestamps.
public sealed class TopP5DeltaAi
{
    // Tether-resolve positions in scenario-local coords (origin (0,0) = world (100,100)).
    // Even slots are caller-specified, odd slots are mirrored across the east-west axis
    // (same X, negated Z) so each pair lands on opposite sides. EyeSpawn==South flips
    // the entire layout 180° (negate both X and Z).

    private readonly TopP5DeltaState state;

    public void Run(SimWorld world)
    {
        var ai = new AiManager(world);
        ai.Move(0.5f, InitialPositions);
        ai.Move(13f, TetherPrePosition);
        ai.Move(21f, FistResolveSlots);
        ai.Move(28.8f, TetherResolveStep);
        ai.Move(31.2f, HyperPulseBaitArms);
        ai.Move(36.2f, HyperPulseDodge, 0);
        ai.Move(38.4f, MonitorPositions);
        ai.Move(40, MonitorAdjustment, 0);
        ai.Move(46f, SwivelDodge);
        ai.Move(50f, RescueUnsafe);
        ai.Move(56f, ReturnToMiddle);
        ai.Move(56.5f, TankForward);
        ai.Move(60.0f, BreakLastTether);
    }


    public TopP5DeltaAi(TopP5DeltaState state)
    {
        this.state = state;
    }

    private void Swap01(IAiRoles s)
    {
        if (state.FistColors[0] == state.FistColors[2])
            s.ByPosition(0, 1);
    }

    private void Swap45(IAiRoles s)
    {
        if (state.FistColors[4] == state.FistColors[6])
            s.ByPosition(4, 5);
    }

    private void BeyondDefence(IAiPositions move)
    {
        move.AddX(state.BeyondDefenseTarget, 13f);
    }

    private void PlayerMonitorOffset(IAiPositions move)
    {
        move.AddY(state.PlayerMonitorRole, 2);
    }

    private void PlayerMonitorFacing(IAiPositions move)
    {
        move.AddX(state.PlayerMonitorRole, 1.2f * state.PlayerMonitorSide.Mul * state.OmegaMonitorSide.Mul);
    }

    private void OmegaMonitorSafeSide(IAiPositions move)
    {
        var mul = -state.OmegaMonitorSide.Mul * state.EyeSpawn.Mul;
        move.MultiplyY(0, mul);
        move.MultiplyY(1, mul);
        move.MultiplyY(2, mul);
        move.MultiplyY(3, mul);
    }

    private void SwivelSafeSide(IAiPositions move)
    {
        move.MultiplyY(state.SwivelCannonSide.Mul * state.EyeSpawn.Mul);
    }

    private void SwivelSwaps(IAiRoles s)
    {
        s.ByRole(state.TetherOrder[0], state.FarWorldRole);
        s.ByRole(state.FarWorldTetherIndex == 1 ? state.TetherOrder[0] : state.TetherOrder[1] , state.NearWorldRole);
        // make safe side plant consistent for Swivel Cannon
        if (state.SwivelCannonSide.Mul * state.EyeSpawn.Mul > 0)
            s.ByPosition(6, 7);
    }

    private void SwivelCannonAdjust(IAiPositions move)
    {
        var cannonMul = state.SwivelCannonSide.Mul * state.EyeSpawn.Mul;
        // 4/5 should not be adjusted by swivel side, so pre-unadjust it
        move.MultiplyY(4, cannonMul);
        move.MultiplyY(5, cannonMul);
    }

    private void AdjustEyePosition(IAiPositions move)
    {
        move.MultiplyX(state.EyeSpawn.Mul);
    }

    private IAiMove InitialPositions()
    {
        return AiMove.Create(
            new(-2.10f, -5.08f),
            new(2.10f, -5.08f),
            new(-0.7f, 5.7f),
            new(-0.7f, 6.5f),
            new(-0.7f, 7.3f),
            new(0.7f, 5.7f),
            new(0.7f, 6.5f),
            new(0.7f, 7.3f)
        ).NaturalOrder();
    }

    private IAiMove TetherPrePosition()
    {
        return AiMove.Create(
            new(-6f, -3f),
            new(-6f, 3f),
            new(-10f, -7f),
            new(-10f, 7f),
            new(4f, -6f),
            new(4f, 6f),
            new(9.5f, -10f),
            new(9.5f, 10f)
        )
        .Assignments(state.TetherOrder)
        .ApplyPositions(AdjustEyePosition);
    }

    private IAiMove FistResolveSlots()
    {
        return AiMove.Create(
            new Vector2(-10f, -3f),
            new Vector2(-10f, 3f),
            null,
            null,
            new Vector2(8.5f, -10f),
            new Vector2(8.5f, 10f),
            null,
            null
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap01, Swap45)
        .ApplyPositions(AdjustEyePosition);
    }

    private IAiMove TetherResolveStep()
    {
        return AiMove.Create(
            null, null,
            new(-10f, -3f),
            new(-10f, 3f),
            null, null, null, null
        )
        .Assignments(state.TetherOrder)
        .ApplyPositions(AdjustEyePosition);
    }

    private IAiMove HyperPulseBaitArms()
    {
        return AiMove.Create(
            ArmUnitPlacements.Select((placement, i) =>
                                         placement.MoveForward(0.5f)
                                                  .RotateAroundOrigin(
                                                      0.15f * state.ArmHandedness[i].Mul * state.EyeSpawn.Mul)
                                                  .Position2)
                             .Prepend(new Vector2(0f, 6f))
                             .Prepend(new Vector2(0f, -6f))
                             .Cast<Vector2?>()
                             .ToArray()
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap01, Swap45)
        .ApplyPositions(AdjustEyePosition);
    }

    private IAiMove HyperPulseDodge()
    {
        return AiMove.Create(
            new(0, 1f), 
            new(0, 1f),
            new(0, 1f),
            new(0, 1f),
            new(0, -12),
            new(0, 12),
            new(9, -10),
            new(9, 10)
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap45)
        .ApplyPositions(BeyondDefence, PlayerMonitorOffset, OmegaMonitorSafeSide, AdjustEyePosition);
    }

    private IAiMove MonitorPositions()
    {
        return AiMove.Create(
            null, null, null, null,
            new(-10, -12),
            new(-10, 12),
            new(10, -12),
            new(10, 12)
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap45)
        .ApplyPositions(AdjustEyePosition);
    }

    private IAiMove MonitorAdjustment()
    {
        return AiMove.Single(state.PlayerMonitorRole, new(0f, 1f))
        .ApplyPositions(
            BeyondDefence,
            PlayerMonitorOffset,
            PlayerMonitorFacing, // move monitor player a little so they face their monitor properly
            OmegaMonitorSafeSide,
            AdjustEyePosition
        );
    }

    private IAiMove SwivelDodge()
    {
        return AiMove.Create(
            new(0f, 19f), // far
            new(0f, 6f),  // near
            new(9.5f, 17f),
            new(9.5f, 17f),
            new(-9.5f, -9.5f),
            new(-9.5f, 9.5f),
            new(16f, 10f),
            new(-19f, 1f)
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(SwivelSwaps, Swap45)
        .ApplyPositions(SwivelCannonAdjust, SwivelSafeSide, AdjustEyePosition);
    }

    private IAiMove RescueUnsafe()
    {
        return AiMove.Single(
            state.SwivelCannonSide.Mul * state.EyeSpawn.Mul > 0 ? 4 : 5,
            new(-9.5f, 3.5f)
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(Swap45)
        .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }

    private IAiMove ReturnToMiddle()
    {
        return AiMove.Create(
            new(-0.7f, 5.7f),
            new(-0.7f, 6.5f),
            new(-0.7f, 7.3f),
            new(0.7f, 5.7f),
            new(0.7f, 6.5f),
            new(0.7f, 7.3f),
            new(8f, 4f),
            new(-9f, 1f)
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(SwivelSwaps)
        .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }

    private IAiMove TankForward()
    {
        return AiMove.Single(PartyRole.OffTank, new(0, -8f))
            .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }

    private IAiMove BreakLastTether()
    {
        return AiMove.Create(
            null, null, null, null, null, null,
            new(4f, 2.7f),
            new(-4f, 2.5f)
        )
        .Assignments(state.TetherOrder)
        .ApplySwaps(SwivelSwaps)
        .ApplyPositions(SwivelSafeSide, AdjustEyePosition);
    }
}
