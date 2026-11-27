using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Dsr.P3Nidhogg;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Tests.NegativeRun;
using ActionId = AnoMech.Scenarios.Dsr.DsrConstants.ActionId;

namespace AnoMech.Tests;

// Every roll is pinned: arrows on the Firsts only (H1 High, M1 up, R2 down), MT and H2 Second,
// OT M2 R1 Third, all High; Lashing first then Gnashing, then Gnashing then Lashing; 2-2-2-2
// counted towers with NE tethered; Drachenlance on M2. Westhogg strat.
//
//   33.33  Firsts jump: H1 (0,7.6), M1 (7.5,0.2) facing west, R2 (-7.6,0.3); the rest stack at
//          (0,-8.4). Eye of the Tyrant snapshots on the stack at 33.48.
//   36.95  Lashing Wheel; towers at (0,7.6) (-6.5,0.2) (6.4,0.3), soaked by M2 OT R1 at 39.65.
//   40.00  Gnashing Wheel. The round-1 lines aim at 41.63 at OT (-7.4,0.3), M2 (0.2,8.6),
//          R1 (7.6,0.5), who leave at 41.8; the lines go off at 46.75.
//   43.33  Seconds jump: MT (-7.7,-7.8), H2 (7.8,-7.9). Their towers go off at 49.65, soaked by
//          R2 and M1.
//   54.32  Thirds jump: OT (-7.2,0), M2 (-0.2,7.6), R1 (7.3,0.1). Eye at 55.00 on the stack at
//          (0,-8.6).
//   58.47  Gnashing Wheel; 60.65 towers soaked by MT H1 H2; 61.48 Lashing Wheel. The round-3 lines
//          aim at 62.64 at MT (-8.1,-0.1), H1 (0,8.6), H2 (8.1,0.1), who leave at 62.8.
//   69.60  Nidhogg faces M2 in the north stack; Drachenlance snapshots at 73.28.
//   79.80  Counted towers: NE (8,-8) R1 MT, SE (8,8) H2 M2, SW (-8,8) R2 OT, NW (-8,-8) M1 H1.
//          Lines aim at 81.72 at H1 (-9,-8.8), H2 (8.6,8.6), R2 (-8.4,8.7), who leave at 81.8.
//   87.17  Soul Tether snapshots on MT (0,-3) and OT (11.1,-11.3).
//
// A run pauses 5s after its first death.
public class DsrP3NidhoggScenarioTests
{
    private static readonly float[] WheelAt = [36.95f, 40.00f, 58.47f, 61.48f];
    private const float FacingEast = MathF.PI / 2f;

    internal static void Pin(DsrP3NidhoggStateOverrides o)
    {
        o.ArrowPattern = 3;
        o.FirstOutFirst = false;
        o.SecondOutFirst = true;
        o.CounterTowerShape = 0;
        o.TetherTower = 0;
        o.FirstDrachenlanceTarget = MeleeDpsB;
        (PartyRole Role, int Order, JumpKind Jump)[] deal =
        [
            (RegenHealer, 1, JumpKind.High), (MeleeDpsA, 1, JumpKind.Forward), (CasterDps, 1, JumpKind.Backward),
            (MainTank, 2, JumpKind.High), (ShieldHealer, 2, JumpKind.High),
            (OffTank, 3, JumpKind.High), (MeleeDpsB, 3, JumpKind.High), (PhysRangedDps, 3, JumpKind.High),
        ];
        foreach (var (role, order, jump) in deal)
        {
            o.Order[role] = order;
            o.Jump[role] = jump;
        }
    }

    private static NegativeRun<DsrP3NidhoggScenario> Nidhogg(PartyRole player, Action<DsrP3NidhoggStateOverrides>? tweak = null)
        => Negative<DsrP3NidhoggScenario>(player)
            .Overrides<DsrP3NidhoggStateOverrides>(o =>
            {
                Pin(o);
                tweak?.Invoke(o);
            });

    private static Action<DsrP3NidhoggStateOverrides> WheelIs(int wheel, bool gnashing) => o =>
    {
        var outFirst = (wheel % 2 == 0) == gnashing;
        if (wheel < 2) o.FirstOutFirst = outFirst;
        else o.SecondOutFirst = outFirst;
    };

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void StayingInsideGnashingWheelDies(int wheel)
        => Nidhogg(ShieldHealer, WheelIs(wheel, gnashing: true))
            .TeleportAt(WheelAt[wheel] - 0.3f, to: new(0, -2))
            .ShouldKill(ActionId.GnashingWheel, ShieldHealer);

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void StayingOutsideLashingWheelDies(int wheel)
        => Nidhogg(ShieldHealer, WheelIs(wheel, gnashing: false))
            .TeleportAt(WheelAt[wheel] - 0.3f, to: new(0, -12))
            .ShouldKill(ActionId.LashingWheel, ShieldHealer);

    // Whoever Eye picks, the stack is one short: the four left in it die, or the player alone.
    [TestCase(33.40f)]
    [TestCase(54.90f)]
    public void LeavingEyeOfTheTyrantStackKillsSomeone(float leave)
        => Nidhogg(ShieldHealer)
            .TeleportAt(leave, to: new(0, 0))
            .ShouldKillSomeone(ActionId.EyeOfTheTyrant);

    // Round 3's spot is inside: its Lashing Wheel goes off before the fail lands.
    [TestCase(39.50f, PhysRangedDps, 0f, -12f)]
    [TestCase(49.50f, CasterDps, 0f, -12f)]
    [TestCase(60.50f, ShieldHealer, 0f, -4f)]
    public void UnsoakedDiveTowerWipes(float leave, PartyRole soaker, float x, float z)
        => Nidhogg(soaker)
            .TeleportAt(leave, to: new(x, z))
            .ShouldKill(ActionId.DarkdragonDiveFail, PerRole.All);

    // Each jumper stays on the tower their own jump leaves, clear of the wheel before it.
    [TestCase(34.0f, RegenHealer, 0f, 6f)]
    [TestCase(43.4f, ShieldHealer, 7.8f, -7.9f)]
    [TestCase(54.4f, OffTank, -11f, 0f)]
    public void SoakingOwnJumpsTowerWhileBurningDies(float stay, PartyRole jumper, float x, float z)
        => Nidhogg(jumper)
            .TeleportAt(stay, to: new(x, z))
            .ShouldKill(ActionId.DarkdragonDive, jumper);

    [Test]
    public void SecondTowerWhileBurningDies()
        => Nidhogg(MeleeDpsB)
            .TeleportAt(49.4f, to: new(6.5f, -6.5f))
            .ShouldKill(ActionId.DarkdragonDive, MeleeDpsB);

    [Test]
    public void StandingInSecondRoundJumpWhileBurningDies()
        => Nidhogg(MeleeDpsB)
            .TeleportAt(43.2f, to: new(7f, -7f))
            .ShouldKill(ActionId.DarkHighJump, MeleeDpsB);

    // H1 is the one bystander no longer burning by round 3. The stack it leaves is short for Eye,
    // but those deaths land after the wall.
    [Test]
    public void ClippedByJumpIsThrownIntoTheWall()
        => Nidhogg(RegenHealer)
            .TeleportAt(54.2f, to: new(-7.2f, -1.5f))
            .ShouldKill(TheEnvironment, RegenHealer);

    // The player's knockback starts as the jump resolves, so the wall takes them before either hit
    // lands; H1 takes the second jump on the first one's vuln.
    [Test]
    public void TwoJumpersTogetherBothDie()
        => Nidhogg(CasterDps)
            .TeleportAt(33.0f, to: new(-1.5f, 7.6f))
            .ShouldKill(TheEnvironment, CasterDps)
            .ShouldKill(ActionId.DarkElusiveJump, RegenHealer);

    // The up arrow's tower lands 14y east, past the arena's edge, where nobody goes. M1 then keeps
    // out of the other towers and the wheels until the fail lands.
    [Test]
    public void ArrowJumperFacingWrongWayWipes()
        => Nidhogg(MeleeDpsA)
            .TeleportAt(33.0f, to: new(7.5f, 0f), facing: FacingEast)
            .TeleportAt(34.0f, to: new(0, -4))
            .TeleportAt(39.8f, to: new(0, -12))
            .ShouldKill(ActionId.DarkdragonDiveFail, PerRole.All);

    // Round 2 has no case: its baiters are Firsts who must be back in the stack for round 3's Eye.
    [TestCase(41.75f, OffTank)]
    [TestCase(62.75f, ShieldHealer)]
    [TestCase(81.75f, CasterDps)]
    public void GeirskogulBaiterStayingPutDies(float freeze, PartyRole baiter)
        => Nidhogg(baiter)
            .FreezeAt(freeze)
            .ShouldKill(ActionId.Geirskogul, baiter);

    [Test]
    public void StandingInFrontForDrachenlanceDies()
        => Nidhogg(MeleeDpsB)
            .FreezeAt(69.5f)
            .ShouldKill(ActionId.DrachenlanceCleave, MeleeDpsB);

    [Test]
    public void UnderSoakedCountedTowerWipes()
        => Nidhogg(CasterDps)
            .TeleportAt(79.6f, to: new(0, 0))
            .ShouldKill(ActionId.DarkdragonDiveFail, PerRole.All);

    // Behind the MT, off the line from Nidhogg, so the tether stays with the MT.
    [Test]
    public void StandingNextToSoulTetherTankDies()
        => Nidhogg(CasterDps)
            .TeleportAt(87.0f, to: new(0, -4.5f))
            .ShouldKill(ActionId.SoulTether, CasterDps);

    // Between Nidhogg and the MT after the tanks' last step, so the tether stays on the player.
    [Test]
    public void HoldingSoulTetherAsDpsDies()
        => Nidhogg(CasterDps)
            .TeleportAt(86.0f, to: new(0, -1))
            .ShouldKill(ActionId.SoulTether, CasterDps);

    [Test]
    public void TanksStackedForSoulTetherDie()
        => Nidhogg(OffTank)
            .TeleportAt(86.9f, to: new(0.5f, -3))
            .ShouldKill(ActionId.SoulTether, MainTank, OffTank);

    [Test]
    public void WalkingOffTheArenaDies()
        => Nidhogg(MainTank)
            .TeleportAt(20f, to: new(0, 25))
            .ShouldKill(TheEnvironment, MainTank);
}
