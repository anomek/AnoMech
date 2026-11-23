using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P3UltimateRelativity;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// Pinned: short fire M1 M2 MT, medium R OT, long RH SH, Dark Blizzard on C (the long DPS); Unholy
// Darkness on RH, MT, OT; hourglasses unturned, all sweeping counterclockwise; Shell Crusher on MT.
// Wave 0 hourglasses (-6.7,-6.7) (6.7,-6.7) (0,9.5); wave 1 (-6.7,6.7) (6.7,6.7) (0,-9.5); wave 2 (±9.5,0).
//
//   22.82 Dark Fire (8y) on MT (0,-13.5), M1 (-9.9,9.9), M2 (9.7,9.8); Unholy Darkness (6y) on RH,
//         stacked with R SH C OT at (1,-0.8).
//   27.97 wave 0 lasers at RH (8.2,-5.6), C (-1.6,9.8), SH (-5.7,-8.2); MT waits at (-0.2,-8.8),
//         Fire Resistance Down until 34.8. The sweeps follow from 29.99, 1.075s apart.
//   32.82 Dark Blizzard on C at the centre stack; fires on OT (-13.8,0) and R (13.8,0).
//   51.75 stun; 52.83 rewind. 54.82 Dark Eruption on MT (-0.2,-8.8), OT (-8.5,-0.1), M1 (-6.6,6.6),
//         M2 (6.6,6.6); Dark Water on R with RH C SH at (1.4,0.2); Shadoweye from RH SH C there.
//   59.22 Shell Crusher (8 to share) on MT, everyone stacked at the centre.
public class FruP3UltimateRelativityScenarioTests
{
    private static NegativeRun<FruP3UltimateRelativityScenario> Relativity(PartyRole player, Action<FruP3UltimateRelativityStateOverrides>? tweak = null)
        => Negative<FruP3UltimateRelativityScenario>(player)
            .Overrides<FruP3UltimateRelativityStateOverrides>(o =>
            {
                Pin(o);
                tweak?.Invoke(o);
            });

    private static void Pin(FruP3UltimateRelativityStateOverrides o)
    {
        o.Debuffs = new Dictionary<PartyRole, RelativityDebuff>
        {
            [MeleeDpsA] = RelativityDebuff.ShortDps,
            [MeleeDpsB] = RelativityDebuff.ShortDps,
            [PhysRangedDps] = RelativityDebuff.MediumDps,
            [CasterDps] = RelativityDebuff.LongDps,
            [MainTank] = RelativityDebuff.ShortSupport,
            [OffTank] = RelativityDebuff.MediumSupport,
            [RegenHealer] = RelativityDebuff.LongSupport,
            [ShieldHealer] = RelativityDebuff.LongSupport,
        };
        o.DpsIce = true;
        o.North = 0;
        o.ClockwiseMask = 0;
        o.Darkness = [RegenHealer, MainTank, OffTank];
        o.ShellCrusherTarget = MainTank;
    }

    [Test]
    public void DarkFiresStackedDie()
        => Relativity(MeleeDpsA)
            .TeleportAt(22.6f, to: new(9.7f, 9.8f))
            .ShouldKill(ActionId.DarkFire, MeleeDpsA, MeleeDpsB);

    // The ice holder stands in the centre stack either way.
    [TestCase(true)]
    [TestCase(false)]
    public void StandingInDarkBlizzardRingDies(bool dpsIce)
        => Relativity(MeleeDpsA, o => o.DpsIce = dpsIce)
            .TeleportAt(32.6f, to: new(0, -7))
            .ShouldKill(ActionId.DarkBlizzard, MeleeDpsA);

    // An ice holder has no fire to drop, so it stays in the stack and a stack one stacker short still holds.
    [Test]
    public void FirstStackHoldsWithTheIceSupportInIt()
        => Relativity(RegenHealer, o => o.DpsIce = false)
            .MoveBotAt(22.5f, PhysRangedDps, to: new(12, -6))
            .ShouldKillNoOneElse();

    [Test]
    public void ThirdStackHoldsWithTheIceDpsInIt()
        => Relativity(RegenHealer)
            .MoveBotAt(42.5f, MeleeDpsA, to: new(0, 9))
            .ShouldKillNoOneElse();

    // Done with their fire, the 20 s players wait in the middle while the 10 s players bait.
    [Test]
    public void TwentySecondPlayersWaitMidThroughTheSecondBaits()
    {
        var away = new List<string>();
        var run = ScenarioRun.Execute(typeof(FruP3UltimateRelativityScenario), 0, 7, new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = o => Pin((FruP3UltimateRelativityStateOverrides)o),
            Probe = p =>
            {
                if (!p.Crossed(36.5f) && !p.Crossed(37.8f)) return;
                foreach (var role in new[] { PhysRangedDps, OffTank })
                {
                    var member = p.Member(role)!;
                    var radius = new Vector2(member.Position.X, member.Position.Z).Length();
                    if (radius > 2f) away.Add($"{role} {radius:F1}y out at {p.Time:F1}");
                }
            },
        });
        Assert.That(run.Passed, run.ToString);
        Assert.That(away, Is.Empty);
    }

    [Test]
    public void UnholyDarknessShortABodyKillsStackers()
        => Relativity(PhysRangedDps)
            .TeleportAt(22.6f, to: new(8, -4))
            .ShouldKill(ActionId.UnholyDarkness, RegenHealer, ShieldHealer, CasterDps, OffTank);

    // Baited from the outside, the beam leaves the arena instead of crossing the centre.
    [Test]
    public void FireHolderBaitingLaserDies()
        => Relativity(MainTank)
            .TeleportAt(27.8f, to: new(7.5f, -7.5f))
            .ShouldKill(ActionId.SinboundMeltdownFirst, MainTank);

    // On the line of the NE hourglass's first sweep, facing 1.17 at 30.0.
    [Test]
    public void StandingInLaserSweepDies()
        => Relativity(OffTank)
            .TeleportAt(28.3f, to: new(9.5f, -5.5f))
            .ShouldKill(ActionId.SinboundMeltdownRest, OffTank);

    [Test]
    public void DarkWaterShortABodyKillsHolder()
        => Relativity(PhysRangedDps)
            .TeleportAt(54.6f, to: new(8, -4), facing: MathF.Atan2(8, -4))
            .ShouldKill(ActionId.DarkWater, PhysRangedDps);

    [Test]
    public void DarkEruptionsOverlappingDie()
        => Relativity(MeleeDpsA)
            .TeleportAt(54.6f, to: new(6.2f, 5.9f), facing: MathF.Atan2(6.2f, 5.9f))
            .ShouldKill(ActionId.DarkEruption, MeleeDpsA, MeleeDpsB);

    // A 20 s support who rewinds 1 y further in than its bot, a little toward the 10 s DPS beside it,
    // takes only its own eruption; otherwise it plays the support's part as the bot does.
    [Test]
    public void TwentySecondSupportRewindingFurtherInTakesOnlyItsOwnEruption([Range(1, 12)] int seed)
        => Relativity(OffTank)
            .TeleportAt(27.6f, to: new(-7.594f, 0.305f))
            .TeleportAt(31.8f, to: new(-13.8f, 0f))
            .TeleportAt(34f, to: new(-4.35f, 0f))
            .TeleportAt(38.7f, to: new(-1.27f, 0f))
            .TeleportAt(45.5f, to: new(-9.75f, -1.74f))
            .TeleportAt(49.5f, to: new(-1.27f, 0f), facing: -MathF.PI / 2)
            .TeleportAt(57.5f, to: new(0f, 0.5f))
            .Seed(seed)
            .ShouldKillNoOneElse();

    [Test]
    public void LookingAtShadoweyeDies()
        => Relativity(MainTank)
            .TeleportAt(54.6f, to: new(-0.2f, -8.8f), facing: 0f)
            .ShouldKill(ActionId.Shadoweye, MainTank);

    [Test]
    public void ShellCrusherShortABodyKillsStackers()
        => Relativity(OffTank)
            .TeleportAt(59f, to: new(0, -12))
            .ShouldKill(ActionId.ShellCrusher, AllBut(OffTank));
}
