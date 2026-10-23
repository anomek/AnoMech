using AnoMech.Core.UserActions;

namespace AnoMech.Tests;

public class EsunaTests
{
    [TestCase(JobId.Conjurer, true)]
    [TestCase(JobId.WhiteMage, true)]
    [TestCase(JobId.Scholar, true)]
    [TestCase(JobId.Astrologian, true)]
    [TestCase(JobId.Sage, true)]
    [TestCase(JobId.Paladin, false)]
    [TestCase(JobId.Bard, false)]
    [TestCase(JobId.BlackMage, false)]
    public void OnlyHealersKnowEsuna(JobId job, bool known)
        => Assert.That(Esuna.KnownBy((byte)job), Is.EqualTo(known));

    [TestCase(JobId.WhiteMage, true)]
    [TestCase(JobId.Bard, true)]
    [TestCase(JobId.Archer, false)]
    [TestCase(JobId.Paladin, false)]
    [TestCase(JobId.BlackMage, false)]
    public void HealersAndBardsCanCleanse(JobId job, bool can)
        => Assert.That(Esuna.CanCleanse((byte)job), Is.EqualTo(can));
}
