using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Tea.P1LivingLiquid;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

public class DollDrainTests
{
    private static readonly float[] Percentiles = [0.1f, 0.5f, 0.9f];

    private static IEnumerable<DollRoll> Rolls()
    {
        foreach (var pace in Percentiles)
        foreach (var before in Percentiles)
        foreach (var after in Percentiles)
        foreach (var wait in Percentiles)
            yield return new DollRoll(pace, before, after, wait);
    }

    [Test]
    public void FallsAtEveryHitAndCrossesAQuarterOnlyOnTheLast([Values(MeleeDpsA, PhysRangedDps, CasterDps, MainTank)] PartyRole taker)
    {
        foreach (var roll in Rolls())
        for (var seed = 0; seed < 20; seed++)
        {
            var hits = DollDrain.Of(taker).Hits(roll, new Rng(seed));
            Assert.Multiple(() =>
            {
                Assert.That(hits[0].At, Is.Zero, $"{roll}");
                for (var i = 1; i < hits.Count; i++)
                {
                    Assert.That(hits[i].At, Is.GreaterThan(hits[i - 1].At), $"{roll} hit {i}");
                    Assert.That(hits[i].Hp, Is.LessThan(hits[i - 1].Hp), $"{roll} hit {i}");
                }
                for (var i = 0; i < hits.Count - 1; i++)
                    Assert.That(hits[i].Hp, Is.GreaterThanOrEqualTo(0.25f), $"{roll} hit {i}");
                Assert.That(hits[^1].Hp, Is.LessThan(0.25f).And.GreaterThan(0f), $"{roll}");
            });
        }
    }

    [TestCase(MeleeDpsA, 0.1f, 4.0f)]
    [TestCase(MeleeDpsA, 0.5f, 6.8f)]
    [TestCase(MeleeDpsA, 0.9f, 9.8f)]
    [TestCase(PhysRangedDps, 0.1f, 7.6f)]
    [TestCase(PhysRangedDps, 0.9f, 15.2f)]
    [TestCase(CasterDps, 0.1f, 6.4f)]
    [TestCase(CasterDps, 0.9f, 17.7f)]
    public void CrossesAQuarterAtTheRolledPace(PartyRole taker, float pace, float seconds)
    {
        foreach (var wait in Percentiles)
            Assert.That(DollDrain.Of(taker).Hits(new DollRoll(pace, 0.5f, 0.5f, wait), new Rng(1))[^1].At, Is.EqualTo(seconds).Within(0.001f));
    }
}
