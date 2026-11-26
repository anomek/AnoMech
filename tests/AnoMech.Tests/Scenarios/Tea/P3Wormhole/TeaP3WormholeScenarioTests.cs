using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea.P3Wormhole;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Tea.TeaConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Every roll is pinned: Brute Justice north-west and the wormholes WNW/ESE; numbers 1 C, 2 M2, 3 H1,
// 4 MT, 5 OT, 6 M1, 7 R, 8 H2; Enumerations on M2 and R.
//
//    0.00  The run picks up under Alexander Prime, OT holding him; Wormhole Formation at 1.00.
//   20.33  The wormholes; the numbers' hits run 26.70 to 40.88, Repentance at 31.30 / 35.54 / 39.77.
//   40.78  Incinerating Heat, the Enumerations at 50.44.
public class TeaP3WormholeScenarioTests
{
    private static readonly PartyRole[] PinnedNumbers =
        [CasterDps, MeleeDpsB, RegenHealer, MainTank, OffTank, MeleeDpsA, PhysRangedDps, ShieldHealer];

    private static void Pin(TeaP3WormholeStateOverrides o)
    {
        o.BruteJusticeNorthWest = true;
        o.PuddlesMirrored = false;
        for (var k = 0; k < PinnedNumbers.Length; k++) o.Number[PinnedNumbers[k]] = k + 1;
        o.Enumeration[MeleeDpsB] = true;
        o.Enumeration[PhysRangedDps] = true;
    }

    private static NegativeRun<TeaP3WormholeScenario> Wormhole(PartyRole player)
        => Negative<TeaP3WormholeScenario>(player).Overrides<TeaP3WormholeStateOverrides>(Pin);

    private static TeaP3WormholeState State(ScenarioProbe p)
        => p.Game.Catalog.Scenarios.OfType<TeaP3WormholeScenario>().Single().LastState!;

    [Test]
    public void DiesWalkingIntoWall()
        => Wormhole(MeleeDpsB)
            .TeleportAt(4.8f, to: new(0, 20.5f))
            .ShouldKill(TheEnvironment, MeleeDpsB);

    // OT, #5 and already vulnerable, steps between Cruise Chaser and M2 as M2 takes the charge for #2.
    [Test]
    public void StandingInAnotherNumbersChargeWhileVulnerableDies()
        => Wormhole(OffTank)
            .FreezeAt(27.8f)
            .At(27.9f, p =>
            {
                var offTank = p.Member(OffTank)!;
                offTank.AddStatus(StatusId.PhysicalVulnerabilityUp, 17f);
                offTank.SetPosition((State(p).Objects.CruiseChaser!.Position + p.Member(MeleeDpsB)!.Position) / 2f);
            })
            .ShouldKill(ActionId.SuperBlasstyChargeP3, OffTank);

    // #5 waits on the west wall past the first soak, 11.7y from the wormhole.
    [Test]
    public void UnsoakedWormholeWipesTheParty()
        => Wormhole(OffTank)
            .TeleportAt(25.8f, to: new(-18.5f, 4.5f))
            .ShouldKill(ActionId.Confession, PerRole.All);

    // #2 still carries the charge's Magic Vulnerability Up and steps into the east wormhole beside #6.
    [Test]
    public void SoakingOnMagicVulnerabilityDies()
        => Wormhole(MeleeDpsB)
            .TeleportAt(31.0f, to: new(13.5f, 3f))
            .ShouldKill(ActionId.Repentance1, MeleeDpsB);

    // #1 stays north instead of joining the stack: either C is the stack and alone, or the seven without
    // C are short of eight.
    [Test]
    public void IncineratingHeatShortOfEightKills()
        => Wormhole(CasterDps)
            .TeleportAt(42.8f, to: new(0, -15f))
            .ShouldKillSomeone(ActionId.IncineratingHeat);

    // OT stands between the two stacks and H2 leaves the west one, so each holds exactly three.
    [Test]
    public void StandingInBothEnumerationsDies()
        => Wormhole(OffTank)
            .TeleportAt(49.0f, to: new(-0.4f, -0.3f))
            .MoveBotAt(49.0f, ShieldHealer, to: new(-6f, -8f))
            .ShouldKill(ActionId.Enumeration, OffTank);
}
