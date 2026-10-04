using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P5Omega;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: first F/M pair NE/SW as Staff + Sword, second SE/NW as Legs + Shield, first
// Diffuse Wave Cannon front/back, monitors on the left, beetle north. Hello World goes Near OT, Far SH,
// then Near M1, Far M2; OT SH M1 M2 start with the extra Dynamis, so M1 M2 take the monitors and OT SH
// reach 3 stacks and take the Blaster tethers.
//
//   24.21 Optimized Blizzard III from F at (6.9,-6.9), Efficient Bladework from M at (-6.9,6.9); party at (-17,0).
//   24.58 Diffuse Wave Cannon north and south.
//   28.16 Superliminal Steel from F at (6.9,6.9), Beyond Strength from M at (-6.9,-6.9); party at (0,-2.5).
//   28.58 Diffuse Wave Cannon east and west.
//   41.25 Near on OT (10,0) -> RH (19,-4) and R (19,4) in either order; Far on SH (0,-10) -> C (0,19) -> MT (0,-19).
//   41.74 monitors on the west half: M1 (-10,-10), M2 (-10,10).
//   57.58 Blaster on OT (-9.5,-16.5) and SH (9.5,-16.5).
//   59.27 Near on M1 (0,10) -> RH (-4,19) and R (4,19) in either order; Far on M2 (10,0) -> MT (-19,0) -> C (19,0).
public class TopP5OmegaScenarioTests
{
    private static readonly Vector2 OutsideArena = new(0, 20.5f);

    private static NegativeRun<TopP5OmegaScenario> Omega(PartyRole player, Action<TopP5OmegaStateOverrides>? tweak = null)
        => Negative<TopP5OmegaScenario>(player)
            .Overrides<TopP5OmegaStateOverrides>(o =>
            {
                o.FirstAttackDirection = Direction.NE;
                o.SecondAttackDirection = Direction.SE;
                o.FirstFAttack = OmegaAttack.Staff;
                o.FirstMAttack = OmegaAttack.Sword;
                o.SecondFAttack = OmegaAttack.Legs;
                o.SecondMAttack = OmegaAttack.Shield;
                o.FirstWaveCannonFront = true;
                o.MonitorSide = MonitorSide.Left;
                o.BettleSpawnDirection = Direction.N;
                HelloWorld(o, OffTank, HelloWorldOrderOption.First, HelloWorldTypeOption.Near);
                HelloWorld(o, ShieldHealer, HelloWorldOrderOption.First, HelloWorldTypeOption.Far);
                HelloWorld(o, MeleeDpsA, HelloWorldOrderOption.Second, HelloWorldTypeOption.Near);
                HelloWorld(o, MeleeDpsB, HelloWorldOrderOption.Second, HelloWorldTypeOption.Far);
                foreach (var role in new[] { OffTank, ShieldHealer, MeleeDpsA, MeleeDpsB })
                    o.ExtraDynamis[role] = true;
                o.MonitorTargets = [MeleeDpsA, MeleeDpsB];
                o.HelloWorld1JumpOrder = [MainTank, RegenHealer, PhysRangedDps, CasterDps];
                o.BlasterTethers = [OffTank, ShieldHealer];
                o.HelloWorld2 = [MeleeDpsA, MeleeDpsB, OffTank, ShieldHealer, MainTank, RegenHealer, PhysRangedDps, CasterDps];
                tweak?.Invoke(o);
            });

    private static void HelloWorld(TopP5OmegaStateOverrides o, PartyRole role, HelloWorldOrderOption order, HelloWorldTypeOption type)
    {
        o.HelloWorldOrder[role] = order;
        o.HelloWorldType[role] = type;
    }

    // In the frame of an attack pair: F at (0,-9.8), M at (0,9.8), turned to `direction`.
    private static Vector2 Around(Direction direction, float x, float y)
    {
        var p = direction.Apply(new Vector3(x, 0, y));
        return new(p.X, p.Z);
    }

    [Test]
    public void DiesWalkingIntoWall()
        => Omega(RegenHealer)
            .TeleportAt(5f, to: OutsideArena)
            .ShouldKill(ArenaWall, RegenHealer);

    [TestCase(OffTank)]
    [TestCase(MeleeDpsA)]
    public void DyingWithNearWorldWipes(PartyRole holder)
        => Omega(holder)
            .TeleportAt(12f, to: OutsideArena)
            .ShouldKill(ArenaWall, holder)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(holder));

    [TestCase(ShieldHealer)]
    [TestCase(MeleeDpsB)]
    public void DyingWithDistantWorldWipes(PartyRole holder)
        => Omega(holder)
            .TeleportAt(12f, to: OutsideArena)
            .ShouldKill(ArenaWall, holder)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(holder));

    // Out along F's diagonal, which is one arm of the cross.
    [TestCase(1)]
    [TestCase(3)]
    [TestCase(5)]
    [TestCase(7)]
    public void DiesToOptimizedBlizzard(int first)
    {
        var direction = Direction.All[first];
        Omega(MainTank, o =>
            {
                o.FirstAttackDirection = direction;
                o.SecondAttackDirection = direction.Rotate(2);
            })
            .TeleportAt(23.5f, to: Around(direction, 0, -14))
            .ShouldKill(ActionId.OptimizedBlizzardIII, MainTank);
    }

    // Beside M, clear of the cross.
    [Test]
    public void DiesToEfficientBladework()
        => Omega(MainTank)
            .TeleportAt(23.5f, to: Around(Direction.NE, 6, 9.8f))
            .ShouldKill(ActionId.EfficientBladework, MainTank);

    // Beside M, so Beyond Strength misses, but off F's line.
    [Test]
    public void DiesToSuperliminalSteel()
        => Omega(MainTank)
            .TeleportAt(27.5f, to: Around(Direction.SE, 6, 9.8f))
            .ShouldKill(ActionId.SuperliminalSteelOmenL, MainTank);

    // On F's line, so Superliminal Steel misses, but far from M.
    [Test]
    public void DiesToBeyondStrength()
        => Omega(MainTank)
            .TeleportAt(27.5f, to: Around(Direction.SE, 0, -5))
            .ShouldKill(ActionId.BeyondStrength, MainTank);

    // Clear of the first Omega attacks, 26.6° off the middle of a first cone.
    [TestCase(true)]
    [TestCase(false)]
    public void DiesToFirstDiffuseWaveCannon(bool front)
        => Omega(MainTank, o => o.FirstWaveCannonFront = front)
            .TeleportAt(23.5f, to: front ? new(-6, -12) : new(-12, -6))
            .ShouldKill(ActionId.OmegaDiffuseWaveCannonAOE, MainTank);

    // Clear of the second Omega attacks, 36.9° off the middle of a second cone.
    [TestCase(true)]
    [TestCase(false)]
    public void DiesToSecondDiffuseWaveCannon(bool firstFront)
        => Omega(MainTank, o => o.FirstWaveCannonFront = firstFront)
            .TeleportAt(27.5f, to: firstFront ? new(-8, -6) : new(-6, -8))
            .ShouldKill(ActionId.OmegaDiffuseWaveCannonAOE, MainTank);

    [Test]
    public void BystanderInFirstNearWorldWipes()
        => Omega(MainTank)
            .TeleportAt(40.5f, to: new(10, -3))
            .ShouldKill(ActionId.HelloWorldFail, PerRole.All);

    [Test]
    public void BystanderInFirstDistantWorldWipes()
        => Omega(MainTank)
            .TeleportAt(40.5f, to: new(0, -7))
            .ShouldKill(ActionId.HelloWorldFail, PerRole.All);

    // With RH gone the near jumps go OT -> R -> OT again, still vulnerable from the first hit.
    [Test]
    public void NearWorldJumpingBackToHolderWipes()
        => Omega(RegenHealer)
            .TeleportAt(41.5f, to: new(12, -12))
            .ShouldKill(ActionId.HelloNearWorldJump, OffTank)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(OffTank));

    // Fresh off its monitor, nearer to OT than RH and R.
    [Test]
    public void NearWorldJumpOntoMonitorTargetWipes()
        => Omega(MeleeDpsA)
            .TeleportAt(41.9f, to: new(5, -4))
            .ShouldKill(ActionId.HelloNearWorldJump, MeleeDpsA)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(MeleeDpsA));

    // Fresh off its monitor and farther from SH than C, who steps in.
    [Test]
    public void DistantWorldJumpOntoMonitorTargetWipes()
        => Omega(MeleeDpsB)
            .TeleportAt(41.9f, to: new(-8, 17))
            .MoveBotAt(41.9f, CasterDps, to: new(-3, -3))
            .ShouldKill(ActionId.HelloDistantWorldJump, MeleeDpsB)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(MeleeDpsB));

    // M2 steps out of the west half, leaving OT, still vulnerable from its Near World, one of the two picks.
    [Test]
    public void HelloWorldHolderDiesToMonitor()
        => Omega(OffTank)
            .TeleportAt(41.4f, to: new(-10, 0))
            .MoveBotAt(41.4f, MeleeDpsB, to: new(5, 10))
            .ShouldKill(ActionId.OversampledWaveCannonAoe, OffTank);

    [Test]
    public void OverlappingMonitorTargetsKillEachOther()
        => Omega(MeleeDpsB)
            .TeleportAt(41.5f, to: new(-10, -4))
            .ShouldKill(ActionId.OversampledWaveCannonAoe, MeleeDpsA, MeleeDpsB)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(MeleeDpsA, MeleeDpsB));

    [Test]
    public void OverlappingBlasterTargetsKillEachOther()
        => Omega(OffTank)
            .TeleportAt(57f, to: new(2, -16.5f))
            .ShouldKill(ActionId.OmegaBlasterAoe, OffTank, ShieldHealer);

    // Still vulnerable from Blaster, nearer to M1 than RH and R.
    [Test]
    public void BlasterTargetSoakingHelloWorldWipes()
        => Omega(OffTank)
            .TeleportAt(59.5f, to: new(0, 14.5f))
            .ShouldKill(ActionId.HelloNearWorldJump, OffTank)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(OffTank));

    // MT and OT swap places in the second Hello World: MT takes the tether, and OT, at 3 stacks, the far jump.
    [Test]
    public void HelloWorldOnThreeDynamisStacksWipes()
        => Omega(OffTank, o =>
            {
                o.BlasterTethers = [MainTank, ShieldHealer];
                o.HelloWorld2 = [MeleeDpsA, MeleeDpsB, MainTank, ShieldHealer, OffTank, RegenHealer, PhysRangedDps, CasterDps];
            })
            .ShouldKill(ActionId.HelloDistantWorldJump, OffTank)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(OffTank));
}
