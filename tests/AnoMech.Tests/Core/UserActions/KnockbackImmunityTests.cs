using AnoMech.Core.UserActions;

namespace AnoMech.Tests;

public class KnockbackImmunityTests
{
    [TestCase(JobId.WhiteMage)]
    [TestCase(JobId.Scholar)]
    [TestCase(JobId.Astrologian)]
    [TestCase(JobId.Sage)]
    [TestCase(JobId.BlackMage)]
    [TestCase(JobId.Summoner)]
    [TestCase(JobId.RedMage)]
    [TestCase(JobId.Pictomancer)]
    public void HealersAndCastersPressSurecast(JobId job)
        => Assert.That(KnockbackImmunity.ActionId((byte)job), Is.EqualTo(KnockbackImmunity.SurecastActionId));

    [TestCase(JobId.Paladin)]
    [TestCase(JobId.Warrior)]
    [TestCase(JobId.DarkKnight)]
    [TestCase(JobId.Gunbreaker)]
    [TestCase(JobId.Monk)]
    [TestCase(JobId.Dragoon)]
    [TestCase(JobId.Ninja)]
    [TestCase(JobId.Samurai)]
    [TestCase(JobId.Reaper)]
    [TestCase(JobId.Viper)]
    [TestCase(JobId.Bard)]
    [TestCase(JobId.Machinist)]
    [TestCase(JobId.Dancer)]
    public void EveryoneElsePressesArmsLength(JobId job)
        => Assert.That(KnockbackImmunity.ActionId((byte)job), Is.EqualTo(KnockbackImmunity.ArmsLengthActionId));

    [Test]
    public void TheRoleStatusesAndInnerStrengthAreImmunity()
    {
        Assert.Multiple(() =>
        {
            Assert.That(KnockbackImmunity.IsImmunity(KnockbackImmunity.ArmsLength));
            Assert.That(KnockbackImmunity.IsImmunity(KnockbackImmunity.Surecast));
            Assert.That(KnockbackImmunity.IsImmunity(KnockbackImmunity.InnerStrength));
            Assert.That(KnockbackImmunity.IsImmunity(1191), Is.False);
        });
    }
}
