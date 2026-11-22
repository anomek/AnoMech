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
//   51.75 stun; 52.83 rewind. 54.82 Dark Eruption on MT (-0.2,-8.8), OT (-8.5,-0.1), M1 (-6.1,6.1),
//         M2 (6.2,5.9); Dark Water on R with RH C SH at (1.4,0.2); Shadoweye from RH SH C there.
//   59.22 Shell Crusher (8 to share) on MT, everyone stacked at the centre.
public class FruP3UltimateRelativityScenarioTests
{
    private static NegativeRun<FruP3UltimateRelativityScenario> Relativity(PartyRole player, Action<FruP3UltimateRelativityStateOverrides>? tweak = null)
        => Negative<FruP3UltimateRelativityScenario>(player)
            .Overrides<FruP3UltimateRelativityStateOverrides>(o =>
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
                tweak?.Invoke(o);
            });

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
