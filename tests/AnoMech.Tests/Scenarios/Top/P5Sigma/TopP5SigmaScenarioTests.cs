using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P5Sigma;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: both norths N, no tower flip, Mid Glitch, clockwise spinner, Omega F's staff,
// so the AI's coordinates below are its own. Tether pairs MT-OT, RH-SH, M1-M2, R-C; Wave Cannon
// skips MT and RH, who bait the arms. Near World on OT, Distant World on SH; the second arms are
// baited by MT and RH, the jumps taken by M1 M2 R C.
//
//   30.93 arms at (-7.07,7.07) and (7.07,7.07) pulse RH (8.8,-8.8) and MT (-8.8,-8.8) through the centre.
//   31.15 Wave Cannon cones from the centre at M1 N, C E, OT SE, M2 S, SH SW, R W, all 12.5 out.
//   40.18 Discharger knocks everyone 13 out from the centre, into their towers.
//   43.69 towers: M1+SH (-15.7,6.5), R+MT (15.7,6.5), RH (-6.5,-15.7), OT (6.5,-15.7), C (-6.5,15.7), M2 (6.5,15.7).
//   58.0  Rear Lasers fire north-south through the centre, then sweep clockwise.
//   60.82 Optimized Blizzard III from Omega F at (0,-10), facing south.
//   67.9  Near World on OT (-10,0) -> M2 (-19.5,0) -> R (-18.9,5); Distant on SH (0,10) -> M1 (0,-19.5) -> C (0,19.5).
//   68.0  arms at (-14.14,14.14) and (14.14,14.14) pulse MT (13.5,-14.2) and RH (-13.5,-14.2) through the centre.
public class TopP5SigmaScenarioTests
{
    private static readonly Vector2 OutsideArena = new(0, 20.5f);

    private static NegativeRun<TopP5SigmaScenario> Sigma(PartyRole player, Action<TopP5SigmaStateOverrides>? tweak = null)
        => Negative<TopP5SigmaScenario>(player)
            .Overrides<TopP5SigmaStateOverrides>(o =>
            {
                o.NewNorthA = Direction.N;
                o.NewNorthB = Direction.N;
                o.TowerNorthFlip = false;
                o.CloseFarTether = GlitchType.Mid;
                o.SpinnerRotation = Rotation.Clockwise;
                o.OmegaFForm = OmegaAttack.Staff;
                o.Order = [MainTank, OffTank, RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
                o.WaveCannonSkips = (0, 2);
                o.HelloWorld[OffTank] = HelloWorldOption.Near;
                o.HelloWorld[ShieldHealer] = HelloWorldOption.Far;
                o.Dynamis[MeleeDpsA] = false;
                o.Dynamis[MeleeDpsB] = false;
                o.HandBait = [MainTank, RegenHealer];
                o.HelloWorldJumpOrder = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
                tweak?.Invoke(o);
            });

    private static Vector2 Rotated(Direction north, Vector2 at)
    {
        var p = north.Apply(new Vector3(at.X, 0, at.Y));
        return new(p.X, p.Z);
    }

    [Test]
    public void DiesWalkingIntoWall()
        => Sigma(RegenHealer)
            .TeleportAt(5f, to: OutsideArena)
            .ShouldKill(ArenaWall, RegenHealer);

    [Test]
    public void DyingWithNearWorldWipes()
        => Sigma(OffTank)
            .TeleportAt(12f, to: OutsideArena)
            .ShouldKill(ArenaWall, OffTank)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(OffTank));

    [Test]
    public void DyingWithDistantWorldWipes()
        => Sigma(ShieldHealer)
            .TeleportAt(12f, to: OutsideArena)
            .ShouldKill(ArenaWall, ShieldHealer)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(ShieldHealer));

    // 18.5 from M1, under Mid Glitch's 20: both carry Vulnerability Up into their cones.
    [Test]
    public void MidGlitchTetherTooShortKillsBothInWaveCannon()
        => Sigma(MeleeDpsB)
            .TeleportAt(30.5f, to: new(0, 6))
            .ShouldKill(ActionId.WaveCannonAoe, MeleeDpsA, MeleeDpsB);

    [Test]
    public void MidGlitchTetherTooLongKillsBothInWaveCannon()
        => Sigma(MeleeDpsB)
            .TeleportAt(30.5f, to: new(0, 15))
            .ShouldKill(ActionId.WaveCannonAoe, MeleeDpsA, MeleeDpsB);

    // The spread goes to the wall, M1 at (0,-18.75); 30.75 is under Far Glitch's 34.
    [Test]
    public void FarGlitchTetherTooShortKillsBothInWaveCannon()
        => Sigma(MeleeDpsB, o => o.CloseFarTether = GlitchType.Far)
            .TeleportAt(30.5f, to: new(0, 12))
            .ShouldKill(ActionId.WaveCannonAoe, MeleeDpsA, MeleeDpsB);

    // On the line from the SW arm to RH: the pulse leaves both vulnerable, and M1's cone, now
    // aimed NE, takes RH as well as M1.
    [Test]
    public void WaveCannonTargetInArmLineKillsItAndTheBait()
        => Sigma(MeleeDpsA)
            .TeleportAt(30.5f, to: new(7, -7))
            .ShouldKill(ActionId.WaveCannonAoe, MeleeDpsA, RegenHealer);

    // 18° from OT: each is inside the other's cone, and the first one leaves both vulnerable.
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    public void OverlappingWaveCannonTargetsKillEachOther(int northA)
    {
        var north = Direction.All[northA];
        Sigma(CasterDps, o => o.NewNorthA = north)
            .TeleportAt(30.5f, to: Rotated(north, new(10, 5)))
            .ShouldKill(ActionId.WaveCannonAoe, CasterDps, OffTank);
    }

    [Test]
    public void KnockedIntoWall()
        => Sigma(PhysRangedDps)
            .TeleportAt(39.6f, to: new(8, 0))
            .ShouldKill(ArenaWall, PhysRangedDps);

    // 21.4 from MT, so the tether stays clean and only OT's tower is missing.
    [Test]
    public void SoloTowerUnfilledWipes()
        => Sigma(OffTank)
            .TeleportAt(42f, to: new(0, -8))
            .ShouldKill(ActionId.StorageViolationObliteration, PerRole.All);

    // 23.7 from C, so the tether stays clean and MT is left alone in the pair tower.
    [TestCase(false)]
    [TestCase(true)]
    public void PairTowerWithOneWipes(bool towerNorthFlip)
    {
        var north = towerNorthFlip ? Direction.S : Direction.N;
        Sigma(PhysRangedDps, o => o.TowerNorthFlip = towerNorthFlip)
            .TeleportAt(42f, to: Rotated(north, new(5, -5)))
            .ShouldKill(ActionId.StorageViolationObliteration, PerRole.All);
    }

    // Still in its tower, but 26.6 from SH: both soak carrying Vulnerability Up.
    [Test]
    public void TowerSoakedWithTetherTooLongKillsBoth()
        => Sigma(RegenHealer)
            .TeleportAt(42f, to: new(-4.8f, -17.8f))
            .ShouldKill(ActionId.StorageViolationSolo, RegenHealer)
            .ShouldKill(ActionId.StorageViolationPair, ShieldHealer);

    // The beam runs out both sides of the unit, so the spot mirrored through the centre dies too.
    [TestCase(false)]
    [TestCase(true)]
    public void DiesToFirstRearLaser(bool behind)
        => Sigma(CasterDps)
            .TeleportAt(57f, to: new Vector2(0, 10) * (behind ? -1 : 1))
            .ShouldKill(ActionId.RearLasersCharging, CasterDps);

    // West of south for the clockwise sweep, east for the counter-clockwise one.
    [TestCase(true, false)]
    [TestCase(true, true)]
    [TestCase(false, false)]
    [TestCase(false, true)]
    public void DiesToRearLaserSweep(bool clockwise, bool behind)
    {
        var rotation = clockwise ? Rotation.Clockwise : Rotation.CounterClockwise;
        Sigma(CasterDps, o => o.SpinnerRotation = rotation)
            .TeleportAt(58f, to: new Vector2(15 * rotation.Mul, 4) * (behind ? -1 : 1))
            .ShouldKill(ActionId.RearLasersShoot, CasterDps);
    }

    // Behind Omega F on the cross's long arm, where the lasers never reach.
    [TestCase(0)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(6)]
    public void DiesToOptimizedBlizzard(int northB)
    {
        var north = Direction.All[northB];
        Sigma(MainTank, o => o.NewNorthB = north)
            .TeleportAt(60f, to: Rotated(north, new(0, -17)))
            .ShouldKill(ActionId.OptimizedBlizzardIII, MainTank);
    }

    // Stays on the Rear Lasers spot instead of stepping into Omega F's line.
    [Test]
    public void DiesToSuperliminalSteel()
        => Sigma(MainTank, o => o.OmegaFForm = OmegaAttack.Legs)
            .TeleportAt(58.5f, to: new(-6.5f, -17))
            .ShouldKill(ActionId.SuperliminalSteelOmenR, MainTank);

    // Steps onto the second arm's line to MT just after Near World's first hit made it vulnerable.
    [Test]
    public void DiesInArmLineAfterTakingNearWorld()
        => Sigma(OffTank)
            .TeleportAt(67.95f, to: new(-5, 5))
            .ShouldKill(ActionId.HyperPulseSigma, OffTank);

    [Test]
    public void BystanderInNearWorldWipes()
        => Sigma(PhysRangedDps)
            .TeleportAt(67.5f, to: new(-12, 2))
            .ShouldKill(ActionId.HelloWorldFail, PerRole.All);

    // With M2 gone the near jumps go OT -> R -> OT again, still vulnerable from the first hit.
    [Test]
    public void NearWorldJumpingBackToHolderWipes()
        => Sigma(MeleeDpsB)
            .TeleportAt(67.5f, to: new(8, -3))
            .ShouldKill(ActionId.HelloNearWorldJump, OffTank)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(OffTank));

    // RH comes off its arm vulnerable and ends up nearest OT.
    [Test]
    public void NearWorldJumpOntoArmBaitWipes()
        => Sigma(RegenHealer)
            .TeleportAt(68.2f, to: new(-10, -5))
            .ShouldKill(ActionId.HelloNearWorldJump, RegenHealer)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(RegenHealer));

    // From SH's new spot MT, still vulnerable from its arm, is farther than M1.
    [Test]
    public void DistantWorldJumpOntoArmBaitWipes()
        => Sigma(ShieldHealer)
            .TeleportAt(67.5f, to: new(-5, 10))
            .ShouldKill(ActionId.HelloDistantWorldJump, MainTank)
            .ShouldKill(ActionId.HelloWorldFail, AllBut(MainTank));
}
