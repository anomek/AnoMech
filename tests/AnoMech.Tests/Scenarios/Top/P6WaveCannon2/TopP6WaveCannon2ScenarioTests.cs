using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P6WaveCannon2;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: In first, protean order MT OT RH SH then M1 M2 R C, Wild Charge on MT.
// Cosmo Arrow lines run the full arena both ways, at x = c and z = c.
//
//    9.90 first set: In first the cross |c| < 5, Out first the bands 10 < |c| < 20; 11.91 the other set.
//   11.91 on, every 2s, rolling lines |c - line| < 2.5:
//         In first  ±7.5 / ±7.5 ±12.5 / ±2.5 ±17.5 / ±2.5 / ±7.5 / ±12.5 / ±17.5
//         Out first ±7.5 / ±2.5 ±7.5 / ±2.5 ±12.5 / ±7.5 ±17.5 / ±12.5 / ±17.5
//   19.07 proteans on MT (0,-11), OT (0,11), RH (-11,0), SH (11,0); 21.00 on M1 (-9,9), M2 (9,9), R (9,-9), C (-9,-9).
//   27.40 Wild Charge on MT, with MT OT at (0,9) and the rest at (0,11).
public class TopP6WaveCannon2ScenarioTests
{
    private const float WildChargeLinedUp = 27.1f;

    private static NegativeRun<TopP6WaveCannon2Scenario> WaveCannon2(PartyRole player, Action<TopP6WaveCannon2StateOverrides>? tweak = null)
        => Negative<TopP6WaveCannon2Scenario>(player)
            .Overrides<TopP6WaveCannon2StateOverrides>(o =>
            {
                o.InFirst = true;
                o.ProteanOrder = [MainTank, OffTank, RegenHealer, ShieldHealer, MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
                o.WildChargeTarget = MainTank;
                tweak?.Invoke(o);
            });

    private static NegativeRun<TopP6WaveCannon2Scenario> WaveCannon2(PartyRole player, bool inFirst)
        => WaveCannon2(player, o => o.InFirst = inFirst);

    [Test]
    public void DiesWalkingIntoWall()
        => WaveCannon2(RegenHealer)
            .TeleportAt(5f, to: new(0, 20.5f))
            .ShouldKill(TheEnvironment, RegenHealer);

    [TestCase(true)]
    [TestCase(false)]
    public void DiesToFirstCosmoArrow(bool inFirst)
        => WaveCannon2(RegenHealer, inFirst)
            .TeleportAt(5f, to: inFirst ? new(3, 8) : new(8, 15))
            .ShouldKill(ActionId.CosmoArrowOmen, RegenHealer);

    // In the second set either way, clear of the rolling lines that land with it.
    [TestCase(true)]
    [TestCase(false)]
    public void DiesToSecondCosmoArrow(bool inFirst)
        => WaveCannon2(RegenHealer, inFirst)
            .TeleportAt(10.2f, to: new(3, 13))
            .ShouldKill(ActionId.CosmoArrowOmen, RegenHealer);

    [TestCase(true)]
    [TestCase(false)]
    public void DiesToFirstRollingLine(bool inFirst)
        => WaveCannon2(RegenHealer, inFirst)
            .TeleportAt(10.5f, to: new(7.5f, 7.5f))
            .ShouldKill(ActionId.CosmoArrowDamage, RegenHealer);

    [TestCase(true)]
    [TestCase(false)]
    public void DiesToRollingLineComingBackIn(bool inFirst)
        => WaveCannon2(RegenHealer, inFirst)
            .TeleportAt(14f, to: new(2, 8))
            .ShouldKill(ActionId.CosmoArrowDamage, RegenHealer);

    [TestCase(true)]
    [TestCase(false)]
    public void DiesToLastRollingLineAtTheEdge(bool inFirst)
        => WaveCannon2(RegenHealer, inFirst)
            .TeleportAt(20f, to: new(17.5f, 0))
            .ShouldKill(ActionId.CosmoArrowDamage, RegenHealer);

    // In MT's line for the first protean, then back on its own spot for its own.
    [Test]
    public void SecondProteanTargetHitByFirstProteanDies()
        => WaveCannon2(MeleeDpsA)
            .TeleportAt(18.7f, to: new(2, -6))
            .TeleportAt(19.2f, to: new(-11, 11))
            .TeleportAt(20.6f, to: new(-9, 9))
            .ShouldKill(ActionId.WaveCannonProtean, MeleeDpsA);

    [Test]
    public void FirstProteanTargetInSecondProteanDies()
        => WaveCannon2(MainTank)
            .TeleportAt(20.6f, to: new(-6, 6))
            .ShouldKill(ActionId.WaveCannonProtean, MainTank);

    // In MT's line, and MT in RH's.
    [Test]
    public void OverlappingFirstProteansKillBoth()
        => WaveCannon2(RegenHealer)
            .TeleportAt(18.7f, to: new(2, -6))
            .ShouldKill(ActionId.WaveCannonProtean, MainTank, RegenHealer);

    [TestCase(OffTank)]
    [TestCase(CasterDps)]
    public void LeavingWildChargeKillsTheStack(PartyRole leaver)
        => WaveCannon2(leaver)
            .TeleportAt(26f, to: new(-10, 0))
            .ShouldKill(ActionId.WaveCannonWildCharge, AllBut(leaver));

    [Test]
    public void WildChargeFollowsItsTarget()
        => WaveCannon2(CasterDps, o => o.WildChargeTarget = CasterDps)
            .TeleportAt(26f, to: new(-10, 0))
            .ShouldKill(ActionId.WaveCannonWildCharge, CasterDps);

    [TestCase(RegenHealer)]
    [TestCase(MeleeDpsA)]
    public void NonTankFirstInWildChargeDies(PartyRole player)
        => WaveCannon2(player)
            .TeleportAt(WildChargeLinedUp, to: new(0, 6))
            .ShouldKill(ActionId.WaveCannonWildCharge, player);

    [TestCase(RegenHealer)]
    [TestCase(MeleeDpsA)]
    public void NonTankSecondInWildChargeDies(PartyRole player)
        => WaveCannon2(player)
            .MoveBotAt(WildChargeLinedUp, MainTank, to: new(0, 6))
            .TeleportAt(WildChargeLinedUp, to: new(0, 7))
            .ShouldKill(ActionId.WaveCannonWildCharge, player);
}
