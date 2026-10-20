using AnoMech.Core.UserActions;

namespace AnoMech.Tests;

public class EnmityTests
{
    [TestCase(JobId.Gladiator, true)]
    [TestCase(JobId.Paladin, true)]
    [TestCase(JobId.Marauder, true)]
    [TestCase(JobId.Warrior, true)]
    [TestCase(JobId.DarkKnight, true)]
    [TestCase(JobId.Gunbreaker, true)]
    [TestCase(JobId.WhiteMage, false)]
    [TestCase(JobId.Monk, false)]
    [TestCase(JobId.Bard, false)]
    public void OnlyTanksKnowProvoke(JobId job, bool known)
        => Assert.That(Enmity.KnowsProvoke((byte)job), Is.EqualTo(known));
}
