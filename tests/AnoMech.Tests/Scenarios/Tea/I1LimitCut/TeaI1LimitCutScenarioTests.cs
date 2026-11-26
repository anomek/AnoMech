using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea;
using AnoMech.Scenarios.Tea.I1LimitCut;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: numbers 1 H2, 2 MT, 3 OT, 4 H1, 5 C, 6 M1, 7 M2, 8 R; the first Hawk Blasters
// on E/W.
//
//    0.11  Cruise Chaser's line; Brute Justice's at 31.14.
//    3.19  The numbers go out, 1 to 8 over 0.21s.
//    5.27  Hawk Blasters every 2.2s: pairs 14y out from E/W, turning 45° clockwise; the centre at 14.05
//          and 25.07; from 16.25 the pairs start over on E/W.
//   11.55  Cruise Chaser warps 2y behind H2 (#1), facing H2's way; Alpha Sword on H2 at 12.64, Super
//          Blassty Charge on MT (#2) at 14.14. OT and H1 at 16.16 / 17.31 / 18.80, C and M1 at
//          20.80 / 21.95 / 23.42, M2 and R at 25.50 / 26.58 / 28.05.
//   31.26  J Kick; Brute Justice and Cruise Chaser targetable at 34.29.
public class TeaI1LimitCutScenarioTests
{
    private static readonly PartyRole[] PinnedNumbers =
        [ShieldHealer, MainTank, OffTank, RegenHealer, CasterDps, MeleeDpsA, MeleeDpsB, PhysRangedDps];

    private static void Pin(TeaI1LimitCutStateOverrides o) => Pin(o, axis: 2);

    private static void Pin(TeaI1LimitCutStateOverrides o, int axis)
    {
        o.FirstBlasterAxis = axis;
        for (var k = 0; k < PinnedNumbers.Length; k++) o.Number[PinnedNumbers[k]] = k + 1;
    }

    private static NegativeRun<TeaI1LimitCutScenario> LimitCut(PartyRole player)
        => Negative<TeaI1LimitCutScenario>(player).Overrides<TeaI1LimitCutStateOverrides>(Pin);

    private static SimEnemy CruiseChaser(ScenarioProbe p)
        => p.World.Children.OfType<SimEnemy>().Single(e => e.BNpcBaseId == BNpcBaseId.CruiseChaser);

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static Vector2 OnCompass(float degrees, float radius) => Flat(Geometry.OnCompass(degrees, radius));

    private static void StandOutsideCruiseChaser(ScenarioProbe p, PartyRole role, bool facingCruiseChaser)
        => StandOutsideCruiseChaser(p, role, facingOffDegrees: facingCruiseChaser ? 0f : 180f);

    // 3y further out than Cruise Chaser, so the 20y knockback has nowhere to go but off the arena.
    private static void StandOutsideCruiseChaser(ScenarioProbe p, PartyRole role, float facingOffDegrees)
    {
        var cruiseChaser = Flat(CruiseChaser(p).Position);
        var outward = Vector2.Normalize(cruiseChaser);
        var member = p.Member(role)!;
        var spot = cruiseChaser + outward * 3f;
        member.SetPosition(new Vector3(spot.X, 0f, spot.Y));
        member.SetRotation(MathF.Atan2(-outward.X, -outward.Y) + facingOffDegrees * MathF.PI / 180f);
    }

    [Test]
    public void DiesWalkingIntoWall()
        => LimitCut(MeleeDpsB)
            .TeleportAt(2f, to: new(0, 20.5f))
            .ShouldKill(TheEnvironment, MeleeDpsB);

    // The far end of the first pair, on each axis.
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void DiesToFirstHawkBlasters(int axis)
        => Negative<TeaI1LimitCutScenario>(MeleeDpsB)
            .Overrides<TeaI1LimitCutStateOverrides>(o => Pin(o, axis))
            .TeleportAt(6f, to: OnCompass(axis * 45f + 180f, 14f))
            .ShouldKill(ActionId.HawkBlaster, MeleeDpsB);

    [Test]
    public void DiesToCentreHawkBlaster()
        => LimitCut(PhysRangedDps)
            .TeleportAt(14.8f, to: new(0, 0))
            .ShouldKill(ActionId.HawkBlaster, PhysRangedDps);

    // Wave 6 lands on wave 1's E/W again, not on the next axis round.
    [Test]
    public void SecondRunOfHawkBlastersStartsOverOnFirstAxis()
        => LimitCut(MeleeDpsB)
            .TeleportAt(17f, to: new(14, 0))
            .ShouldKill(ActionId.HawkBlaster, MeleeDpsB);

    [Test]
    public void AlphaSwordOnAlreadyVulnerableOddKills()
        => LimitCut(ShieldHealer)
            .At(13.3f, p => p.Member(ShieldHealer)!.AddStatus(StatusId.PhysicalVulnerabilityUp, 17f))
            .ShouldKill(ActionId.AlphaSword, ShieldHealer);

    [Test]
    public void SuperBlasstyChargeOnAlreadyVulnerableEvenKills()
        => LimitCut(MainTank)
            .At(14.8f, p => p.Member(MainTank)!.AddStatus(StatusId.PhysicalVulnerabilityUp, 17f))
            .ShouldKill(ActionId.SuperBlasstyCharge, MainTank);

    // OT, already vulnerable, steps in front of H2 as H2 takes #1.
    [Test]
    public void StandingInFrontOfOddWhileVulnerableDiesToAlphaSword()
        => LimitCut(OffTank)
            .FreezeAt(13.3f)
            .At(13.4f, p =>
            {
                var odd = p.Member(ShieldHealer)!;
                var offTank = p.Member(OffTank)!;
                offTank.AddStatus(StatusId.PhysicalVulnerabilityUp, 17f);
                offTank.SetPosition(odd.Position + new Vector3(MathF.Sin(odd.Rotation), 0f, MathF.Cos(odd.Rotation)) * 3f);
            })
            .ShouldKill(ActionId.AlphaSword, OffTank);

    // OT, already vulnerable, steps between Cruise Chaser and MT as MT takes #2.
    [Test]
    public void StandingInSuperBlasstyChargeWhileVulnerableDies()
        => LimitCut(OffTank)
            .FreezeAt(14.9f)
            .At(15.0f, p =>
            {
                var offTank = p.Member(OffTank)!;
                offTank.AddStatus(StatusId.PhysicalVulnerabilityUp, 17f);
                offTank.SetPosition((CruiseChaser(p).Position + p.Member(MainTank)!.Position) / 2f);
            })
            .ShouldKill(ActionId.SuperBlasstyCharge, OffTank);


    // Taken off the AI before the bot would press Arm's Length.
    [Test]
    public void SuperBlasstyChargeThrowsEvenFacingAwayOffArena()
        => LimitCut(MainTank)
            .FreezeAt(11.9f)
            .At(14.9f, p => StandOutsideCruiseChaser(p, MainTank, facingCruiseChaser: false))
            .ShouldKill(TheEnvironment, MainTank);

    // Past the arc's 68° outer bound.
    [Test]
    public void EvenFacing72DegreesOffCruiseChaserIsThrownOffArena()
        => LimitCut(MainTank)
            .FreezeAt(11.9f)
            .At(14.9f, p => StandOutsideCruiseChaser(p, MainTank, facingOffDegrees: 72f))
            .ShouldKill(TheEnvironment, MainTank);
}
