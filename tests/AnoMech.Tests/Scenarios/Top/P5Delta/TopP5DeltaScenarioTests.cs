using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top.P5Delta;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: eye north, Swivel Cannon and both monitors Left, so the AI's coordinates
// below are its own. Tether slots in order are MT OT (close inner, blue), RH SH (close outer, blue),
// M1 M2 (far inner, green), R C (far outer, green); the fist colours need no swaps.
//
//   28.1  RH-SH's blue tether is already stretched and breaks; MT-OT's breaks on the way out at 31.2.
//   30.1  fists land on MT+RH (-10,-3), OT+SH (-10,3), M1+R (8.5/9.5,-10), M2+C (8.5/9.5,10).
//   31.2  MT (0,-6), OT (0,6); RH SH M1 M2 R C bait the arms NW SW N S NE SE.
//   35.7  Beyond Defense on MT, who leaves for (13,-1); OT RH stack at (0,-1), SH (the monitor) at (1.2,-3).
//   40.5  Omega's monitor takes M2 (-10,12) and C (10,12); SH, facing east, takes M1 (-10,-12) and R (10,-12).
//   41.0  Pile Pitch on OT RH SH.
//   50    M1 rescued from the cone to (-9.5,3.5), breaking M1-M2's green tether.
//   54.1  Hello World: near OT (0,6) -> M1 and M2 in either order, far RH (0,19) -> R (-19,1) -> C (16,10).
//   60    R and C meet to break the last green tether.
public class TopP5DeltaScenarioTests
{
    private static readonly PartyRole[] Order =
        [MainTank, OffTank, RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];

    private const uint Yellow = BNpcBaseId.RocketPunchYellow;
    private const uint Blue = BNpcBaseId.RocketPunchBlue;

    private const float FacingNorth = MathF.PI;
    private const float FacingWest = -MathF.PI / 2;

    private static readonly Vector2 OutsideArena = new(0, 20.5f);


    private static NegativeRun<TopP5DeltaScenario> Delta(PartyRole player, Action<TopP5DeltaStateOverrides>? tweak = null)
        => Negative<TopP5DeltaScenario>(player)
            .Overrides<TopP5DeltaStateOverrides>(o =>
            {
                o.EyeSpawn = EyeDirection.North;
                o.SwivelCannonSide = Side.Left;
                o.OmegaMonitorSide = Side.Left;
                o.PlayerMonitorSide = Side.Left;
                o.TetherOrder = Order;
                o.FistColors = [Yellow, Blue, Blue, Yellow, Yellow, Blue, Blue, Yellow];
                o.ArmModels = [ArmModel.Left, ArmModel.Left, ArmModel.Left, ArmModel.Right, ArmModel.Right, ArmModel.Right];
                o.ArmRotations =
                [
                    ArmRotation.CounterClockwise, ArmRotation.CounterClockwise, ArmRotation.CounterClockwise,
                    ArmRotation.Clockwise, ArmRotation.Clockwise, ArmRotation.Clockwise,
                ];
                o.HelloWorld[RegenHealer] = HelloWorldOption.Far;
                o.HelloWorld[OffTank] = HelloWorldOption.Near;
                o.Monitor[ShieldHealer] = true;
                o.BeyondDefence[MainTank] = true;
                tweak?.Invoke(o);
            });

    [Test]
    public void DiesWalkingIntoWall()
        => Delta(RegenHealer)
            .TeleportAt(5f, to: OutsideArena)
            .ShouldKill(ArenaWall, RegenHealer);

    [Test]
    public void DyingWithNearWorldWipes()
        => Delta(OffTank)
            .TeleportAt(12f, to: OutsideArena)
            .ShouldKill(ArenaWall, OffTank)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(OffTank));

    [Test]
    public void DyingWithDistantWorldWipes()
        => Delta(RegenHealer)
            .TeleportAt(12f, to: OutsideArena)
            .ShouldKill(ArenaWall, RegenHealer)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(RegenHealer));

    // The second break's first hit lands on the Trice Come Ruin and vuln stacks the first one left.
    [Test]
    public void BreakingBothBlueTethersAtOnceWipes()
        => Delta(MainTank)
            .TeleportAt(28.3f, to: new(0, -6))
            .ShouldKill(ActionId.HwTetherBreak, PerRole.All);

    [Test]
    public void DiesToOpticalLaser()
        => Delta(RegenHealer)
            .TeleportAt(29.2f, to: new(0, -3))
            .ShouldKill(ActionId.OpticalLaser, RegenHealer);

    // Leaves MT's fist and its own with nothing to pair with, then steps off its own fist.
    [Test]
    public void UnpairedFistsWipe()
        => Delta(RegenHealer)
            .TeleportAt(29.8f, to: new(-10, -10))
            .TeleportAt(31f, to: new(-14, -10))
            .ShouldKill(ActionId.DeltaUnmitigatedExplosion, PerRole.All);

    // MT and RH share a colour, so MT should have swapped with OT; neither does.
    [Test]
    public void SameColourFistPairsWipe()
        => Delta(MainTank, o => o.FistColors = [Yellow, Blue, Yellow, Blue, Yellow, Blue, Blue, Yellow])
            .TeleportAt(29.8f, to: new(-10, -3))
            .MoveBotAt(29.8f, OffTank, to: new(-10, 3))
            .TeleportAt(31f, to: new(0, -6))
            .ShouldKill(ActionId.DeltaUnmitigatedExplosion, PerRole.All);

    // RH joins OT and SH, leaving MT's fist with nothing to pair with.
    [Test]
    public void ThreeStackedFistsWipe()
        => Delta(RegenHealer)
            .TeleportAt(29.8f, to: new(-10, 3))
            .TeleportAt(31f, to: new(-14, 0))
            .ShouldKill(ActionId.DeltaUnmitigatedExplosion, PerRole.All);

    [Test]
    public void DiesStandingInOwnFist()
        => Delta(RegenHealer)
            .FreezeAt(31f)
            .ShouldKill(ActionId.DeltaExplosion, RegenHealer);

    [Test]
    public void DiesNextToBeyondDefenseTarget()
        => Delta(RegenHealer)
            .TeleportAt(35.4f, to: new(0, -3))
            .ShouldKill(ActionId.BeyondDefenseAOE, RegenHealer);

    // Beyond Defense moved to RH so MT can stay with OT: beside it from the fists on, then in the stack.
    [Test]
    public void UnbrokenBlueTetherWipesWhenItExpires()
        => Delta(MainTank, o =>
            {
                o.BeyondDefence[MainTank] = null;
                o.BeyondDefence[RegenHealer] = true;
            })
            .TeleportAt(31.6f, to: new(0, 1))
            .TeleportAt(36.2f, to: new(0, -1))
            .ShouldKill(ActionId.HwTetherFail, PerRole.All);

    [Test]
    public void DiesStayingInFrontOfBaitedArm()
        => Delta(RegenHealer)
            .FreezeAt(36f)
            .ShouldKill(ActionId.HyperPulseDeltaCharging, RegenHealer);

    // The NW arm turns counter-clockwise; baited from its clockwise side it sweeps into the arena
    // and catches M2 on the way to its monitor spot.
    [Test]
    public void ArmBaitedTheWrongWaySweepsTheParty()
        => Delta(RegenHealer)
            .TeleportAt(33.6f, to: new(-18.15f, -7.1f))
            .TeleportAt(36.2f, to: new(14, -1))
            .ShouldKill(ActionId.HyperPulseDeltaShoot, MeleeDpsB)
            .ShouldKill(ActionId.HwTetherFail, AllBut(MeleeDpsB));

    // OT carries Near World, so the party goes with them.
    [Test]
    public void PilePitchWithTwoKillsBoth()
        => Delta(RegenHealer)
            .TeleportAt(40.6f, to: new(14, -1))
            .ShouldKill(ActionId.PilePitch, OffTank, ShieldHealer)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(OffTank, ShieldHealer));

    // Still carrying Twice Come Ruin from Beyond Defense.
    [Test]
    public void BeyondDefenseTargetDiesInPilePitch()
        => Delta(MainTank)
            .TeleportAt(36.5f, to: new(0, -1))
            .ShouldKill(ActionId.PilePitch, MainTank);

    [Test]
    public void OmegaMonitorTargetsOverlappingKillEachOther()
        => Delta(CasterDps)
            .TeleportAt(40.45f, to: new(-5, 12))
            .ShouldKill(ActionId.OversampledWaveCannonAoe, MeleeDpsB, CasterDps)
            .ShouldKill(ActionId.HwTetherFail, AllBut(MeleeDpsB, CasterDps));

    // C steps out of Omega's half so MT, still carrying Twice Come Ruin, is one of its only two picks.
    [Test]
    public void BeyondDefenseTargetDiesToOmegaMonitor()
        => Delta(MainTank)
            .TeleportAt(40.45f, to: new(10, 12))
            .MoveBotAt(40.45f, CasterDps, to: new(5, -1))
            .ShouldKill(ActionId.OversampledWaveCannonAoe, MainTank);

    // Faces north at x = -7, so only M1 and M2 are on its monitor's side; M2 is also Omega's.
    [Test]
    public void PlayerMonitorOnOmegaMonitorTargetKillsThem()
        => Delta(ShieldHealer)
            .TeleportAt(40.45f, to: new(-7, -1), facing: FacingNorth)
            .ShouldKill(ActionId.OversampledWaveCannonAoe, MeleeDpsB)
            .ShouldKill(ActionId.HwTetherFail, AllBut(MeleeDpsB));

    // Turned around, the monitor picks two of OT, RH and Omega's targets at random. Every pick kills:
    // Omega's targets are hit twice, and OT and RH share a spot, so each is inside the other's
    // circle. MT steps off that side: its Twice Come Ruin would only turn to Doom.
    [Test]
    public void PlayerMonitorFacingAwayKillsSomeone()
        => Delta(ShieldHealer)
            .TeleportAt(40.45f, to: new(1.2f, -3), facing: FacingWest)
            .MoveBotAt(40.45f, MainTank, to: new(13, -4))
            .ShouldKillSomeone(ActionId.OversampledWaveCannonAoe);

    // Monitor targets still have Magic Vulnerability Up. The Pile Pitch stack's Twice Come Ruin only
    // turns to Doom, but R and C's deaths fail the last green tether first.
    [Test]
    public void BreakingFirstGreenTetherEarlyKillsTheVulnerable()
        => Delta(MeleeDpsA)
            .TeleportAt(44f, to: new(-10, 4))
            .ShouldKill(ActionId.HwTetherBreak, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps)
            .ShouldKill(ActionId.HwTetherFail, MainTank, OffTank, RegenHealer, ShieldHealer);

    // The safe side's sign is eye x cannon side; mirrored by the eye east to west as well.
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void DiesToSwivelCannon(bool eyeNorth, bool cannonLeft)
    {
        var eye = eyeNorth ? EyeDirection.North : EyeDirection.South;
        var mirror = eyeNorth ? 1 : -1;
        var side = cannonLeft ? Side.Left : Side.Right;
        Delta(RegenHealer, o =>
            {
                o.EyeSpawn = eye;
                o.SwivelCannonSide = side;
            })
            .TeleportAt(52f, to: new(9.5f * mirror, -5f * mirror * side.Mul))
            .ShouldKill(side.SwivelCannonActionId, RegenHealer);
    }

    [Test]
    public void BystanderInFirstNearWorldWipes()
        => Delta(MainTank)
            .TeleportAt(52.5f, to: new(1, 9))
            .ShouldKill(ActionId.HelloWorldFail, PerRole.All);

    // Beside M1, whose jump comes first or second depending on the AI's jitter.
    [Test]
    public void BystanderInNearWorldJumpWipes()
        => Delta(MainTank)
            .TeleportAt(54f, to: new(-9.5f, 2))
            .ShouldKill(ActionId.HelloWorldFail, PerRole.All);

    // With M2 gone the near jumps go OT -> M1 -> OT again, still vulnerable from the first hit. R
    // steps back from the tie it otherwise has with OT for M1's nearest; it stays the far's farthest.
    [Test]
    public void NearWorldJumpingBackToFirstTargetWipes()
        => Delta(MeleeDpsB)
            .TeleportAt(53.6f, to: new(8, -5))
            .MoveBotAt(53.6f, PhysRangedDps, to: new(-19, -3))
            .ShouldKill(ActionId.HelloNearWorldJump, OffTank)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(OffTank));

    // Every Hello World target (OT RH M1 M2 R C) still has Magic Vulnerability Up. Which of M1 and M2
    // the near jumps take first is down to the AI's jitter.
    [Test]
    public void BreakingLastGreenTetherEarlyKillsHelloWorldJumpTargets()
        => Delta(PhysRangedDps)
            .TeleportAt(58.6f, to: new(2, 3))
            .ShouldKill(ActionId.HwTetherBreak, OffTank, RegenHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps);

    // R stays put when it should walk in to C.
    [Test]
    public void UnbrokenLastGreenTetherWipesWhenItExpires()
        => Delta(PhysRangedDps)
            .FreezeAt(59f)
            .ShouldKill(ActionId.HwTetherFail, PerRole.All);
}
