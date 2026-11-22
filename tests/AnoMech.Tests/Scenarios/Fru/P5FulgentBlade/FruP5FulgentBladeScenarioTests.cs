using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P5FulgentBlade;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: the pocket south, unturned and unmirrored, every cardinal dark strip rolling inward; each
// Akh Morn picks the furthest to its side.
//
//   18.28 the first seams fire: light from (0,17) southward over z 17-22, dark northward over z 12-17,
//         and a diagonal pair from (10.6,10.6). Each strip is 5y deep and rolls on 5y every ~2.05s:
//         20.67 dark from (0,12) over z 7-12, the diagonal light from (7.1,7.1) towards the NW.
//   34.47 Akh Morn (4y, 4 to share) on the east group OT SH M2 C at (4.2,6) and the west group R MT RH M1
//         at (-4.3,6.1), each picked at 34.05 by bearing from Pandora.
public class FruP5FulgentBladeScenarioTests
{
    private static NegativeRun<FruP5FulgentBladeScenario> FulgentBlade(PartyRole player)
        => Negative<FruP5FulgentBladeScenario>(player)
            .Overrides<FruP5FulgentBladeStateOverrides>(o =>
            {
                o.PositionIndex = 0;
                o.RotationIndex = 0;
                o.Mirrored = false;
                o.CardinalDarkInward = [true, true, true];
                o.AkhMornLeftPick = 0;
                o.AkhMornRightPick = 0;
            });

    [TestCase(18f, -3f, 18.5f, ActionId.PathOfLightFirst)]
    [TestCase(18f, -3f, 14.5f, ActionId.PathOfDarknessFirst)]
    [TestCase(20.5f, 5f, 5f, ActionId.PathOfLightRest)]
    [TestCase(20.5f, -3f, 9.5f, ActionId.PathOfDarknessRest)]
    public void StandingInExalineDies(float at, float x, float z, uint strip)
        => FulgentBlade(MainTank)
            .TeleportAt(at, to: new(x, z))
            .ShouldKill(strip, MainTank);

    // Each stack's target is whoever stands furthest round to that side, which a fraction of a yalm
    // decides within a group; SH is never the east one's.
    [Test]
    public void AkhMornShortABodyKillsStackers()
        => FulgentBlade(ShieldHealer)
            .TeleportAt(34f, to: new(0, 15))
            .ShouldKill(ActionId.AkhMornPandoraLeft, OffTank, MeleeDpsB, CasterDps);

    // Out west, R is sure to be picked at 34.05, then carries that stack into the east one.
    [Test]
    public void AkhMornStacksOverlappingKillEveryoneInBoth()
        => FulgentBlade(PhysRangedDps)
            .TeleportAt(33.9f, to: new(-7, 2))
            .TeleportAt(34.3f, to: new(3.5f, 6))
            .ShouldKill(ActionId.AkhMornPandoraRight, PhysRangedDps, OffTank, ShieldHealer, MeleeDpsB, CasterDps);
}
