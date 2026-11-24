using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P3UltimateRelativity;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

// The JP Mana strat orients on the single yellow tether as north (the hourglass at layout clock 4):
// DPS long/Blizzard N, short DPS NW/NE, mid DPS E, short T/H S, long T/H SW/SE, mid T/H W. Unturned
// layout, so the hourglass clocks are Mana's turned half a circle.
public class FruP3UltimateRelativityJpAiTests
{
    private static readonly int Mana = new FruP3UltimateRelativityScenario().AiStrats
        .Select((ai, index) => (ai, index))
        .Single(s => s.ai is FruP3UltimateRelativityJpAi)
        .index;

    private static readonly Dictionary<PartyRole, RelativityDebuff> Debuffs = new()
    {
        [MeleeDpsA] = RelativityDebuff.ShortDps,
        [MeleeDpsB] = RelativityDebuff.ShortDps,
        [PhysRangedDps] = RelativityDebuff.MediumDps,
        [CasterDps] = RelativityDebuff.LongDps,
        [MainTank] = RelativityDebuff.LongSupport,
        [OffTank] = RelativityDebuff.LongSupport,
        [RegenHealer] = RelativityDebuff.MediumSupport,
        [ShieldHealer] = RelativityDebuff.ShortSupport,
    };

    // Whoever presses their self-mark macro first takes number 1 of the shared mark: Attack 1 is NW
    // (layout clock 3), Bind 1 is SW (layout clock 1). The press order is random per seed.
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void EveryoneOutForTheirFireStandsAtTheirManaHourglass(int seed)
    {
        var firstFire = Capture(FruP3UltimateRelativityState.FireAt(0) - 0.05f, seed);
        var secondFire = Capture(FruP3UltimateRelativityState.FireAt(1) - 0.05f, seed);
        var thirdFire = Capture(FruP3UltimateRelativityState.FireAt(2) - 0.05f, seed);
        Assert.Multiple(() =>
        {
            ExpectPairSplit(firstFire, MeleeDpsA, MeleeDpsB, 3, 5);
            ExpectToward(secondFire, PhysRangedDps, clock: 6);
            ExpectToward(secondFire, RegenHealer, clock: 2);
            ExpectPairSplit(thirdFire, MainTank, OffTank, 1, 7);
        });
    }

    [Test]
    public void ThePairsSplitByWhoMarkedFirstNotByRole()
    {
        var meleeANorthwest = Enumerable.Range(1, 12)
            .Select(seed => Clock(Capture(FruP3UltimateRelativityState.FireAt(0) - 0.05f, seed)[MeleeDpsA]) == 3)
            .ToList();
        Assert.That(meleeANorthwest, Does.Contain(true).And.Contain(false),
            "across seeds, D1 should sometimes mark first (NW) and sometimes second (NE)");
    }

    private static void ExpectPairSplit(Dictionary<PartyRole, Vector3> positions, PartyRole a, PartyRole b, int clockA, int clockB)
    {
        var clocks = new[] { Clock(positions[a]), Clock(positions[b]) }.OrderBy(c => c).ToArray();
        Assert.That(clocks, Is.EqualTo(new[] { clockA, clockB }.OrderBy(c => c).ToArray()), $"{a}/{b} should take one hourglass each");
    }

    private static int Clock(Vector3 p) => ((int)MathF.Round(MathF.Atan2(p.X, -p.Z) / (MathF.PI / 4)) % 8 + 8) % 8;

    private static Dictionary<PartyRole, Vector3> Capture(float at, int seed = 1)
    {
        var positions = new Dictionary<PartyRole, Vector3>();
        var options = new ScenarioRunOptions
        {
            PlayerRole = ShieldHealer,
            Overrides = o =>
            {
                var overrides = (FruP3UltimateRelativityStateOverrides)o;
                overrides.Debuffs = Debuffs;
                overrides.DpsIce = true;
                overrides.North = 0;
            },
            Probe = p =>
            {
                if (!p.Crossed(at)) return;
                foreach (var role in PerRole.All)
                    if (p.Member(role) is { } member)
                        positions[role] = member.Position;
            },
        };
        var run = ScenarioRun.Execute(typeof(FruP3UltimateRelativityScenario), Mana, seed, options);
        Assert.That(run.Passed, Is.True, run.ToString);
        return positions;
    }

    private static void ExpectToward(Dictionary<PartyRole, Vector3> positions, PartyRole role, int clock)
    {
        var p = positions[role];
        var octant = MathF.Atan2(p.X, -p.Z) / (MathF.PI / 4);
        var off = ((octant - clock) % 8 + 12) % 8 - 4;
        Assert.That(MathF.Abs(off), Is.LessThan(0.3f), $"{role} toward clock {octant:F2}, expected {clock}");
    }
}
