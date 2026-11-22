using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P2LightRampant;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: R and RH bait the hammers, the loop is OT-MT-SH-C-M1-M2, OT and MT carry the Weight of
// Light, MT OT SH C start steeped, the north orbs go first, Banish goes as pairs on the supports.
//
//   15.87 Luminous Hammer x5, 1.58s apart: R from (-19.1,0) and RH from (19,0.2) walking in;
//         each puddle shows 0.95s after its hit and arms 1.5s later.
//   18.77 curses (break under 25y); 19.02 towers: OT (-13.9,-8), MT (0,16), SH (13.9,-8),
//         C (-13.9,8), M1 (0,-16), M2 (13.9,8).
//   25.02 Powerful Light on OT with SH M1 R at (4.4,-18.2), on MT with M2 C RH at (-4.7,18.2).
//   27.15 Burst (0,-16) (13.9,8) (-13.9,8); 27.8 the curses end; 30.13 Burst (0,16) (13.9,-8) (-13.9,-8).
//   33.82 centre tower: RH M1 M2 R; MT and C wait at (-5.3,3).
//   37.12 Banish pairs: MT+R (-5.2,-5.2), OT+C (5.2,-5.2), RH+M1 (-5,5.1), SH+M2 (5.1,5).
//         Spreads instead: MT (0,-7.7), OT (7.7,0), RH (-7.5,0), SH (0,7.6), the DPS on the intercards at 7.2y.
//   45.97 House of Light from (0,0) at each player: MT (0,-7.7), OT (7.5,0), RH (-7.7,0), SH (0,7.6).
public class FruP2LightRampantScenarioTests
{
    private static NegativeRun<FruP2LightRampantScenario> LightRampant(PartyRole player, Action<FruP2LightRampantStateOverrides>? tweak = null)
        => Negative<FruP2LightRampantScenario>(player)
            .Overrides<FruP2LightRampantStateOverrides>(o =>
            {
                foreach (var role in PerRole.All)
                    o.Duty[role] = role is PhysRangedDps or RegenHealer ? LightRampantDuty.Bait : LightRampantDuty.Tether;
                o.NorthOrbsFirst = true;
                o.Pairs = true;
                o.SupportPairs = true;
                o.WeightSlot = 0;
                o.InitiallySteeped = [MainTank, OffTank, ShieldHealer, CasterDps];
                tweak?.Invoke(o);
            });

    [Test]
    public void HammerBaitersStackedDie()
        => LightRampant(RegenHealer)
            .TeleportAt(15.6f, to: new(-19.1f, 0.5f))
            .ShouldKill(ActionId.LuminousHammer, RegenHealer, PhysRangedDps);

    // RH leaves their Powerful Light stack too, but bleeds out before it lands.
    [Test]
    public void SteppingInHammerPuddleBleedsOut()
        => LightRampant(RegenHealer)
            .TeleportAt(22.4f, to: new(18.96f, 0.19f))
            .ShouldDieOfStatus(StatusId.Bleeding, RegenHealer);

    // Each soaker steps off far enough from both curse partners to keep the chain whole.
    [TestCase(OffTank, -9.8f, -16.9f)]
    [TestCase(MainTank, -3.5f, 19.2f)]
    [TestCase(ShieldHealer, 9.8f, -16.9f)]
    [TestCase(CasterDps, -9.8f, 16.9f)]
    [TestCase(MeleeDpsA, -3.5f, -19.2f)]
    [TestCase(MeleeDpsB, 9.8f, 16.9f)]
    public void UnsoakedTowerWipes(PartyRole soaker, float x, float z)
        => LightRampant(soaker)
            .TeleportAt(18.95f, to: new(x, z))
            .ShouldKill(ActionId.InescapableIllumination, PerRole.All);

    [Test]
    public void FifthLightsteepedStackWipes()
        => LightRampant(MainTank)
            .At(18.9f, p => p.Member(MainTank)!.AddStatus(StatusId.Lightsteeped, 30f, 4, overrideStacks: true))
            .ShouldKill(ActionId.Lightsteep, PerRole.All);

    [TestCase(27f, 0f, -12f)]
    [TestCase(30f, 0f, 13f)]
    public void StandingInBurstDies(float at, float x, float z)
        => LightRampant(MeleeDpsA)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(ActionId.Burst, MeleeDpsA);

    // MT M2 C are chained, so their deaths snap the curses on the rest.
    [Test]
    public void PowerfulLightShortABodyKillsStackers()
        => LightRampant(RegenHealer)
            .TeleportAt(24.9f, to: new(0, 0))
            .ShouldKill(ActionId.PowerfulLight, MainTank, MeleeDpsB, CasterDps)
            .ShouldKill(ActionId.RefulgentFate, OffTank, ShieldHealer, MeleeDpsA, PhysRangedDps, RegenHealer);

    // The centre tower's own stack kills the three soakers; the tower then punishes the rest.
    [Test]
    public void CenterTowerShortABodyWipes()
        => LightRampant(PhysRangedDps)
            .TeleportAt(33.6f, to: new(0, 10))
            .ShouldKill(ActionId.BrightHunger, RegenHealer, MeleeDpsA, MeleeDpsB)
            .ShouldKill(ActionId.InescapableIllumination, MainTank, OffTank, ShieldHealer, CasterDps, PhysRangedDps);

    [Test]
    public void BanishPairShortABodyKillsHolder()
        => LightRampant(PhysRangedDps)
            .TeleportAt(36.9f, to: new(0, -15))
            .ShouldKill(ActionId.BanishPairs, MainTank);

    [Test]
    public void BanishSpreadsOverlappingDie()
        => LightRampant(MainTank, o => o.Pairs = false)
            .TeleportAt(36.9f, to: new(7.7f, -0.15f))
            .ShouldKill(ActionId.BanishSpread, MainTank, OffTank);

    [Test]
    public void HouseOfLightOverlapDies()
        => LightRampant(MainTank)
            .TeleportAt(45.8f, to: new(7.53f, 0))
            .ShouldKill(ActionId.HouseOfLight, MainTank, OffTank);

    [Test]
    public void CurseTooShortWipes()
        => LightRampant(MainTank)
            .TeleportAt(21.5f, to: new(8, -5))
            .ShouldKill(ActionId.RefulgentFate, PerRole.All);
}
