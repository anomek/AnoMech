using AnoMech.Core.EnemyActions;
using AnoMech.Core.UserActions;

namespace AnoMech.Tests;

public class MitigationTests
{
    private const ushort Rampart = 1191;
    private const ushort Reprisal = 1193;
    private const ushort DarkMind = 746;
    private const ushort HallowedGround = 82;
    private const ushort BlackestNight = 1178;
    private const ushort Guardian = 3829;
    private const ushort GuardiansWill = 3830;
    private const ushort ThrillOfBattle = 87;

    [Test]
    public void NoKnownStatusesMitigateNothing()
    {
        Assert.That(Mitigation.Effective([], DamageKind.Magic), Is.EqualTo(0f));
        Assert.That(Mitigation.Effective([9999], DamageKind.Magic), Is.EqualTo(0f));
    }

    [Test]
    public void PercentagesStackMultiplicatively()
    {
        Assert.That(Mitigation.Effective([Rampart, Reprisal], DamageKind.Physical), Is.EqualTo(0.28f).Within(1e-5f));
    }

    [TestCase(DamageKind.Magic, 0.20f)]
    [TestCase(DamageKind.Physical, 0.10f)]
    public void TypedReductionFollowsTheDamageKind(DamageKind kind, float expected)
    {
        Assert.That(Mitigation.Effective([DarkMind], kind), Is.EqualTo(expected).Within(1e-5f));
    }

    [Test]
    public void AllAndTypedReductionsCombine()
    {
        Assert.That(Mitigation.Effective([Rampart, DarkMind], DamageKind.Magic), Is.EqualTo(0.36f).Within(1e-5f));
    }

    [Test]
    public void InvulnMitigatesEverything()
    {
        Assert.That(Mitigation.Effective([HallowedGround], DamageKind.Magic), Is.EqualTo(1f));
    }

    [Test]
    public void ShieldAddsToThePoolTheHitLandsOn()
    {
        // A 25% shield survives a hit of 1.25x max HP: 1 - 1/1.25.
        Assert.That(Mitigation.Effective([BlackestNight], DamageKind.Magic), Is.EqualTo(0.20f).Within(1e-5f));
    }

    [Test]
    public void BonusMaxHpCountsLikeAShield()
    {
        Assert.That(Mitigation.Effective([ThrillOfBattle], DamageKind.Magic), Is.EqualTo(1f - 1f / 1.2f).Within(1e-5f));
    }

    [Test]
    public void PercentagesAndPoolCombine()
    {
        // Rampart leaves 80% of the hit, landing on 125% of max HP.
        Assert.That(Mitigation.Effective([Rampart, BlackestNight], DamageKind.Magic), Is.EqualTo(1f - 0.8f / 1.25f).Within(1e-5f));
    }

    [Test]
    public void PotencyShieldConvertsToAShareOfMaxHp()
    {
        // Guardian: 60% of the hit, landing on 100% + 15% (Guardian's Will, 1000 potency) of max HP.
        Assert.That(Mitigation.Effective([Guardian, GuardiansWill], DamageKind.Physical), Is.EqualTo(1f - 0.6f / 1.15f).Within(1e-5f));
    }

    [Test]
    public void NoStatusMixesAShieldWithAReduction()
    {
        foreach (var (id, m) in Mitigation.ByStatusId)
            if (m.IsShield)
                Assert.That(m with { ShieldHp = 0f, ShieldPotency = 0f }, Is.EqualTo(default(Mitigation)), $"status {id}");
    }
}
